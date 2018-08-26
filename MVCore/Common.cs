using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using MVCore.GMDL;

namespace MVCore.Common
{
    public static class RenderState
    {
        //Add a random generator just for the procgen procedures
        public static Random randgen = new Random();

        //Force Procgen
        public static bool forceProcGen;

        //Temporarily store mvp matrix
        public static Matrix4 mvp;

        //Current Gbuffer
        public static GBuffer gbuf;

        //ResourceManager
        public static MVCore.ResourceMgr activeResMgr;

    }

    //Delegates - Function Types for Callbacks
    public delegate void UpdateStatusCallBack(string msg);
    public delegate void OpenAnimCallBack(string filepath, MVCore.GMDL.scene animScene);
    public delegate void LogCallBack(string msg);

    public static class CallBacks
    {
        public static UpdateStatusCallBack updateStatus = null;
        public static OpenAnimCallBack openAnim = null;
        public static LogCallBack Log = null;
    }
}
