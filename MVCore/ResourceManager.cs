using System;
using System.Collections.Generic;
using System.Text;
using MVCore.GMDL;

namespace MVCore
{
    public interface baseResourceManager
    {
        void cleanup();
        
    }
    

    //Class Which will store all the texture resources for better memory management
    public class ResourceManager
    {
        //public Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        public Dictionary<string, GMDL.Material> GLmaterials = new Dictionary<string, GMDL.Material>();
        public Dictionary<string, GMDL.GeomObject> GLgeoms = new Dictionary<string, GMDL.GeomObject>();
        public Dictionary<string, GMDL.scene> GLScenes = new Dictionary<string, GMDL.scene>();
        public Dictionary<string, GMDL.Texture> GLTextures = new Dictionary<string, GMDL.Texture>();

        public Dictionary<ulong, mainVAO> GLVaos = new Dictionary<ulong, mainVAO>();
        public Dictionary<string, mainVAO> GLPrimitiveVaos = new Dictionary<string, mainVAO>();
        public List<GMDL.Light> GLlights = new List<GMDL.Light>();
        public List<GMDL.Decal> GLDecals = new List<GMDL.Decal>();
        public List<Camera> GLCameras = new List<Camera>();
        //public Dictionary<string, int> GLShaders = new Dictionary<string, int>();
        public Dictionary<string, GLSLHelper.GLSLShaderConfig> GLShaders = new Dictionary<string, GLSLHelper.GLSLShaderConfig>();
        //public int[] shader_programs;

        public textureManager texMgr = new textureManager();

        //Procedural Generation Options
        //TODO: This is 99% NOT correct
        //public Dictionary<string, int> procTextureLayerSelections = new Dictionary<string, int>();
        
        //public DebugForm DebugWin;

        public void Cleanup()
        {
            //Cleanup global texture manager
            texMgr.cleanup();
            //procTextureLayerSelections.Clear();

            foreach (GMDL.scene p in GLScenes.Values)
                p.Dispose();
            GLScenes.Clear();

            //Cleanup VAOs
            foreach (GMDL.mainVAO v in GLVaos.Values)
                v.Dispose();
            GLVaos.Clear();

            //Cleanup Geom Objects
            foreach (GMDL.GeomObject p in GLgeoms.Values)
                p.Dispose();
            GLgeoms.Clear();

            //Cleanup Decals
            foreach (GMDL.model p in GLDecals)
                p.Dispose();
            GLDecals.Clear();

            //Cleanup Materials
            foreach (GMDL.Material p in GLmaterials.Values)
                p.Dispose();
            GLmaterials.Clear();

            //Cleanup Lights
            foreach (GMDL.Light p in GLlights)
                p.Dispose();
            GLlights.Clear();

            //Cleanup Cameras
            //TODO: Make Camera Disposable
            //foreach (GMDL.Camera p in GLCameras)
            //    p.Dispose();
            GLCameras.Clear();
        
        }
    }

    public class textureManager: baseResourceManager
    {
        public Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        private textureManager masterTexManager;

        public textureManager()
        {
        
        }

        public void cleanup()
        {
            deleteTextures();
            removeTextures();
        }

        public void deleteTextures()
        {
            foreach (GMDL.Texture p in GLtextures.Values)
                p.Dispose();
        }

        public void removeTextures()
        {
            //Warning does not free the textures. Use wisely
            GLtextures.Clear();
        }

        public bool hasTexture(string name)
        {
            //Search on the masterTextureManager first
            if (masterTexManager != null && masterTexManager.hasTexture(name))
                return true;
            else
                return GLtextures.ContainsKey(name);
        }

        public void addTexture(GMDL.Texture t)
        {
            GLtextures[t.name] = t;
        }

        public GMDL.Texture getTexture(string name)
        {
            //Fetches the textures from the masterTexture Manager if it exists
            if (masterTexManager != null && masterTexManager.hasTexture(name))
                return masterTexManager.getTexture(name);
            else
                return GLtextures[name];
        }

        public void deleteTexture(string name)
        {
            if (GLtextures.ContainsKey(name))
            {
                GMDL.Texture t = GLtextures[name];
                t.Dispose();
                GLtextures.Remove(name);
            }
            else
            {
                Console.WriteLine("Texture not found\n");
            }
        }

        public void setMasterTexManager(textureManager mtMgr)
        {
            masterTexManager = mtMgr;
        }



    }


   
}
