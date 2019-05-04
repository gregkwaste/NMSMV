﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System.Diagnostics;
using System.IO;
using System.Reflection;

//Custom Imports
using MVCore;
using MVCore.GMDL;
using GLSLHelper;
using gImage;
using OpenTK.Graphics;
using ClearBufferMask = OpenTK.Graphics.OpenGL.ClearBufferMask;
using CullFaceMode = OpenTK.Graphics.OpenGL.CullFaceMode;
using EnableCap = OpenTK.Graphics.OpenGL4.EnableCap;
using GL = OpenTK.Graphics.OpenGL4.GL;
using PolygonMode = OpenTK.Graphics.OpenGL4.PolygonMode;
using System.ComponentModel;
using System.Threading;

namespace Model_Viewer
{
    public enum GLTEXT_INDEX
    {
        FPS,
        MSG1,
        MSG2,
        COUNT
    };

    public enum THREAD_REQUEST
    {
        NEW_SCENE_REQUEST,
        RESIZE_REQUEST,
        TERMINATE_REQUEST,
        COMPILE_SHADER_REQUEST,
        MODIFY_SHADER_REQUEST,
        NULL
    };

    public enum THREAD_REQUEST_STATUS
    {
        ACTIVE,
        FINISHED,
        NULL
    };

    public class ThreadRequest
    {
        public List<object> arguments;
        public THREAD_REQUEST req;
        public THREAD_REQUEST_STATUS status;
        public ThreadRequest()
        {
            req = THREAD_REQUEST.NULL;
            status = THREAD_REQUEST_STATUS.ACTIVE;
            arguments = new List<object>(); 
        }
    }

    public class CGLControl : GLControl
    {
        public model rootObject;

        //Common Transforms
        private Matrix4 rotMat, mvp;

        private Vector3 rot = new Vector3(0.0f, 0.0f, 0.0f);
        private Camera activeCam;

        //Use public variables for now because getters/setters are so not worth it for our purpose
        public float light_angle_y = 0.0f;
        public float light_angle_x = 0.0f;
        public float light_distance = 5.0f;
        public float light_intensity = 1.0f;
        public float scale = 1.0f;
        
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;
        //Camera Movement Speed
        public int movement_speed = 1;

        //Control Identifier
        private int index;
        private int occludedNum = 0;
        private bool has_focus;

        //Custom Palette
        private Dictionary<string,Dictionary<string,Vector4>> palette;

        //Animation Stuff
        private bool animationStatus = false;
        public List<scene> animScenes = new List<scene>();
        
        //Control private ResourceManagement
        public ResourceMgr resMgr = new ResourceMgr();

        public GBuffer gbuf;

        //Init-GUI Related
        private ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private ToolStripMenuItem loadAnimationToolStripMenuItem;
        private OpenFileDialog openFileDialog1;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private Form pform;

        //Timers
        public System.Timers.Timer t;

        //Control Font and Text Objects
        public FontGL font;
        public GLText[] texObs = new GLText[(int) GLTEXT_INDEX.COUNT];

        //Private fps Counter
        private int frames = 0;
        private DateTime oldtime;

        //Gamepad Setup
        public GamepadHandler gpHandler;
        public KeyboardHandler kbHandler;
        private bool disposed;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        //Rendering Thread Stuff
        private Thread rendering_thread;
        private Queue<ThreadRequest> rt_req_queue = new Queue<ThreadRequest>();
        private bool rt_exit;
        
        private void registerFunctions()
        {
            this.Load += new System.EventHandler(this.genericLoad);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.Resize += new System.EventHandler(this.genericResize);
            this.MouseHover += new System.EventHandler(this.genericHover);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.genericMouseMove);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.CGLControl_MouseClick);
            //this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.generic_KeyDown);
            this.Enter += new System.EventHandler(this.genericEnter);
            this.Leave += new System.EventHandler(this.genericLeave);
        }

        //Default Constructor
        public CGLControl(): base(new GraphicsMode(32, 24, 0, 8))
        {
            registerFunctions();

            //Default Setup
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Control Timer
            t = new System.Timers.Timer();
            t.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            t.Interval = 10;
            t.Start();

        }

        //Constructor
        public CGLControl(int index, Form parent)
        {
            registerFunctions();
            
            //Set Control Identifiers
            this.index = index;

            //Default Setup
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Assign new palette to GLControl
            palette = Model_Viewer.Palettes.createPalettefromBasePalettes();

            //Set parent form
            if (parent != null)
                pform = parent;

            //Control Timer
            t = new System.Timers.Timer();
            t.Elapsed += new System.Timers.ElapsedEventHandler(input_poller);
            t.Interval = 10;
            t.Start();
        }

        private void input_poller(object sender, System.Timers.ElapsedEventArgs e)
        {
            //Console.WriteLine(gpHandler.getAxsState(0, 0).ToString() + " " +  gpHandler.getAxsState(0, 1).ToString());
            //gpHandler.reportButtons();
            gamepadController(); //Move camera according to input
            bool focused = false;

            this.Invoke((MethodInvoker) delegate
            {
                focused = this.Focused;
            });

            if (focused)
                keyboardController(); //Move camera according to input
        }

        private void rt_render()
        {
            //Update per frame data
            frameUpdate();

            gbuf?.start();

            //Console.WriteLine(active_fbo);
            render_scene();

            //Store the dumps

            //gbuf.dump();
            //render_decals();

            //render_cameras();

            if (RenderOptions.RenderLights)
                render_lights();

            //Dump Gbuffer
            //gbuf.dump();
            //System.Threading.Thread.Sleep(1000);

            //Render Deferred
            gbuf.render();

            //No need to blit without a renderbuffer
            //gbuf?.stop();

            //Render info right on the 0 buffer
            if (RenderOptions.RenderInfo)
                render_info();
        }

        public void SetupItems()
        {
            //This function is used to setup all necessary additional parameters on the objects.
            
            //Set new palettes
            traverse_oblistPalette(rootObject, palette);
            //Find animScenes
            findAnimScenes();
            GC.Collect();

        }

        public void findAnimScenes()
        {
            foreach (GeomObject geom in resMgr.GLgeoms.Values)
            {
                if (geom.rootObject.jointDict.Values.Count > 0)
                    this.animScenes.Add(geom.rootObject);
            }
        }

        public void traverse_oblistPalette(model root,Dictionary<string,Dictionary<string,Vector4>> palette)
        {
            foreach (model m in root.children)
            {
                
                //Fix New Recoulors
                if (m.material != null)
                {
                    m.material.palette = palette;
                    for (int i = 0; i < 8; i++)
                    {
                        PaletteOpt palOpt = m.material.palOpts[i];
                        if (palOpt != null)
                            m.material.reColourings[i] = new float[] { palette[palOpt.PaletteName][palOpt.ColorName][0],
                                                                       palette[palOpt.PaletteName][palOpt.ColorName][1],
                                                                       palette[palOpt.PaletteName][palOpt.ColorName][2],
                                                                                                                   1.0f };
                        else
                            m.material.reColourings[i] = new float[] { 1.0f, 1.0f, 1.0f, 0.0f};
                    }

                    //Recalculate Textures
                    GL.DeleteTexture(m.material.fDiffuseMap.bufferID);
                    GL.DeleteTexture(m.material.fMaskMap.bufferID);
                    GL.DeleteTexture(m.material.fNormalMap.bufferID);


                    m.material.prepTextures();
                    m.material.mixTextures();
                }
                if (m.children.Count != 0)
                    traverse_oblistPalette(m, palette);
            }
        }

        //Per Frame Updates
        private void frameUpdate()
        {
            //Fetch Updates on Joints on all animscenes
            for (int i = 0; i < animScenes.Count; i++)
            {
                scene animScene = animScenes[i];
                foreach (Joint j in animScene.jointDict.Values)
                {
                    MathUtils.insertMatToArray16(animScene.JMArray, j.jointIndex * 16, j.worldMat);
                }
            }
            

            //Calculate skinning matrices for each joint for each geometry object
            foreach (GeomObject g in resMgr.GLgeoms.Values)
            {
                MathUtils.mulMatArrays(ref g.skinMats, g.invBMats, g.rootObject.JMArray, 256);
            }


            //Camera & Light Positions
            //Update common transforms
            activeCam.aspect = (float) ClientSize.Width / ClientSize.Height;
            
            Matrix4 Rotx = Matrix4.CreateRotationX(MathUtils.radians(rot[0]));
            Matrix4 Roty = Matrix4.CreateRotationY(MathUtils.radians(rot[1]));
            Matrix4 Rotz = Matrix4.CreateRotationZ(MathUtils.radians(rot[2]));
            rotMat = Rotz * Rotx * Roty;
            mvp = rotMat * activeCam.viewMat; //Full mvp matrix
            MVCore.Common.RenderState.mvp = mvp;

            resMgr.GLCameras[0].updateViewMatrix();
            resMgr.GLCameras[1].updateViewMatrix();

            //Update Custom Light Position
            updateLightPosition(0);

        }

        //Main Rendering Routines
        private void RenderLoop()
        {
            //Setup new Context
            IGraphicsContext new_context = new GraphicsContext(new GraphicsMode(32, 24, 0, 8), this.WindowInfo);
            new_context.MakeCurrent(this.WindowInfo);
            this.MakeCurrent(); //This is essential

            //Add default primitives trying to avoid Vao Request queue traffic
            addDefaultLights();
            addDefaultTextures();
            addCamera(true);
            addCamera(false); //Add second camera
            setActiveCam(0);
            addDefaultPrimitives();
            addTestObjects();


            //Create gbuffer
            gbuf = new GBuffer(this.resMgr, this.ClientSize.Width, this.ClientSize.Height);
            MVCore.Common.RenderState.gbuf = gbuf;
            gbuf.init();

            //Rendering Loop
            while (!rt_exit)
            {
                rt_render();

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
                        switch (req.req)
                        {
                            case THREAD_REQUEST.NEW_SCENE_REQUEST:
                                lock (t)
                                {
                                    t.Stop();
                                    rt_addRootScene((string)req.arguments[0]);
                                    req.status = THREAD_REQUEST_STATUS.FINISHED;
                                    t.Start();
                                }
                                break;
                            case THREAD_REQUEST.RESIZE_REQUEST:
                                rt_ResizeViewport((int)req.arguments[0], (int)req.arguments[1]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST.MODIFY_SHADER_REQUEST:
                                modifyShader((GLSLShaderConfig) req.arguments[0],
                                             (string)req.arguments[1],
                                             (OpenTK.Graphics.OpenGL4.ShaderType)req.arguments[2]);
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST.TERMINATE_REQUEST:
                                rt_exit = true;
                                t.Stop();
                                req.status = THREAD_REQUEST_STATUS.FINISHED;
                                break;
                            case THREAD_REQUEST.NULL:
                                break;
                        }
                    }
                }
                
                Thread.Sleep(2);

                this.SwapBuffers();
                this.Invalidate();
            }
        }

        #region Rendering Methods

        private void traverse_render(model root, int program)
        {
            int active_program = root.shader_programs[program];

            GL.UseProgram(active_program);

            if (active_program == -1)
                throw new ApplicationException("Shit program");

            int loc;

            loc = GL.GetUniformLocation(active_program, "worldMat");
            Matrix4 wMat = root.worldMat;
            GL.UniformMatrix4(loc, false, ref wMat);

            //Send mvp to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref mvp);

            if (root.renderable)
            {
                if (root.type == TYPES.MESH)
                {

                    //Sent rotation matrix individually for light calculations
                    loc = GL.GetUniformLocation(active_program, "rotMat");
                    GL.UniformMatrix4(loc, false, ref rotMat);

                    //Send DiffuseFlag
                    loc = GL.GetUniformLocation(active_program, "diffuseFlag");
                    GL.Uniform1(loc, RenderOptions.UseTextures);

                    //Upload Selected Flag
                    loc = GL.GetUniformLocation(active_program, "use_lighting");
                    GL.Uniform1(loc, RenderOptions.UseLighting);

                    //Upload Selected Flag
                    loc = GL.GetUniformLocation(active_program, "selected");
                    GL.Uniform1(loc, root.selected);

                    //Object program
                    //Local Transformation is the same for all objects 
                    //Pending - Personalize local matrix on each object
                    loc = GL.GetUniformLocation(active_program, "light");
                    GL.Uniform3(loc, this.resMgr.GLlights[0].localPosition);

                    //Upload Light Intensity
                    loc = GL.GetUniformLocation(active_program, "intensity");
                    //GL.Uniform1(loc, this.resMgr.GLlights[0].intensity);
                    GL.Uniform1(loc, light_intensity);


                    //Upload camera position as the light
                    //GL.Uniform3(loc, cam.Position);

                    //Apply frustum culling only for mesh objects
                    if (activeCam.frustum_occlude(root, rotMat))
                        root.render(program);
                    else occludedNum++;
                    
                }
                else if (root.type == TYPES.LOCATOR || root.type == TYPES.SCENE || root.type == TYPES.JOINT || root.type == TYPES.LIGHT || root.type == TYPES.COLLISION)
                {
                    //Locator Program
                    //TESTING
                    root.render(program);
                }
            }

            //Render children
            foreach (model child in root.children)
                traverse_render(child, program);

        }
        
        private void render_scene()
        {
            //Console.WriteLine("Rendering Scene Cam Position : {0}", this.activeCam.Position);
            //Console.WriteLine("Rendering Scene Cam Orientation: {0}", this.activeCam.Orientation);
            //GL.CullFace(CullFaceMode.Back);
            GL.Enable(EnableCap.DepthTest);

            occludedNum = 0; //This will be incremented from traverse_render
            //Render only the first scene for now
            if (this.rootObject != null)
            {
                //Drawing Phase
                traverse_render(this.rootObject, 0);
                //Drawing Debug
                //if (RenderOptions.RenderDebug) traverse_render(this.mainScene, 1);
            }

        }

        private void render_lights()
        {
            int active_program = MVCore.Common.RenderState.activeResMgr.GLShaders["LIGHT_SHADER"];
            GL.UseProgram(active_program);
            
            //Send mvp to all shaders
            int loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref mvp);
            for (int i=0; i<resMgr.GLlights.Count; i++)
                resMgr.GLlights[i].render(0);
        }

        private void render_cameras()
        {
            int active_program = resMgr.GLShaders["BBOX_SHADER"];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref activeCam.viewMat);
            //Send object world Matrix to all shaders

            foreach (Camera cam in resMgr.GLCameras)
            {
                //Old rendering the inverse clip space
                //Upload uniforms
                //loc = GL.GetUniformLocation(active_program, "self_mvp");
                //Matrix4 self_mvp = cam.viewMat;
                //GL.UniformMatrix4(loc, false, ref self_mvp);

                //New rendering the exact frustum plane
                loc = GL.GetUniformLocation(active_program, "worldMat");
                Matrix4 test = Matrix4.Identity;
                test[0,0] = -1.0f;
                test[1,1] = -1.0f;
                test[2,2] = -1.0f;
                GL.UniformMatrix4(loc, false, ref test);

                //Render all inactive cameras
                if (!cam.isActive) cam.render();
                    
            }

        }

        private void render_info()
        {
            //GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            GL.PolygonMode(OpenTK.Graphics.OpenGL4.MaterialFace.FrontAndBack, PolygonMode.Fill);

            GL.UseProgram(MVCore.Common.RenderState.activeResMgr.GLShaders["TEXT_SHADER"]);

            //Load uniforms
            int loc;
            loc = GL.GetUniformLocation(this.resMgr.GLShaders["TEXT_SHADER"], "w");
            GL.Uniform1(loc, (float) this.Width);
            loc = GL.GetUniformLocation(this.resMgr.GLShaders["TEXT_SHADER"], "h");
            GL.Uniform1(loc, (float) this.Height);

            fps();
            texObs[1]?.Dispose();
            texObs[1] = font.renderText(occludedNum.ToString(), new Vector2(1.0f, 0.0f), 0.75f);
            //Render Text Objects
            foreach (GLText t in texObs)
                t?.render();

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

        }

        #endregion Rendering Methods

        #region GLControl Methods
        private void genericEnter(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //Debug.WriteLine("Entered Focus Control " + index);
            t.Start();
        }

        private void genericHover(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            //this.MakeCurrent(); //Control should have been active on hover
            t.Start();
        }

        private void genericLeave(object sender, EventArgs e)
        {
            //Don't update the control when its not focused
            //Debug.WriteLine("Left Focus of Control "+ index);
            t.Stop();
        }

        private void genericLoad(object sender, EventArgs e)
        {

            this.InitializeComponent();
            this.Size = new System.Drawing.Size(640, 480);
            this.MakeCurrent();
            
            //No Gbuffer Options
            //GL.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);
            //GL.ClearColor(RenderOptions.clearColor);
        }

        private void genericPaint(object sender, EventArgs e)
        {
            
        }

        private void genericMouseMove(object sender, MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
            //int delta_x = (int) (Math.Pow(activeCam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(activeCam.fov, 4) * (e.Y - mouse_y));
            int delta_x = (e.X - mouse_x);
            int delta_y = (e.Y - mouse_y);

            delta_x = Math.Min(Math.Max(delta_x, -10), 10);
            delta_y = Math.Min(Math.Max(delta_y, -10), 10);

            if (e.Button == MouseButtons.Left)
            {
                //Debug.WriteLine("Deltas {0} {1} {2}", delta_x, delta_y, e.Button);
                activeCam.AddRotation(delta_x, delta_y);
            }

            mouse_x = e.X;
            mouse_y = e.Y;
            
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
                    animationStatus = !animationStatus;
                    if (animationStatus)
                        backgroundWorker1.RunWorkerAsync();
                    else
                        backgroundWorker1.CancelAsync();
                    break;
                default:
                    //Console.WriteLine("Not Implemented Yet");
                    break;
            }

        }

        private void genericResize(object sender, EventArgs e)
        {
            //DO NOT ALLOW THE HEIGHT TO DROP THAT MUCH. THIS IS STUPID
            //if (this.ClientSize.Height == 0)
            //    this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, 1);

            //Console.WriteLine("GLControl {0} Resizing {1} x {2}",this.index, this.ClientSize.Width, this.ClientSize.Height);
            //this.MakeCurrent(); At this point I have to make sure that this control is already the active one

            //Request a resize

            lock (rt_req_queue)
            {
                //Make new request
                ThreadRequest req = new ThreadRequest();
                req.req = THREAD_REQUEST.RESIZE_REQUEST;
                req.arguments.Clear();
                req.arguments.Add(ClientSize.Width);
                req.arguments.Add(ClientSize.Height);


                //SLOPPY SOLUTION
                //TODO USE A LINKED LIST AND DO NOT DESTROY THE ORDER OF THE REQUESTS
                //Check last request
                if (rt_req_queue.Count > 0)
                {
                    ThreadRequest prev_req = rt_req_queue.Dequeue();
                    if (prev_req.req != THREAD_REQUEST.RESIZE_REQUEST)
                    {
                        rt_req_queue.Enqueue(prev_req);
                    }
                }

                rt_req_queue.Enqueue(req);


            }
                
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.exportToObjToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadAnimationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToObjToolStripMenuItem,
            this.loadAnimationToolStripMenuItem});
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
            // loadAnimationToolStripMenuItem
            // 
            this.loadAnimationToolStripMenuItem.Name = "loadAnimationToolStripMenuItem";
            this.loadAnimationToolStripMenuItem.Size = new System.Drawing.Size(180, 22);
            this.loadAnimationToolStripMenuItem.Text = "Load Animation";
            this.loadAnimationToolStripMenuItem.Click += new System.EventHandler(this.loadAnimationToolStripMenuItem_Click);
            // 
            // openFileDialog1
            // 
            this.openFileDialog1.FileName = "openFileDialog1";
            // 
            // backgroundWorker1
            // 
            this.backgroundWorker1.WorkerReportsProgress = true;
            this.backgroundWorker1.WorkerSupportsCancellation = true;
            this.backgroundWorker1.DoWork += new System.ComponentModel.DoWorkEventHandler(this.backgroundWorker1_DoWork);
            this.backgroundWorker1.ProgressChanged += new System.ComponentModel.ProgressChangedEventHandler(this.backgroundWorker1_ProgressChanged);
            // 
            // CGLControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.Name = "CGLControl";
            this.Size = new System.Drawing.Size(314, 213);
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

               
        private void CGLControl_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                contextMenuStrip1.Show(Control.MousePosition);
            }
            //TODO: ADD SELECT OBJECT FUNCTIONALITY IN THE FUTURE
            //else if ((e.Button == MouseButtons.Left) && (ModifierKeys == Keys.Control))
            //{
            //    selectObject(e.Location);
            //}
        }

        #endregion GLControl Methods

        #region ShaderMethods

        public void compileShader(GLSLShaderConfig config)
        {
            int vertexObject;
            int fragmentObject;

            if (config.program_id != -1)
                GL.DeleteProgram(config.program_id);

            GLShaderHelper.CreateShaders(config, out vertexObject, out fragmentObject, out config.program_id);
        }

        public void modifyShader(GLSLShaderConfig shader_conf, string shaderText, OpenTK.Graphics.OpenGL4.ShaderType shadertype)
        {
            Console.WriteLine("Actually Modifying Shader");

            int[] attached_shaders = new int[20];
            int count;
            GL.GetAttachedShaders(shader_conf.program_id, 20, out count, attached_shaders);
            
            for (int i = 0; i < count; i++)
            {
                int[] shader_params = new int[10];
                GL.GetShader(attached_shaders[i], OpenTK.Graphics.OpenGL4.ShaderParameter.ShaderType, shader_params);

                if (shader_params[0] == (int) shadertype)
                {
                    Console.WriteLine("Found modified shader");

                    string info;
                    int status_code;
                    int new_shader_ob = GL.CreateShader(shadertype);
                    GL.ShaderSource(new_shader_ob, shaderText);
                    GL.CompileShader(new_shader_ob);
                    GL.GetShaderInfoLog(new_shader_ob, out info);
                    GL.GetShader(new_shader_ob, OpenTK.Graphics.OpenGL4.ShaderParameter.CompileStatus, out status_code);
                    if (status_code != 1)
                    {
                        Console.WriteLine("Shader Compilation Failed, Aborting...");
                        Console.WriteLine(info);
                        return;
                    }

                    //Attach new shader back to program
                    GL.DetachShader(shader_conf.program_id, attached_shaders[i]);
                    GL.AttachShader(shader_conf.program_id, new_shader_ob);
                    GL.LinkProgram(shader_conf.program_id);
                    Console.WriteLine("Shader was modified successfully");
                    break;
                }
            }
            Console.WriteLine("Shader was not found...");
        }

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

            obj.WriteLine("# No Mans Model Viewer OBJ File:");
            obj.WriteLine("# www.3dgamedevblog.com");

            //Iterate in objects
            uint index = 1;
            findGeoms(rootObject, obj, ref index);
            
            obj.Close();
            
        }

        private void findGeoms(model m, StreamWriter s, ref uint index)
        {
            if (m.type == TYPES.MESH || m.type==TYPES.COLLISION)
            {
                //Get converted text
                meshModel me = (meshModel) m;
                me.writeGeomToStream(s, ref index);

            }
            foreach (model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }


        private void loadAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            AnimationSelectForm aform = new AnimationSelectForm(this);
            aform.Show();
        }

        #endregion ContextMethods

        #region ControlSetup_Init

        //Setup
        public void setupControlParameters()
        {
            //Everything ready to swap threads
            setupRenderingThread();

        }

        public void setupRenderingThread()
        {
            
            //Setup rendering thread
            Context.MakeCurrent(null);
            rendering_thread = new Thread(RenderLoop);
            rendering_thread.IsBackground = true;

            //Start RT Thread
            rendering_thread.Start();

        }

        #endregion ControlSetup_Init

        #region Camera Update Functions
        public void setActiveCam(int index)
        {
            if (activeCam != null)
                activeCam.isActive = false;
            activeCam = resMgr.GLCameras[index];
            activeCam.isActive = true;
            Console.WriteLine("Switching Camera to {0}", index);
        }

        public void updateActiveCam(int FOV, float zNear, float zFar)
        {
            //TODO: REMOVE, FOR TESTING I"M WORKING ONLY ON THE FIRST CAM
            resMgr.GLCameras[0].setFOV(FOV);
            resMgr.GLCameras[0].zFar = zFar;
            resMgr.GLCameras[0].zNear = zNear;
        }

        public void updateActiveCamPos(float x, float y, float z)
        {
            activeCam.Position = new Vector3(x, y, z);
        }

        #endregion

        public void updateControlRotation(float rx, float ry)
        {
            rot.X = rx;
            rot.Y = ry;
        }

        #region AddObjectMethods

        private void addCamera(bool cull)
        {
            //Set Camera position
            Camera cam = new Camera(60, this.resMgr.GLShaders["BBOX_SHADER"], 0, cull);
            for (int i = 0; i < 20; i++)
                cam.Move(0.0f, -0.1f, 0.0f);
            cam.isActive = false;
            resMgr.GLCameras.Add(cam);
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            //Add Default textures
            //White tex
            string texpath = Path.Combine(execpath, "default.dds");
            Texture tex = new Texture(texpath);
            this.resMgr.GLtextures["default.dds"] = tex;
            //Transparent Mask
            texpath = Path.Combine(execpath, "default_mask.dds");
            tex = new Texture(texpath);
            this.resMgr.GLtextures["default_mask.dds"] = tex;

        }

        private void addDefaultPrimitives()
        {
            //Default quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad(1.0f, 1.0f);
            resMgr.GLPrimitiveVaos["default_quad"] = q.getVAO();

            //Default render quad
            q = new MVCore.Primitives.Quad();
            resMgr.GLPrimitiveVaos["default_renderquad"] = q.getVAO();

            //Default cross
            MVCore.Primitives.Cross c = new MVCore.Primitives.Cross();
            resMgr.GLPrimitiveVaos["default_cross"] = c.getVAO();

            //Default cube
            MVCore.Primitives.Box bx = new MVCore.Primitives.Box(1.0f, 1.0f, 1.0f);
            resMgr.GLPrimitiveVaos["default_box"] = bx.getVAO();

            //Default sphere
            MVCore.Primitives.Sphere sph = new MVCore.Primitives.Sphere(new Vector3(0.0f,0.0f,0.0f), 100.0f);
            resMgr.GLPrimitiveVaos["default_sphere"] = sph.getVAO();
        }

        private void addTestObjects()
        {
            
        }

        #endregion AddObjectMethods


        public void issueRequest(ThreadRequest r)
        {
            lock (rt_req_queue)
            {
                rt_req_queue.Enqueue(r);
            }
        }

        private void rt_ResizeViewport(int w, int h)
        {
            gbuf?.resize(w, h);
            GL.Viewport(0, 0, w, h);
            //GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
        }

        private void rt_addRootScene(string filename)
        {
            //Once the new scene has been loaded, 
            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();

            //Clear Form Resources
            resMgr.Cleanup();
            ModelProcGen.procDecisions.Clear();
            //Clear animScenes
            animScenes.Clear();
            //Throw away the old model
            rootObject.Dispose(); //Prevent rendering
            rootObject = null;

            //Add defaults
            addDefaultLights();
            addDefaultTextures();
            addCamera(true);
            addCamera(false);
            setActiveCam(0);
            addDefaultPrimitives();

            //Setup new object
            scene new_scn = GEOMMBIN.LoadObjects(filename);
            rootObject = new_scn;

            //find Animation Capable Scenes
            this.findAnimScenes();

        }

        //Light Functions
        private void addDefaultLights()
        {
            //Add one and only light for now
            Light light = new Light();
            light.shader_programs = new int[] { this.resMgr.GLShaders["LIGHT_SHADER"] };
            light.localPosition = new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0)));

            this.resMgr.GLlights.Add(light);
        }

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

            if (time.TotalMilliseconds > 1000)
            {
                float fps = 1000.0f * frames / (float)time.TotalMilliseconds;
                //Console.WriteLine("{0} {1}", frames, fps);
                //Reset
                frames = 0;
                oldtime = now;
                texObs[(int) GLTEXT_INDEX.FPS]?.Dispose(); //Dispose the old text
                texObs[(int) GLTEXT_INDEX.FPS] = font.renderText("FPS: " + Math.Round(fps, 1).ToString(), new Vector2(1.3f, 0.0f), 0.75f);
            }
            else
                frames += 1;

        }


        #region INPUT_HANDLERS

        //Gamepad handler
        private void gamepadController()
        {
            if (gpHandler == null) return;
            
            //This Method handles and controls the gamepad input
            gpHandler.updateState();
            //gpHandler.reportAxes();
            
            //Move camera
            //Console.WriteLine(gpHandler.getBtnState(1) - gpHandler.getBtnState(0));
            //Console.WriteLine(gpHandler.getAxsState(0, 1));
            for (int i = 0; i < movement_speed; i++)
                activeCam.Move(0.1f * gpHandler.getAxsState(0, 0),
                               0.1f * gpHandler.getAxsState(0, 1),
                               gpHandler.getBtnState(1) - gpHandler.getBtnState(0));
            
            //Rotate Camera
            //for (int i = 0; i < movement_speed; i++)
            activeCam.AddRotation(-3.0f * gpHandler.getAxsState(1, 0), 3.0f * gpHandler.getAxsState(1, 1));
            //Console.WriteLine("Camera Orientation {0} {1}", activeCam.Orientation.X,
            //    activeCam.Orientation.Y,
            //    activeCam.Orientation.Z);
        }

        //Keyboard handler
        private void keyboardController()
        {
            if (kbHandler == null) return;

            //This Method handles and controls the gamepad input
            
            kbHandler.updateState();
            //gpHandler.reportAxes();

            //Camera Movement
            float step = movement_speed * 0.01f;
            activeCam.Move(
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.D) - kbHandler.getKeyStatus(OpenTK.Input.Key.A)),
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.W) - kbHandler.getKeyStatus(OpenTK.Input.Key.S)),
                    step * (kbHandler.getKeyStatus(OpenTK.Input.Key.R) - kbHandler.getKeyStatus(OpenTK.Input.Key.F)));

            //Rotate Axis
            rot.Y += step * (kbHandler.getKeyStatus(OpenTK.Input.Key.E) - kbHandler.getKeyStatus(OpenTK.Input.Key.Q));
            rot.X += step * (kbHandler.getKeyStatus(OpenTK.Input.Key.C) - kbHandler.getKeyStatus(OpenTK.Input.Key.Z));
            
        }

        #endregion

        #region ANIMATION_PLAYBACK
        //Animation Playback
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            while (true)
            {
                double pause = (1000.0d / (double) RenderOptions.animFPS);
                System.Threading.Thread.Sleep((int)(Math.Round(pause, 1)));
                backgroundWorker1.ReportProgress(0);

                if (backgroundWorker1.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
            }

        }
        
        //Animation Worker
        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            //this.MakeCurrent();
            foreach (scene s in animScenes)
                if (s.animMeta != null) s.animate();
        }

        #endregion ANIMATION_PLAYBACK

        #region DISPOSE_METHODS

        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                rootObject.Dispose();
                gbuf.Dispose();
                font=null;
            }

            //Free unmanaged resources
            disposed = true;
        }

        #endregion DISPOSE_METHODS

    }

}
