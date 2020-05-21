using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using MVCore.GMDL;
using MVCore;
using GLSLHelper;

namespace MVCore.Common
{
    public static class RenderState
    {
        //Add a random generator just for the procgen procedures
        public static Random randgen = new Random();

        //Force Procgen
        public static bool forceProcGen;

        //Keep the view rotation Matrix
        public static Matrix4 rotMat;

        //Keep the main camera global
        public static Camera activeCam;

        //ResourceManager
        public static MVCore.ResourceManager activeResMgr;

        public static RenderOptions renderOpts = new RenderOptions();

        public static bool enableShaderCompilationLog = false;
        public static string shaderCompilationLog;

    }

    public class RenderOptions
    {
        //Set Full rendermode by default
        public static PolygonMode RENDERMODE = PolygonMode.Fill;
        public static System.Drawing.Color clearColor = System.Drawing.Color.FromArgb(255, 33, 33, 33);
        public static float _useTextures = 1.0f;
        public static float _useLighting = 1.0f;
        private static bool _useFrustumCulling = true;
        private static bool _useLODFiltering = true;
        private static bool _useFXAA = true;
        private static bool _useVSYNC = false;
        private static bool _useBLOOM = true;
        private static bool _toggleAnimations = true;
        private static bool _renderLights = true;
        private static bool _renderInfo = true;
        private static bool _renderJoints = true;
        private static bool _renderLocators = true;
        private static bool _renderCollisions = false;
        private static bool _renderBoundHulls = false;
        private static bool _renderDebug = false;
        public static int animFPS = 60;
        public static float _HDRExposure = 0.2f;

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
            get => _useFrustumCulling;
            set => _useFrustumCulling = value;
        }

        public static bool LODFiltering
        {
            get => _useLODFiltering;
            set => _useLODFiltering = value;
        }

        public static bool UseFXAA
        {
            get => _useFXAA;
            set => _useFXAA = value;
        }

        public static bool UseBLOOM
        {
            get => _useBLOOM;
            set => _useBLOOM = value;
        }

        public static bool UseVSYNC
        {
            get => _useVSYNC;
            set => _useVSYNC = value;
        }


        public static bool ToggleWireframe
        {
            get => (RENDERMODE == PolygonMode.Line);
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
            get => _toggleAnimations;
            set => _toggleAnimations = value;
        }

        public static bool RenderInfo
        {
            get => _renderInfo;
            set => _renderInfo = value;
        }

        public static bool RenderLights
        {
            get => _renderLights;
            set => _renderLights = value;
        }


        public static bool RenderJoints
        {
            get => _renderJoints;
            set => _renderJoints = value;
        }

        public static bool RenderLocators
        {
            get => _renderLocators;
            set => _renderLocators = value;
            
        }

        public static bool RenderCollisions {
            get => _renderCollisions;
            set => _renderCollisions = value;
        }

        public static bool RenderBoundHulls
        {
            get => _renderBoundHulls;
            set => _renderBoundHulls = value;
        }

        public static string AnimFPS
        {
            get => animFPS.ToString();
            set
            {
                int.TryParse(value, out animFPS);
            }
        }

        public static string HDRExposure
        {
            get => _HDRExposure.ToString();
            set
            {
                float.TryParse(value, out _HDRExposure);
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
        public static int occludedNum = 0;

        public static void ClearStats()
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
    public delegate void SendRequestCallBack(ref ThreadRequest req);
    
    public static class CallBacks
    {
        public static UpdateStatusCallBack updateStatus = null;
        public static OpenAnimCallBack openAnim = null;
        public static OpenPoseCallBack openPose = null;
        public static LogCallBack Log = null;
        public static SendRequestCallBack issueRequestToGLControl = null;
    }
}
