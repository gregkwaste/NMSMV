using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
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
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using System.Timers;
using MVCore.Engine;

namespace Model_Viewer
{
    public class CGLControl : GLControl
    {
        //Mouse Pos
        private MouseMovementState mouseState = new MouseMovementState();
        private MouseMovementStatus mouseMovementStatus = MouseMovementStatus.IDLE;

        //Control Identifier
        private int index;
        
        //Animation Stuff
        private bool animationStatus = false;


        //Scene Stuff
        //public Model rootObject;
        public Model activeModel; //Active Model Reference
        public List<Model> animScenes = new List<Model>();
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
        private Thread rendering_thread;
        private bool rendering_thread_initialized = false;

        //Main Work Thread
        private Thread work_thread;

        //Init-GUI Related
        private ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private ToolStripMenuItem exportToAssimpMenuItem;
        private OpenFileDialog openFileDialog1;
        private Form pform;

        //Resize Timer
        public System.Timers.Timer resizeTimer;

        //Private fps Counter
        private int frames = 0;
        private double dt = 0.0f;
        private DateTime oldtime;
        private DateTime prevtime;

        //Gamepad Setup
        private bool disposed;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        private void registerFunctions()
        {
            this.Load += new System.EventHandler(genericLoad);
            //this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.Resize += new System.EventHandler(OnResize); 
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(genericMouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(genericMouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(genericMouseUp);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(genericMouseClick);
            //this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(generic_KeyDown);
            this.MouseEnter += new System.EventHandler(genericEnter);
            this.MouseLeave += new System.EventHandler(genericLeave);
        }

        //Default Constructor
        public CGLControl(): base(new GraphicsMode(32, 24, 0, 8), 4, 6, GraphicsContextFlags.ForwardCompatible)
        {
            registerFunctions();
            
            //Default Setup
            RenderState.rotAngles.Y = 0;
            
            //Resize Timer
            resizeTimer = new System.Timers.Timer();
            resizeTimer.Elapsed += new ElapsedEventHandler(ResizeControl);
            resizeTimer.Interval = 10;

            //Set properties
            DoubleBuffered = true;
            VSync = RenderState.renderSettings.UseVSYNC;

            //Generate Engine instance
            engine = new Engine();
            
            //Initialize Rendering Thread
            rendering_thread = new Thread(Render);
            rendering_thread.IsBackground = true;
            rendering_thread.Priority = ThreadPriority.Normal;

            //Initialize Work Thread
            work_thread = new Thread(Work);
            work_thread.IsBackground = true;
            work_thread.Priority = ThreadPriority.Normal;

        }

        public void StartWorkThreads()
        {
            Context.MakeCurrent(null); //Release GL Context from the GLControl
            resizeTimer.Start();
            rendering_thread.Start(); //A new context is created in the rendering thread
            //work_thread.Start();
        }

        private void Render()
        {
            
            //Setup new Context
            Console.WriteLine("Intializing Rendering Thread");
#if (DEBUG)
            GraphicsContext gfx_context = new GraphicsContext(new GraphicsMode(32, 24, 0, 8), WindowInfo, 4, 3,
                GraphicsContextFlags.Debug);
#else
            GraphicsContext gfx_context = new GraphicsContext(new GraphicsMode(32, 24, 0, 8), WindowInfo, 4 , 6,
            GraphicsContextFlags.ForwardCompatible);
#endif
            gfx_context.MakeCurrent(WindowInfo);
            MakeCurrent();

            engine.SetControl(this); //Set engine Window to the GLControl
            engine.init();
            rendering_thread_initialized = true;

            while (engine.rt_State != EngineRenderingState.EXIT)
            {
                engine.handleRequests();
                
                if (engine.rt_State == EngineRenderingState.ACTIVE)
                {
                    frameUpdate();
                    engine.renderMgr.render(); //Render Everything
                    SwapBuffers();
                }
                
                Thread.Sleep(1); //TODO: Replace that in the future with some smarter logic to maintain constant framerates
            }
            
        }

        public void setActiveCam(int index)
        {
            if (RenderState.activeCam != null)
                RenderState.activeCam.isActive = false;
            RenderState.activeCam = RenderState.activeResMgr.GLCameras[index];
            RenderState.activeCam.isActive = true;
            Console.WriteLine("Switching Camera to {0}", index);
        }

        public void updateActiveCam(int FOV, float zNear, float zFar, float speed, float speedPower)
        {
            //TODO: REMOVE, FOR TESTING I"M WORKING ONLY ON THE FIRST CAM
            RenderState.activeResMgr.GLCameras[0].setFOV(FOV);
            RenderState.activeResMgr.GLCameras[0].zFar = zFar;
            RenderState.activeResMgr.GLCameras[0].zNear = zNear;
            RenderState.activeResMgr.GLCameras[0].Speed = speed;
            RenderState.activeResMgr.GLCameras[0].SpeedPower = speedPower;
        }


        //Constructor
        public CGLControl(int index, Form parent)
        {
            registerFunctions();
            
            //Set Control Identifiers
            this.index = index;
            
            //Set parent form
            if (parent != null)
                pform = parent;

            
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
            MakeCurrent();
        }

        private void genericMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
            //int delta_x = (int) (Math.Pow(activeCam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(activeCam.fov, 4) * (e.Y - mouse_y));
            System.Drawing.Point p = PointToClient(Cursor.Position);
            mouseState.Delta.X = (p.X - mouseState.Position.X);
            mouseState.Delta.Y = (p.Y - mouseState.Position.Y);

            mouseState.Delta.X = Math.Min(Math.Max(mouseState.Delta.X, -10), 10);
            mouseState.Delta.Y = Math.Min(Math.Max(mouseState.Delta.Y, -10), 10);

            //Take action
            switch (mouseMovementStatus)
            {
                case MouseMovementStatus.CAMERA_MOVEMENT:
                    {
                        // Debug.WriteLine("Deltas {0} {1} {2}", mouseState.Delta.X, mouseState.Delta.Y, e.Button);
                        engine.targetCameraPos.Rotation.X += mouseState.Delta.X;
                        engine.targetCameraPos.Rotation.Y += mouseState.Delta.Y;
                        break;
                    }
                case MouseMovementStatus.GIZMO_MOVEMENT:
                    {
                        //Find movement axis
                        GIZMO_PART_TYPE t = activeGizmo.activeType;
                        float movement_step = (float)Math.Sqrt(mouseState.Delta.X * mouseState.Delta.X / (Size.Width * Size.Width) +
                                                                mouseState.Delta.Y * mouseState.Delta.Y / (Size.Height * Size.Height));
                        Console.WriteLine("Moving by {0}", movement_step);

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

            
            mouseState.Position.X = p.X;
            mouseState.Position.Y = p.Y;

        }

        private void genericMouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (activeGizmo != null && (e.Button == MouseButtons.Left) && activeGizmo.isActive)
            {
                //Engage movement
                Console.WriteLine("Engaging gizmo movement");
                mouseMovementStatus = MouseMovementStatus.GIZMO_MOVEMENT;
            } else if (e.Button == MouseButtons.Left)
            {
                mouseMovementStatus = MouseMovementStatus.CAMERA_MOVEMENT;
            }
        }

        private void genericMouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                mouseMovementStatus = MouseMovementStatus.IDLE;
            }

        }

        private void genericMouseClick(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            
            if ((e.Button == MouseButtons.Left) && (ModifierKeys == Keys.Control))
            {
                selectObject(new Vector2(e.X, e.Y));
            }
            else if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(Control.MousePosition);
            }
            
        }

        private void generic_KeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            //Debug.WriteLine("Key pressed {0}",e.KeyCode);
            switch (e.KeyCode)
            {
                //Light Rotation
                case Keys.N:
                    engine.light_angle_y -= 1;
                    break;
                case Keys.M:
                    engine.light_angle_y += 1;
                    break;
                case Keys.Oemcomma:
                    engine.light_angle_x -= 1;
                    break;
                case Keys.OemPeriod:
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
                case Keys.NumPad0:
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
                    //Console.WriteLine("Not Implemented Yet");
                    break;
            }
        }

        private void ResizeControl(object sender, System.Timers.ElapsedEventArgs e)
        {
            resizeTimer.Stop();
            
            //Make new request
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.GL_RESIZE_REQUEST;
            req.arguments.Clear();
            req.arguments.Add(ClientSize.Width);
            req.arguments.Add(ClientSize.Height);

            engine.issueRenderingRequest(ref req);
        }

        
        private void OnResize(object sender, EventArgs e)
        {
            //Check the resizeTimer
            resizeTimer.Stop();
            resizeTimer.Start();
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.exportToObjToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.exportToAssimpMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
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
            // 
            // CGLControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Name = "CGLControl";
            this.Size = new System.Drawing.Size(314, 213);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

               
        

#endregion GLControl Methods

#region ShaderMethods

#endregion ShaderMethods

#region ContextMethods

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to obj");
            SaveFileDialog sv = new SaveFileDialog();
            sv.Filter = "OBJ Files | *.obj";
            sv.DefaultExt = "obj";
            DialogResult res = sv.ShowDialog();

            if (res != DialogResult.OK)
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
                    Console.WriteLine(ex.Message);
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
                        me.writeGeomToStream(s, ref index);
                        break;
                    }
                case TYPES.COLLISION:
                    Console.WriteLine("NOT IMPLEMENTED YET");
                    break;
                default:
                    break;
            }
            
            foreach (Model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }

        private Vector3 unProject(Vector2 vec)
        {
            Vector3 v;

            Vector2 screenPos = vec;

            //Normalize screenPos
            float fov_fact = 0.5f * RenderState.activeCam.fov;
            float dx = fov_fact * (screenPos.X - 0.5f * Size.Width) / Size.Width;
            float dy = -fov_fact * (screenPos.Y - 0.5f * Size.Height) / Size.Height;

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
            
            Console.WriteLine("Ray Vector : {0}, {1}, {2} ", v.X, v.Y, v.Z);

            //Intersect Ray with scene
            Model intersectedModel = null;
            bool intersectionStatus = false;
            float intersectionDistance = float.MaxValue;
            findIntersectedModel(RenderState.rootObject, v, ref intersectionStatus, ref intersectedModel, ref intersectionDistance);

            if (intersectedModel != null)
            {
                Console.WriteLine("Ray intersects model : " + intersectedModel.name);
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

        public void addScene(string filename)
        {
            //Cleanup first
            animScenes.Clear(); //Clear animScenes
            modelUpdateQueue.Clear(); //Clear Update Queues

            //Generate Request for rendering thread
            ThreadRequest req1 = new ThreadRequest();
            req1.type = THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST;
            req1.arguments.Clear();
            req1.arguments.Add(filename);

            issueRenderingRequest(ref req1);
            
            //Wait for requests to finish before return
            waitForRenderingRequest(ref req1);

            //find Animation Capable nodes
            activeModel = null; //TODO: Fix that with the gizmos
            findAnimScenes(RenderState.rootObject); //Repopulate animScenes

        }

        public void findAnimScenes(Model node)
        {
            if (node.animComponentID >= 0)
                animScenes.Add(node);

            foreach (Model child in node.children)
                findAnimScenes(child);
        }

        private void frameUpdate()
        {
            VSync = RenderState.renderSettings.UseVSYNC; //Update Vsync 

            //Console.WriteLine(RenderState.renderSettings.RENDERMODE);

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
                gizTranslate.setReference(activeModel);
                gizTranslate.updateMeshInfo();
                //GLMeshVao gz = resMgr.GLPrimitiveMeshVaos["default_translation_gizmo"];
                //GLMeshBufferManager.addInstance(ref gz, TranslationGizmo);
            }

            //Identify dynamic Objects
            foreach (Model s in animScenes)
            {
                modelUpdateQueue.Enqueue(s.parentScene);
            }

            //Progress animations
            if (RenderState.renderSettings.ToggleAnimations)
                progressAnimations();

            //Camera & Light Positions
            //Update common transforms
            RenderState.activeResMgr.GLCameras[0].aspect = (float) ClientSize.Width / ClientSize.Height;

            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;

            RenderState.activeResMgr.GLCameras[0].Move(dt);
            RenderState.activeResMgr.GLCameras[0].updateViewMatrix();
            RenderState.activeResMgr.GLCameras[1].updateViewMatrix();

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
            foreach (Model anim_model in animScenes)
            {
                AnimComponent ac = anim_model._components[anim_model.hasComponent(typeof(AnimComponent))] as AnimComponent;
                bool found_first_active_anim = false;

                foreach (AnimData ad in ac.Animations)
                {
                    if (ad._animationToggle)
                    {
                        if (!ad.loaded)
                            ad.loadData();

                        found_first_active_anim = true;
                        //Load updated local joint transforms
                        foreach (libMBIN.NMS.Toolkit.TkAnimNodeData node in ad.animMeta.NodeData)
                        {
                            if (!anim_model.parentScene.jointDict.ContainsKey(node.Node))
                                continue;

                            Joint tj = anim_model.parentScene.jointDict[node.Node];
                            ad.applyNodeTransform(tj, node.Node);
                        }

                        //Once the current frame data is fetched, progress to the next frame
                        ad.animate((float)dt);
                    }
                    //TODO: For now I'm just using the first active animation. Blending should be kinda more sophisticated
                    if (found_first_active_anim)
                        break;
                }
            }
        }

        private void fps()
        {
            //Get FPS
            DateTime now = DateTime.UtcNow;
            TimeSpan time = now - oldtime;
            dt = (now - prevtime).TotalMilliseconds;

            if (time.TotalMilliseconds > 1000)
            {
                //Console.WriteLine("{0} {1} {2}", frames, RenderStats.fpsCount, time.TotalMilliseconds);
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



        #region DISPOSE_METHODS

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                RenderState.rootObject.Dispose();
            }

            //Free unmanaged resources
            disposed = true;
        }

#endregion DISPOSE_METHODS

    }

}
