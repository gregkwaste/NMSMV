using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using GLSLHelper;
//Custom Imports
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using OpenTK;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Input;
using GL = OpenTK.Graphics.OpenGL4.GL;


namespace Model_Viewer
{
    public class CGLControl : GLControl
    {
        public model rootObject;
        public model activeModel; //Active Model Reference

        public Gizmo activeGizmo;
        public TranslationGizmo gizTranslate;
        
        
        //private Camera activeCam;

        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;

        //Mouse Pos
        private MouseMovementState mouseState = new MouseMovementState();
        private MouseMovementStatus mouseMovementStatus = MouseMovementStatus.IDLE;
        
        //Camera Movement Speed
        public int movement_speed = 1;

        //Control Identifier
        private int index;
        
        //Custom Palette
        private Dictionary<string,Dictionary<string,Vector4>> palette;

        //Animation Stuff
        private bool animationStatus = false;

        
        public bool PAnimationStatus
        {
            get
            {
                return animationStatus;
            }

            set
            {
                animationStatus = value;
            }
        }

        public List<model> animScenes = new List<model>();
        public Queue<model> modelUpdateQueue = new Queue<model>();
        public List<Tuple<AnimComponent, AnimData>> activeAnimScenes = new List<Tuple<AnimComponent, AnimData>>();

        //Control private Managers
        public ResourceManager resMgr = new ResourceManager();
        public renderManager renderMgr = new renderManager();
        
        //Init-GUI Related
        private ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private ToolStripMenuItem exportToAssimpMenuItem;
        private OpenFileDialog openFileDialog1;
        private Form pform;

        //Timers
        public System.Timers.Timer inputPollTimer;
        public System.Timers.Timer resizeTimer;

        //Private fps Counter
        private int frames = 0;
        private double dt = 0.0f;
        private DateTime oldtime;
        private DateTime prevtime;

        //Gamepad Setup
        public BaseGamepadHandler gpHandler;
        public KeyboardHandler kbHandler;
        private bool disposed;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        //Rendering Thread Stuff
        private Thread rendering_thread;
        private Queue<ThreadRequest> rt_req_queue = new Queue<ThreadRequest>();
        private bool rt_exit;
        
        private void registerFunctions()
        {
            this.Load += new System.EventHandler(genericLoad);
            //this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.Resize += new System.EventHandler(OnResize); 
            this.MouseHover += new System.EventHandler(genericHover);
            this.MouseDown += new System.Windows.Forms.MouseEventHandler(genericMouseDown);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(genericMouseMove);
            this.MouseUp += new System.Windows.Forms.MouseEventHandler(genericMouseUp);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(genericMouseClick);
            //this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(generic_KeyDown);
            this.Enter += new System.EventHandler(genericEnter);
            this.Leave += new System.EventHandler(genericLeave);
        }

        //Default Constructor
        public CGLControl(): base(new GraphicsMode(32, 24, 0, 8))
        {
            registerFunctions();

            //Default Setup
            RenderState.rotAngles.Y = 0;
            light_angle_y = 190;

            //Input Polling Timer
            inputPollTimer = new System.Timers.Timer();
            inputPollTimer.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            inputPollTimer.Interval = 1;
            
            //Resize Timer
            resizeTimer = new System.Timers.Timer();
            resizeTimer.Elapsed += new System.Timers.ElapsedEventHandler(ResizeControl);
            resizeTimer.Interval = 10;

            //Set properties
            DoubleBuffered = true;
            VSync = RenderState.renderSettings.UseVSYNC;

        }

        //Constructor
        public CGLControl(int index, Form parent)
        {
            registerFunctions();
            
            //Set Control Identifiers
            this.index = index;
            
            //Default Setup
            RenderState.rotAngles.Y = 0;
            this.light_angle_y = 190;

            //Assign new palette to GLControl
            palette = Model_Viewer.Palettes.createPalettefromBasePalettes();

            //Set parent form
            if (parent != null)
                pform = parent;

            //Control Timer
            inputPollTimer = new System.Timers.Timer();
            inputPollTimer.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            inputPollTimer.Interval = 10;
            inputPollTimer.Start();
        }

        private void input_poller(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Console.WriteLine(gpHandler.getAxsState(0, 0).ToString() + " " +  gpHandler.getAxsState(0, 1).ToString());
            //gpHandler.reportButtons();
            //gamepadController(); //Move camera according to input

            bool focused = false;

            this.Invoke((MethodInvoker) delegate
            {
                focused = Focused;
            });

            if (focused)
            {
                kbHandler?.updateState();
                //gpHandler?.updateState();
            }
        }

        private void rt_render()
        {
            //Update per frame data
            frameUpdate();
            
            SwapBuffers();

            renderMgr.render(); //Render Everything

            Thread.Sleep(1);
        }
        
        public void findAnimScenes(model node)
        {
            if (node.animComponentID >= 0)
                animScenes.Add(node);
            
            foreach (model child in node.children)
                findAnimScenes(child);
        }

        //Per Frame Updates
        private void frameUpdate()
        {
            VSync = RenderState.renderSettings.UseVSYNC; //Update Vsync 

            //Console.WriteLine(RenderState.renderSettings.RENDERMODE);

            //Update movement
            keyboardController();
            //gamepadController();

            //Gizmo Picking
            //Send picking request
            //Make new request
            ThreadRequest req = new ThreadRequest();
            req.type = THREAD_REQUEST_TYPE.GIZMO_PICKING_REQUEST;
            req.arguments.Clear();
            req.arguments.Add(activeGizmo);
            req.arguments.Add(mouseState.Position);

            issueRequest(ref req);

            //No need to wait for the request
            //while (req.status != THREAD_REQUEST_STATUS.FINISHED)
            //    Thread.Sleep(2);

            
            
            //Set time to the renderManager
            renderMgr.progressTime(dt);

            //Reset Stats
            RenderStats.occludedNum = 0;

            //Update moving queue
            while (modelUpdateQueue.Count > 0)
            {
                model m = modelUpdateQueue.Dequeue();
                m.update();
            }

            //rootObject?.update(); //Update Distances from camera
            rootObject?.updateLODDistances(); //Update Distances from camera
            renderMgr.clearInstances(); //Clear All mesh instances
            rootObject?.updateMeshInfo(); //Reapply frustum culling and re-setup visible instances

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
            foreach (model s in animScenes)
            {
                modelUpdateQueue.Enqueue(s.parentScene);
            }
            
            //Progress animations
            if (RenderState.renderSettings.ToggleAnimations)
                progressAnimations();
            
            //Camera & Light Positions
            //Update common transforms
            RenderState.activeCam.aspect = (float) ClientSize.Width / ClientSize.Height;
                
            //Apply extra viewport rotation
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(RenderState.rotAngles.X));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(RenderState.rotAngles.Y));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(RenderState.rotAngles.Z));
            RenderState.rotMat = Rotz * Rotx * Roty;
            //RenderState.rotMat = Matrix4.Identity;

            resMgr.GLCameras[0].updateViewMatrix();
            resMgr.GLCameras[1].updateViewMatrix();

            //Update Frame Counter
            fps();
        }

        private void progressAnimations()
        {
            //Update active animations
            foreach (model anim_model in animScenes)
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

        //Main Rendering Routines
        private void ControlLoop()
        {
            //Setup new Context
#if(DEBUG)
            IGraphicsContext new_context = new GraphicsContext(new GraphicsMode(32, 24, 0, 8), WindowInfo, 4, 3,
                GraphicsContextFlags.Debug);
#else
            IGraphicsContext new_context = new GraphicsContext(new GraphicsMode(32, 24, 0, 8), WindowInfo, 4 ,3,
                GraphicsContextFlags.Default);
#endif
            new_context.MakeCurrent(WindowInfo);
            MakeCurrent(); //This is essential

            //Add default primitives trying to avoid Vao Request queue traffic
            resMgr.Cleanup();
            resMgr.Init();
            addCamera();
            addCamera(cull:false); //Add second camera
            setActiveCam(0);
            addTestObjects();

            //Init Gizmos
            gizTranslate = new TranslationGizmo();
            activeGizmo = gizTranslate;

            //Initialize the render manager
            renderMgr.init(resMgr);
            renderMgr.setupGBuffer(ClientSize.Width, ClientSize.Height);

            bool renderFlag = true; //Toggle rendering on/off
            
            //Rendering Loop
            while (!rt_exit)
            {
                //Check for new scene request
                if (rt_req_queue.Count > 0)
                {
                    ThreadRequest req;
                    lock (rt_req_queue)
                    {
                        //Try to group  Resizing requests
                        req = rt_req_queue.Dequeue();
                    }

                    lock (req)
                    {
                        switch (req.type)
                        {
                            case THREAD_REQUEST_TYPE.QUERY_GLCONTROL_STATUS_REQUEST:
                                //At this point the renderer is up and running
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.NEW_SCENE_REQUEST:
                                lock (inputPollTimer)
                                {
                                    inputPollTimer.Stop();
                                    rt_addRootScene((string)req.arguments[0]);
                                    req.status = THREAD_REQUEST_STATUS.FINISHED;
                                    inputPollTimer.Start();
                                }
                                break;
                            case THREAD_REQUEST_TYPE.CHANGE_MODEL_PARENT_REQUEST:
                                model source = (model) req.arguments[0];
                                model target = (model) req.arguments[1];

                                System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
                                {
                                    if (source.parent != null)
                                        source.parent.Children.Remove(source);

                                    //Add to target node
                                    source.parent = target;
                                    target.Children.Add(source);
                                }));
                                
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.UPDATE_SCENE_REQUEST:
                                scene req_scn = (scene) req.arguments[0];
                                req_scn.update();
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.MOUSEPOSITION_INFO_REQUEST:
                                Vector4[] t = (Vector4[]) req.arguments[2];
                                renderMgr.getMousePosInfo((int)req.arguments[0], (int)req.arguments[1],
                                    ref t);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.GL_RESIZE_REQUEST:
                                rt_ResizeViewport((int)req.arguments[0], (int)req.arguments[1]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.GL_MODIFY_SHADER_REQUEST:
                                GLShaderHelper.modifyShader((GLSLShaderConfig) req.arguments[0],
                                             (GLSLShaderText) req.arguments[1]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.GIZMO_PICKING_REQUEST:
                                //TODO: Send the nessessary arguments to the render manager and mark the active gizmoparts
                                Gizmo g = (Gizmo) req.arguments[0];
                                renderMgr.gizmoPick(ref g, (Vector2)req.arguments[1]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.TERMINATE_REQUEST:
                                rt_exit = true;
                                renderFlag = false;
                                inputPollTimer.Stop();
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.GL_PAUSE_RENDER_REQUEST:
                                renderFlag = false;
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.GL_RESUME_RENDER_REQUEST:
                                renderFlag = true;
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST_TYPE.NULL:
                                break;
                        }
                    }
                }
                
                if (renderFlag)
                {
                    rt_render();
                }

            }
        }

        

#region GLControl Methods
        private void genericEnter(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //Debug.WriteLine("Entered Focus Control " + index);
            inputPollTimer.Start();
        }

        private void genericHover(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //this.MakeCurrent(); //Control should have been active on hover
            inputPollTimer.Start();
        }

        private void genericLeave(object sender, EventArgs e)
        {
            //Don't update the control when its not focused
            //Debug.WriteLine("Left Focus of Control "+ index);
            inputPollTimer.Stop();

        }

        private void genericPaint(object sender, PaintEventArgs e)
        {
            //TODO: Should I add more stuff in here?
            //SwapBuffers();
            Console.WriteLine("Painting");
        }

        private void genericLoad(object sender, EventArgs e)
        {

            InitializeComponent();
            MakeCurrent();

            //Once the context is initialized compile the shaders
            compileMainShaders();

            kbHandler = new KeyboardHandler();
            //gpHandler = new PS4GamePadHandler(0); //TODO: Add support for PS4 controller

            RenderState.activeGamepad = gpHandler;

            //Everything ready to swap threads
            setupRenderingThread();

            //Start Timers
            inputPollTimer.Start();

            //Start rendering Thread
            rendering_thread.Start();

        }

        private void genericMouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
            //int delta_x = (int) (Math.Pow(activeCam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(activeCam.fov, 4) * (e.Y - mouse_y));
            mouseState.Delta.X = (e.X - mouseState.Position.X);
            mouseState.Delta.Y = (e.Y - mouseState.Position.Y);

            mouseState.Delta.X = Math.Min(Math.Max(mouseState.Delta.X, -10), 10);
            mouseState.Delta.Y = Math.Min(Math.Max(mouseState.Delta.Y, -10), 10);

            //Take action
            switch (mouseMovementStatus)
            {
                case MouseMovementStatus.CAMERA_MOVEMENT:
                    {
                        // Debug.WriteLine("Deltas {0} {1} {2}", mouseState.Delta.X, mouseState.Delta.Y, e.Button);
                        RenderState.activeCam.Move(0, 0, 0, mouseState.Delta.X, mouseState.Delta.Y);
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

            
            mouseState.Position.X = e.X;
            mouseState.Position.Y = e.Y;

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
                    this.light_angle_y -= 1;
                    break;
                case Keys.M:
                    this.light_angle_y += 1;
                    break;
                case Keys.Oemcomma:
                    this.light_angle_x -= 1;
                    break;
                case Keys.OemPeriod:
                    this.light_angle_x += 1;
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
                    if (this.resMgr.GLCameras[0].isActive)
                        setActiveCam(1);
                    else
                        setActiveCam(0);
                    break;
                //Animation playback (Play/Pause Mode) with Space
                case Keys.Space:
                    toggleAnimation();
                    break;
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

            issueRequest(ref req);
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

        private void compileMainShaders()
        {

#if (DEBUG)
            //Query GL Extensions
            Console.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Console.WriteLine(s);
                if (s.Contains("texture"))
                    Console.WriteLine(s);
                if (s.Contains("16"))
                    Console.WriteLine(s);
            }

            //Query maximum buffer sizes
            Console.WriteLine("MaxUniformBlock Size {0}", GL.GetInteger(GetPName.MaxUniformBlockSize));
#endif

            //Populate shader list
            string log = "";
            GLSLHelper.GLSLShaderConfig shader_conf;

            //Geometry Shader
            //Compile Object Shaders
            GLSLShaderText geometry_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText geometry_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            GLSLShaderText geometry_shader_gs = new GLSLShaderText(ShaderType.GeometryShader);
            geometry_shader_vs.addStringFromFile("Shaders/Simple_VSEmpty.glsl");
            geometry_shader_fs.addStringFromFile("Shaders/Simple_FSEmpty.glsl");
            geometry_shader_gs.addStringFromFile("Shaders/Simple_GS.glsl");

            GLShaderHelper.compileShader(geometry_shader_vs, geometry_shader_fs, geometry_shader_gs, null, null,
                            SHADER_TYPE.DEBUG_MESH_SHADER, ref log);


            //Compile Object Shaders
            GLSLShaderText gizmo_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gizmo_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gizmo_shader_vs.addStringFromFile("Shaders/Gizmo_VS.glsl");
            gizmo_shader_fs.addStringFromFile("Shaders/Gizmo_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gizmo_shader_vs, gizmo_shader_fs, null, null, null,
                            SHADER_TYPE.GIZMO_SHADER, ref log);
            
            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader_conf, "_COMMON_PER_FRAME", 0);
            resMgr.GLShaders[SHADER_TYPE.GIZMO_SHADER] = shader_conf;


#if DEBUG
            //Report UBOs
            GLShaderHelper.reportUBOs(shader_conf);
#endif

            //Picking Shader

            //Compile Default Shaders

            //BoundBox Shader
            GLSLShaderText bbox_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText bbox_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            bbox_shader_vs.addStringFromFile("Shaders/Bound_VS.glsl");
            bbox_shader_fs.addStringFromFile("Shaders/Bound_FS.glsl");
            GLShaderHelper.compileShader(bbox_shader_vs, bbox_shader_fs, null, null, null,
                GLSLHelper.SHADER_TYPE.BBOX_SHADER, ref log);

            //Texture Mixing Shader
            GLSLShaderText texture_mixing_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText texture_mixing_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            texture_mixing_shader_vs.addStringFromFile("Shaders/texture_mixer_VS.glsl");
            texture_mixing_shader_fs.addStringFromFile("Shaders/texture_mixer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(texture_mixing_shader_vs, texture_mixing_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER] = shader_conf;

            //GBuffer Shaders

            //UNLIT
            GLSLShaderText gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/Gbuffer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GBUFFER_UNLIT_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_UNLIT_SHADER] = shader_conf;

            //LIT
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addString("#define _D_LIGHTING");
            gbuffer_shader_fs.addStringFromFile("Shaders/Gbuffer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GBUFFER_LIT_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_LIT_SHADER] = shader_conf;


            //GAUSSIAN HORIZONTAL BLUR SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gaussian_blur_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gaussian_blur_shader_fs.addStringFromFile("Shaders/gaussian_horizontalBlur_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER] = shader_conf;


            //GAUSSIAN VERTICAL BLUR SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gaussian_blur_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gaussian_blur_shader_fs.addStringFromFile("Shaders/gaussian_verticalBlur_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER] = shader_conf;

            
            //BRIGHTNESS EXTRACTION SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/brightness_extract_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER] = shader_conf;


            //ADDITIVE BLEND
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/additive_blend_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.ADDITIVE_BLEND_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.ADDITIVE_BLEND_SHADER] = shader_conf;

            //FXAA
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/fxaa_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.FXAA_SHADER, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.FXAA_SHADER] = shader_conf;

            //TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/tone_mapping_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TONE_MAPPING, ref log);
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.TONE_MAPPING] = shader_conf;

            //INV TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/inv_tone_mapping_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            SHADER_TYPE.INV_TONE_MAPPING, ref log);
            resMgr.GLShaders[SHADER_TYPE.INV_TONE_MAPPING] = shader_conf;


            //BWOIT SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/bwoit_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            SHADER_TYPE.BWOIT_COMPOSITE_SHADER, ref log);
            resMgr.GLShaders[SHADER_TYPE.BWOIT_COMPOSITE_SHADER] = shader_conf;


            //Text Shaders
            //TODO: CHECK IF A TEXT SHADER WILL BE REQUIRED FOR CUSTOM TEXT RENDERING

            //Camera Shaders
            //TODO: Add Camera Shaders if required
            resMgr.GLShaders[GLSLHelper.SHADER_TYPE.CAMERA_SHADER] = null;

            //FILTERS - EFFECTS

            //Pass Shader
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText passthrough_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            passthrough_shader_fs.addStringFromFile("Shaders/PassThrough_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, passthrough_shader_fs, null, null, null,
                            SHADER_TYPE.PASSTHROUGH_SHADER, ref log);
            resMgr.GLShaders[SHADER_TYPE.PASSTHROUGH_SHADER] = shader_conf;

            

        }


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
            findGeoms(rootObject, obj, ref index);
            

            obj.Close();

        }

        private void exportToAssimp(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to assimp");

            if (rootObject != null)
            {
                Assimp.AssimpContext ctx = new Assimp.AssimpContext();
                
                
                
                Dictionary<int, int> meshImportStatus = new Dictionary<int, int>();
                Assimp.Scene aScene = new Assimp.Scene();
                Assimp.Node rootNode = rootObject.assimpExport(ref aScene, ref meshImportStatus);
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

        private void findGeoms(model m, StreamWriter s, ref uint index)
        {
            switch (m.type)
            {
                case TYPES.MESH:
                    {
                        //Get converted text
                        meshModel me = (meshModel)m;
                        me.writeGeomToStream(s, ref index);
                        break;
                    }
                case TYPES.COLLISION:
                    Console.WriteLine("NOT IMPLEMENTED YET");
                    break;
                default:
                    break;
            }
            
            foreach (model c in m.children)
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
            model intersectedModel = null;
            bool intersectionStatus = false;
            float intersectionDistance = float.MaxValue;
            findIntersectedModel(rootObject, v, ref intersectionStatus, ref intersectedModel, ref intersectionDistance);

            if (intersectedModel != null)
            {
                Console.WriteLine("Ray intersects model : " + intersectedModel.name);
                gizTranslate.setReference(intersectedModel);
                gizTranslate.update();
            }

        }

        
        private void findIntersectedModel(model m, Vector3 ray, ref bool foundIntersection, ref model interSectedModel, ref float intersectionDistance)
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
            foreach (model c in m.children)
            { 
                findIntersectedModel(c, ray, ref foundIntersection, ref interSectedModel, ref intersectionDistance);
            }


        }

#endregion ContextMethods

#region ControlSetup_Init

        //Setup
        
        public void setupRenderingThread()
        {
            
            //Setup rendering thread
            Context.MakeCurrent(null);
            rendering_thread = new Thread(ControlLoop);
            rendering_thread.IsBackground = true;
            rendering_thread.Priority = ThreadPriority.Normal;
        
        }

#endregion ControlSetup_Init

#region Camera Update Functions
        public void setActiveCam(int index)
        {
            if (RenderState.activeCam != null)
                RenderState.activeCam.isActive = false;
            RenderState.activeCam = resMgr.GLCameras[index];
            RenderState.activeCam.isActive = true;
            Console.WriteLine("Switching Camera to {0}", index);
        }

        public void updateActiveCam(int FOV, float zNear, float zFar)
        {
            //TODO: REMOVE, FOR TESTING I"M WORKING ONLY ON THE FIRST CAM
            resMgr.GLCameras[0].setFOV(FOV);
            resMgr.GLCameras[0].zFar = zFar;
            resMgr.GLCameras[0].zNear = zNear;
        }

        public void updateActiveCam(Vector3 pos, Vector3 rot)
        {
            RenderState.activeCam.Position = pos;
            RenderState.activeCam.pitch = rot.X; //Radians rotation on X axis
            RenderState.activeCam.yaw = rot.Y; //Radians rotation on Y axis
            RenderState.activeCam.roll = rot.Z; //Radians rotation on Z axis
    }

#endregion


#region AddObjectMethods

        private void addCamera(bool cull = true)
        {
            //Set Camera position
            Camera cam = new Camera(90, -1, 0, cull);
            for (int i = 0; i < 20; i++)
                cam.Move(0.0f, -0.1f, 0.0f, 0, 0);
            cam.isActive = false;
            resMgr.GLCameras.Add(cam);
        }

        
        private void addTestObjects()
        {
            
        }

#endregion AddObjectMethods


        public void issueRequest(ref ThreadRequest r)
        {
            lock (rt_req_queue)
            {
                rt_req_queue.Enqueue(r);
            }
        }

        private void rt_ResizeViewport(int w, int h)
        {
            renderMgr.resize(w, h);
        }

        private void rt_addRootScene(string filename)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Palettes.set_palleteColors();

            //Clear Form Resources
            resMgr.Cleanup();
            resMgr.Init();
            MVCore.Common.RenderState.activeResMgr = resMgr;
            ModelProcGen.procDecisions.Clear();
            //Clear animScenes
            animScenes.Clear();
            rootObject = null;
            activeModel = null;
            //Clear Gizmos
            gizTranslate = null;
            activeGizmo = null;

            //Clear Update Queues
            modelUpdateQueue.Clear();

            //Clear RenderStats
            RenderStats.ClearStats();
            
            //Stop animation if on
            if (RenderState.renderSettings.ToggleAnimations)
                toggleAnimation();
            
            addCamera();
            addCamera(cull: false); //Add second camera
            setActiveCam(0);

            //Setup new object
            rootObject = GEOMMBIN.LoadObjects(filename);

            //Explicitly add default light to the rootObject
            rootObject.children.Add(resMgr.GLlights[0]);

            //find Animation Capable nodes
            findAnimScenes(rootObject);

            rootObject.updateLODDistances();
            rootObject.update(); //Refresh all transforms
            rootObject.setupSkinMatrixArrays();
            
            //Populate RenderManager
            renderMgr.populate(rootObject);
            
            //Clear Instances
            renderMgr.clearInstances();
            rootObject.updateMeshInfo(); //Update all mesh info

            activeModel = rootObject; //Set the new scene as the new activeModel
            activeModel.selected = 1;

            //Reinitialize gizmos
            gizTranslate = new TranslationGizmo();
            activeGizmo = gizTranslate;

            //Restart anim worker if it was active
            if (!RenderState.renderSettings.ToggleAnimations)
                toggleAnimation();
        }

        //Light Functions
        
        public void updateLightPosition(int light_id)
        {
            Light light = resMgr.GLlights[light_id];
            light.updatePosition(new Vector3 ((float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Sin(MathUtils.radians(light_angle_y))),
                                                (float)(light_distance * Math.Sin(MathUtils.radians(light_angle_x))),
                                                (float)(light_distance * Math.Cos(MathUtils.radians(light_angle_x)) *
                                                            Math.Cos(MathUtils.radians(light_angle_y)))));
        }

        private void fps()
        {
            //Get FPS
            DateTime now = DateTime.UtcNow;
            TimeSpan time = now - oldtime;
            dt = (now - prevtime).TotalMilliseconds;
            
            if (time.TotalMilliseconds > 1000)
            {
                RenderStats.fpsCount = frames;
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
        }


#region INPUT_HANDLERS

        //Gamepad handler
        private void gamepadController()
        {
            if (gpHandler == null) return;
            if (!gpHandler.isConnected()) return;

            //Camera Movement
            float step = movement_speed * 0.002f; 
            float cameraSensitivity = 2.0f;
            float x, y, z, rotx, roty;

            x = step * gpHandler.getAction(ControllerActions.MOVE_X);
            y = step * (gpHandler.getAction(ControllerActions.ACCELERATE) - gpHandler.getAction(ControllerActions.DECELERATE));
            z = step * (gpHandler.getAction(ControllerActions.MOVE_Y_NEG) - gpHandler.getAction(ControllerActions.MOVE_Y_POS));
            rotx = -cameraSensitivity * gpHandler.getAction(ControllerActions.CAMERA_MOVE_H);
            roty = cameraSensitivity * gpHandler.getAction(ControllerActions.CAMERA_MOVE_V);

            RenderState.activeCam.Move(x, y, z, rotx, roty);

        }

        //Keyboard handler
        private void keyboardController()
        {
            if (kbHandler == null) return;

            //Camera Movement
            float step = movement_speed * 0.002f;
            float x, y, z, rotx, roty;

            x = step * (kbHandler.getKeyStatus(OpenTK.Input.Key.D) - kbHandler.getKeyStatus(OpenTK.Input.Key.A));
            y = step * (kbHandler.getKeyStatus(OpenTK.Input.Key.W) - kbHandler.getKeyStatus(OpenTK.Input.Key.S));
            z = step * (kbHandler.getKeyStatus(OpenTK.Input.Key.R) - kbHandler.getKeyStatus(OpenTK.Input.Key.F));


            //Camera rotation is done exclusively using the mouse

            //rotx = 50 * step * (kbHandler.getKeyStatus(OpenTK.Input.Key.E) - kbHandler.getKeyStatus(OpenTK.Input.Key.Q));
            //roty = 50 * step * (kbHandler.getKeyStatus(OpenTK.Input.Key.C) - kbHandler.getKeyStatus(OpenTK.Input.Key.Z));

            RenderState.rotAngles.Y += 100 * step * (kbHandler.getKeyStatus(Key.E) - kbHandler.getKeyStatus(Key.Q));
            RenderState.rotAngles.Y %= 360;


            //Move Camera
            RenderState.activeCam.Move(x, y, z, 0.0f, 0.0f);
            
        }

#endregion

#region ANIMATION_PLAYBACK
        //Animation Playback

        public void toggleAnimation()
        {
            RenderState.renderSettings.ToggleAnimations = !RenderState.renderSettings.ToggleAnimations;
        }

#endregion ANIMATION_PLAYBACK

#region DISPOSE_METHODS

        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                rootObject.Dispose();
            }

            //Free unmanaged resources
            disposed = true;
        }

#endregion DISPOSE_METHODS

    }

}
