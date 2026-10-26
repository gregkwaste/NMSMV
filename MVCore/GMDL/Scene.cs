using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using MVCore.Utils;

namespace MVCore.GMDL
{
    public class Scene : Locator
    {
        public GeomObject gobject; //Keep GeomObject reference
        public textureManager texMgr;

        //Keep reference of all the animation Joints of the scene and the skinmatrices
        public float[] skinMats; //Final Matrices
        public Dictionary<string, Joint> jointDict;
        public Dictionary<string, Model> nodeDict;
        public int activeLOD = 0;
        public int actionComponentID = -1;

        public Scene()
        {
            type = TYPES.MODEL;
            texMgr = new textureManager();
            //Init Animation Stuff
            skinMats = new float[1024 * 16];
            jointDict = new();
            nodeDict = new();
        }

        
        //TODO Add button in the UI to toggle that shit
        private void resetAnimation()
        {
            foreach (Joint j in jointDict.Values)
            {
                j._localScale = j.BindMat.ExtractScale();
                j._localRotation = Matrix4.CreateFromQuaternion(j.BindMat.ExtractRotation());
                j._localPosition = j.BindMat.ExtractTranslation();
                j._localPoseMatrix = Matrix4.Identity;
            }
        }

        public override Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {

            return base.assimpExport(ref scn, ref meshImportStatus);
        }

        public Scene(Scene input) : base(input)
        {
            gobject = input.gobject;
        }

        public void copyFrom(Scene input)
        {
            base.copyFrom(input); //Copy base stuff
            gobject = input.gobject;
            texMgr = input.texMgr;
            
        }


        public override Model Clone()
        {
            Scene new_s = new Scene();
            new_s.copyFrom(this);

            new_s.meshVao = meshVao;
            new_s.instanceId = GLMeshBufferManager.addInstance(ref new_s.meshVao, new_s);

            //Clone children
            foreach (Model child in Children)
            {
                Model new_child = child.Clone();
                new_s.AddChild(new_child);
            }

            //Recursively update parentScene to all the new objects
            new_s.setParentScene(new_s);

            //Initialize jointDictionary
            new_s.jointDict.Clear();
            new_s.setupJointDict(new_s);

            return new_s;
        }

        //Child scenes keep their own owner: this scene's nodes belong to it, the child scene's nodes to the child
        public override void setParentScene(Scene scene)
        {
            parentScene = scene;
            foreach (Model child in Children)
                child.setParentScene(this);
        }

        public void setupJointDict(Model m)
        {
            //Child scenes have their own joints and nodes, like on the first load. A Reference node is
            //listed, but what it loaded is not
            if (m != this && m is Scene)
                return;

            if (m is Reference)
            {
                nodeDict[m.Name] = m;
                return;
            }

            //Joints go in both, like on the first load: the animation system finds the animated nodes in nodeDict
            if (m.type == TYPES.JOINT)
                jointDict[m.Name] = (Joint)m;
            nodeDict[m.Name] = m;
            
            foreach (Model c in m.Children)
                setupJointDict(c);
        }

        public override void updateLODDistances()
        {
            activeLOD = selectLOD(worldPosition, _LODDistances, _LODNum, false);
            base.updateLODDistances();
        }

        public override void updateMeshInfo(bool lod_filter = false)
        {
            //Update Skin Matrices
            foreach (Joint j in jointDict.Values)
            {
                Matrix4 jointSkinMat = j.invBMat * j.worldMat;
                MathUtils.insertMatToArray16(skinMats, j.jointIndex * 16, jointSkinMat);
            }

            base.updateMeshInfo(lod_filter);
        }

        
        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                skinMats = null;
                jointDict.Clear();

                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }

    }

}
