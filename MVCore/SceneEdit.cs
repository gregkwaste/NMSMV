using System;
using System.Collections.Generic;
using System.IO;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;

namespace MVCore
{
    //Structural edits of the live scene. Export rebuilds TkSceneNodeData from this runtime graph.
    //The functions run on the render thread (through the engine requests), the tree is shown by the UI
    public static class SceneEdit
    {
        public enum NODE_KIND
        {
            LOCATOR,
            LIGHT,
            REFERENCE
        }

        //The root node of the file the node belongs to (the node itself for a file root). Null for viewer nodes
        public static Model fileRoot(Model m)
        {
            for (; m != null; m = m.parent)
            {
                if (!m.IsSceneNode)
                    return null;
                if (m.SourceFile != "")
                    return m;
            }
            return null;
        }

        //The scene whose node and joint dictionaries hold the children of the node
        private static Scene ownerScene(Model m)
        {
            return (m is Scene s) ? s : m.parentScene;
        }

        private static bool containsJoint(Model m)
        {
            if (m is Joint)
                return true;
            foreach (Model c in m.Children)
            {
                //Other files under the node (references, LOD models) go with it but are not edited
                if (c.SourceFile == "" && containsJoint(c))
                    return true;
            }
            return false;
        }

        public static bool canAdd(Model parent)
        {
            return fileRoot(parent) != null;
        }

        public static bool canRemove(Model m, out string reason)
        {
            reason = "";
            if (fileRoot(m) == null)
                reason = "The node is not part of a scene file";
            else if (m.SourceFile != "")
                reason = "The node is the root of a scene file";
            else if (containsJoint(m))
                reason = "Skinned meshes use the joints by index, joints cannot be removed";
            return reason == "";
        }

        public static bool canReparent(Model source, Model target, out string reason)
        {
            reason = "";
            if (source == target || source.parent == null || source.parent == target)
                reason = "Nothing to move";
            else if (fileRoot(source) == null || fileRoot(target) == null)
                reason = "Only nodes of scene files can be moved";
            else if (source.SourceFile != "")
                reason = "The root of a scene file cannot be moved";
            else if (fileRoot(source) != fileRoot(target))
                reason = "Nodes can only be moved inside their own scene file";
            else
            {
                for (Model m = target; m != null; m = m.parent)
                {
                    if (m == source)
                    {
                        reason = "A node cannot be moved under itself";
                        break;
                    }
                }
            }
            return reason == "";
        }

        //Removes the nodes of the subtree from the dictionaries of their scene (nested files keep theirs)
        private static void unregister(Model m, Scene scene)
        {
            if (scene != null)
            {
                if (scene.nodeDict.TryGetValue(m.name, out Model n) && n == m)
                    scene.nodeDict.Remove(m.name);
                if (m is Joint && scene.jointDict.TryGetValue(m.name, out Joint j) && j == m)
                    scene.jointDict.Remove(m.name);
            }
            foreach (Model c in m.Children)
            {
                if (c.SourceFile == "" && !(c is Scene))
                    unregister(c, scene);
            }
        }

        private static void register(Model m, Scene scene)
        {
            if (scene != null)
            {
                scene.nodeDict[m.name] = m;
                if (m is Joint j)
                    scene.jointDict[m.name] = j;
            }
            foreach (Model c in m.Children)
            {
                if (c.SourceFile == "" && !(c is Scene))
                    register(c, scene);
            }
        }

        //Runs the change on the UI thread, which owns the tree view bound to the Children collections
        private static void onUIThread(System.Action a)
        {
            System.Windows.Threading.Dispatcher dispatcher =
                System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                a();
            else
                dispatcher.Invoke(a);
        }

        public static void reparent(Model source, Model target)
        {
            if (!canReparent(source, target, out string reason))
            {
                CallBacks.Log("Cannot move " + source.name + ": " + reason);
                return;
            }

            Model oldParent = source.parent;
            Scene oldScene = ownerScene(oldParent);
            Scene newScene = ownerScene(target);

            //Viewer tree
            onUIThread(() =>
            {
                oldParent.Children.Remove(source);
                source.parent = target;
                target.Children.Add(source);
            });

            if (oldScene != newScene)
            {
                unregister(source, oldScene);
                source.setParentScene(newScene);
                register(source, newScene);
            }

            //Move the node's world transforms under the new parent
            source.update();
        }

        public static void remove(Model m, List<Light> lights, Action<Model> onRemoved)
        {
            if (!canRemove(m, out string reason))
            {
                CallBacks.Log("Cannot remove " + m.name + ": " + reason);
                return;
            }

            Model parent = m.parent;
            onUIThread(() => parent.Children.Remove(m));
            m.parent = null;
            unregister(m, ownerScene(parent));

            //The renderer and the systems drop everything of the subtree, nested files included
            forEach(m, n =>
            {
                if (n is Light l)
                    lights.Remove(l);
                onRemoved?.Invoke(n);
            });
        }

        private static void forEach(Model m, Action<Model> a)
        {
            a(m);
            foreach (Model c in m.Children)
                forEach(c, a);
        }

        //A name that no other node of the scene uses
        private static string uniqueName(Scene scene, string baseName)
        {
            if (scene == null || !scene.nodeDict.ContainsKey(baseName))
                return baseName;
            int i = 1;
            while (scene.nodeDict.ContainsKey(baseName + "_" + i))
                i++;
            return baseName + "_" + i;
        }

        private static Reference createReference(string scenePath)
        {
            string normalizedPath = (scenePath ?? "").Replace('/', '\\');
            Reference reference = new Reference(normalizedPath);
            reference.SourceAttributes["EMBEDGEOMETRY"] = "TRUE";

            if (!RenderState.settings.LoadReferences)
                return reference;

            Scene loaded = null;
            if (RenderState.activeResMgr.GLScenes.TryGetValue(normalizedPath, out Scene cached))
                loaded = (Scene)cached.Clone();
            else if (NMSUtils.NMSFileExists(normalizedPath, RenderState.activeResMgr))
                loaded = GEOMMBIN.LoadObjects(normalizedPath);

            if (loaded != null)
            {
                reference.AddChild(loaded);
                reference.isRefLoaded = true;
            }
            return reference;
        }

        //Adds a new node under the parent. Returns the viewer node, or null when the parent is not in a file
        public static Model add(Model parent, NODE_KIND kind, string scenePath = "")
        {
            if (!canAdd(parent))
            {
                CallBacks.Log("Nodes can only be added under nodes of a scene file");
                return null;
            }

            Scene scene = ownerScene(parent);
            string baseName;
            switch (kind)
            {
                case NODE_KIND.LIGHT:
                    baseName = "NewLight";
                    break;
                case NODE_KIND.REFERENCE:
                    baseName = Path.GetFileName(scenePath).Split('.')[0];
                    break;
                default:
                    baseName = "NewLocator";
                    break;
            }

            string name = uniqueName(scene, baseName);
            Model m;
            try
            {
                m = kind switch
                {
                    NODE_KIND.LOCATOR => new Locator(),
                    NODE_KIND.LIGHT => new Light { falloff = ATTENUATION_TYPE.QUADRATIC },
                    NODE_KIND.REFERENCE => createReference(scenePath),
                    _ => throw new ArgumentOutOfRangeException(nameof(kind))
                };
            }
            finally
            {
                NMSUtils.DisposeArchives(); //A reference may have been read from the PAKs
            }

            m.Name = name;
            m.IsSceneNode = true;
            m.parent = parent;
            m.parentScene = scene;
            m.init(new float[] { 0, 0, 0, 0, 0, 0, 1, 1, 1 });
            register(m, scene);
            if (m is Light light)
            {
                light.update_struct();
                RenderState.activeResMgr.GLlights.Add(light);
            }
            onUIThread(() => parent.AddChild(m));
            m.update();
            return m;
        }

        //Attaches an imported hierarchy to the editable runtime scene.
        public static bool addImportedGeometry(Model parent, Model root)
        {
            if (parent is not Scene || !parent.IsSceneNode || root?.IsSceneNode != true)
            {
                CallBacks.Log("Imported geometry can only be added beneath an exportable scene root");
                return false;
            }

            Scene scene = ownerScene(parent);
            root.setParentScene(scene);
            register(root, scene);
            onUIThread(() => parent.AddChild(root));
            root.update();
            return true;
        }
    }
}
