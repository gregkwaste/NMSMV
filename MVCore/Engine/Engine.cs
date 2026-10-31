using GLSLHelper;
using libMBIN.NMS.Toolkit;
using Model_Viewer;
using MVCore.Common;
using MVCore.Engine.Systems;
using MVCore.GMDL;
using MVCore.Input;
using MVCore.Utils;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MVCore.Engine
{
    public enum EngineRenderingState
    {
        ACTIVE=0x0,
        EXIT
    }

    public sealed class Engine : IDisposable
    {
        public ResourceManager resMgr { get; private set; }

        //Init Systems
        public readonly ActionSystem actionSys;
        public readonly AnimationSystem animationSys;
        public readonly AudioSystem audioSys;
        public readonly RotationSystem rotationSys;
        public readonly ParticleSystem particleSys;
        private sealed class RenderCommand
        {
            public readonly System.Action Work;
            public readonly TaskCompletionSource Completion =
                new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            public RenderCommand(System.Action work)
            {
                Work = work;
            }
        }

        private readonly ConcurrentQueue<RenderCommand> renderCommands = new ConcurrentQueue<RenderCommand>();
        private bool disposed;
        
        //Rendering 
        public readonly renderManager renderMgr;
        public EngineRenderingState rt_State;

        //Input
        public readonly BaseGamepadHandler gpHandler;
        public readonly MVKeyboardState kbHandler;
        public readonly MVMouseState msHandler;

        
        public Engine()
        {
            kbHandler = new MVKeyboardState();
            msHandler = new MVMouseState();
            gpHandler = new XBOXGamePadHandler(0); //Takes any pad GLFW knows, PlayStation ones included
            RenderState.activeGamepad = gpHandler;

            renderMgr = new renderManager(); //Init renderManager of the engine

            //Systems Init
            animationSys = new AnimationSystem();
            audioSys = new AudioSystem();
            rotationSys = new RotationSystem();
            actionSys = new ActionSystem(animationSys, audioSys);
            particleSys = new ParticleSystem();

        }

        public void init()
        {
            //Init Gizmos
            //gizTranslate = new TranslationGizmo();
            //activeGizmo = gizTranslate;
            resMgr = new ResourceManager();
            RenderState.activeResMgr = resMgr; //Set reference first because the object generators use the activeResMgr
            if (!resMgr.initialized)
                resMgr.Init();

            particleSys.Init(resMgr);
            renderMgr.init(resMgr, particleSys);
            renderMgr.setupGBuffer(800, 600);
        }

        //Stop rendering right away and drop everything still queued, so nothing touches GL resources after shutdown
        public void stop()
        {
            rt_State = EngineRenderingState.EXIT;
            while (renderCommands.TryDequeue(out RenderCommand command))
                command.Completion.TrySetCanceled();
        }

        public Task InvokeOnRenderThreadAsync(System.Action work)
        {
            if (work == null)
                throw new ArgumentNullException(nameof(work));
            if (rt_State == EngineRenderingState.EXIT)
                return Task.FromException(new ObjectDisposedException(nameof(Engine)));

            RenderCommand command = new RenderCommand(work);
            renderCommands.Enqueue(command);
            return command.Completion.Task;
        }

        public void executeRenderCommands()
        {
            //Only commands present at the frame boundary are executed. A command queued by another command
            //waits for the next frame, preventing an accidental infinite producer loop.
            int count = renderCommands.Count;
            for (int i = 0; i < count; i++)
            {
                if (rt_State == EngineRenderingState.EXIT)
                    break;

                if (!renderCommands.TryDequeue(out RenderCommand command))
                    break;
                try
                {
                    command.Work();
                    command.Completion.TrySetResult();
                }
                catch (Exception ex)
                {
                    command.Completion.TrySetException(ex);
                }
            }
        }

        public void updateSystems(float dt)
        {
            actionSys.SetRunning(RenderState.renderViewSettings.EmulateActions);
            actionSys.update(dt);

            if (RenderState.renderSettings.ToggleAnimations)
            {
                animationSys.update(dt);
                rotationSys.update(dt);
            }

            //Audio animation triggers observe the animation frames reached above.
            audioSys.SetRunning(RenderState.renderViewSettings.PlayAudio);
            audioSys.update(dt);
        }

        //Main Rendering Routines

        private bool resetSceneForLoad()
        {
            bool animationsEnabled = RenderState.renderSettings.ToggleAnimations;
            RenderState.renderSettings.ToggleAnimations = false;

            actionSys.CleanUp();
            animationSys.CleanUp();
            audioSys.CleanUp();
            rotationSys.CleanUp();
            particleSys.CleanUp();

            resMgr.Cleanup();
            resMgr.Init();
            RenderState.activeResMgr = resMgr;
            RenderState.rootObject = null;
            RenderState.activeModel = null;
            RenderState.activeGizmo = null;
            RenderStats.ClearStats();
            return animationsEnabled;
        }

        private void refreshSceneRuntime(Model root)
        {
            if (root == null)
                return;

            root.updateLODDistances();
            root.update();
            root.setupSkinMatrixArrays();
            particleSys.Rebuild(root);

            //The frame update owns instance rebuilding. Rebuilding here as well copied every instance twice.
            RenderState.instancesDirty = true;
        }

        private void discoverSceneSystems(Model node)
        {
            if (node == null)
                return;
            if (node.animComponentID >= 0)
                animationSys.Add(node);
            if (node.hasComponent(typeof(RotationComponent)) >= 0)
                rotationSys.Add(node);
            if (node.actionComponentID >= 0 || node.hasComponent(typeof(InteractionComponent)) >= 0)
                actionSys.Add(node);
            audioSys.Add(node);
            foreach (Model child in node.Children)
                discoverSceneSystems(child);
        }

#if DEBUG

        private void rt_SpecularTestScene()
        {
            //Once the new scene has been loaded,
            //Initialize Palettes
            Palettes.set_palleteColors();

            bool animToggleStatus = resetSceneForLoad();

            //Setup new object
            Scene scene = new Scene();
            scene.name = "DEFAULT SCENE";


            //Add Lights
            Light l = new Light();
            l.Name = "Light 1";
            l.localPosition = new Vector3(0.2f, 0.2f, -2.0f);
            l.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            l.Intensity = 100.0f;
            l.falloff = ATTENUATION_TYPE.QUADRATIC;
            resMgr.GLlights.Add(l);
            scene.AddChild(l);
            

            Light l1 = new Light();
            l1.Name = "Light 2";
            l1.localPosition = new Vector3(0.2f, -0.2f, -2.0f);
            l1.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            l1.Intensity = 100.0f;
            l1.falloff = ATTENUATION_TYPE.QUADRATIC;
            resMgr.GLlights.Add(l1);
            scene.AddChild(l1);

            Light l2 = new Light();
            l2.Name = "Light 3";
            l2.localPosition = new Vector3(-0.2f, 0.2f, -2.0f);
            l2.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            resMgr.GLlights.Add(l2);
            l2.Intensity = 100.0f;
            l2.falloff = ATTENUATION_TYPE.QUADRATIC;
            scene.AddChild(l2);

            Light l3 = new Light();
            l3.Name = "Light 4";
            l3.localPosition = new Vector3(-0.2f, -0.2f, -2.0f);
            l3.Color = new MVector4(1.0f, 1.0f, 1.0f, 1.0f);
            resMgr.GLlights.Add(l3);
            l3.Intensity = 100.0f;
            l3.falloff = ATTENUATION_TYPE.QUADRATIC;
            scene.AddChild(l3);

            //Generate a Sphere and center it in the scene
            Model sphere = new Mesh();
            sphere.Name = "Test Sphere";
            sphere.parent = scene;
            sphere.setParentScene(scene);
            MeshMetaData sphere_metadata = new MeshMetaData();

            int bands = 80;

            sphere_metadata.batchcount = bands * bands * 6;
            sphere_metadata.batchstart_graphics = 0;
            sphere_metadata.vertrstart_graphics = 0;
            sphere_metadata.vertrend_graphics = (bands + 1) * (bands + 1) - 1;
            sphere_metadata.indicesLength = DrawElementsType.UnsignedInt;

            sphere.meshVao = new GLMeshVao(sphere_metadata);
            sphere.meshVao.type = TYPES.MESH;
            sphere.meshVao.vao = (new GMDL.Primitives.Sphere(new Vector3(), 2.0f, 40)).getVAO();


            //Sphere Material
            Material mat = new Material();
            mat.Name = "default_scn";
            
            var uf = new TkMaterialUniform_Float();
            uf.Name = "gMaterialColourVec4";
            uf.Values.X = 1.0f;
            uf.Values.Y = 0.0f;
            uf.Values.Z = 0.0f;
            uf.Values.W = 1.0f;
            mat.Uniforms_Float.Add(uf);
            
            uf = new TkMaterialUniform_Float();
            uf.Name = "gMaterialParamsVec4";
            uf.Values.X = 0.15f;
            uf.Values.Y = 0.0f;
            uf.Values.Z = 0.2f;
            uf.Values.W = 0.0f;
            mat.Uniforms_Float.Add(uf);

            mat.init();
            resMgr.GLmaterials["test_mat1"] = mat;
            sphere.meshVao.material = mat;
            sphere.instanceId = GLMeshBufferManager.addInstance(ref sphere.meshVao, sphere); //Add instance

            scene.AddChild(sphere);
            
            //Explicitly add default light to the rootObject
            scene.AddChild(resMgr.GLlights[0]);
            
            refreshSceneRuntime(scene);

            //Save scene path to resourcemanager
            resMgr.GLScenes["TEST_SCENE_1"] = scene; //Use input path

            //Populate RenderManager
            renderMgr.populate();

            scene.selected = 1;
            RenderState.rootObject = scene;


            //Reinitialize gizmos
            RenderState.activeGizmo = new TransformGizmo();

            //Restart anim worker if it was active
            RenderState.renderSettings.ToggleAnimations = animToggleStatus;
            discoverSceneSystems(RenderState.rootObject);

        }

        private void rt_addTestScene(int sceneID)
        {
            
            switch (sceneID)
            {
                case 0:
                    rt_SpecularTestScene();
                    break;
                default:
                    Common.CallBacks.Log("Non Implemented Test Scene");
                    break;
            }

        }

#endif
            
        private void rt_addRootScene(string filename)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Palettes.set_palleteColors();

            bool animToggleStatus = resetSceneForLoad();
            
            //Setup new object
            Model root;
            try
            {
                root = GEOMMBIN.LoadObjects(filename);
            }
            catch (Exception ex)
            {
                //Fall back to an empty scene so the viewer stays usable after a failed import
                Common.CallBacks.Log("Failed to import " + filename + ": " + ex);
                CallBacks.showError("Failed to import " + filename + "\n" + ex.Message, "Import Error");
                Scene empty = new Scene();
                empty.type = TYPES.MODEL;
                empty.name = "EMPTY SCENE";
                resMgr.GLScenes["EMPTY_SCENE"] = empty;
                root = empty;
            }
            finally
            {
                //Cleanup NMS related file resources
                NMSUtils.DisposeArchives();
            }

            //Explicitly add default light to the rootObject
            root.AddChild(resMgr.GLlights[0]);
            
            refreshSceneRuntime(root);

            //Populate RenderManager
            renderMgr.populate();

            root.selected = 1;
            RenderState.rootObject = root;

            //Frame the new model and fit the clip planes and move speed to its size
            RenderState.activeCam?.frameScene(root, true);
            

            //Reinitialize gizmos
            RenderState.activeGizmo = new TransformGizmo();

            //Restart anim worker if it was active
            RenderState.renderSettings.ToggleAnimations = animToggleStatus;
            discoverSceneSystems(RenderState.rootObject);
        
        }

        //New palette colours and layer picks for every procedural texture of the loaded scene
        private void rt_regenProcTextures()
        {
            if (RenderState.rootObject == null)
                return;

            Palettes.set_palleteColors();
            HashSet<string> mixed = new HashSet<string>();
            try
            {
                foreach (Material mat in resMgr.GLmaterials.Values)
                    mat.regenProcTextures(mixed);
            }
            finally
            {
                NMSUtils.DisposeArchives(); //Layer textures may have been read from the PAKs
            }
            CallBacks.Log("Regenerated {0} procedural textures", mixed.Count);
        }

        //Scene edits (SceneEdit). The node lists of the renderer are rebuilt like after loading a reference
        private void rt_addNode(Model parent, SceneEdit.NODE_KIND kind, string scenePath)
        {
            bool animToggleStatus = RenderState.renderSettings.ToggleAnimations;
            RenderState.renderSettings.ToggleAnimations = false;
            try
            {
                Model m = SceneEdit.add(parent, kind, scenePath);
                if (m != null)
                {
                    renderMgr.process_models(m);
                    renderMgr.identifyActiveShaders();
                }
            }
            catch (Exception ex)
            {
                Common.CallBacks.Log("Failed to add node: " + ex);
                CallBacks.showError("Failed to add node\n" + ex.Message, "Scene Edit");
            }
            rt_refreshScene();
            discoverSceneSystems(RenderState.rootObject);
            RenderState.renderSettings.ToggleAnimations = animToggleStatus;
        }

        private void rt_addImportedGeometry(Model parent, Model importedRoot)
        {
            bool animToggleStatus = RenderState.renderSettings.ToggleAnimations;
            RenderState.renderSettings.ToggleAnimations = false;
            try
            {
                HashSet<Material> initializedMaterials = new HashSet<Material>();
                prepareImportedGeometry(importedRoot, initializedMaterials);
                if (!SceneEdit.addImportedGeometry(parent, importedRoot))
                    return;
                renderMgr.process_models(importedRoot);
                renderMgr.identifyActiveShaders();
                rt_refreshScene();
                discoverSceneSystems(RenderState.rootObject);
                RenderState.activeCam?.frameScene(RenderState.rootObject, false);
                CallBacks.Log("[Import] Added runtime geometry hierarchy {0}", importedRoot.Name);
            }
            catch (Exception ex)
            {
                importedRoot?.Dispose();
                Common.CallBacks.Log("Failed to add imported geometry: " + ex);
                CallBacks.showError("Failed to add imported geometry\n" + ex.Message, "Geometry Import");
            }
            finally
            {
                RenderState.renderSettings.ToggleAnimations = animToggleStatus;
            }
        }

        private void prepareImportedGeometry(Model node, HashSet<Material> initializedMaterials)
        {
            if (node is Mesh mesh)
            {
                if (mesh.gobject == null || mesh.meshVao == null)
                    throw new InvalidOperationException("Imported mesh " + mesh.Name + " has no geometry buffers");
                if (mesh.meshVao.material == null)
                    throw new InvalidOperationException("Imported mesh " + mesh.Name + " has no material");
                if (initializedMaterials.Add(mesh.meshVao.material))
                {
                    mesh.meshVao.material.texMgr ??= resMgr.texMgr;
                    mesh.meshVao.material.init();
                }
                mesh.meshVao.vao = mesh.gobject.generateVAO();
                mesh.instanceId = GLMeshBufferManager.addInstance(ref mesh.meshVao, mesh);
            }

            foreach (Model child in node.Children)
                prepareImportedGeometry(child, initializedMaterials);
        }

        private void rt_removeNode(Model m)
        {
            SceneEdit.remove(m, resMgr.GLlights, n =>
            {
                actionSys.Remove(n);
                animationSys.Remove(n);
                audioSys.Remove(n);
                rotationSys.Remove(n);
                if (RenderState.activeModel == n)
                {
                    RenderState.activeModel = null;
                    RenderState.activeGizmo?.ClearReference();
                }
            });
            rt_refreshScene();
        }

        private void rt_refreshScene()
        {
            refreshSceneRuntime(RenderState.rootObject);
        }

        private void rt_addRefScene(string scene_name)
        {
            //Stop animation if on
            bool animToggleStatus = RenderState.renderSettings.ToggleAnimations;
            RenderState.renderSettings.ToggleAnimations = false;

            //Load new object
            Model new_scene;
            try
            {
                new_scene = GEOMMBIN.LoadObjects(scene_name);
            }
            catch (Exception ex)
            {
                Common.CallBacks.Log("Failed to load reference " + scene_name + ": " + ex);
                CallBacks.showError("Failed to load reference " + scene_name + "\n" + ex.Message, "Import Error");
                RenderState.renderSettings.ToggleAnimations = animToggleStatus;
                return;
            }
            finally
            {
                //Cleanup NMS related file resources
                NMSUtils.DisposeArchives();
            }

            //Explicitly add default light to the rootObject

            //Find all scenes who reference the new scene
            List<Reference> referers = new List<Reference>();
            GetReferersToScene(RenderState.rootObject, referers, scene_name);
            
            //The first referer takes the loaded scene, the others get copies. The loaded scene stays in
            //GLScenes, so it must not be disposed while it is in use
            for (int i = 0; i < referers.Count; i++)
            {
                Model inst = (i == 0) ? new_scene : new_scene.Clone();
                renderMgr.process_models(inst);
                referers[i].isRefLoaded = true;
                referers[i].AddChild(inst);
            }

            if (referers.Count == 0)
            {
                resMgr.GLScenes.Remove(scene_name);
                new_scene.Dispose();
            }
                
            Model root = RenderState.rootObject;
            refreshSceneRuntime(root);

            //Add Scene to RenderManager
            renderMgr.identifyActiveShaders(); //Re-update active shaders

            //The references can grow the scene, refresh the bounds without moving the camera
            RenderState.activeCam?.frameScene(root, false);

            root.selected = 1;
            
            //Restart anim worker if it was active
            RenderState.renderSettings.ToggleAnimations = animToggleStatus;
            discoverSceneSystems(RenderState.rootObject);

        }

        private void GetReferersToScene(Model _m, List<Reference> referers, string scene_path)
        {
            if (_m is Reference)
            {
                Reference _m_ref = (Reference) _m;
                if (_m_ref.ref_scene_filepath == scene_path && !_m_ref.isRefLoaded)
                    referers.Add(_m_ref);
            }

            foreach (Model c in _m.Children)
                GetReferersToScene(c, referers, scene_path);
        }

        public Task LoadSceneAsync(string filename) =>
            InvokeOnRenderThreadAsync(() => rt_addRootScene(filename));

        public Task LoadReferenceAsync(string sceneName) =>
            InvokeOnRenderThreadAsync(() => rt_addRefScene(sceneName));

#if DEBUG
        public Task LoadTestSceneAsync(int sceneId) =>
            InvokeOnRenderThreadAsync(() => rt_addTestScene(sceneId));
#endif

        public Task ReparentAsync(Model source, Model target) =>
            InvokeOnRenderThreadAsync(() =>
            {
                SceneEdit.reparent(source, target);
                rt_refreshScene();
            });

        public Task AddNodeAsync(Model parent, SceneEdit.NODE_KIND kind, string scenePath) =>
            InvokeOnRenderThreadAsync(() => rt_addNode(parent, kind, scenePath));

        public Task AddImportedGeometryAsync(Model parent, Model importedRoot) =>
            InvokeOnRenderThreadAsync(() => rt_addImportedGeometry(parent, importedRoot));

        public Task RemoveNodeAsync(Model node) =>
            InvokeOnRenderThreadAsync(() => rt_removeNode(node));

        public Task RegenerateProceduralTexturesAsync() =>
            InvokeOnRenderThreadAsync(rt_regenProcTextures);

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;

            stop();
            audioSys.Dispose();
            particleSys.Dispose();
            renderMgr.Dispose();
            resMgr?.Cleanup();
        }




    }
}
