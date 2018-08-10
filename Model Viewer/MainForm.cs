using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Xml;
using System.Reflection;
using Model_Viewer.Properties;

//Custom imports
using GLHelpers;
using gImage;
using System.Linq;

namespace Model_Viewer
{

    enum treeviewCheckStatus
    {
        Single=0x0,
        Children=0x1
    }

    public partial class Form1 : Form
    {
        private bool glloaded = false;
        private Vector3 rot = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 target = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 eye_pos = new Vector3(0.0f, 5.5f, 50.0f);
        private Vector3 eye_dir = new Vector3(0.0f, 0.0f, -10.0f);
        private Vector3 eye_up = new Vector3(0.0f, 1.0f, 0.0f);
        private Camera activeCam;

        //Common transforms
        private Matrix4 rotMat = Matrix4.Identity;
        private Matrix4 mvp = Matrix4.Identity;
        private int occludedNum = 0;

        private float scale = 1.0f;
        
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;
       
        public int childCounter = 0;

        //Shader objects
        int vertex_shader_ob;
        int fragment_shader_ob;

        public List<GMDL.model> animScenes = new List<GMDL.model>();
        //Deprecated
        //private List<GMDL.model> vboobjects = new List<GMDL.model>();
        //private GMDL.model rootObject;
        private XmlDocument xmlDoc = new XmlDocument();
        private Dictionary<string, int> index_dict = new Dictionary<string, int>();
        private OrderedDictionary joint_dict = new OrderedDictionary();
        private treeviewCheckStatus tvchkstat = treeviewCheckStatus.Children;

        //Animation Meta
        public GMDL.AnimeMetaData meta = new GMDL.AnimeMetaData();

        //Joint Array for shader
        //TEST public float[] JMArray = new float[256 * 16];
        
        //public float[] JColors = new float[256 * 3];

        //Selected Object
        public GMDL.model selectedOb;

        //Path
        private string mainFilePath;
        //Private Settings Window
        private SettingsForm Settings = new SettingsForm();
        
        //Custom defined GLControl
        private CGLControl glcontrol1;


        public Form1()
        {
            //Custom stuff
            this.xyzControl1 = new XYZControl("worldPosition");
            this.xyzControl2 = new XYZControl("localPosition");
            
            InitializeComponent();

            this.rightFlowPanel.Controls.Add(xyzControl2);
            this.rightFlowPanel.Controls.Add(xyzControl1);

            //
            // xyzControl2
            //
            this.xyzControl2.Name = "xyzControl2";
            this.xyzControl2.Size = new System.Drawing.Size(112, 119);
            this.xyzControl2.TabIndex = 4;
            this.xyzControl2.TabStop = false;
            this.xyzControl2.Text = "LocalPosition";

            //
            // xyzControl1
            //
            this.xyzControl1.Name = "xyzControl1";
            this.xyzControl1.Size = new System.Drawing.Size(112, 119);
            this.xyzControl1.TabIndex = 3;
            this.xyzControl1.TabStop = false;
            this.xyzControl1.Text = "WorldPosition";


        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Opening File");
            DialogResult res = openFileDialog1.ShowDialog();
            if (res == DialogResult.Cancel)
                return;

            var filename = openFileDialog1.FileName;
            mainFilePath = openFileDialog1.FileName;

            var split = filename.Split('.');
            var ext = split[split.Length - 1].ToUpper();
            Console.WriteLine(ext);

            if (ext != "MBIN" & ext != "EXML")
            {
                MessageBox.Show("Please select a SCENE.MBIN or a SCENE.exml File", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();

            //this.glcontrol1.Paint -= this.glControl1_Paint;
            glcontrol1.t.Stop();
            glcontrol1.rootObject.Dispose(); //Prevent rendering
            glcontrol1.rootObject = null;
            glcontrol1.t.Start();
            RightSplitter.Panel1.Controls.Clear();

            //Clear Form Resources
            glcontrol1.resMgmt.Cleanup();
            ModelProcGen.procDecisions.Clear();
            
            //Reset the activeControl and create new gbuffer
            Util.activeControl = glcontrol1;

            //Reload Default Resources
            glcontrol1.setupControlParameters();

            RightSplitter.Panel1.Controls.Add(glcontrol1);

            glcontrol1.Update();
            glcontrol1.MakeCurrent();
            GMDL.scene scene = GEOMMBIN.LoadObjects(filename);
            scene.ID = this.childCounter;
            glcontrol1.rootObject = scene;
            this.childCounter++;

            Util.setStatus("Creating Nodes...", this.toolStripStatusLabel1);

            //Console.WriteLine("Objects Returned: {0}",oblist.Count);
            MyTreeNode node = new MyTreeNode(scene.name);
            node.Checked = true;
            node.model = scene;
            //Clear index dictionary
            index_dict.Clear();
            joint_dict.Clear();
            animScenes.Clear();

            this.childCounter = 0;
            //Add root to dictionary
            index_dict[scene.name] = this.childCounter;
            this.childCounter += 1;
            //Set indices and TreeNodes 
            traverse_oblist(scene, node);
            //Load joints
            glcontrol1.findAnimScenes();
            //Add root to treeview
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(node);
            GC.Collect();

            //glControl1_Resize(new object(), null); //Try to call resize event
            //glcontrol1.Resize();

            glcontrol1.Update();
            glcontrol1.Invalidate();

            //Cleanup Materials
            foreach (GMDL.Material mat in Util.activeResMgmt.GLmaterials.Values)
            {
                //mat.cleanupOriginals();
            }

            Util.setStatus("Ready", this.toolStripStatusLabel1);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //Redirect console output to file if Release Mode
#if !DEBUG 
            FileStream filestream = new FileStream("out.txt", FileMode.Create);
            var streamwriter = new StreamWriter(filestream);
            streamwriter.AutoFlush = true;
            Console.SetOut(streamwriter);
            Console.SetError(streamwriter);
#endif

            setupGLControl();
            
            //Load NMSTemplate Enumerators
            loadNMSEnums();

            //Set active components
            Util.activeResMgmt = glcontrol1.resMgmt;
            Util.activeControl = glcontrol1;

            //Compile Shaders
            compileShaders();
            
            GMDL.scene scene = new GMDL.scene();
            scene.type = TYPES.SCENE;
            scene.shader_programs = new int[] { Util.activeResMgmt.shader_programs[1],
                                                Util.activeResMgmt.shader_programs[5],
                                                Util.activeResMgmt.shader_programs[6]};


            if (!this.glloaded)
                return;

            
            scene.ID = this.childCounter;

            //Add Frustum cube
            GMDL.Collision cube = new GMDL.Collision();
            //cube.vbo = (new Capsule(new Vector3(), 14.0f, 2.0f)).getVBO();
            cube.main_Vao = (new Box(1.0f, 1.0f, 1.0f)).getVAO();

            //Create model
            GMDL.Collision so = new GMDL.Collision();

            //Remove that after implemented all the different collision types
            cube.shader_programs = new int[] { Util.activeResMgmt.shader_programs[0],
                                               Util.activeResMgmt.shader_programs[5],
                                               Util.activeResMgmt.shader_programs[6]}; //Use Mesh program for collisions
            cube.name = "FRUSTUM";
            cube.collisionType = (int) COLLISIONTYPES.BOX;


            scene.children.Add(cube);

            glcontrol1.rootObject = scene;
            this.childCounter++;
            MyTreeNode node = new MyTreeNode("ORIGIN");
            node.model = scene;
            node.Checked = true;
            treeView1.Nodes.Add(node);

            //Set to current cam fov
            //numericUpDown1.Value = 35;
            //numericUpDown2.Value = (decimal)5.0;

            //Joystick Init
            for (int i = 0; i < 2; i++)
            {
                var caps = OpenTK.Input.GamePad.GetCapabilities(i);
                if (caps.GamePadType == OpenTK.Input.GamePadType.Unknown) break;
                Util.gamepadID = i;
                Console.WriteLine(caps + caps.GetType().Name);
            }
            
            //Setup GamePad Handler for the control
            glcontrol1.gpHandler = new GamepadHandler(Util.gamepadID);

            //Load Settings
            Settings.loadSettings();

            //Setup GL Control once shaders have been successfully compiled
            //Setup Lights, Cameras, Default Textures, Gbuffer
            glcontrol1.setupControlParameters();
            //Apply custom positioning attributes
            glcontrol1.Dock = DockStyle.Fill;

            //Set global gbuffer
            Util.gbuf = glcontrol1.gbuf;
            
            //Set GEOMMBIN statusStrip
            GEOMMBIN.strip = this.toolStripStatusLabel1;

            //Set Default JMarray
            for (int i = 0; i < 256; i++)
                Util.insertMatToArray16(Util.JMarray, i * 16, Matrix4.Identity);

            int maxfloats;
            GL.GetInteger(GetPName.MaxVertexUniformVectors, out maxfloats);
            toolStripStatusLabel1.Text = "Ready";

           //Load font
            glcontrol1.font = setupFont();

            //Add some text for rendering
            glcontrol1.texObs.Add(glcontrol1.font.renderText("Greetings", new Vector2(0.02f, 0.0f), scale));
            glcontrol1.texObs.Add(glcontrol1.font.renderText(occludedNum.ToString(), new Vector2(1.0f, 0.0f), 1.0f));



            //Add 2 Cams
            Camera cam;
            cam = new Camera(50, Util.activeResMgmt.shader_programs[8], 0, true);
            Util.activeResMgmt.GLCameras.Add(cam);
            //cam = new Camera(50, ResourceMgmt.shader_programs[8], 0, false);
            //ResourceMgmt.GLCameras.Add(cam);
            activeCam = Util.activeResMgmt.GLCameras[0];
            activeCam.isActive = true;

            
            //Check if Temp folder exists
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
            
        }

        private void Form1_Close(object sender, EventArgs e)
        {

        }

        
        private void setupGLControl()
        {

            glcontrol1 = new CGLControl();
            RightSplitter.Panel1.Controls.Add(glcontrol1);
            glcontrol1.Update();
            glcontrol1.MakeCurrent();
            this.glloaded = true;
        }

        private FontGL setupFont()
        {
            FontGL font = new FontGL();

            //Test BMP Image Class
            //BMPImage bm = new BMPImage("courier.bmp");

            //Create font to memory
            MemoryStream ms = FontGL.createFont();
            BMPImage bm = new BMPImage(ms);

            //Testing some inits
            font.initFromImage(bm);
            font.tex = bm.GLid;
            font.program = Util.activeResMgmt.shader_programs[4];

            //Set default settings
            float scale = 0.75f;
            font.space = scale * 0.20f;
            font.width = scale * 0.20f;
            font.height = scale * 0.35f;

            return font;
        }

        private void compileShaders()
        {
            //Query GL Extensions
            Console.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Console.WriteLine(s);
                if (s.Contains("16"))
                    Console.WriteLine(s);
            }

            //Query maximum buffer sizes
            Console.WriteLine("MaxUniformBlock Size {0}", GL.GetInteger(GetPName.MaxUniformBlockSize));

            //Populate shader list
            Util.activeResMgmt.shader_programs = new int[11];
            string vvs, ggs, ffs;
            //Geometry Shader
            //Compile Object Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VSEmpty.glsl");
            ggs = GLSL_Preprocessor.Parser("Shaders/Simple_GS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FSEmpty.glsl");

            GLShaderHelper.CreateShaders(vvs, ffs, ggs, "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.activeResMgmt.shader_programs[5]);

            //Picking Shaders
            GLShaderHelper.CreateShaders(Resources.pick_vert, Resources.pick_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.activeResMgmt.shader_programs[6]);

            //Main Shader
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.activeResMgmt.shader_programs[0]);

            //Texture Mixing Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/pass_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/pass_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.activeResMgmt.shader_programs[3]);

            //GBuffer Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.activeResMgmt.shader_programs[9]);

            //Decal Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/decal_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Decal_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.activeResMgmt.shader_programs[10]);

            //Locator Shaders
            GLShaderHelper.CreateShaders(Resources.locator_vert, Resources.locator_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.activeResMgmt.shader_programs[1]);

            //Joint Shaders
            GLShaderHelper.CreateShaders(Resources.joint_vert, Resources.joint_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.activeResMgmt.shader_programs[2]);

            //Text Shaders
            GLShaderHelper.CreateShaders(Resources.text_vert, Resources.text_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.activeResMgmt.shader_programs[4]);

            //Light Shaders
            GLShaderHelper.CreateShaders(Resources.light_vert, Resources.light_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.activeResMgmt.shader_programs[7]);

            //Camera Shaders
            GLShaderHelper.CreateShaders(Resources.camera_vert, Resources.camera_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.activeResMgmt.shader_programs[8]);
        }

        private void render_decals()
        {
            //gbuf.dump();
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.DepthFunc(DepthFunction.Gequal);


            int active_program = Util.activeResMgmt.shader_programs[10];
            GL.UseProgram(active_program);
            int loc;
            Matrix4 temp;

            glcontrol1.gbuf.dump();

            //Upload inverse decat world matrix
            //for ( int i = 0;i< Math.Min(1, this.resMgmt.GLDecals.Count); i++)
            foreach (GMDL.model decal in Util.activeResMgmt.GLDecals)
            {
                //GMDL.Decal decal = (GMDL.Decal) this.resMgmt.GLDecals[i];
                GL.UseProgram(active_program);

                //Upload mvp
                loc = GL.GetUniformLocation(active_program, "mvp");
                GL.UniformMatrix4(loc, false, ref mvp);

                //Upload projection
                loc = GL.GetUniformLocation(active_program, "proj");
                GL.UniformMatrix4(loc, false, ref activeCam.projMat);


                //Upload view
                loc = GL.GetUniformLocation(active_program, "look");
                GL.UniformMatrix4(loc, false, ref activeCam.lookMat);

                //Upload projection matrix inverse
                loc = GL.GetUniformLocation(active_program, "invProj");
                temp = Matrix4.Invert(activeCam.projMat);
                GL.UniformMatrix4(loc, false, ref temp);

                //Upload view matrix inverse
                loc = GL.GetUniformLocation(active_program, "invView");
                temp = Matrix4.Invert(activeCam.lookMat);
                GL.UniformMatrix4(loc, false, ref temp);

                //Upload Inverse of decal Matrix
                loc = GL.GetUniformLocation(active_program, "decalInvMat");
                Matrix4 wMat = decal.worldMat;
                Matrix4 decal_inv = Matrix4.Invert(wMat);
                GL.UniformMatrix4(loc, false, ref decal_inv);

                //Upload world decal Matrix
                loc = GL.GetUniformLocation(active_program, "worldMat");
                GL.UniformMatrix4(loc, false, ref wMat);
                

                decal.render(0);

                //gbuf.dump();
            }
            //GL.DepthFunc(DepthFunction.Lequal);
            GL.Disable(EnableCap.Blend);
        }

        private void render_lights()
        {
            int active_program = Util.activeResMgmt.shader_programs[7];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref mvp);
            
            foreach (GMDL.model light in Util.activeResMgmt.GLlights)
                light.render(0);
        }

        private void render_cameras()
        {
            int active_program = Util.activeResMgmt.shader_programs[8];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            Matrix4 cam_mvp = activeCam.viewMat;
            GL.UniformMatrix4(loc, false, ref cam_mvp);
            //Send object world Matrix to all shaders

            foreach (Camera cam in Util.activeResMgmt.GLCameras)
            {
                //Upload uniforms
                loc = GL.GetUniformLocation(active_program, "self_mvp");
                Matrix4 self_mvp = cam.viewMat;
                GL.UniformMatrix4(loc, false, ref self_mvp);
                if (!cam.isActive) cam.render();
            }
                
        }

        private void selectObject(System.Drawing.Point p)
        {
            if (!GL.GetString(StringName.Extensions).Contains("GL_EXT_framebuffer_object"))
            {
                throw new NotSupportedException(
                     "GL_EXT_framebuffer_object extension is required. Please update your drivers.");
            }

            //First of all clear the color buffer
            //Create the texture to render to
            int tex_w = glcontrol1.Width;
            int tex_h = glcontrol1.Height;

            //Create Frame and renderbuffers
            int fb = GL.Ext.GenFramebuffer();
            //GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);
            //GL.Viewport(0, 0, tex_w, tex_h);

            //Create depth texture for testing
            int depth_tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, depth_tex);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, tex_w, tex_h, 0, PixelFormat.DepthComponent,  PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);

            int[] rbufs = new int[2];
            GL.Ext.GenRenderbuffers(2, rbufs);
            int depth_rb = rbufs[1];
            int color_rb = rbufs[0];

            Console.WriteLine("Selected Ob: Last GL Error: " + GL.GetError());

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);
            //Bind color renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, color_rb);
            GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.Rgba8, tex_w, tex_h);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, RenderbufferTarget.RenderbufferExt, color_rb);

            Console.WriteLine("Selected Ob: Last GL Error: " + GL.GetError());

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);
            //Bind depth renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, depth_rb);
            GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.DepthComponent, tex_w, tex_h);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, RenderbufferTarget.RenderbufferExt, depth_rb);
            GL.Ext.FramebufferTexture(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, depth_tex, 0);

            Console.WriteLine("Selected Ob: Last GL Error: " + GL.GetError());

            //Draw Scene
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);//Now render objects with the picking program
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.Viewport(0, 0, tex_w, tex_h);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Console.WriteLine("Selected Ob: Last GL Error: " + GL.GetError());

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            if (glcontrol1.rootObject != null) traverse_render(glcontrol1.rootObject, 2);

            Console.WriteLine("Selected Ob: Last GL Error: " + GL.GetError());

            //Store Framebuffer to Disk
            byte[] pixels = new byte[4 * tex_w * tex_h];

            //Store depth texture to disk
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            //GL.CopyTexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, 0, 0, tex_w, tex_h, 0);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, pixels);
            Console.WriteLine("Selected Ob: Last GL Error: " + GL.GetError());

#if DEBUG
            FileStream fs = new FileStream("depthTexture", FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(pixels);
            fs.Flush();
            fs.Close();


            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Console.WriteLine("Saving Picking Buffer. Dimensions: " + tex_w + " " + tex_h);
            GL.ReadPixels(0, 0, tex_w, tex_h, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            fs = new FileStream("pickBuffer", FileMode.Create);
            bw = new BinaryWriter(fs);
            bw.Write(pixels);
            fs.Flush();
            fs.Close();
#endif

            //Pick object here :)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Console.WriteLine("Selecting Object at Position: " + p.X + " " + (tex_h -p.Y));
            byte[] buffer = new byte[4];
            GL.ReadPixels(p.X, tex_h - p.Y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, buffer);

            //Convert color to id
            int ob_id = (buffer[1] << 8) | buffer[0];

            //Deselect everything first
            traverse_oblist_rs(glcontrol1.rootObject, "selected", 0);
            
            //Try to find object
            selectedOb = (GMDL.meshModel) traverse_oblist_field<int>(glcontrol1.rootObject, ob_id, "selected", 1);
            if (selectedOb !=null) loadSelectedObect(); //Update the Form

            //Restore Rendering Buffer
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);

            //Cleanup
            GL.DeleteFramebuffer(fb);
            GL.DeleteRenderbuffer(color_rb);
            GL.DeleteRenderbuffer(depth_rb);
            pixels = null;
    
        }

        private void loadSelectedObect()
        {
            //Set World Position
            xyzControl1.bind_model(selectedOb);
            //Set Local Position
            xyzControl2.bind_model(selectedOb);
            //Set Material Name
            switch (selectedOb.type)
            {
                case TYPES.MESH:
                    selMatName.Text = selectedOb.material.name;
                    break;
                default:
                    selMatName.Text = "NaN";
                    break;
            }

            //Report
            Console.WriteLine(selectedOb.localRotation);
            Console.WriteLine(selectedOb.localPosition);
            Console.WriteLine(selectedOb.localScale);
            glcontrol1.Invalidate();

            
        }

        //Set Camera FOV
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            
            activeCam.setFOV((int)Math.Max(1, numericUpDown1.Value));
            glcontrol1.Invalidate();
        }

        //Znear
        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            activeCam.zNear = (float)this.numericUpDown4.Value;
            glcontrol1.Invalidate();
        }

        //Zfar
        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            activeCam.zFar = (float)this.numericUpDown5.Value;
            glcontrol1.Invalidate();
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            //Console.WriteLine("{0} {1}", e.Node.Checked, e.Node.Index);
            //Toggle Renderability of node
            traverse_oblist_rs(((MyTreeNode)e.Node).model, "renderable", e.Node.Checked);
            //Handle Children in treeview
            if (this.tvchkstat == treeviewCheckStatus.Children)
            {
                foreach (TreeNode node in e.Node.Nodes)
                    node.Checked = e.Node.Checked;
            }

            glcontrol1.Invalidate();
        }

        private bool setObjectField<T>(string field, GMDL.model ob, T value)
        {
            Type t = ob.GetType();

            FieldInfo[] fields = t.GetFields();
            foreach (FieldInfo f in fields)
            {
                if (f.Name == field)
                {
                    f.SetValue(ob, value);
                    return true;
                }
            }

            return false;
        }

        //Light Distance
        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            Util.activeControl.light_distance = (float)numericUpDown2.Value;
            Util.activeControl.updateLightPosition(0);
            Util.activeControl.Invalidate();
        }

        private void traverse_oblist_altid(ref List<string> alt, TreeNode parent)
        {
            //Detect Checked Descriptor

            if (parent.Checked)
            {
                if (parent.Text.StartsWith("_"))
                    if (!alt.Contains(parent.Text)) alt.Add(parent.Text);

                foreach (TreeNode child in parent.Nodes)
                    traverse_oblist_altid(ref alt, child);
            }
        }

        private void traverse_oblist(GMDL.model ob, TreeNode parent)
        {
            ob.ID = this.childCounter;
            this.childCounter++;


            //At this point LoadObjects should have properly parsed the skeleton if any
            //I can init the joint matrix array 

            if (ob.children.Count > 0)
            {
                foreach (GMDL.model child in ob.children)
                {
                    //Set object index
                    //Check if child is a scene
                    MyTreeNode node = new MyTreeNode(child.name);
                    node.model = child; //Reference model

                    //Console.WriteLine("Testing Geom {0}  Node {1}", child.Index, node.Index);
                    node.Checked = true;
                    parent.Nodes.Add(node);
                    traverse_oblist(child, node);
                }
            }
        }

        private void traverse_oblist_rs<T>(GMDL.model root, string field, T status)
        {
            setObjectField<T>(field, (GMDL.model)root, status);
            foreach (GMDL.model child in root.children)
                traverse_oblist_rs(child, field, status);
        }

        private GMDL.model traverse_oblist_field<T>(GMDL.model root, int id, string field, T value)
        {
            if (root.ID == id)
            {
                Console.WriteLine("Object Found: " + root.name + " ID: " + root.ID);
                setObjectField<T>(field, root, value);
                return root;
            }
            
            foreach (GMDL.model child in root.children)
            {
                GMDL.model m;
                m =  traverse_oblist_field(child, id, field, value);
                if (m != null) return m;
            }

            return null;
        }

        private void traverse_render(GMDL.model root, int program)
        {

            int active_program = root.shader_programs[program];

            GL.UseProgram(active_program);

            if (active_program == -1)
                throw new ApplicationException("Shit program");

            int loc;


            //Global stuff
            //loc = GL.GetUniformLocation(active_program, "worldMat");
            Matrix4 wMat = root.worldMat;
            GL.UniformMatrix4(10, false, ref wMat); //WORLD MATRIX

            //Send mvp to all shaders
            GL.UniformMatrix4(7, false, ref mvp); //MVP


            switch (program)
            {
                //Basic Pass
                case 0:
                    break;
                //Picking PAss
                case 2:
                    //Send object id
                    loc = GL.GetUniformLocation(active_program, "id");
                    GL.Uniform1(loc, root.ID);
                    break;
                default:
                    break;

            }

            //Object specific instructions

            switch (root.type)
            {
                case (TYPES.MESH):
                    //Sent rotation matrix individually for light calculations
                    //loc = GL.GetUniformLocation(active_program, "rotMat");
                    GL.UniformMatrix4(9, false, ref rotMat); //ROTATION MATRIX

                    //Normal Matrix
                    //loc = GL.GetUniformLocation(active_program, "nMat");
                    Matrix4 nMat = Matrix4.Invert(root.worldMat * rotMat);
                    GL.UniformMatrix4(8, false, ref nMat); //NORMAL MATRIX

                    //Send DiffuseFlag
                    loc = GL.GetUniformLocation(active_program, "diffuseFlag");
                    GL.Uniform1(loc, RenderOptions.UseTextures);

                    //Object program
                    //Local Transformation is the same for all objects 
                    //Pending - Personalize local matrix on each object
                    loc = GL.GetUniformLocation(active_program, "scale");
                    GL.Uniform1(loc, this.scale);

                    loc = GL.GetUniformLocation(active_program, "light");
                    GL.Uniform3(loc, Util.activeResMgmt.GLlights[0].localPosition);

                    //Upload Light Intensity
                    loc = GL.GetUniformLocation(active_program, "intensity");
                    GL.Uniform1(loc, Util.activeControl.light_intensity);

                    //Upload camera position as the light
                    //GL.Uniform3(loc, cam.Position);

                    //Apply frustum culling only for mesh objects
                    //root.render(program);
                    if (activeCam.frustum_occlude(root, rotMat)) root.render(program);
                    else occludedNum++;


                    root.render(program);
                    break;

                case (TYPES.LOCATOR):
                case (TYPES.SCENE):
                case (TYPES.JOINT):
                case (TYPES.LIGHT):
                case (TYPES.COLLISION):
                    root.render(program);
                    break;
                case (TYPES.DECAL):
                    //root.render(program);
                    break;
                default:
                    break;
            }

            

            //Render children
            foreach (GMDL.model child in root.children)
                traverse_render(child, program);

        }

        private TreeNode findNodeFromText(TreeNodeCollection coll, string text)
        {
            foreach (TreeNode node in coll)
            {
                //Console.WriteLine(node.Text + " " + text + " {0}", node.Text == text);
                if (node.Text == text)
                {
                    return node;
                } else
                {
                    TreeNode retnode =  findNodeFromText(node.Nodes, text);
                    if (retnode != null)
                        return retnode;
                    else
                        continue;
                }
            }

            return null;
        }
        
        private GMDL.model collectPart(List<GMDL.model> coll, string name)
        {
            foreach (GMDL.model child in coll)
            {
                if (child.name == name)
                {
                    return child;
                }
                else
                {
                    
                    GMDL.model ret = collectPart(child.children, name);
                    if (ret != null)
                        return ret;
                    else
                        continue;
                } 
            }
            return null;
        }


        private void procWinCLose(object sender, EventArgs e)
        {
            Console.WriteLine("procwin closing");
            //This function applies specifically to the structure of the procgen window
            //Get glcontrol table
            Form vpwin = (Form)sender;
            TableLayoutPanel table = (TableLayoutPanel) vpwin.Controls[0];

            foreach (CGLControl c in table.Controls)
            {
                c.t.Stop();
                c.unsubscribePaint();
                c.rootObject.Dispose(); //Prevent rendering
                c.rootObject = null;
                //Cleanup control resources
                Console.WriteLine("Cleaning Up control Resources");
                c.resMgmt.Cleanup();
            }

            glcontrol1.MakeCurrent();

        }

        private void randgenClickNew(object sender, EventArgs e)
        {
            Util.setStatus("Procedural Generation Init", this.toolStripStatusLabel1);
            GC.Collect();
            //Check if any file has been loaded exists at all
            if (mainFilePath == null)
            {
                MessageBox.Show("No File Loaded", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Util.setStatus("Error on ProcGen", this.toolStripStatusLabel1);
                return;
            }

            //Construct Descriptor Path
            string[] split = mainFilePath.Split('.');
            string descrpath = "";
            for (int i = 0; i< split.Length-2; i++)
                descrpath = Path.Combine(descrpath, split[i]);
            descrpath += ".DESCRIPTOR.MBIN";

            string exmlPath = Util.getExmlPath(descrpath);
            Console.WriteLine("Opening " + descrpath);

            //Check if Descriptor exists at all
            if (!File.Exists(descrpath))
            {
                MessageBox.Show("Not a ProcGen Model","Error",MessageBoxButtons.OK, MessageBoxIcon.Error);
                Util.setStatus("Error on ProcGen", this.toolStripStatusLabel1);
                return;
            }


            //Convert only if file does not exist
            if (!File.Exists(exmlPath))
            {
                Console.WriteLine("Exml does not exist, Converting...");
                //Convert Descriptor MBIN to exml
                Util.MbinToExml(descrpath, exmlPath);
            }
            
            //Parse exml now
            XmlDocument descrXml = new XmlDocument();
            descrXml.Load(exmlPath);
            XmlElement root = (XmlElement) descrXml.ChildNodes[2].ChildNodes[0];

            //List<GMDL.model> allparts = new List<GMDL.model>();

            //Revise Procgen Creation

            //First Create the form and the table
            Form vpwin = new Form();
            //vpwin.parentForm = this; //Set parent to this form
            vpwin.FormClosed += new FormClosedEventHandler(this.procWinCLose);
            vpwin.Text = "Procedural Generated Models";
            vpwin.FormBorderStyle = FormBorderStyle.Sizable;
            
            TableLayoutPanel table = new TableLayoutPanel();
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.Dock = DockStyle.Fill;
            //Calculate TableRowCount and ColumnCount from the procGenNumSetting
            int rowspan = 5;
            //table.Anchor= AnchorStyles.Bottom
            //table.Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Left;
            //Set form minmaxsizes after the row,columncount are decided
            // no smaller than design time size

            //Try to factorize
            int fact = 1;
            for (int i = (rowspan-1)/2 + 1; i > 0; i--)
                if (Util.procGenNum % i == 0) fact = Math.Max(fact,i);
            
            if (fact == 1)
            {
                //Factorization failed
                int rc = 1, cc = 2;
                bool flag = true;
                while (true)
                {
                    if (rc * cc >= Util.procGenNum) break;
                    else
                    {
                        if (flag) rc += 1;
                        else cc += 1;
                        flag = !flag;
                    }
                }
                table.RowCount = rc;
                table.ColumnCount = cc;
            }
            else
            {
                //Use Factorization values
                table.ColumnCount = Math.Max(fact, Util.procGenNum / fact);
                table.RowCount = Util.procGenNum / table.ColumnCount;
            }
            

            vpwin.MinimumSize = new System.Drawing.Size(table.ColumnCount * 300, table.RowCount * 256);
            // no larger than screen size
            vpwin.MaximumSize = new System.Drawing.Size(1920, 1080);


            //Fix RowStyles
            for (int i = 0; i < table.RowCount; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / table.RowCount));

            //Fix ColumnStyles
            for (int i = 0; i < table.ColumnCount; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / table.ColumnCount));



            for (int i = 0; i < Util.procGenNum; i++)
            {
                //Create New GLControl
                CGLControl n = new CGLControl(i, vpwin);
                //n.MakeCurrent(); //Make current

                //Prepare control Resource Object
                n.resMgmt.shader_programs = this.glcontrol1.resMgmt.shader_programs; //Copy the same shader programs
                //n.resMgmt.GLgeoms = this.resMgmt.GLgeoms;
                //readd textures
                n.setupControlParameters();
                
                //----PROC GENERATION START----
                List<string> parts = new List<string>();
                ModelProcGen.parse_descriptor(ref parts, root);

                Console.WriteLine(String.Join(" ", parts.ToArray()));
                GMDL.model m;
                m = ModelProcGen.get_procgen_parts(ref parts, glcontrol1.rootObject);
                //----PROC GENERATION END----

                n.rootObject = m;
                n.SetupItems();
                table.Controls.Add(n, i%rowspan, i/rowspan);
                n.Invalidate();
            }
            //for (int i = 0; i < table.RowCount; i++)
            //{
            //    for (int j = 0; j < table.ColumnCount; j++)
            //    {
            //        //Create New GLControl
            //        CGLControl n = new CGLControl(i * table.ColumnCount + j, vpwin);
            //        n.MakeCurrent(); //Make current

            //        //----PROC GENERATION START----
            //        List<string> parts = new List<string>();
            //        ModelProcGen.parse_descriptor(ref parts, root);

            //        Console.WriteLine(String.Join(" ", parts.ToArray()));
            //        GMDL.model m;
            //        m = ModelProcGen.get_procgen_parts(ref parts, this.mainScene);
            //        //----PROC GENERATION END----

            //        n.rootObject = m;
            //        n.shader_programs = ResourceMgmt.shader_programs;

            //        n.SetupItems();
            //        table.Controls.Add(n, j, i);
            //        n.Invalidate();
            //    }
            //}

            vpwin.Controls.Add(table);
            Util.setStatus("Ready", this.toolStripStatusLabel1);
            GC.Collect();
            vpwin.Show();
            
        }
        
        //Animation file Open
        private void openAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Opening Animation File
            Console.WriteLine("Opening File");

            AnimationSelectForm aform = new AnimationSelectForm(this);
            aform.Show();


            return;
        }

        //Animation Playback
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            while (true)
            {
                double pause = (1000.0d/(double) RenderOptions.animFPS);
                System.Threading.Thread.Sleep((int) (Math.Round(pause, 1)));
                backgroundWorker1.ReportProgress(0);

                if (backgroundWorker1.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
            }
        }
        
        //Play Pause Button
        private void newButton1_Click(object sender, EventArgs e)
        {
            //Animation Play/Pause Button
            if (newButton1.status)
            {
                backgroundWorker1.RunWorkerAsync();
            } else
            {
                backgroundWorker1.CancelAsync();
            }
            newButton1.status = !newButton1.status;
            newButton1.Invalidate();
            glcontrol1.Focus();
        }

        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            foreach (GMDL.model s in animScenes)
                if (typeof(GMDL.scene).IsInstanceOfType(s))
                    if (((GMDL.scene) s).animMeta != null) ((GMDL.scene) s).animate();
        }

        //MenuBar Stuff

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Settings.Show();
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Form about = new AboutDialog();
            about.Show();
        }

        //Movement Speed
        private void numericUpDown3_ValueChanged(object sender, EventArgs e)
        {
            NumericUpDown s = (NumericUpDown)sender;
            Util.activeControl.movement_speed = (int) s.Value;
        }
        
        private void getAltIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            List<string> altId = new List<string>();
            Console.WriteLine(this.treeView1.Nodes[0].Text);
            traverse_oblist_altid(ref altId, this.treeView1.Nodes[0]);
            string finalalt = "";
            for (int i = 0; i < altId.Count; i++)
                finalalt += altId[i] + " ";
            //Console.WriteLine(finalalt);
            Clipboard.SetText(finalalt);
            Util.setStatus("AltID Copied to clipboard.", toolStripStatusLabel1);
        }

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Exporting to obj");
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
            findGeoms(glcontrol1.rootObject, obj, ref index);

            obj.Close();

            Console.WriteLine("Scene successfully converted!");

        }

        private void findGeoms(GMDL.model m, StreamWriter s, ref uint index)
        {
            if (m.type == TYPES.MESH || m.type ==TYPES.COLLISION)
            {
                //Get converted text
                GMDL.meshModel me = (GMDL.meshModel) m;
                me.writeGeomToStream(s, ref index);
            
            }
            foreach (GMDL.model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (glcontrol1.rootObject != null)
                glcontrol1.rootObject.Dispose();
            this.glcontrol1.resMgmt.Cleanup();
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            Console.WriteLine("Clicked");
            selectedOb = ((MyTreeNode)e.Node).model;
            loadSelectedObect();
            //Deselect everything first
            traverse_oblist_rs(glcontrol1.rootObject, "selected", 0);
            
            //Try to find object
            selectedOb = traverse_oblist_field<int>(glcontrol1.rootObject, selectedOb.ID, "selected", 1);


        }

        private void getObjectTexturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //This function tries to save to disk the selected objects textures - if any
            glcontrol1.gbuf.dump();


            if (selectedOb == null) return;
            if (selectedOb.material == null) return;

            List<GMDL.Texture> texlist = new List<GMDL.Texture>();

            texlist.Add(selectedOb.material.fDiffuseMap);
            texlist.Add(selectedOb.material.fMaskMap);
            texlist.Add(selectedOb.material.fNormalMap);
            
            FileStream fs;
            BinaryWriter bw;

            for (int i = 0; i < 3; i++)
            {
                GMDL.Texture tex = texlist[i];
                if (tex == null) continue;

                //Save Texture
                int texsize = 1024;
                byte[] pixels = new byte[4 * texsize * texsize];
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);
                GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

                //Save to disk
                fs = new FileStream("dump." + i, FileMode.Create, FileAccess.Write);
                bw = new BinaryWriter(fs);
                bw.Write(pixels);
                bw.Close();
                fs.Close();

            }


        }


        private void loadNMSEnums()
        {
            //Load Palette Names
            var tx = new libMBIN.Models.Structs.TkPaletteTexture();
            var paletteNames = tx.PaletteValues();
            var colourAltNames = tx.ColourAltValues();

            for (int i = 0; i < paletteNames.Length; i++)
            {
                Palettes.palette_IDToName[i] = paletteNames[i];
                Palettes.palette_NameToID[paletteNames[i]] = i;
            }

            for (int i = 0; i < colourAltNames.Length; i++)
            {
                Palettes.colourAlt_IDToName[i] = colourAltNames[i];
                Palettes.colourAlt_NameToID[colourAltNames[i]] = i;
            }

        }

        //Light Intensity
        private void l_intensity_nud_ValueChanged(object sender, EventArgs e)
        {
            Util.activeControl.light_intensity = (float)this.l_intensity_nud.Value;
            Util.activeControl.updateLightPosition(0);
            Util.activeControl.Invalidate();
        }

    }

    public class GBuffer
    {
        public int fbo = -1;
        //Dump fbo stuff
        public int dump_fbo;
        public int dump_diff;
        public int dump_pos;
        public int dump_depth;

        public int diff_rbo;
        public int depth_rbo;
        public int diffuse = -1;
        public int positions = -1;
        public int normals = -1;
        public int depth = -1;
        public int quad_vbo, quad_ebo;
        public int program = -1;
        public int[] size;
        private int msaa_samples = 4;
        
        public GBuffer()
        {
            //Create Quad Geometry
            program = Util.activeResMgmt.shader_programs[9];
            
            //Define Quad
            float[] quad = new float[6 * 3] {
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f };

            //Indices
            int[] indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

            int arraysize = sizeof(float) * 6 * 3;

            //Generate OpenGL buffers
            GL.GenBuffers(1, out quad_vbo);
            GL.GenBuffers(1, out quad_ebo);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, quad);

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * 6), indices, BufferUsageHint.StaticDraw);


            //Setup all stuff
            //Init size to the current GLcontrol size
            size = new int[] { Util.activeControl.Width, Util.activeControl.Height };

            setup();

        }

        public void setup()
        {
            
            //Init the FBO
            fbo = GL.Ext.GenFramebuffer();

            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            int[] rbufs = new int[2];
            GL.Ext.GenRenderbuffers(2, rbufs);
            depth_rbo = rbufs[1];
            diff_rbo = rbufs[0];

            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //Bind color renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, diff_rbo);
            //Normal Version
            //GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.Rgba8, size[0], size[1]);
            //Multisampling version
            GL.Ext.RenderbufferStorageMultisample(RenderbufferTarget.RenderbufferExt, msaa_samples, RenderbufferStorage.Rgb8, size[0], size[1]);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, RenderbufferTarget.RenderbufferExt, diff_rbo);
            
            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());


            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //Bind depth renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, depth_rbo);
            //Normal Version
            //GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.DepthComponent, size[0], size[1]);
            //Multisampling version
            GL.Ext.RenderbufferStorageMultisample(RenderbufferTarget.RenderbufferExt, msaa_samples, RenderbufferStorage.DepthComponent, size[0], size[1]);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, RenderbufferTarget.RenderbufferExt, depth_rbo);

            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Setup diffuse texture
            setup_texture(ref diffuse, 0);
            //Setup positions texture
            setup_texture(ref positions, 1);
            //Setup normals texture
            setup_texture(ref normals, 2);
            //Setup Depth texture
            setup_texture(ref depth, 10);


            //Setup dump_fbo
            setup_dump();

            //Revert Back the fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }

        public void setup_dump()
        {
            if (dump_fbo != 0)
            {
                GL.DeleteFramebuffer(dump_fbo);
                GL.DeleteTexture(dump_diff);
                GL.DeleteTexture(dump_pos);
                GL.DeleteTexture(dump_depth);
            }
                
            //Create Intermediate Framebuffer
            dump_fbo = GL.Ext.GenFramebuffer();
            dump_diff = GL.GenTexture();
            dump_pos = GL.GenTexture();
            dump_depth = GL.GenTexture();

            //Setup Textures
            GL.BindTexture(TextureTarget.Texture2D, dump_diff);
            
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, size[0], size[1], 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            GL.BindTexture(TextureTarget.Texture2D, dump_pos);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.BindTexture(TextureTarget.Texture2D, dump_depth);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, dump_fbo);

            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());
            GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, TextureTarget.Texture2D, dump_diff, 0);
            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());
            GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment1Ext, TextureTarget.Texture2D, dump_pos, 0);
            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());
            GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, TextureTarget.Texture2D, dump_depth, 0);
            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());

            
        }

        public void setup_texture(ref int handle, int attachment)
        {

            if (handle != -1) GL.DeleteTexture(handle);
            handle = GL.GenTexture();

            GL.BindTexture(TextureTarget.Texture2DMultisample, handle);
            
            //Bind to class fbo
            FramebufferAttachment t;
            switch (attachment)
            {
                //Depth Case
                case 10:
                    t = FramebufferAttachment.DepthAttachmentExt;
                    //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                    GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, PixelInternalFormat.DepthComponent, size[0], size[1], true);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                    GL.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
                    GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, t, TextureTarget.Texture2DMultisample, handle, 0);
                    //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

                    break;
                //ColorAttachment1 Positions
                case 1:
                    t = FramebufferAttachment.ColorAttachment0Ext + attachment;
                    //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba32f, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
                    GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, PixelInternalFormat.Rgba32f, size[0], size[1], true);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                    GL.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
                    GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, t, TextureTarget.Texture2DMultisample, handle, 0);
                    //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

                    break;
                default:
                    t = FramebufferAttachment.ColorAttachment0Ext + attachment;
                    //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, size[0], size[1], 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                    GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, PixelInternalFormat.Rgba8, size[0], size[1], true);

                    GL.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
                    GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, t, TextureTarget.Texture2DMultisample, handle, 0);
                    
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                    //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                    //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());
                    break;
            }

           
        }

        public void render()
        {
            GL.UseProgram(program);
            //Vertex attribute
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
            
            //vPosition #0
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);

            //Upload mvp matrix
            int loc = GL.GetUniformLocation(program, "mvp");
            GL.UniformMatrix4(loc, false, ref Util.mvp);

            //Upload the GBuffer textures
            int tex0_Id = (int)TextureUnit.Texture0;
            loc = GL.GetUniformLocation(program, "diffuseTex");
            GL.Uniform1(loc, tex0_Id);

            //loc = GL.GetUniformLocation(program, "depthTex");
            //GL.Uniform1(loc, tex0_Id + 1);

            //loc = GL.GetUniformLocation(program, "diffuseTex");
            //GL.Uniform1(loc, tex0_Id + 2);

            
            GL.ActiveTexture((TextureUnit) tex0_Id);
            GL.BindTexture(TextureTarget.Texture2D, diffuse);

            ////Positions Texture
            //GL.ActiveTexture((TextureUnit) (tex0_Id + 1));
            //GL.BindTexture(TextureTarget.Texture2D, depth);

            ////Depth Texture
            //GL.ActiveTexture((TextureUnit) (tex0_Id + 2));
            //GL.BindTexture(TextureTarget.Texture2D, diffuse);
            

            //GL.BindTexture(TextureTarget.Texture2D, depth);
            //GL.ActiveTexture((TextureUnit) tex0_Id + 1);

            //Render quad
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            
            
            GL.DisableVertexAttribArray(0);

            
        }

        public void start()
        {
            //Draw Scene
            
            //Bind Gbuffer fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            GL.Enable(EnableCap.Multisample); //not making any difference probably needs to be removed
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.DepthTest);
            GL.PatchParameter(PatchParameterFloat.PatchDefaultInnerLevel, new float[] { 2.0f });
            GL.PatchParameter(PatchParameterFloat.PatchDefaultOuterLevel, new float[] { 4.0f, 4.0f, 4.0f });
            GL.PatchParameter(PatchParameterInt.PatchVertices, 3);

            GL.Viewport(0, 0, size[0], size[1]);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //GL.ClearTexImage(positions, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            //GL.ClearTexImage(depth, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            //GL.ClearTexImage(diffuse, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            
            GL.DrawBuffers(2, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 });
        }

        public void stop()
        {
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            GL.Enable(EnableCap.Texture2D);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        }

        public void blit()
        {
            //Blit can replace the render & stop funtions
            //Simply resolves and copies the ms offscreen fbo to the default framebuffer without any need to render the textures and to any other post proc effects
            //I guess that I don't need the textures as well, when I'm rendering like this
            GL.Ext.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.Ext.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            GL.Ext.BlitFramebuffer(0, 0, size[0], size[1], 
                                   0, 0, size[0], size[1], 
                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, 
                                   BlitFramebufferFilter.Nearest);
        }

        public void dump_blit()
        {
            //Setup View
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, dump_fbo);
            GL.Viewport(0, 0, size[0], size[1]);
            GL.DrawBuffers(2, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1 });
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            //Resolving Buffers
            GL.Ext.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.Ext.BindFramebuffer(FramebufferTarget.DrawFramebuffer, dump_fbo);
            GL.Ext.BlitFramebuffer(0, 0, size[0], size[1],
                                   0, 0, size[0], size[1], 
                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit, 
                                   BlitFramebufferFilter.Nearest);

            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());
        }


        public void dump()
        {
            //Bind Buffers
            //Resolving Buffers
            GL.Ext.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.Ext.BindFramebuffer(FramebufferTarget.DrawFramebuffer, dump_fbo);
            
            //FileStream fs;
            //BinaryWriter bw;
            //byte[] pixels;
            //pixels = new byte[4 * size[0] * size[1]];
            //Console.WriteLine("Dumping Framebuffer textures " + size[0] + " " + size[1]);

            //Read Color1
            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment1);
            GL.Ext.BlitFramebuffer(0, 0, size[0], size[1],
                                   0, 0, size[0], size[1],
                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
                                   BlitFramebufferFilter.Nearest);

#if TEST
            //Save Positions
            pixels = new byte[16 * size[0] * size[1]];
            GL.BindTexture(TextureTarget.Texture2D, dump_pos);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.Float, pixels);

            //Save to disk
            fs = new FileStream("dump.color1", FileMode.Create, FileAccess.Write);
            bw = new BinaryWriter(fs);
            bw.Write(pixels);
            bw.Close();
            fs.Close();

            //Save Depth Texture
            pixels = new byte[4 * size[0] * size[1]];
            GL.BindTexture(TextureTarget.Texture2D, dump_depth);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, pixels);

            //Save to disk
            fs = new FileStream("dump.depth", FileMode.Create, FileAccess.Write);
            bw = new BinaryWriter(fs);
            bw.Write(pixels);
            bw.Close();
            fs.Close();

#endif

            //Read Color0
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.Ext.BlitFramebuffer(0, 0, size[0], size[1],
                                   0, 0, size[0], size[1],
                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
                                   BlitFramebufferFilter.Nearest);

#if TEST
            //Save Diffuse Color
            GL.BindTexture(TextureTarget.Texture2D, dump_diff);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);


            //Save to disk
            fs = new FileStream("dump.color0", FileMode.Create, FileAccess.Write);
            bw = new BinaryWriter(fs);
            bw.Write(pixels);
            bw.Close();
            fs.Close();
#endif


            //Rebind Gbuffer fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            //GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

        }

        public void Cleanup()
        {
            
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            //Delete bound renderbuffers
            GL.Ext.DeleteRenderbuffer(depth_rbo);
            GL.Ext.DeleteRenderbuffer(diff_rbo);
            GL.Ext.DeleteFramebuffer(fbo);

            //Delete textures
            GL.DeleteTexture(diffuse);
            GL.DeleteTexture(positions);
            GL.DeleteTexture(normals);
            GL.DeleteTexture(depth);

        }

        public void resize(int w, int h)
        {
            size = new int[] { w, h};

            Cleanup();
            setup();
        }

        
    }


    //Class Which will store all the texture resources for better memory management
    public class ResourceMgmt
    {
        public Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        public Dictionary<string, GMDL.Material> GLmaterials = new Dictionary<string, GMDL.Material>();
        public Dictionary<string, GMDL.GeomObject> GLgeoms = new Dictionary<string, GMDL.GeomObject>();
        public List<GMDL.model> GLlights = new List<GMDL.model>();
        public List<GMDL.model> GLDecals = new List<GMDL.model>();
        public List<Camera> GLCameras = new List<Camera>();
        public int[] shader_programs;
        public DebugForm DebugWin;
        
        public void Cleanup()
        {
            foreach (GMDL.Texture p in GLtextures.Values)
                p.Dispose();
            GLtextures.Clear();

            foreach (GMDL.GeomObject p in GLgeoms.Values)
                p.Dispose();
            GLgeoms.Clear();

            //Cleanup Decals
            foreach (GMDL.model p in GLDecals)
                p.Dispose();
            GLDecals.Clear();

            foreach (GMDL.Material p in GLmaterials.Values)
                p.Dispose();
            GLmaterials.Clear();

            GC.Collect();
            
        }

    }
}
