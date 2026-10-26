using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Input;
using Assimp.Unmanaged;
using GLSLHelper;
//Custom Imports
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;
using MVCore.Input;
using MVCore.Engine.Systems;
using OpenTK.Mathematics;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using OpenTK.Wpf;
using System.Timers;
using MVCore.Engine;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using System.Windows.Media;
using System.Windows.Controls;
using System.Linq.Expressions;
using System.Windows;
using System.Threading.Tasks;
using libMBIN.NMS.GameComponents;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Model_Viewer
{
    public class CGLControl
    {
        //Mouse Pos
        //private MouseMovementState mouseState = new MouseMovementState();
        private MouseMovementStatus mouseMovementStatus = MouseMovementStatus.IDLE;

        //Control Identifier
        private int index;

        private GLWpfControl _control;
        private CameraPos _camPos = new();

        //Animation Stuff
        private bool animationStatus = false;

        //Scene Stuff
        //public Model rootObject;
        public Model activeModel; //Active Model Reference
        public Queue<Model> modelUpdateQueue = new Queue<Model>();
        private Matrix4 lastViewMat; //Camera state the instances were last culled with
        private Matrix4 lastRotMat;
        public List<Tuple<AnimComponent, AnimData>> activeAnimScenes = new List<Tuple<AnimComponent, AnimData>>();

        //Gizmo
        public Gizmo activeGizmo;
        public TranslationGizmo gizTranslate;

        //Rendering Engine
        public Engine engine;

        //Rendering Thread
        private bool rendering_thread_initialized = false;
        //Main Work Thread
        private Thread work_thread;

        //Init-GUI Related
        private ContextMenu contextMenuStrip1;
        private System.ComponentModel.IContainer components;


        //Frame clock. dt is the last frame's length in milliseconds (animations, actions and shader time use ms)
        private int frames = 0;
        private double dt = 0.0;
        private readonly Stopwatch frameClock = new Stopwatch();
        private readonly Stopwatch fpsClock = new Stopwatch();
        //A longer frame (a file load, a pause, a breakpoint) counts as this long, so the camera and animations don't jump
        private const double MAX_FRAME_MS = 100.0;

        //Completed once the engine (resource manager, cameras, shaders) exists, which happens on the first render
        public readonly TaskCompletionSource ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        //Input Polling Thread
        private bool _capture_input = false;
        
        //Gamepad Setup
        private bool disposed;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        private void registerFunctions()
        {
            _control.Loaded += new RoutedEventHandler(genericLoad);
            _control.SizeChanged += new SizeChangedEventHandler(OnResize);
            
            _control.Render += new((TimeSpan time) =>
            {
                if (!rendering_thread_initialized)
                {
                    engine.init();
                    engine.renderMgr.screen_fbo = _control.Framebuffer;
                    rendering_thread_initialized = true;

                    //Set Camera Settings
                    MVCore.GMDL.Camera.SetCameraSettings(ref RenderState.activeCam, RenderState.camSettings.settings);
                    MVCore.GMDL.Camera.SetCameraPosition(ref RenderState.activeCam,
                        new OpenTK.Mathematics.Vector3(RenderState.camSettings.PosX, 
                        RenderState.camSettings.PosY, 
                        RenderState.camSettings.PosZ));
                    RenderState.activeCam.yaw = RenderState.camSettings.Yaw;
                    RenderState.activeCam.pitch = RenderState.camSettings.Pitch;

                    ready.TrySetResult();
                }

                RenderLocal();
            });

            //A key released while another window has focus never reaches us, so forget all keys when focus goes
            _control.LostKeyboardFocus += new((object sender, KeyboardFocusChangedEventArgs args) =>
            {
                engine.kbHandler.Clear();
            });

            //Capture can be taken away mid-drag (Alt+Tab, a dialog), which ends the camera drag
            _control.LostMouseCapture += new((object sender, MouseEventArgs args) =>
            {
                if (mouseMovementStatus == MouseMovementStatus.CAMERA_MOVEMENT)
                    mouseMovementStatus = MouseMovementStatus.IDLE;
            });

            //Mouse wheel during a camera drag changes the move speed by 25% per notch
            _control.MouseWheel += new((object sender, System.Windows.Input.MouseWheelEventArgs args) =>
            {
                if (mouseMovementStatus != MouseMovementStatus.CAMERA_MOVEMENT)
                    return;
                RenderState.activeCam.settings.Speed *= (float) Math.Pow(1.25, args.Delta / 120.0);
                args.Handled = true;
            });

            _control.MouseDown += new MouseButtonEventHandler(genericMouseDown);
            _control.MouseMove += new MouseEventHandler(genericMouseMove);
            _control.MouseUp += new MouseButtonEventHandler(genericMouseUp);
            _control.KeyDown += new KeyEventHandler(generic_KeyDown);
            _control.KeyUp += new KeyEventHandler(generic_KeyUp);

            //Register handlers after the engine has been initialized
            _control.MouseLeave += new((object sender, MouseEventArgs args) =>
            {
                //A camera drag keeps the mouse captured and goes on outside the viewport
                if (!_control.IsMouseCaptured)
                    _capture_input = false;
            });

            _control.MouseEnter += new((object sender, MouseEventArgs args) =>
            {
                _capture_input = true;
                engine.kbHandler.Clear();
                engine.msHandler.Clear();

                //Start mouse deltas from here, not from where the cursor left the viewport
                Point p = args.GetPosition(_control);
                engine.msHandler.Position = engine.msHandler.PrevPosition = new((float)p.X, (float)p.Y);
                engine.msHandler.Delta = Vector2.Zero;

                //GLWpfControl 4.3+ only gets key events while focused (4.2 hooked the window).
                //Take focus on hover like before, unless the user is typing in a text box
                if (Keyboard.FocusedElement is not System.Windows.Controls.Primitives.TextBoxBase)
                    _control.Focus();
            });
        }

        //Default Constructor
        public CGLControl(GLWpfControl baseControl)
        {
            _control = baseControl;
            
            //Default Setup
            RenderState.rotAngles.Y = 0;
            
            //Generate Engine instance
            engine = new Engine();
            
            registerFunctions();

            
        }

        private void applyPendingResize()
        {
            //The control can recreate its framebuffer when it resizes, so fetch it every frame
            int w = _control.FrameBufferWidth;
            int h = _control.FrameBufferHeight;
            engine.renderMgr.screen_fbo = _control.Framebuffer;
            engine.renderMgr.setScreenSize(w, h);

            if (w <= 0 || h <= 0 || engine.renderMgr.hasRenderSize(w, h))
                return; //Minimized or already the right size
            if (resizeTimer.IsRunning && resizeTimer.ElapsedMilliseconds < RESIZE_SETTLE_MS)
                return; //Still resizing

            resizeTimer.Reset();
            engine.renderMgr.resize(w, h);
        }

        private void RenderLocal()
        {
            //One clock for everything that moves: the camera gets seconds, the rest milliseconds
            dt = frameClock.IsRunning ? Math.Min(frameClock.Elapsed.TotalMilliseconds, MAX_FRAME_MS) : 0.0;
            frameClock.Restart();

            if (engine.rt_State != EngineRenderingState.EXIT)
            {
                engine.handleRequests();

                if (engine.rt_State == EngineRenderingState.ACTIVE)
                {
                    applyPendingResize();
                    if (_capture_input)
                        processInput(dt / 1000.0);
                    frameUpdate();
                    engine.renderMgr.render(); //Render Everything

                    GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                }
            }

            //GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            

        }

        //Stop the engine, then free the GL resources with this control's context current.
        //Called from the window's Closing handler, before the control and its context go away
        public void shutdown()
        {
            engine.stop();
            try
            {
                _control.Context?.MakeCurrent();
                engine.resMgr.Cleanup();
            }
            catch (Exception ex)
            {
                CallBacks.Log("GL cleanup on exit failed: " + ex.Message);
            }
        }

        public void setActiveCam(int index)
        {
            if (index < 0 || index >= RenderState.activeResMgr.GLCameras.Count)
                return;
            if (RenderState.activeCam != null)
                RenderState.activeCam.isActive = false;
            RenderState.activeCam = RenderState.activeResMgr.GLCameras[index];
            RenderState.activeCam.isActive = true;
            CallBacks.Log("Switching Camera to {0}", index);
        }

        //Constructor
        public CGLControl(int index)
        {
            registerFunctions();
            
            //Set Control Identifiers
            this.index = index;
        }


#region AddObjectMethods

        private void addCamera(bool cull = true)
        {
            //Set Camera position
            Camera cam = new Camera(90, -1, 0, cull);
            cam.isActive = false;
            RenderState.activeResMgr.GLCameras.Add(cam);
        }


#endregion AddObjectMethods


#region GLControl Methods
        private void genericLoad(object sender, EventArgs e)
        {

            InitializeComponent();
        }

        private void genericMouseMove(object sender, MouseEventArgs e)
        {
            //Debug.WriteLine($"Mouse moving {e.Timestamp}");
            //int delta_x = (int) (Math.Pow(activeCam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(activeCam.fov, 4) * (e.Y - mouse_y));
            Point p = e.GetPosition(_control);
            var temp = engine.msHandler.Position;
            engine.msHandler.PrevPosition = temp;
            engine.msHandler.Position = new((float)p.X,
                                            (float)p.Y);

            //Debug.WriteLine("Mouse Old Pos {0} {1}", engine.msHandler.PrevPosition.X, engine.msHandler.PrevPosition.Y);
            //Debug.WriteLine("Mouse New Pos {0} {1}", engine.msHandler.Position.X, engine.msHandler.Position.Y);

            engine.msHandler.Delta = engine.msHandler.Position - temp;



            //Update Camera Rotation, only for a drag that started in the viewport
            if (mouseMovementStatus == MouseMovementStatus.CAMERA_MOVEMENT && e.LeftButton == MouseButtonState.Pressed)
            {
                //pitch is the horizontal angle and yaw the vertical one. The vertical angle is kept inside the
                //view limit, so dragging back responds at once, and the horizontal one wraps around
                Camera cam = RenderState.activeCam;
                cam.pitch = (cam.pitch + engine.msHandler.Delta.X * cam.settings.Sensitivity) % 360.0f;
                cam.yaw = Math.Clamp(cam.yaw - engine.msHandler.Delta.Y * cam.settings.Sensitivity, -89.0f, 89.0f);
            }

            //Take action
            switch (mouseMovementStatus)
            {
                case MouseMovementStatus.CAMERA_MOVEMENT:
                    {
                        break;
                    }
                case MouseMovementStatus.GIZMO_MOVEMENT:
                    {
                        //TODO
                        break;
                    }
                default:
                    break;

            }

            e.Handled = true;
        }

        private void genericMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _control.Focus(); //Clicking the viewport always gives it the keyboard
            if (!_capture_input)
            {
                e.Handled = true;
                return;
            }

            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Left, e.LeftButton == MouseButtonState.Pressed);
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Right, e.RightButton == MouseButtonState.Pressed);
            
            if (activeGizmo != null && (e.LeftButton == MouseButtonState.Pressed) && activeGizmo.isActive)
            {
                //Engage movement
                CallBacks.Log("Engaging gizmo movement");
                mouseMovementStatus = MouseMovementStatus.GIZMO_MOVEMENT;
            } else if (e.LeftButton == MouseButtonState.Pressed)
            {
                mouseMovementStatus = MouseMovementStatus.CAMERA_MOVEMENT;
                _control.CaptureMouse(); //Keep turning when the cursor leaves the viewport
            }

            e.Handled = true;
        }

        //dt in seconds
        private void processInput(double dt)
        {
            float x = engine.kbHandler.getKeyStatus(RenderState.settings.KeyRight) - engine.kbHandler.getKeyStatus(RenderState.settings.KeyLeft);
            float z = engine.kbHandler.getKeyStatus(RenderState.settings.KeyUp) - engine.kbHandler.getKeyStatus(RenderState.settings.KeyDown);
            float y = engine.kbHandler.getKeyStatus(Key.R) - engine.kbHandler.getKeyStatus(Key.F);
            Vector3 newPos = RenderState.activeCam.Right * x + RenderState.activeCam.Front * z + Camera.BaseUp * y;
            if (newPos.LengthSquared > 1.0f)
                newPos.Normalize(); //Diagonals are not faster
            newPos *= (float) dt * RenderState.activeCam.settings.Speed * RenderState.activeCam.speedScale;
            newPos += RenderState.activeCam.Position;

            Camera.SetCameraPosition(ref RenderState.activeCam, newPos);
            //Debug.WriteLine("Deltas {0} {1}", _camPos.Rotation.X, _camPos.Rotation.Y);
            //RenderState.activeCam?.updateTarget(_camPos, (float) input_poller.Interval);
            //Scene rotation in degrees per second, so it doesn't depend on the frame rate
            RenderState.rotAngles.Y += 30.0f * (float) dt * (engine.kbHandler.getKeyStatus(Key.E) - engine.kbHandler.getKeyStatus(Key.Q));
            RenderState.rotAngles.Y %= 360;
        }

        private void genericMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            //End a camera drag wherever the button is released
            if (e.ChangedButton == System.Windows.Input.MouseButton.Left && mouseMovementStatus == MouseMovementStatus.CAMERA_MOVEMENT)
            {
                mouseMovementStatus = MouseMovementStatus.IDLE;
                _control.ReleaseMouseCapture();
                _capture_input = _control.IsMouseOver;
                engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Left, false);
                e.Handled = true;
                return;
            }

            if (!_capture_input)
            {
                e.Handled = true;
                return;
            }

            //The event carries the state after the release, so a released button reads as not pressed
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Left, e.LeftButton == MouseButtonState.Pressed);
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Right, e.RightButton == MouseButtonState.Pressed);
            

            if (e.LeftButton == MouseButtonState.Released && e.ChangedButton == System.Windows.Input.MouseButton.Left)
            {
                mouseMovementStatus = MouseMovementStatus.IDLE;
            } else if (e.RightButton == MouseButtonState.Released && e.ChangedButton == System.Windows.Input.MouseButton.Right)
            {
                contextMenuStrip1.IsOpen = true;
            }
            e.Handled = true;
        }

        private void generic_KeyUp(object sender, KeyEventArgs e)
        {
            if (!_capture_input)
            {
                e.Handled = true;
                return;
            }

            engine.kbHandler.SetKeyState(e.Key, false);
            e.Handled = true;
        }

        private void generic_KeyDown(object sender, KeyEventArgs e)
        {
            if (!_capture_input)
            {
                e.Handled = true;
                return;
            }

            engine.kbHandler.SetKeyState(e.Key, true);

            //Debug.WriteLine("Key pressed {0}",e.Key.ToString());
            switch (e.Key)
            {
                //Frame the model
                case Key.Home:
                    if (RenderState.rootObject != null)
                        RenderState.activeCam.frameScene(RenderState.rootObject, true);
                    break;
                //Light Rotation
                case Key.N:
                    engine.light_angle_y -= 1;
                    break;
                case Key.M:
                    engine.light_angle_y += 1;
                    break;
                case Key.OemComma:
                    engine.light_angle_x -= 1;
                    break;
                case Key.OemPeriod:
                    engine.light_angle_x += 1;
                    break;
                /*
                //Toggle Wireframe
                case Keys.I:
                    if (RenderOptions.RENDERMODE == PolygonMode.Fill)
                        RenderOptions.RENDERMODE = PolygonMode.Line;
                    else
                        RenderOptions.RENDERMODE = PolygonMode.Fill;
                    break;
                //Toggle Texture Render
                case Keys.O:
                    RenderOptions.UseTextures = 1.0f - RenderOptions.UseTextures;
                    break;
                //Toggle Collisions Render
                case Keys.OemOpenBrackets:
                    RenderOptions.RenderCollisions = !RenderOptions.RenderCollisions;
                    break;
                //Toggle Debug Render
                case Keys.OemCloseBrackets:
                    RenderOptions.RenderDebug = !RenderOptions.RenderDebug;
                    break;
                */
                //Switch cameras
                case Key.NumPad0:
                    //Cycle through the cameras, when there is more than one
                    if (engine.resMgr.GLCameras.Count > 1)
                        setActiveCam((engine.resMgr.GLCameras.IndexOf(RenderState.activeCam) + 1) % engine.resMgr.GLCameras.Count);
                    break;
                //Animation playback (Play/Pause Mode) with Space
                //case Keys.Space:
                //    toggleAnimation();
                //    break;
                default:
                    //Common.CallBacks.Log("Not Implemented Yet");
                    break;
            }

            e.Handled = true;
        }

        //Reallocating every render target on each step of a window drag makes resizing stutter, so the
        //targets are resized once the size has stopped changing. Until then the old ones are stretched
        private readonly Stopwatch resizeTimer = new Stopwatch();
        private const int RESIZE_SETTLE_MS = 100;

        private void OnResize(object sender, SizeChangedEventArgs e)
        {
            resizeTimer.Restart();
#if DEBUG
            CallBacks.Log($"RESIZING VIEWPORT {e.NewSize.Width} {e.NewSize.Height} RENDERSIZE {_control.RenderSize.Width} {_control.RenderSize.Height} FRAMEBUFFERSIZE {_control.FrameBufferWidth}  {_control.FrameBufferHeight}");
            CallBacks.Log($"Control Size {_control.Width}  {_control.Height}");
#endif
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            
            MenuItem obj_export = new MenuItem();
            obj_export.Header = "Export to Obj";
            obj_export.Click += new RoutedEventHandler(exportToObjToolStripMenuItem_Click);

            MenuItem assimp_export = new MenuItem();
            assimp_export.Header = "Export to Assimp";
            assimp_export.Click += new RoutedEventHandler(exportToAssimp);

            contextMenuStrip1 = new ContextMenu();
            contextMenuStrip1.Items.Add(obj_export);
            //contextMenuStrip1.Items.Add(assimp_export);
        }

               
        

#endregion GLControl Methods

#region ShaderMethods

#endregion ShaderMethods

#region ContextMethods

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to obj");
            Microsoft.Win32.SaveFileDialog sv = new();
            sv.Filter = "OBJ Files | *.obj";
            sv.DefaultExt = "obj";
            
            if (sv.ShowDialog() != true)
                return;

            StreamWriter obj = new StreamWriter(sv.FileName);

            obj.WriteLine("# No Mans Sky Model Viewer OBJ File:");
            obj.WriteLine("# www.3dgamedevblog.com");

            //Iterate in objects
            uint index = 1;
            findGeoms(RenderState.rootObject, obj, ref index);
            
            obj.Close();

        }

        private void exportToAssimp(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to assimp");

            if (RenderState.rootObject != null)
            {
                Assimp.AssimpContext ctx = new Assimp.AssimpContext();
                
                Dictionary<int, int> meshImportStatus = new Dictionary<int, int>();
                Assimp.Scene aScene = new Assimp.Scene();
                Assimp.Node rootNode = RenderState.rootObject.assimpExport(ref aScene, ref meshImportStatus);
                aScene.RootNode = rootNode;

                //add a single material for now
                Assimp.Material aMat = new Assimp.Material();
                aMat.Name = "testMaterial";
                aScene.Materials.Add(aMat);

                Assimp.ExportFormatDescription[] supported_formats = ctx.GetSupportedExportFormats();
                //Assimp.Scene blenderScene = ctx.ImportFile("SimpleSkin.gltf");
                //ctx.ExportFile(blenderScene, "SimpleSkin.glb", "glb2");
                try
                {
                    ctx.ExportFile(aScene, "test.glb", "glb2");
                    //ctx.ExportFile(aScene, "test.fbx", "fbx");
                }
                catch (Exception ex)
                {
                    CallBacks.Log(ex.Message);
                }
                

            }
            
        }

        private void findGeoms(Model m, StreamWriter s, ref uint index)
        {
            switch (m.type)
            {
                case TYPES.MESH:
                    {
                        //Get converted text
                        Mesh me = (Mesh) m;
                        if (m.renderable)
                            me.writeGeomToStream(s, ref index);
                        break;
                    }
                case TYPES.COLLISION:
                    CallBacks.Log("NOT IMPLEMENTED YET");
                    break;
                default:
                    break;
            }
            
            foreach (Model c in m.Children)
                findGeoms(c, s, ref index);
        }

        private Vector3 unProject(Vector2 vec)
        {
            Vector3 v;

            Vector2 screenPos = vec;

            //Normalize screenPos
            float fov_fact = 0.5f * RenderState.activeCam.settings._fovRadians;
            float dx = fov_fact * (screenPos.X - (float) (0.5 * _control.RenderSize.Width)) / (float)_control.RenderSize.Width;
            float dy = -fov_fact * (screenPos.Y - (float) (0.5 * _control.RenderSize.Height)) / (float)_control.RenderSize.Height;

            throw new Exception("WRONG IMPLEMENTATION, FIX");
            //v = RenderState.activeCam.Front;
            //v += 2.0f * RenderState.activeCam.Up * dy;
            //v += 2.0f * RenderState.activeCam.Right * dx;

            return v.Normalized();
        }

        
        private void selectObject(Vector2 screenPos)
        {
            //WARNING: NOT ACCURATE
            //Vector3 near = unProject(new Vector3(screenPos.X, screenPos.Y, 0.0f));
            //Vector3 near = unProject(new Vector3(screenPos.X, screenPos.Y, 0.0f));
            Vector3 v = unProject(new Vector2(screenPos.X, screenPos.Y));
            
            CallBacks.Log("Ray Vector : {0}, {1}, {2} ", v.X, v.Y, v.Z);

            //Intersect Ray with scene
            Model intersectedModel = null;
            bool intersectionStatus = false;
            float intersectionDistance = float.MaxValue;
            findIntersectedModel(RenderState.rootObject, v, ref intersectionStatus, ref intersectedModel, ref intersectionDistance);

            if (intersectedModel != null)
            {
                CallBacks.Log("Ray intersects model : " + intersectedModel.name);
                gizTranslate.setReference(intersectedModel);
                gizTranslate.update();
            }

        }

        
        private void findIntersectedModel(Model m, Vector3 ray, ref bool foundIntersection, ref Model interSectedModel, ref float intersectionDistance)
        {
            if (!m.renderable)
                return;
            
            //Skip if intersection was found
            if (foundIntersection)
                return;
            
            //Check interection with m
            if (m.intersects(RenderState.activeCam.Position, ray, ref intersectionDistance))
            {
                foundIntersection = true;
                interSectedModel = m;
                return;
            }
                
            //Iterate in children
            foreach (Model c in m.Children)
            { 
                findIntersectedModel(c, ray, ref foundIntersection, ref interSectedModel, ref intersectionDistance);
            }


        }

#endregion ContextMethods

        public void issueRenderingRequest (ref ThreadRequest req)
        {
            engine.issueRenderingRequest(ref req);
        }

        public void waitForRenderingRequest(ref ThreadRequest req)
        {
            while (true)
            {
                lock (req)
                {
                    if (req.status == THREAD_REQUEST_STATUS.FINISHED)
                        return;
                    else
                        Thread.Sleep(2);
                }
            }
        }

        public void addTestScene(int sceneID)
        {
            //Cleanup first
            modelUpdateQueue.Clear(); //Clear Update Queues

            //Generate Request for rendering thread
            ThreadRequest req1 = new ThreadRequest();
            req1.type = THREAD_REQUEST_TYPE.NEW_TEST_SCENE_REQUEST;
            req1.arguments.Add(sceneID);

            issueRenderingRequest(ref req1);

            //Wait for requests to finish before return
            waitForRenderingRequest(ref req1);

            //find Animation Capable nodes
            activeModel = null; //TODO: Fix that with the gizmos
            findAnimScenes(RenderState.rootObject); //Repopulate animScenes
            findActionScenes(RenderState.rootObject); //Re-populate actionSystem

        }

        public void addScene(string filename)
        {
            //Cleanup first
            modelUpdateQueue.Clear(); //Clear Update Queues
            
            //Generate Request for rendering thread
            ThreadRequest req1 = new ThreadRequest();
            req1.type = THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST;
            req1.arguments.Clear();
            req1.arguments.Add(filename);

            issueRenderingRequest(ref req1);
            engine.handleRequests();
            
            //Wait for requests to finish before return
            //waitForRenderingRequest(ref req1);

            //find Animation Capable nodes
            activeModel = null; //TODO: Fix that with the gizmos
            findAnimScenes(RenderState.rootObject); //Repopulate animScenes
            findActionScenes(RenderState.rootObject); //Re-populate actionSystem
        }

        public void findAnimScenes(Model node)
        {
            if (node.animComponentID >= 0)
                engine.animationSys.Add(node);
            foreach (Model child in node.Children)
                findAnimScenes(child);
        }

        public void findActionScenes(Model node)
        {
            if (node.actionComponentID >= 0)
                engine.actionSys.Add(node);

            foreach (Model child in node.Children)
                findActionScenes(child);
        }

        private void frameUpdate()
        {
            //Capture Input
            //engine.input_poller(dt);

            //Common.CallBacks.Log(RenderState.renderSettings.RENDERMODE);

            //Gizmo Picking
            //Send picking request
            //Make new request
            activeGizmo = null;
            //Picking needs an active gizmo, otherwise the request would fail every frame
            if (RenderState.renderViewSettings.RenderGizmos && activeGizmo != null)
            {
                ThreadRequest req = new ThreadRequest();
                req.type = THREAD_REQUEST_TYPE.GIZMO_PICKING_REQUEST;
                req.arguments.Clear();
                req.arguments.Add(activeGizmo);
                req.arguments.Add(engine.msHandler.Position);
                engine.issueRenderingRequest(ref req);
            }

            //Set time to the renderManager
            engine.renderMgr.progressTime(dt);
            
            //Everything that moves is updated first, so culling and instancing below see this frame's state

            //Common.CallBacks.Log("Dt {0}", dt);
            if (RenderState.renderViewSettings.EmulateActions)
            {
                engine.actionSys.update((float)dt);
            }

            //Progress animations. Only scenes whose pose changed need their transforms updated
            if (RenderState.renderSettings.ToggleAnimations)
            {
                engine.animationSys.update((float) dt);
                foreach (Model s in engine.animationSys.ActiveScenes)
                    modelUpdateQueue.Enqueue(s.parentScene);
            }

            //Update moving queue (propagates the new local transforms to world matrices)
            while (modelUpdateQueue.Count > 0)
            {
                Model m = modelUpdateQueue.Dequeue();
                m.update();
            }

            //Camera & Light Positions. A collapsed or minimized viewport has no size, then the last aspect is kept
            Camera cam = RenderState.activeCam;
            if (_control.RenderSize.Width > 0 && _control.RenderSize.Height > 0)
                cam.aspect = (float) (_control.RenderSize.Width / _control.RenderSize.Height);

            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;

            cam.updateViewMatrix();

            Matrix4 viewMat = cam.viewMat;
            if (viewMat != lastViewMat || RenderState.rotMat != lastRotMat)
            {
                lastViewMat = viewMat;
                lastRotMat = RenderState.rotMat;
                RenderState.instancesDirty = true;
            }

            //Culling and instancing use the camera and transforms updated above. The instances stay valid
            //until the camera, a transform, an animation or a setting changes
            if (RenderState.instancesDirty)
            {
                RenderState.instancesDirty = false;
                RenderStats.occludedNum = 0;

                RenderState.rootObject?.updateLODDistances(); //Pick the LOD levels (forced, LOD0 with filtering off, or by distance)

                engine.renderMgr.clearInstances(); //Clear All mesh instances
                RenderState.rootObject?.updateMeshInfo(); //Reapply frustum culling and re-setup visible instances
            }

            //Update gizmo
            if (activeModel != null)
            {
                //TODO: Move gizmos
                //gizTranslate.setReference(activeModel);
                //gizTranslate.updateMeshInfo();
                //GLMeshVao gz = resMgr.GLPrimitiveMeshVaos["default_translation_gizmo"];
                //GLMeshBufferManager.addInstance(ref gz, TranslationGizmo);
            }
            
            //Update Frame Counter
            fps(); //The stats overlay in MainWindow reads RenderStats

        }

        private void progressAnimations()
        {
            //Update active animations
            
        }

        //Frames per second, averaged over about a second
        private void fps()
        {
            if (!fpsClock.IsRunning)
                fpsClock.Start();

            frames++;
            double ms = fpsClock.Elapsed.TotalMilliseconds;
            if (ms >= 1000.0)
            {
                RenderStats.fpsCount = (float) (1000.0 * frames / ms);
                frames = 0;
                fpsClock.Restart();
            }
        }
    
    }

}
