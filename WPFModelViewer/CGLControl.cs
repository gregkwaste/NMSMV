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
using MVCore.Text;
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


        //Private fps Counter
        private int frames = 0;
        private double dt = 0.0f;
        private DateTime oldtime;
        private DateTime prevtime;

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

                    rendering_thread_initialized = true;
                }

                //GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
                //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
                RenderLocal(time.TotalSeconds);
            });

            _control.MouseDown += new MouseButtonEventHandler(genericMouseDown);
            _control.MouseMove += new MouseEventHandler(genericMouseMove);
            _control.MouseUp += new MouseButtonEventHandler(genericMouseUp);
            _control.KeyDown += new KeyEventHandler(generic_KeyDown);
            _control.KeyUp += new KeyEventHandler(generic_KeyUp);

            //Register handlers after the engine has been initialized
            _control.MouseLeave += new((object sender, MouseEventArgs args) =>
            {
                _capture_input = false;
            });

            _control.MouseEnter += new((object sender, MouseEventArgs args) =>
            {
                _capture_input = true;
                engine.kbHandler.Clear();
                engine.msHandler.Clear();
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

        private void RenderLocal(double dt)
        {
            if (engine.rt_State != EngineRenderingState.EXIT)
            {
                engine.handleRequests();

                if (engine.rt_State == EngineRenderingState.ACTIVE)
                {
                    if (_capture_input)
                        processInput(dt);
                    frameUpdate();
                    engine.renderMgr.render(); //Render Everything

                    GL.ClearColor(1.0f, 1.0f, 1.0f, 1.0f);
                }
            }

            //GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);
            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            

        }

        public void setActiveCam(int index)
        {
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



            //Update Camera Rotation
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                //Debug.WriteLine("Mouse Delta {0} {1}", engine.msHandler.Delta.X, engine.msHandler.Delta.Y);

                if (RenderState.activeCam.pitch > 360) RenderState.activeCam.pitch = 0;
                if (RenderState.activeCam.pitch < -360) RenderState.activeCam.pitch = 0;

                RenderState.activeCam.pitch += engine.msHandler.Delta.X * RenderState.activeCam.settings.Sensitivity;
                RenderState.activeCam.yaw -= engine.msHandler.Delta.Y * RenderState.activeCam.settings.Sensitivity;
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
            }

            e.Handled = true;
        }

        private void processInput(double dt)
        {
            float step = 0.002f;
            float x = engine.kbHandler.getKeyStatus(RenderState.settings.KeyRight) - engine.kbHandler.getKeyStatus(RenderState.settings.KeyLeft);
            float z = engine.kbHandler.getKeyStatus(RenderState.settings.KeyUp) - engine.kbHandler.getKeyStatus(RenderState.settings.KeyDown);
            float y = engine.kbHandler.getKeyStatus(Key.R) - engine.kbHandler.getKeyStatus(Key.F);
            Vector3 newPos = RenderState.activeCam.Right * x + RenderState.activeCam.Front * z + Camera.BaseUp * y;
            newPos *= (float) dt * RenderState.activeCam.settings.Speed;
            newPos += RenderState.activeCam.Position;

            Camera.SetCameraPosition(ref RenderState.activeCam, newPos);
            //Debug.WriteLine("Deltas {0} {1}", _camPos.Rotation.X, _camPos.Rotation.Y);
            //RenderState.activeCam?.updateTarget(_camPos, (float) input_poller.Interval);
            RenderState.rotAngles.Y += 100 * step * (engine.kbHandler.getKeyStatus(Key.E) - engine.kbHandler.getKeyStatus(Key.Q));
            RenderState.rotAngles.Y %= 360;
        }

        private void genericMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (!_capture_input)
            {
                e.Handled = true;
                return;
            }

            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Left, e.LeftButton == MouseButtonState.Released);
            engine.msHandler.SetButtonState(System.Windows.Input.MouseButton.Right, e.RightButton == MouseButtonState.Released);
            

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
                    if (engine.resMgr.GLCameras[0].isActive)
                        setActiveCam(1);
                    else
                        setActiveCam(0);
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

        private void OnResize(object sender, SizeChangedEventArgs e)
        {
            engine.renderMgr.resize((int)e.NewSize.Width, (int)e.NewSize.Height);
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
                
            MenuItem load_reference = new MenuItem();
            assimp_export.Header = "Load Reference";
            assimp_export.Click += new RoutedEventHandler(loadReference);

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

        private void loadReference(object sender, EventArgs e)
        {

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
            //VSync = RenderState.renderSettings.UseVSYNC; //Update Vsync 

            //Common.CallBacks.Log(RenderState.renderSettings.RENDERMODE);

            //Gizmo Picking
            //Send picking request
            //Make new request
            activeGizmo = null;
            if (RenderState.renderViewSettings.RenderGizmos)
            {
                ThreadRequest req = new ThreadRequest();
                req.type = THREAD_REQUEST_TYPE.GIZMO_PICKING_REQUEST;
                req.arguments.Clear();
                req.arguments.Add(activeGizmo);
                req.arguments.Add(null);
                engine.issueRenderingRequest(ref req);
            }

            //Set time to the renderManager
            engine.renderMgr.progressTime(dt);
            
            //Reset Stats
            RenderStats.occludedNum = 0;

            //Update moving queue
            while (modelUpdateQueue.Count > 0)
            {
                Model m = modelUpdateQueue.Dequeue();
                m.update();
            }

            if (RenderState.renderSettings.LODFiltering)
            {
                RenderState.rootObject?.updateLODDistances(); //Update Distances from camera
            }
            
            engine.renderMgr.clearInstances(); //Clear All mesh instances
            RenderState.rootObject?.updateMeshInfo(); //Reapply frustum culling and re-setup visible instances

            //Update gizmo
            if (activeModel != null)
            {
                //TODO: Move gizmos
                //gizTranslate.setReference(activeModel);
                //gizTranslate.updateMeshInfo();
                //GLMeshVao gz = resMgr.GLPrimitiveMeshVaos["default_translation_gizmo"];
                //GLMeshBufferManager.addInstance(ref gz, TranslationGizmo);
            }

            //Identify dynamic Objects
            foreach (Model s in engine.animationSys.AnimScenes)
            {
                modelUpdateQueue.Enqueue(s.parentScene);
            }

            //Common.CallBacks.Log("Dt {0}", dt);
            if (RenderState.renderViewSettings.EmulateActions)
            {
                engine.actionSys.update((float)dt);
            }

            //Progress animations
            if (RenderState.renderSettings.ToggleAnimations)
            {
                engine.animationSys.update((float) dt);
            }
                

            //Camera & Light Positions
            //Update common transforms
            RenderState.activeResMgr.GLCameras[0].aspect = (float) (_control.RenderSize.Width / _control.RenderSize.Height);

            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;

            //RenderState.activeResMgr.GLCameras[0].Move(dt);
            RenderState.activeResMgr.GLCameras[0].updateViewMatrix();
            
            //Update Frame Counter
            fps();

            //Update Text Counters
            RenderState.activeResMgr.txtMgr.getText(TextManager.Semantic.FPS).update(string.Format("FPS: {0:000.0}",
                                                                        (float)RenderStats.fpsCount));
            RenderState.activeResMgr.txtMgr.getText(TextManager.Semantic.OCCLUDED_COUNT).update(string.Format("OccludedNum: {0:0000}",
                                                                        RenderStats.occludedNum));

        }

        private void progressAnimations()
        {
            //Update active animations
            
        }

        private void fps()
        {
            //Get FPS
            DateTime now = DateTime.UtcNow;
            TimeSpan time = now - oldtime;
            dt = (now - prevtime).TotalMilliseconds;

            if (time.TotalMilliseconds > 1000)
            {
                //Common.CallBacks.Log("{0} {1} {2}", frames, RenderStats.fpsCount, time.TotalMilliseconds);
                //Reset
                frames = 0;
                oldtime = now;
            }
            else
            {
                frames += 1;
                prevtime = now;
            }

            RenderStats.fpsCount = 1000.0f * frames / (float)time.TotalMilliseconds;
        }
    
    }

}
