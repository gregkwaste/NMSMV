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

        public static RenderOptions renderOpts = new RenderOptions();


    }

    public class RenderOptions
    {
        //Set Full rendermode by default
        public static PolygonMode RENDERMODE = PolygonMode.Fill;
        public static System.Drawing.Color clearColor = System.Drawing.Color.FromArgb(255, 33, 33, 33);
        public static float _useTextures = 1.0f;
        public static float _useLighting = 1.0f;
        public static bool _useFrustumCulling = true;
        public static bool _toggleAnimations = true;
        public static bool _renderLights = true;
        public static bool _renderInfo = true;
        public static bool _renderJoints = true;
        public static bool _renderCollisions = false;
        public static bool _renderBoundHulls = false;
        public static bool _renderDebug = false;
        public static int animFPS = 50;

        //Add properties
        public static bool UseTextures
        {
            get
            {
                return (_useTextures > 0.0f);
            }

            set
            {
                if (value)
                    _useTextures = 1.0f;
                else
                    _useTextures = 0.0f;
            }
        }

        public static bool UseLighting
        {
            get
            {
                return (_useLighting > 0.0f);
            }

            set
            {
                if (value)
                    _useLighting = 1.0f;
                else
                    _useLighting = 0.0f;
            }
        }

        public static bool UseFrustumCulling
        {
            get
            {
                return _useFrustumCulling;
            }

            set
            {
                _useFrustumCulling = value;
            }
        }

        public static bool ToggleWireframe
        {
            get
            {
                return (RENDERMODE == PolygonMode.Line);
            }

            set
            {
                if (value)
                    RENDERMODE = PolygonMode.Line;
                else
                    RENDERMODE = PolygonMode.Fill;
            }
        }

        public static bool ToggleAnimations
        {
            get
            {
                return _toggleAnimations;
            }

            set
            {
                _toggleAnimations = value;
            }
        }

        public static bool RenderInfo
        {
            get
            {
                return _renderInfo;
            }

            set
            {
                _renderInfo = value;
            }
        }

        public static bool RenderLights
        {
            get
            {
                return _renderLights;
            }

            set
            {
                _renderLights = value;
            }
        }


        public static bool RenderJoints
        {
            get
            {
                return _renderJoints;
            }

            set
            {
                _renderJoints = value;
            }
        }

        public static bool RenderCollisions {
            get
            {
                return _renderCollisions;
            }

            set
            {
                _renderCollisions = value;
            }
        }

        public static bool RenderBoundHulls
        {
            get
            {
                return _renderBoundHulls;
            }

            set
            {
                _renderBoundHulls = value;
            }
        }

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
    public delegate void OpenAnimCallBack(string filepath, MVCore.GMDL.model animScene);
    public delegate void OpenPoseCallBack(string filepath, MVCore.GMDL.model animScene);
    public delegate void LogCallBack(string msg);
    public delegate void SendRequestCallBack(ThreadRequest req);

    public static class CallBacks
    {
        public static UpdateStatusCallBack updateStatus = null;
        public static OpenAnimCallBack openAnim = null;
        public static OpenPoseCallBack openPose = null;
        public static LogCallBack Log = null;
        public static SendRequestCallBack issueRequestToGLControl = null;
    }
}
