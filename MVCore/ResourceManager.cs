﻿using System;
using System.Collections.Generic;
using System.Text;
using MVCore.GMDL;

namespace MVCore
{
    //Class Which will store all the texture resources for better memory management
    public class ResourceMgr
    {
        public Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        public Dictionary<string, GMDL.Material> GLmaterials = new Dictionary<string, GMDL.Material>();
        public Dictionary<string, GMDL.GeomObject> GLgeoms = new Dictionary<string, GMDL.GeomObject>();
        public Dictionary<ulong, mainVAO> GLVaos = new Dictionary<ulong, mainVAO>();
        public List<GMDL.model> GLlights = new List<GMDL.model>();
        public List<GMDL.model> GLDecals = new List<GMDL.model>();
        public List<Camera> GLCameras = new List<Camera>();
        public int[] shader_programs;
        
        //public DebugForm DebugWin;

        public void Cleanup()
        {
            foreach (GMDL.Texture p in GLtextures.Values)
                p.Dispose();
            GLtextures.Clear();

            GLVaos.Clear(); //Individual VAos are handled from each Geom.Dispose call
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

            GC.Collect();

        }




    }
}