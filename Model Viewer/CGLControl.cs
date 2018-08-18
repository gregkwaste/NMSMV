using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Model_Viewer
{
    public class CGLControl : GLControl
    {
        public List<GMDL.model> objects = new List<GMDL.model>();
        public GMDL.model rootObject;

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

        //Custom Palette
        private Dictionary<string,Dictionary<string,Vector4>> palette;

        //Animation Stuff
        private bool animationStatus = false;
        public List<GMDL.scene> animScenes = new List<GMDL.scene>();
        
        //Control private ResourceManagement
        public ResourceMgmt resMgmt = new ResourceMgmt();

        public GBuffer gbuf;

        //Init-GUI Related
        private ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private ToolStripMenuItem loadAnimationToolStripMenuItem;
        private OpenFileDialog openFileDialog1;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;
        private Form pform;
        private ToolStripMenuItem recolourToolStripMenuItem;

        //Timer
        public Timer t;

        //Control Font and Text Objects
        public FontGL font;
        public List<GLText> texObs = new List<GLText>();

        //Private fps Counter
        private int frames = 0;
        private DateTime oldtime;

        //Gamepad Setup
        public GamepadHandler gpHandler;

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
        public CGLControl()
        {
            registerFunctions();

            //Default Setup
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Assign new palette to GLControl
            palette = Model_Viewer.Palettes.createPalette();

            //Control Timer
            t = new Timer();
            t.Tick += new System.EventHandler(timer_ticker);
            t.Interval = 10;
            t.Start();

        }

        //Constructor
        public CGLControl(int index, Form parent)
        {
            registerFunctions();
            
            //Set properties
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.MinimumSize = new System.Drawing.Size(128, 128);
            this.MaximumSize = new System.Drawing.Size(640, 480);

            //Set Control Identifiers
            this.index = index;

            //Set parent form
            pform = parent;
        }

        public void unsubscribePaint()
        {
            this.Paint -= genericPaint;
        }

        //glControl Timer
        private void timer_ticker(object sender, EventArgs e)
        {
            //Console.WriteLine(gpHandler.getAxsState(0, 0).ToString() + " " +  gpHandler.getAxsState(0, 1).ToString());
            //gpHandler.reportButtons();
            gamepadController(); //Move camera according to input

            //Update common transforms
            activeCam.aspect = (float) this.ClientSize.Width / this.ClientSize.Height;
            activeCam.updateViewMatrix();
            activeCam.updateFrustumPlanes();
            //proj = Matrix4.CreatePerspectiveFieldOfView(-w, w, -h, h , znear, zfar);

            Matrix4 Rotx = Matrix4.CreateRotationX(rot[0] * (float)Math.PI / 180.0f);
            Matrix4 Roty = Matrix4.CreateRotationY(rot[1] * (float)Math.PI / 180.0f);
            Matrix4 Rotz = Matrix4.CreateRotationZ(rot[2] * (float)Math.PI / 180.0f);
            rotMat = Rotz * Rotx * Roty;
            mvp = activeCam.viewMat; //Full mvp matrix
            Util.mvp = mvp;
            occludedNum = 0; //Reset Counter

            //Simply invalidate the gl control
            //glControl1.MakeCurrent();
            this.Invalidate();
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
            foreach (GMDL.GeomObject geom in resMgmt.GLgeoms.Values)
            {
                if (geom.rootObject.jointDict.Values.Count > 0)
                    this.animScenes.Add(geom.rootObject);
            }
        }

        public void traverse_oblistPalette(GMDL.model root,Dictionary<string,Dictionary<string,Vector4>> palette)
        {
            foreach (GMDL.model m in root.children)
            {
                
                //Fix New Recoulors
                if (m.material != null)
                {
                    m.material.palette = palette;
                    for (int i = 0; i < 8; i++)
                    {
                        GMDL.PaletteOpt palOpt = m.material.palOpts[i];
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
                GMDL.scene animScene = animScenes[i];
                foreach (GMDL.Joint j in animScene.jointDict.Values)
                {
                    MathUtils.insertMatToArray16(animScene.JMArray, j.jointIndex * 16, j.worldMat);
                }
            }
            

            //Calculate skinning matrices for each joint for each geometry object
            foreach (GMDL.GeomObject g in resMgmt.GLgeoms.Values)
            {
                MathUtils.mulMatArrays(ref g.skinMats, g.invBMats, g.rootObject.JMArray, 256);
            }
        }

        //Main Rendering Routines

        private void render_scene()
        {
            //Console.WriteLine("Rendering Scene Cam Position : {0}", this.activeCam.Position);
            //Console.WriteLine("Rendering Scene Cam Orientation: {0}", this.activeCam.Orientation);
            GL.CullFace(CullFaceMode.Back);

            //Render only the first scene for now

            if (this.rootObject != null)
            {
                //Drawing Phase
                traverse_render(this.rootObject, 0);
                //Drawing Debug
                //if (RenderOptions.RenderDebug) traverse_render(this.mainScene, 1);
            }

        }

        private void render_info()
        {
            this.MakeCurrent();
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);


            GL.UseProgram(Util.activeResMgmt.shader_programs[4]);

            //Load uniforms
            int loc;
            loc = GL.GetUniformLocation(this.resMgmt.shader_programs[4], "w");
            GL.Uniform1(loc, (float) this.Width);
            loc = GL.GetUniformLocation(Util.activeResMgmt.shader_programs[4], "h");
            GL.Uniform1(loc, (float) this.Height);

            fps();
            texObs[1] = font.renderText(occludedNum.ToString(), new Vector2(1.0f, 0.0f), 0.75f);
            //Render Text Objects
            foreach (GLText t in texObs)
                t.render();

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

        }

        private void genericEnter(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            Debug.WriteLine("Entered Focus Control " + index);
            this.MakeCurrent(); //Control should have been active on hover
            t.Start();
        }

        private void genericHover(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            Debug.WriteLine("Hovering Over Control " + index);
            this.MakeCurrent(); //Control should have been active on hover
            t.Start();
        }

        private void genericLeave(object sender, EventArgs e)
        {
            //Don't update the control when its not focused
            Debug.WriteLine("Left Focus of Control "+ index);
            t.Stop();
        }

        private void traverse_render(GMDL.model root, int program)
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

            if (root.type == TYPES.MESH)
            {

                //Sent rotation matrix individually for light calculations
                loc = GL.GetUniformLocation(active_program, "rotMat");
                GL.UniformMatrix4(loc, false, ref rotMat);

                //Send DiffuseFlag
                loc = GL.GetUniformLocation(active_program, "diffuseFlag");
                GL.Uniform1(loc, RenderOptions.UseTextures);

                //Object program
                //Local Transformation is the same for all objects 
                //Pending - Personalize local matrix on each object
                loc = GL.GetUniformLocation(active_program, "scale");
                GL.Uniform1(loc, this.scale);

                loc = GL.GetUniformLocation(active_program, "light");
                GL.Uniform3(loc, this.resMgmt.GLlights[0].localPosition);

                //Upload Light Intensity
                loc = GL.GetUniformLocation(active_program, "intensity");
                GL.Uniform1(loc, 1.0f);

                //Upload camera position as the light
                //GL.Uniform3(loc, cam.Position);

                //Apply frustum culling only for mesh objects
                //root.render(program);
                if (activeCam.frustum_occlude(root, rotMat)) root.render(program);
                else occludedNum++;
            }
            else if (root.type == TYPES.LOCATOR || root.type == TYPES.SCENE || root.type == TYPES.JOINT || root.type == TYPES.LIGHT || root.type == TYPES.COLLISION)
            {
                //Locator Program
                //TESTING
                root.render(program);
            }

            //Cleanup

            //Render children
            foreach (GMDL.model child in root.children)
                traverse_render(child, program);

        }

        private void genericLoad(object sender, EventArgs e)
        {

            this.InitializeComponent();
            this.Size = new System.Drawing.Size(640, 480);
            this.MakeCurrent();
            GL.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);
            GL.ClearColor(RenderOptions.clearColor);
            GL.Enable(EnableCap.DepthTest);
            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            //Debug.WriteLine("GL Cleared");
            //Debug.WriteLine(GL.GetError());
        }

        private void genericPaint(object sender, EventArgs e)
        {

            //Update per frame data
            frameUpdate();
            
            this.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            gbuf.start();

            //Console.WriteLine(active_fbo);
            render_scene();

            //Store the dumps
            
            //gbuf.dump();
            //render_decals();

            //render_cameras();
            //render_lights();
            render_info();

            //gbuf.stop();

            //Render Deferred
            //gbuf.render();
            gbuf.blit();

            this.SwapBuffers();

            //translate_View();
            ////Draw scene
            //Update Joystick 

            //Console.WriteLine("Painting Control");
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
                //Local Transformation
                case Keys.Q:
                    for (int i=0;i<movement_speed;i++)
                        this.rot.Y -= 4.0f;
                    //activeCam.AddRotation(-4.0f,0.0f);
                    break;
                case Keys.E:
                    for (int i = 0; i < movement_speed; i++)
                        this.rot.Y += 4.0f;
                    //activeCam.AddRotation(4.0f, 0.0f);
                    break;
                case Keys.Z:
                    for (int i = 0; i < movement_speed; i++)
                        this.rot.X -= 4.0f;
                    break;
                case Keys.C:
                    for (int i = 0; i < movement_speed; i++)
                        this.rot.X += 4.0f;
                    break;
                //Camera Movement
                case Keys.W:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, 0.1f, 0.0f);
                    break;
                case Keys.S:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, -0.1f, 0.0f);
                    break;
                case (Keys.D):
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(+0.01f, 0.0f, 0.0f);
                    break;
                case Keys.A:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(-0.01f, 0.0f, 0.0f);
                    break;
                case (Keys.R):
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, 0.0f, 0.01f);
                    break;
                case Keys.F:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, 0.0f, -0.01f);
                    break;
                //Light Rotation
                case Keys.N:
                    this.light_angle_y -= 1;
                    updateLightPosition(0);
                    break;
                case Keys.M:
                    this.light_angle_y += 1;
                    updateLightPosition(0);
                    break;
                case Keys.Oemcomma:
                    this.light_angle_x -= 1;
                    updateLightPosition(0);
                    break;
                case Keys.OemPeriod:
                    this.light_angle_x += 1;
                    updateLightPosition(0);
                    break;
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
                //Toggle Small Render
                case Keys.P:
                    RenderOptions.RenderSmall = !RenderOptions.RenderSmall;
                    break;
                //Toggle Collisions Render
                case Keys.OemOpenBrackets:
                    RenderOptions.RenderCollisions = !RenderOptions.RenderCollisions;
                    break;
                //Toggle Debug Render
                case Keys.OemCloseBrackets:
                    RenderOptions.RenderDebug = !RenderOptions.RenderDebug;
                    break;
                //Switch cameras
                case Keys.NumPad0:
                    if (this.resMgmt.GLCameras[0].isActive)
                    {
                        activeCam.isActive = false;
                        activeCam = this.resMgmt.GLCameras[1];
                    }
                    else
                    {
                        activeCam.isActive = false;
                        activeCam = this.resMgmt.GLCameras[0];
                    }

                    activeCam.isActive = true;

                    /*
                     * I need to provide of a way to export the camera settings back to the mainform controls
                     * For now I'll completely skip this step.
                     *
                     */

                    /*
                    //Set info of active cam to the controls
                    numericUpDown1.ValueChanged -= this.numericUpDown1_ValueChanged;
                    numericUpDown1.Value = (decimal)(180.0f * activeCam.fov / (float)Math.PI);
                    numericUpDown1.ValueChanged += this.numericUpDown1_ValueChanged;

                    numericUpDown4.ValueChanged -= this.numericUpDown4_ValueChanged;
                    numericUpDown4.Value = (decimal)activeCam.zNear;
                    numericUpDown4.ValueChanged += this.numericUpDown4_ValueChanged;

                    numericUpDown5.ValueChanged -= this.numericUpDown5_ValueChanged;
                    numericUpDown5.Value = (decimal)activeCam.zFar;
                    numericUpDown5.ValueChanged += this.numericUpDown5_ValueChanged;
                    */

                    //Console.WriteLine(activeCam.GetViewMatrix());

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
                    Debug.WriteLine("Not Implemented Yet");
                    break;
            }

        }

        private void genericResize(object sender, EventArgs e)
        {
            if (this.ClientSize.Height == 0)
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, 1);
            //Console.WriteLine("GLControl {0} Resizing {1} x {2}",this.index, this.ClientSize.Width, this.ClientSize.Height);
            //this.MakeCurrent(); At this point I have to make sure that this control is already the active one
            //TODO add a gbuffer to every CGLControl
            if (gbuf != null)
                gbuf.resize(this.ClientSize.Width, this.ClientSize.Height);
            GL.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);
            //GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
            
        }

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.exportToObjToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.loadAnimationToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.openFileDialog1 = new System.Windows.Forms.OpenFileDialog();
            this.backgroundWorker1 = new System.ComponentModel.BackgroundWorker();
            this.recolourToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToObjToolStripMenuItem,
            this.loadAnimationToolStripMenuItem,
            this.recolourToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(160, 92);
            // 
            // exportToObjToolStripMenuItem
            // 
            this.exportToObjToolStripMenuItem.Name = "exportToObjToolStripMenuItem";
            this.exportToObjToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.exportToObjToolStripMenuItem.Text = "Export to obj";
            this.exportToObjToolStripMenuItem.Click += new System.EventHandler(this.exportToObjToolStripMenuItem_Click);
            // 
            // loadAnimationToolStripMenuItem
            // 
            this.loadAnimationToolStripMenuItem.Name = "loadAnimationToolStripMenuItem";
            this.loadAnimationToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
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
            // recolourToolStripMenuItem
            // 
            this.recolourToolStripMenuItem.Name = "recolourToolStripMenuItem";
            this.recolourToolStripMenuItem.Size = new System.Drawing.Size(159, 22);
            this.recolourToolStripMenuItem.Text = "Recolour";
            this.recolourToolStripMenuItem.Click += new System.EventHandler(this.reColourToolStripMenuItem_Click);
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

        private void findGeoms(GMDL.model m, StreamWriter s, ref uint index)
        {
            if (m.type == TYPES.MESH || m.type==TYPES.COLLISION)
            {
                //Get converted text
                GMDL.meshModel me = (GMDL.meshModel) m;
                me.writeGeomToStream(s, ref index);

            }
            foreach (GMDL.model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }


        private void loadAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Loading Animation");
            //Opening Animation File
            Debug.WriteLine("Opening Animation File");

            //Opening Animation File
            Debug.WriteLine("Opening File");

            AnimationSelectForm aform = new AnimationSelectForm(this);
            aform.Show();
        }

        private void reColourToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Recolouring GLControl " + ((CGLControl) sender).index);
        }

        //Setup
        public void setupControlParameters()
        {
            addDefaultLights();
            addDefaultTextures();
            addCameras();

            //Init Gbuffer
            ResourceMgmt oldmgmt = Util.activeResMgmt;
            Util.activeResMgmt = this.resMgmt;
            gbuf = new GBuffer();
            Util.activeResMgmt = oldmgmt;
        }

        private void addCameras()
        {
            //Set Camera position
            activeCam = new Camera(60, this.resMgmt.shader_programs[8], 0, false);
            for (int i = 0; i < 20; i++)
                activeCam.Move(0.0f, -0.1f, 0.0f);
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            //Add Default textures
            //White tex
            string texpath = Path.Combine(execpath, "default.dds");
            GMDL.Texture tex = new GMDL.Texture(texpath);
            this.resMgmt.GLtextures["default.dds"] = tex;
            //Transparent Mask
            texpath = Path.Combine(execpath, "default_mask.dds");
            tex = new GMDL.Texture(texpath);
            this.resMgmt.GLtextures["default_mask.dds"] = tex;

        }

        //Light Functions

        private void addDefaultLights()
        {
            //Add one and only light for now
            GMDL.Light light = new GMDL.Light();
            light.shader_programs = new int[] { this.resMgmt.shader_programs[7] };
            light.localPosition = new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0)));

            this.resMgmt.GLlights.Add(light);
        }

        public void updateLightPosition(int light_id)
        {
            GMDL.Light light = (GMDL.Light) this.resMgmt.GLlights[light_id];
            light.updatePosition(new Vector3 ((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0))));
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
                texObs[0] = font.renderText("FPS: " + Math.Round(fps, 1).ToString(), new Vector2(1.3f, 0.0f), 0.75f);
            }
            else
                frames += 1;

        }

        //Gamepad handler
        private void gamepadController()
        {
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




        //Animation Playback
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            while (true)
            {
                double pause = (1000.0d / (double)RenderOptions.animFPS);
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
            foreach (GMDL.scene s in animScenes)
                if (s.animMeta != null) s.animate();
        }

    }
    
}
