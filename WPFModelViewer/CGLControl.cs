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

namespace Model_Viewer
{
    public class CGLControl
    {
        //Mouse Pos
        private MouseMovementState mouseState = new MouseMovementState();
        private MouseMovementStatus mouseMovementStatus = MouseMovementStatus.CAMERA_MOVEMENT;

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
        private bool rt_flag;
        private bool rt_exit;
        private bool rendering_thread_initialized = false;

        //Main Work Thread
        private Thread work_thread;

        //Init-GUI Related
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private System.Windows.Forms.ToolStripMenuItem exportToObjToolStripMenuItem;
        private System.Windows.Forms.ToolStripMenuItem exportToAssimpMenuItem;
        private System.Windows.Forms.OpenFileDialog openFileDialog1;
        private System.Windows.Forms.Form pform;

        //Private fps Counter
        private int frames = 0;
        private double dt = 0.0f;
        private DateTime oldtime;
        private DateTime prevtime;

        //Input Polling Thread
        private System.Timers.Timer input_poller;

        //Gamepad Setup
        private bool disposed;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        private void registerFunctions()
        {
            _control.Loaded += new RoutedEventHandler(genericLoad);
            _control.SizeChanged += new SizeChangedEventHandler(OnResize);
            //Resize += new System.EventHandler(OnResize); 
            
            _control.Render += new((TimeSpan time) =>
            {
                RenderLocal();
            });

            
            _control.MouseDown += new MouseButtonEventHandler(genericMouseDown);
            _control.MouseMove += new MouseEventHandler(genericMouseMove);
            _control.MouseUp += new MouseButtonEventHandler(genericMouseUp);
            _control.KeyDown += new KeyEventHandler(generic_KeyDown);
            _control.KeyUp += new KeyEventHandler(generic_KeyUp);
            _control.MouseEnter += new MouseEventHandler(genericEnter);
            _control.MouseLeave += new MouseEventHandler(genericLeave);
            
            //this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
        }

        //Default Constructor
        public CGLControl(GLWpfControl baseControl)
        {
            _control = baseControl;
            registerFunctions();
            
            //Default Setup
            RenderState.rotAngles.Y = 0;
            
            //Generate Engine instance
            engine = new Engine();
            
            //Initialize Rendering Thread
            //rendering_thread = new Thread(Render);
            //rendering_thread.IsBackground = true;
            //rendering_thread.Priority = ThreadPriority.Normal;

            //Initialize Work Thread
            work_thread = new Thread(Work);
            work_thread.IsBackground = true;
            work_thread.Priority = ThreadPriority.Normal;

            input_poller = new();
            input_poller.Enabled = true;
            input_poller.Interval = 1000.0 * (1.0 / 60.0f);
            input_poller.Elapsed += new ElapsedEventHandler(processInput);

        }

        private void RenderLocal()
        {
            if (!rendering_thread_initialized)
            {
                //Setup new Context
                CallBacks.Log("Intializing Rendering Thread");
                engine.init();
                engine.renderMgr.screen_fbo = _control.Framebuffer;
                rendering_thread_initialized = true;
            }

            if (engine.rt_State != EngineRenderingState.EXIT)
            {
                engine.handleRequests();

                if (engine.rt_State == EngineRenderingState.ACTIVE)
                {
                    frameUpdate();
                    engine.renderMgr.render(); //Render Everything
                }
            }

            //GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //GL.ClearColor(1.0f, 0.0f, 0.0f, 1.0f);


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
        private void genericEnter(object sender, EventArgs e)
        {
            engine.CaptureInput(true);
        }

        private void genericLeave(object sender, EventArgs e)
        {
            engine.CaptureInput(false);
        }

        private void Work()
        {
            
        }

        
        private void genericLoad(object sender, EventArgs e)
        {

            InitializeComponent();
            //MakeCurrent();
        }

        private void genericMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
            //int delta_x = (int) (Math.Pow(activeCam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(activeCam.fov, 4) * (e.Y - mouse_y));
            System.Windows.Point p = _control.PointFromScreen(e.GetPosition(_control));
            mouseState.Delta.X = ((float) p.X - mouseState.Position.X);
            mouseState.Delta.Y = ((float) p.Y - mouseState.Position.Y);

            mouseState.Delta.X = Math.Min(Math.Max(mouseState.Delta.X, -10), 10);
            mouseState.Delta.Y = Math.Min(Math.Max(mouseState.Delta.Y, -10), 10);

            //Take action
            switch (mouseMovementStatus)
            {
                case MouseMovementStatus.CAMERA_MOVEMENT:
                    {
                        // Debug.WriteLine("Deltas {0} {1} {2}", mouseState.Delta.X, mouseState.Delta.Y, e.Button);
                        _camPos.Rotation.X += mouseState.Delta.X;
                        _camPos.Rotation.Y += mouseState.Delta.Y;
                        break;
                    }
                case MouseMovementStatus.GIZMO_MOVEMENT:
                    {
                        //Find movement axis
                        GIZMO_PART_TYPE t = activeGizmo.activeType;
                        float movement_step = (float)Math.Sqrt(mouseState.Delta.X * mouseState.Delta.X / (_control.RenderSize.Width * _control.RenderSize.Width) +
                                                                mouseState.Delta.Y * mouseState.Delta.Y / (_control.RenderSize.Height * _control.RenderSize.Height));
                        CallBacks.Log("Moving by {0}", movement_step);

                        switch (t)
                        {
                            case GIZMO_PART_TYPE.T_X:
                                activeModel._localPosition.X += movement_step;
                                break;
                            case GIZMO_PART_TYPE.T_Y:
                                activeModel._localPosition.Y += movement_step;
                                break;
                            case GIZMO_PART_TYPE.T_Z:
                                activeModel._localPosition.Z += movement_step;
                                break;
                        }

                        activeModel.update(); //Trigger model update

                        break;
                    }
                default:
                    break;

            }

            mouseState.Position.X = (float) p.X;
            mouseState.Position.Y = (float) p.Y;

        }

        private void genericMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (activeGizmo != null && (e.LeftButton == MouseButtonState.Pressed) && activeGizmo.isActive)
            {
                //Engage movement
                CallBacks.Log("Engaging gizmo movement");
                mouseMovementStatus = MouseMovementStatus.GIZMO_MOVEMENT;
            } else if (e.LeftButton == MouseButtonState.Pressed)
            {
                mouseMovementStatus = MouseMovementStatus.CAMERA_MOVEMENT;
            }
        }

        private void processInput(object sender, ElapsedEventArgs args)
        {
            float step = 0.002f;
            float x = engine.kbHandler.getKeyStatus(Key.D) - engine.kbHandler.getKeyStatus(Key.A);
            float y = engine.kbHandler.getKeyStatus(Key.W) - engine.kbHandler.getKeyStatus(Key.S);
            float z = engine.kbHandler.getKeyStatus(Key.R) - engine.kbHandler.getKeyStatus(Key.F);

            _camPos.PosImpulse = new Vector3(x, y, z);
            
            RenderState.activeCam?.updateTarget(_camPos, (float) input_poller.Interval);
            _camPos.Reset();
            RenderState.rotAngles.Y += 100 * step * (engine.kbHandler.getKeyStatus(Key.E) - engine.kbHandler.getKeyStatus(Key.Q));
            RenderState.rotAngles.Y %= 360;
        }

        private void genericMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.LeftButton == System.Windows.Input.MouseButtonState.Released)
            {
                mouseMovementStatus = MouseMovementStatus.IDLE;
            }
        }

        private void genericMouseClick(object sender, System.Windows.Input.MouseEventArgs e)
        {
            //if ((e.LeftButton == System.Windows.Input.MouseButtonState.Released) && (e.ModifierKeys == Keys.Control))
            //{
            //    selectObject(new Vector2(e.X, e.Y));
            //}
            //else if (e.RightButton == System.Windows.Input.MouseButtonState.Released == MouseButtons.Right)
            //{
            //    contextMenuStrip1.Show(Control.MousePosition);
            //}
            
        }

        private void generic_KeyUp(object sender, KeyEventArgs e)
        {
            engine.kbHandler.SetKeyState(e.Key, false);
        }

        private void generic_KeyDown(object sender, KeyEventArgs e)
        {
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

        }

        private void OnResize(object sender, SizeChangedEventArgs e)
        {
            engine.renderMgr.resize((int)_control.RenderSize.Width, (int)_control.RenderSize.Height);
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.exportToObjToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToAssimpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.contextMenuStrip1.SuspendLayout();
            
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.Add(this.exportToObjToolStripMenuItem);
            this.contextMenuStrip1.Items.Add(this.exportToAssimpMenuItem);
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(181, 70);
            // 
            // exportToObjToolStripMenuItem
            // 
            this.exportToObjToolStripMenuItem.Name = "exportToObjToolStripMenuItem";
            this.exportToObjToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.exportToObjToolStripMenuItem.Text = "Export to obj";
            this.exportToObjToolStripMenuItem.Click += new System.EventHandler(this.exportToObjToolStripMenuItem_Click);
            // 
            // exportToAssimp
            // 
            this.exportToAssimpMenuItem.Name = "exportToAssimp";
            this.exportToAssimpMenuItem.Size = new System.Drawing.Size(180, 22);
            this.exportToAssimpMenuItem.Text = "Export to assimp";
            this.exportToAssimpMenuItem.Click += new System.EventHandler(this.exportToAssimp);
            this.exportToAssimpMenuItem.Enabled = false;
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            
            this.contextMenuStrip1.ResumeLayout(false);
        }

               
        

#endregion GLControl Methods

#region ShaderMethods

#endregion ShaderMethods

#region ContextMethods

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to obj");
            System.Windows.Forms.SaveFileDialog sv = new();
            sv.Filter = "OBJ Files | *.obj";
            sv.DefaultExt = "obj";
            System.Windows.Forms.DialogResult res = sv.ShowDialog();

            if (res != System.Windows.Forms.DialogResult.OK)
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
            
            foreach (Model c in m.children)
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

            v = RenderState.activeCam.Front;
            v += 2.0f * RenderState.activeCam.Up * dy;
            v += 2.0f * RenderState.activeCam.Right * dx;

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
            foreach (Model c in m.children)
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
            foreach (Model child in node.children)
                findAnimScenes(child);
        }

        public void findActionScenes(Model node)
        {
            if (node.actionComponentID >= 0)
                engine.actionSys.Add(node);

            foreach (Model child in node.children)
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
                req.arguments.Add(mouseState.Position);
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

            //rootObject?.update(); //Update Distances from camera
            RenderState.rootObject?.updateLODDistances(); //Update Distances from camera
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

            RenderState.activeResMgr.GLCameras[0].Move(dt);
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
