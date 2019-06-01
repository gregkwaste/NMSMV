using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using MVCore.GMDL;
using MVCore;

namespace MVCore.Common
{
    public static class RenderState
    {
        //Add a random generator just for the procgen procedures
        public static Random randgen = new Random();

        //Force Procgen
        public static bool forceProcGen;

        //Keep the mvp matrix
        public static Matrix4 mvp;

        //Keep the view rotation Matrix
        public static Matrix4 rotMat;

        //Keep the main camera global
        public static Camera activeCam;

        //ResourceManager
        public static MVCore.ResourceManager activeResMgr;

    }

    public static class RenderOptions
    {
        //Set Full rendermode by default
        public static PolygonMode RENDERMODE = PolygonMode.Fill;
        public static float UseTextures = 1.0f;
        public static float UseLighting = 1.0f;
        public static System.Drawing.Color clearColor = System.Drawing.Color.FromArgb(255, 33, 33, 33);
        public static bool RenderInfo = true;
        public static bool RenderLights = true;
        public static bool RenderJoints = true;
        public static bool RenderCollisions = true;
        public static bool RenderBoundHulls = true;
        public static bool RenderDebug = false;
        public static int animFPS = 50;
    }

    public static class RenderStats
    {
        //Set Full rendermode by default
        public static int vertNum = 0;
        public static int trisNum = 0;
        public static int texturesNum = 0;
        public static int fpsCount = 0;
        
        public static void clearStats()
        {
            vertNum = 0;
            trisNum = 0;
            texturesNum = 0;
        }
    }

    //Delegates - Function Types for Callbacks
    public delegate void UpdateStatusCallBack(string msg);
    public delegate void OpenAnimCallBack(string filepath, MVCore.GMDL.scene animScene);
    public delegate void LogCallBack(string msg);
    public delegate void SendRequestCallBack(ThreadRequest req);

    public static class CallBacks
    {
        public static UpdateStatusCallBack updateStatus = null;
        public static OpenAnimCallBack openAnim = null;
        public static LogCallBack Log = null;
        public static SendRequestCallBack issueRequestToGLControl = null;
    }
}
