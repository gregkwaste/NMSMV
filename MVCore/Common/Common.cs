﻿using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using MVCore.GMDL;
using MVCore.Input;
using MVCore;
using GLSLHelper;
using WPFModelViewer;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Windows.Input;
using Newtonsoft.Json;

namespace MVCore.Common
{

    public enum MouseMovementStatus
    {
        CAMERA_MOVEMENT = 0x0,
        GIZMO_MOVEMENT,
        IDLE
    }

    public class MouseMovementState
    {
        public Vector2 Position = new Vector2();
        public Vector2 Delta = new Vector2();   
    }

    public static class RenderState
    {
        //Add a random generator just for the procgen procedures
        public static Random randgen = new Random();

        //Keep the view rotation Matrix
        public static Matrix4 rotMat = Matrix4.Identity;

        //Keep the view rotation Angles (in degrees)
        public static Vector3 rotAngles = new Vector3(0.0f);

        //RenderSettings
        public static RenderSettings renderSettings = new RenderSettings();

        //renderViewSettings
        public static RenderViewSettings renderViewSettings = new RenderViewSettings();

        //App Settings
        public static AppSettings settings = new AppSettings();
        
        //Keep the main camera global
        public static Camera activeCam;
        //Active ResourceManager
        public static ResourceManager activeResMgr;
        //RootObject
        public static Model rootObject;
        //ActiveModel
        public static Model activeModel;
        //ActiveGizmo
        public static Gizmo activeGizmo;
        //Active GamePad
        public static BaseGamepadHandler activeGamepad;

        public static bool enableShaderCompilationLog = true;
        public static string shaderCompilationLog;

        //Static methods

        public static float progressTime(double dt)
        {
            float new_time = (float) dt / 500;
            new_time = new_time % 1000.0f;
            return new_time;
        }

    }

    public class RenderViewSettings: INotifyPropertyChanged
    {
        //Properties
        
        public bool RenderInfo { get; set; } = true;

        public bool RenderLights { get; set; } = true;

        public bool RenderJoints { get; set; } = true;

        public bool RenderLocators { get; set; } = true;

        public bool RenderCollisions { get; set; } = false;

        public bool RenderBoundHulls { get; set; } = false;

        public bool RenderGizmos { get; set; } = false;

        public bool EmulateActions { get; set; } = false;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

    }

    public class RenderSettings: INotifyPropertyChanged
    {
        private int animFPS = 60;
        private bool _useVSYNC = false;
        private float _HDRExposure = 0.5f;
        //Set Full rendermode by default
        public PolygonMode RENDERMODE = PolygonMode.Fill;
        public System.Drawing.Color clearColor = System.Drawing.Color.FromArgb(255, 33, 33, 33);
        public float _useTextures = 1.0f;
        public float _useLighting = 1.0f;

        //Test Settings
#if (DEBUG)
        public float testOpt1 = 0.0f;
        public float testOpt2 = 0.0f;
        public float testOpt3 = 0.0f;
#endif

        //Properties

        public bool UseFXAA { get; set; } = true;

        public bool UseBLOOM { get; set; } = true;

        public bool UseVSYNC
        {
            get
            {
                return _useVSYNC;
            }
            set
            {
                _useVSYNC = value;
                NotifyPropertyChanged("UseVSYNC");
            }
        }

        
        public int AnimFPS
        {
            get => animFPS;
            set
            {
                animFPS = value;
                NotifyPropertyChanged("AnimFPS");
            }
        }

        public float HDRExposure
        {
            get => _HDRExposure;
            set
            {
                _HDRExposure = value;
                NotifyPropertyChanged("HDRExposure");
            }
        }

        //Add properties
        public bool UseTextures
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
                NotifyPropertyChanged("UseTextures");
            }
        }

        public bool UseLighting
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
                NotifyPropertyChanged("UseLighting");
            }
        }

        public bool UseFrustumCulling { get; set; } = true;

        public bool LODFiltering { get; set; } = true;

        public bool ToggleWireframe
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

        public bool ToggleAnimations { get; set; } = true;

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

    }

    public class AppSettings : INotifyPropertyChanged
    {
        private int forceProcGen;
        private string gamedir;
        private string unpackdir;
        private int _procGenWinNum;
        [JsonIgnore]
        public Key KeyUp = Key.W;
        [JsonIgnore]
        public Key KeyDown = Key.S;
        [JsonIgnore]
        public Key KeyLeft = Key.A;
        [JsonIgnore]
        public Key KeyRight = Key.D;

        public string KeyUpProp { 
            get
            {
                return KeyUp.ToString();
            }

            set
            {
                KeyUp = (Key) Enum.Parse(typeof(Key), value);
                NotifyPropertyChanged("KeyUpProp");
            }
        }

        public string KeyDownProp
        {
            get
            {
                return KeyDown.ToString();
            }

            set
            {
                KeyDown = (Key)Enum.Parse(typeof(Key), value);
                NotifyPropertyChanged("KeyDownProp");
            }
        }

        public string KeyLeftProp
        {
            get
            {
                return KeyLeft.ToString();
            }

            set
            {
                KeyLeft = (Key)Enum.Parse(typeof(Key), value);
                NotifyPropertyChanged("KeyLeftProp");
            }
        }

        public string KeyRightProp
        {
            get
            {
                return KeyRight.ToString();
            }

            set
            {
                KeyRight = (Key)Enum.Parse(typeof(Key), value);
                NotifyPropertyChanged("KeyRightProp");
            }
        }

        
        public string GameDir
        {
            get
            {
                return gamedir;
            }

            set
            {
                gamedir = value;
                NotifyPropertyChanged("GameDir");
            }
        }

        public string UnpackDir
        {

            get
            {
                return unpackdir;
            }

            set
            {
                unpackdir = value;
                NotifyPropertyChanged("UnpackDir");
            }

        }

        public int ProcGenWinNum 
        {
            get
            {
                return _procGenWinNum;
            }
            set
            {
                _procGenWinNum = value;
                NotifyPropertyChanged("ProcGenWinNum");
            }
        }

        public int ForceProcGen
        {
            get => forceProcGen;
            set
            {
                if (value > 0)
                    forceProcGen = 1;
                else
                    forceProcGen = 0;
                NotifyPropertyChanged("ForceProcGen");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        
        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }

    public static class RenderStats
    {
        //Set Full rendermode by default
        public static int vertNum = 0;
        public static int trisNum = 0;
        public static int texturesNum = 0;
        public static float fpsCount = 0;
        public static int occludedNum = 0;

        public static void ClearStats()
        {
            vertNum = 0;
            trisNum = 0;
            texturesNum = 0;
        }
    }

    //Delegates - Function Types for Common.CallBacks
    public delegate void UpdateStatusCallBack(string msg);
    public delegate void OpenAnimCallBack(string filepath, Model animScene);
    public delegate void OpenPoseCallBack(string filepath, Model animScene);
    public delegate void LogCallBack(params object[] msg);
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
