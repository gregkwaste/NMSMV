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
using MVCore;
using MVCore.GMDL;
using System.Linq;
using GLSLHelper;
using gImage;
using OpenTK.Graphics;
using ClearBufferMask = OpenTK.Graphics.OpenGL.ClearBufferMask;
using DrawBufferMode = OpenTK.Graphics.OpenGL.DrawBufferMode;
using EnableCap = OpenTK.Graphics.OpenGL.EnableCap;
using FramebufferAttachment = OpenTK.Graphics.OpenGL.FramebufferAttachment;
using FramebufferErrorCode = OpenTK.Graphics.OpenGL.FramebufferErrorCode;
using FramebufferTarget = OpenTK.Graphics.OpenGL.FramebufferTarget;
using GetPName = OpenTK.Graphics.OpenGL.GetPName;
using GL = OpenTK.Graphics.OpenGL.GL;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;
using PixelInternalFormat = OpenTK.Graphics.OpenGL.PixelInternalFormat;
using PixelType = OpenTK.Graphics.OpenGL.PixelType;
using ReadBufferMode = OpenTK.Graphics.OpenGL.ReadBufferMode;
using RenderbufferStorage = OpenTK.Graphics.OpenGL.RenderbufferStorage;
using RenderbufferTarget = OpenTK.Graphics.OpenGL.RenderbufferTarget;
//Aliases for Renderstate
using RenderState = MVCore.Common.RenderState;
using StringName = OpenTK.Graphics.OpenGL.StringName;
using TextureMagFilter = OpenTK.Graphics.OpenGL.TextureMagFilter;
using TextureMinFilter = OpenTK.Graphics.OpenGL.TextureMinFilter;
using TextureParameterName = OpenTK.Graphics.OpenGL.TextureParameterName;
using TextureTarget = OpenTK.Graphics.OpenGL.TextureTarget;

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
        
        public int childCounter = 0;

        //Shader objects
        int vertex_shader_ob;
        int fragment_shader_ob;

        //Deprecated
        //private List<model> vboobjects = new List<model>();
        //private model rootObject;
        private XmlDocument xmlDoc = new XmlDocument();
        private Dictionary<string, int> index_dict = new Dictionary<string, int>();
        private OrderedDictionary joint_dict = new OrderedDictionary();
        private treeviewCheckStatus tvchkstat = treeviewCheckStatus.Children;

        //Animation Meta
        public AnimeMetaData meta = new AnimeMetaData();

        //Joint Array for shader
        //TEST public float[] JMArray = new float[256 * 16];
        
        //public float[] JColors = new float[256 * 3];

        //Selected Object
        public model selectedOb;

        //Path
        private string mainFilePath;
        //Private Settings Window
        //private SettingsForm Settings = new SettingsForm();
        
        //Custom defined GLControl
        private CGLControl glcontrol1;


        public Form1()
        {
            //Custom stuff
            //this.xyzControl1 = new XYZControl("worldPosition");
            //this.xyzControl2 = new XYZControl("localPosition");
            
            InitializeComponent();

            //this.rightFlowPanel.Controls.Add(xyzControl2);
            //this.rightFlowPanel.Controls.Add(xyzControl1);

            //
            // xyzControl2
            //
            //this.xyzControl2.Name = "xyzControl2";
            //this.xyzControl2.Size = new System.Drawing.Size(112, 119);
            //this.xyzControl2.TabIndex = 4;
            //this.xyzControl2.TabStop = false;
            //this.xyzControl2.Text = "LocalPosition";

            //
            // xyzControl1
            //
            //this.xyzControl1.Name = "xyzControl1";
            //this.xyzControl1.Size = new System.Drawing.Size(112, 119);
            //this.xyzControl1.TabIndex = 3;
            //this.xyzControl1.TabStop = false;
            //this.xyzControl1.Text = "WorldPosition";


        }


        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Console.WriteLine("Opening File");
            openFileDialog1.Filter = "SCENE Files (*.SCENE.MBIN)|*.SCENE.MBIN;";
            DialogResult res = openFileDialog1.ShowDialog();
            if (res == DialogResult.Cancel)
                return;

            var filename = openFileDialog1.FileName;
            openSceneMbin(filename);
        }

        private void openSceneMbin(string filename)
        {
            mainFilePath = filename;
            
            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();

            glcontrol1.t.Stop();
            glcontrol1.rootObject.Dispose(); //Prevent rendering
            glcontrol1.rootObject = null;
            glcontrol1.t.Start();
            RightSplitter.Panel1.Controls.Clear();

            //Clear Form Resources
            glcontrol1.resMgr.Cleanup();
            ModelProcGen.procDecisions.Clear();

            //Reset the activeControl and create new gbuffer
            Util.activeControl = glcontrol1;

            //Reload Default Resources
            glcontrol1.setupControlParameters();

            RightSplitter.Panel1.Controls.Add(glcontrol1);

            glcontrol1.Update();
            glcontrol1.MakeCurrent();
            scene scene = GEOMMBIN.LoadObjects(filename);
            scene.ID = this.childCounter;
            glcontrol1.rootObject = scene;
            this.childCounter++;

            Util.setStatus("Creating Nodes...");

            //Console.WriteLine("Objects Returned: {0}",oblist.Count);
            MyTreeNode node = new MyTreeNode(scene.name)
            {
                Checked = true,
                model = scene
            };

            //Clear index dictionary
            index_dict.Clear();
            joint_dict.Clear();
            glcontrol1.animScenes.Clear();

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

            glcontrol1.Update();
            glcontrol1.Invalidate();

            //Cleanup resource manager
            Util.setStatus("Ready");
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

            //Setup Form Title
            this.Text = "No Man's Model Viewer " + Util.Version;
            
            //setupGLControl();
            
            //Load NMSTemplate Enumerators
            Palettes.loadNMSEnums();

            return;

            //Set active components
            RenderState.activeResMgr = glcontrol1.resMgr;
            Util.activeControl = glcontrol1;

            //Compile Shaders
            compileShaders();
            
            scene scene = new scene();
            scene.type = TYPES.SCENE;
            scene.name = "DEFAULT SCENE";
            scene.shader_programs = new int[] { RenderState.activeResMgr.shader_programs[1],
                                                RenderState.activeResMgr.shader_programs[5],
                                                RenderState.activeResMgr.shader_programs[6]};


            if (!this.glloaded)
                return;

            
            scene.ID = this.childCounter;

            /*
            //Add Frustum cube
            Collision cube = new Collision();
            //cube.vbo = (new Capsule(new Vector3(), 14.0f, 2.0f)).getVBO();
            cube.main_Vao = (new MVCore.Primitives.Box(1.0f, 1.0f, 1.0f)).getVAO();

            //Remove that after implemented all the different collision types
            cube.shader_programs = new int[] { RenderState.activeResMgr.shader_programs[0],
                                               RenderState.activeResMgr.shader_programs[5],
                                               RenderState.activeResMgr.shader_programs[6]}; //Use Mesh program for collisions
            cube.name = "FRUSTUM";
            cube.collisionType = (int) COLLISIONTYPES.BOX;
            scene.children.Add(cube);
            */


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
                var gamepad_name = OpenTK.Input.GamePad.GetName(i);
                Console.WriteLine(caps + " " + gamepad_name);
            }
            
            //Setup GamePad Handler for the control
            glcontrol1.gpHandler = new GamepadHandler(Util.gamepadID);

            //Load Settings
            //Settings.loadSettings();

            //Setup GL Control once shaders have been successfully compiled
            //Setup Lights, Cameras, Default Textures, Gbuffer
            glcontrol1.setupControlParameters();
            //Apply custom positioning attributes
            glcontrol1.Dock = DockStyle.Fill;

            //Set global gbuffer
            RenderState.gbuf = glcontrol1.gbuf;

            //Set active StatusStrup
            Util.activeStatusStrip = this.toolStripStatusLabel1;
            
            int maxfloats;
            GL.GetInteger(GetPName.MaxVertexUniformVectors, out maxfloats);
            Util.setStatus("Ready");
            
            //Load font
            glcontrol1.font = setupFont();

            //Add some text for rendering
            glcontrol1.texObs[(int) GLTEXT_INDEX.MSG1] = glcontrol1.font.renderText("Greetings", new Vector2(0.02f, 0.0f), scale);
            glcontrol1.texObs[(int) GLTEXT_INDEX.MSG2] = glcontrol1.font.renderText(occludedNum.ToString(), new Vector2(1.0f, 0.0f), 1.0f);

            //Add 2 Cams
            Camera cam;
            cam = new Camera(50, RenderState.activeResMgr.shader_programs[8], 0, true);
            RenderState.activeResMgr.GLCameras.Add(cam);
            //cam = new Camera(50, ResourceMgmt.shader_programs[8], 0, false);
            //ResourceMgmt.GLCameras.Add(cam);
            activeCam = RenderState.activeResMgr.GLCameras[0];
            activeCam.isActive = true;

                
            //Check if Temp folder exists
            //if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");


            //FILL THE CALLBACKS OF MVCORE
            MVCore.Common.CallBacks.updateStatus = Util.setStatus;

            //Testing
            AnimationTest();
        
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
            font.program = RenderState.activeResMgr.shader_programs[4];

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
            RenderState.activeResMgr.shader_programs = new int[11];
            string vvs, ggs, ffs;
            string log="";
            //Geometry Shader
            //Compile Object Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VSEmpty.glsl");
            ggs = GLSL_Preprocessor.Parser("Shaders/Simple_GS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FSEmpty.glsl");

            GLShaderHelper.CreateShaders(vvs, ffs, ggs, "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[5], ref log);

            //Picking Shaders
            GLShaderHelper.CreateShaders(Resources.pick_vert, Resources.pick_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[6], ref log);

            //Main Shader
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[0], ref log);

            //Texture Mixing Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/pass_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/pass_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[3], ref log);

            //GBuffer Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[9], ref log);

            //Decal Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/decal_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Decal_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[10], ref log);

            //Locator Shaders
            GLShaderHelper.CreateShaders(Resources.locator_vert, Resources.locator_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[1], ref log);

            //Joint Shaders
            GLShaderHelper.CreateShaders(Resources.joint_vert, Resources.joint_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[2], ref log);

            //Text Shaders
            GLShaderHelper.CreateShaders(Resources.text_vert, Resources.text_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[4], ref log);

            //Light Shaders
            GLShaderHelper.CreateShaders(Resources.light_vert, Resources.light_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[7], ref log);

            //Camera Shaders
            GLShaderHelper.CreateShaders(Resources.camera_vert, Resources.camera_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out RenderState.activeResMgr.shader_programs[8], ref log);

            //Save log
            StreamWriter sr = new StreamWriter("shader_compilation_log_" + DateTime.Now.ToFileTime() + "_log.out");
            sr.Write(log);
            sr.Close();

        }

        private void render_decals()
        {
            //gbuf.dump();
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.DepthFunc(DepthFunction.Gequal);


            int active_program = RenderState.activeResMgr.shader_programs[10];
            GL.UseProgram(active_program);
            int loc;
            Matrix4 temp;

            glcontrol1.gbuf.dump();

            //Upload inverse decat world matrix
            //for ( int i = 0;i< Math.Min(1, this.resMgr.GLDecals.Count); i++)
            foreach (model decal in RenderState.activeResMgr.GLDecals)
            {
                //Decal decal = (Decal) this.resMgr.GLDecals[i];
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
            int active_program = RenderState.activeResMgr.shader_programs[7];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref mvp);
            
            foreach (model light in RenderState.activeResMgr.GLlights)
                light.render(0);
        }

        private void render_cameras()
        {
            int active_program = RenderState.activeResMgr.shader_programs[8];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            Matrix4 cam_mvp = activeCam.viewMat;
            GL.UniformMatrix4(loc, false, ref cam_mvp);
            //Send object world Matrix to all shaders

            foreach (Camera cam in RenderState.activeResMgr.GLCameras)
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
            selectedOb = (meshModel) traverse_oblist_field<int>(glcontrol1.rootObject, ob_id, "selected", 1);
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
            //xyzControl1.bind_model(selectedOb);
            //Set Local Position
            //xyzControl2.bind_model(selectedOb);
            
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

        private bool setObjectField<T>(string field, model ob, T value)
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

        private void traverse_oblist(model ob, TreeNode parent)
        {
            ob.ID = this.childCounter;
            this.childCounter++;


            //At this point LoadObjects should have properly parsed the skeleton if any
            //I can init the joint matrix array 

            if (ob.children.Count > 0)
            {
                foreach (model child in ob.children)
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

        private void traverse_oblist_rs<T>(model root, string field, T status)
        {
            setObjectField<T>(field, (model)root, status);
            foreach (model child in root.children)
                traverse_oblist_rs(child, field, status);
        }

        private model traverse_oblist_field<T>(model root, int id, string field, T value)
        {
            if (root.ID == id)
            {
                Console.WriteLine("Object Found: " + root.name + " ID: " + root.ID);
                setObjectField<T>(field, root, value);
                return root;
            }
            
            foreach (model child in root.children)
            {
                model m;
                m =  traverse_oblist_field(child, id, field, value);
                if (m != null) return m;
            }

            return null;
        }

        private void traverse_render(model root, int program)
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
                    GL.Uniform3(loc, RenderState.activeResMgr.GLlights[0].localPosition);

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
            foreach (model child in root.children)
                traverse_render(child, program);

        }

        /* OBSOLETE
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
        */

        //OBSOLETE
        /*
        private model collectPart(List<model> coll, string name)
        {
            foreach (model child in coll)
            {
                if (child.name == name)
                {
                    return child;
                }
                else
                {
                    
                    model ret = collectPart(child.children, name);
                    if (ret != null)
                        return ret;
                    else
                        continue;
                } 
            }
            return null;
        }
        */

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
                c.rootObject.Dispose(); //Prevent rendering
                c.rootObject = null;
                //Cleanup control resources
                Console.WriteLine("Cleaning Up control Resources");
                c.resMgr.Cleanup();
            }

            glcontrol1.MakeCurrent();

        }

        private void randgenClickNew(object sender, EventArgs e)
        {
            Util.setStatus("Procedural Generation Init");
            GC.Collect();
            //Check if any file has been loaded exists at all
            if (mainFilePath == null)
            {
                MessageBox.Show("No File Loaded", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Util.setStatus("Error on ProcGen");
                return;
            }

            //Construct Descriptor Path
            string[] split = mainFilePath.Split('.');
            string descrpath = "";
            for (int i = 0; i< split.Length-2; i++)
                descrpath = Path.Combine(descrpath, split[i]);
            descrpath += ".DESCRIPTOR.MBIN";

            string exmlPath = FileUtils.getExmlPath(descrpath);
            Console.WriteLine("Opening " + descrpath);

            //Check if Descriptor exists at all
            if (!File.Exists(descrpath))
            {
                MessageBox.Show("Not a ProcGen Model","Error",MessageBoxButtons.OK, MessageBoxIcon.Error);
                Util.setStatus("Error on ProcGen");
                return;
            }


            //Convert only if file does not exist
            if (!File.Exists(exmlPath))
            {
                Console.WriteLine("Exml does not exist, Converting...");
                //Convert Descriptor MBIN to exml
                FileUtils.MbinToExml(descrpath, exmlPath);
            }
            
            //Parse exml now
            XmlDocument descrXml = new XmlDocument();
            descrXml.Load(exmlPath);
            XmlElement root = (XmlElement) descrXml.ChildNodes[2].ChildNodes[0];

            //List<model> allparts = new List<model>();

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
                n.resMgr.shader_programs = this.glcontrol1.resMgr.shader_programs; //Copy the same shader programs
                //n.resMgr.GLgeoms = this.resMgr.GLgeoms;
                //readd textures
                n.setupControlParameters();
                
                //----PROC GENERATION START----
                List<string> parts = new List<string>();
                ModelProcGen.parse_descriptor(Util.randgen, FileUtils.dirpath, ref parts, root);

                Console.WriteLine(String.Join(" ", parts.ToArray()));
                model m;
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
            //        model m;
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
            Util.setStatus("Ready");
            GC.Collect();
            vpwin.Show();
            
        }
        
        //Animation file Open Dialog
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
            foreach (model s in glcontrol1.animScenes)
                if (s.type == TYPES.SCENE)
                    if (((scene) s).animMeta != null) ((scene) s).animate();
        }

        //MenuBar Stuff

        private void settingsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Settings.Show();
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
            Util.setStatus("AltID Copied to clipboard.");
        }

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string obj_credits = "# No Mans Model Viewer OBJ File:\n# www.3dgamedevblog.com";
            Console.WriteLine("Exporting to obj");
            SaveFileDialog sv = new SaveFileDialog();
            sv.Filter = "OBJ Files | *.obj";
            sv.DefaultExt = "obj";
            DialogResult res = sv.ShowDialog();

            if (res != DialogResult.OK)
                return;

            StreamWriter obj = new StreamWriter(sv.FileName);

            obj.WriteLine(obj_credits);

            //Iterate in objects
            uint index = 1;
            findGeoms(glcontrol1.rootObject, obj, ref index);

            obj.Close();

            Console.WriteLine("Scene successfully converted!");

        }

        private void findGeoms(model m, StreamWriter s, ref uint index)
        {
            if (m.type == TYPES.MESH || m.type ==TYPES.COLLISION)
            {
                //Get converted text
                meshModel me = (meshModel) m;
                me.writeGeomToStream(s, ref index);
            
            }
            foreach (model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (glcontrol1.rootObject != null)
                glcontrol1.rootObject.Dispose();
            this.glcontrol1.resMgr.Cleanup();
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

            List<Texture> texlist = new List<Texture>();

            texlist.Add(selectedOb.material.fDiffuseMap);
            texlist.Add(selectedOb.material.fMaskMap);
            texlist.Add(selectedOb.material.fNormalMap);
            
            FileStream fs;
            BinaryWriter bw;

            for (int i = 0; i < 3; i++)
            {
                Texture tex = texlist[i];
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

        //Light Intensity
        private void l_intensity_nud_ValueChanged(object sender, EventArgs e)
        {
            Util.activeControl.light_intensity = (float)this.l_intensity_nud.Value;
            Util.activeControl.updateLightPosition(0);
            Util.activeControl.Invalidate();
        }

        //Unit Tests
        public void AnimationTest()
        {
            //string filepath = "C:\\Users\\gkass\\Downloads\\NMS_Unpacked\\MODELS\\PLANETS\\BIOMES\\COMMON\\INTERACTIVEFLORA\\COMMODITYPLANT1.SCENE.MBIN";
            //string animpath = "C:\\Users\\gkass\\Downloads\\NMS_Unpacked\\MODELS\\PLANETS\\BIOMES\\COMMON\\INTERACTIVEFLORA\\ANIMS\\COMMODITYPLANT1_OPEN.ANIM.MBIN";

            //string filepath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS\\MODELS\\COMMON\\PLAYER\\PLAYERCHARACTER\\NPCVYKEEN.SCENE.MBIN";
            
            //Still animation
            //string animpath = "C:\\Users\\gkass\\Downloads\\NMS_Unpacked\\MODELS\\COMMON\\PLAYER\\PLAYERCHARACTER\\NPCVYKEEN.ANIM.MBIN";
            //Idle 1 Hand
            //string animpath = "C:\\Users\\gkass\\Downloads\\NMS_Unpacked\\MODELS\\COMMON\\PLAYER\\PLAYERCHARACTER\\ANIMS\\IDLES\\1H_IDLE_BASIC.ANIM.MBIN";

            //string animpath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS\\MODELS\\COMMON\\PLAYER\\PLAYERCHARACTER\\ANIMS\\EMOTES\\0H_EMOTE_GREET_WAVE.ANIM.MBIN";
            
            //openSceneMbin(filepath);
            //Util.loadAnimationFile(animpath, (scene) this.glcontrol1.rootObject);
        }


    }


}
