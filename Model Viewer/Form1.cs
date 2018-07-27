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

        private float light_angle_y = 0.0f;
        private float light_angle_x = 0.0f;
        private float light_distance = 5.0f;
        private float light_intensity = 2.0f;

        //Common transforms
        private Matrix4 rotMat = Matrix4.Identity;
        private Matrix4 mvp = Matrix4.Identity;
        private int occludedNum = 0;

        private float scale = 1.0f;
        private int movement_speed = 1;
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;
        //Gamepad
        private int gamepadID = -1;
        private GamepadHandler gpHandler;


        public int childCounter = 0;

        //Shader objects
        int vertex_shader_ob;
        int fragment_shader_ob;

        //Setup Timer to invalidate the viewport
        private Timer t;

        //Debug Font
        FontGL font;
        public List<GLText> texObs = new List<GLText>();
        
        public List<GMDL.model> animScenes = new List<GMDL.model>();
        //Deprecated
        //private List<GMDL.model> vboobjects = new List<GMDL.model>();
        //private GMDL.model rootObject;
        private GMDL.scene mainScene;
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
        //Private fps Counter
        private int frames = 0;
        private DateTime oldtime;

        //Form private ResourceManagement
        private ResourceMgmt resMgmt = new ResourceMgmt();

        //Global Gbuffer
        private GBuffer gbuf;



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

            //If the file is already an exml there is no need for conversions
            string exmlPath = filename;
            if (!(ext == "EXML"))
            {
                exmlPath = Util.getExmlPath(filename);

                //Parse the Scene XML file
                Console.WriteLine("Parsing SCENE XML");

                //Convert only if file does not exist
#if DEBUG
                Console.WriteLine("-DEBUG- Forcing EXML Conversion");
                Util.MbinToExml(filename, exmlPath);
#else
                if (!File.Exists(exmlPath))
                {
                    Console.WriteLine("Exml does not exist");
                    Util.MbinToExml(filename, exmlPath);
                }
#endif
            }

            //Set global resMgmt to form resMgmt
            Util.resMgmt = resMgmt;


            //Open exml
            this.xmlDoc.Load(exmlPath);

            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();

            t.Stop();
            this.glControl1.Paint -= this.glControl1_Paint;
            this.mainScene.Dispose(); //Prevent rendering
            this.mainScene = null;
            t.Start();
            RightSplitter.Panel1.Controls.Clear();

            //Clear Form Resources
            Util.resMgmt.Cleanup();
            ModelProcGen.procDecisions.Clear();
            //Add Defaults
            addDefaultTextures();

            setup_GLControl();

            //Reset the activeControl and create new gbuffer
            Util.activeControl = glControl1;
            //Init Gbuffer
            //gbuf = new GBuffer();

            RightSplitter.Panel1.Controls.Add(glControl1);

            glControl1.Update();
            glControl1.MakeCurrent();
            GMDL.scene scene;
            scene = GEOMMBIN.LoadObjects(this.xmlDoc);
            scene.ID = this.childCounter;
            this.mainScene = scene;
            this.childCounter++;

            Util.setStatus("Creating Nodes...", this.toolStripStatusLabel1);

            //Console.WriteLine("Objects Returned: {0}",oblist.Count);
            MyTreeNode node = new MyTreeNode(scene.name);
            node.Checked = true;
            node.model = this.mainScene;
            //Clear index dictionary
            index_dict.Clear();
            joint_dict.Clear();
            animScenes.Clear();

            this.childCounter = 0;
            //Add root to dictionary
            index_dict[scene.name] = this.childCounter;
            this.childCounter += 1;
            //Set indices and TreeNodes 
            traverse_oblist(this.mainScene, node);
            //Add root to treeview
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(node);
            GC.Collect();

            glControl1_Resize(new object(), null); //Try to call resize event

            glControl1.Update();
            glControl1.Invalidate();

            //Cleanup Materials
            foreach (GMDL.Material mat in Util.resMgmt.GLmaterials.Values)
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
            if (!this.glloaded)
                return;

            //Query GL Extensions
            Console.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Console.WriteLine(s);
            }
                


            

            //Set global resMgmt to form resMgmt
            Util.resMgmt = resMgmt;

            //Populate shader list
            Util.resMgmt.shader_programs = new int[11];
            string vvs, ggs, ffs;
            //Geometry Shader
            //Compile Object Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VSEmpty.glsl");
            ggs = GLSL_Preprocessor.Parser("Shaders/Simple_GS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FSEmpty.glsl");

            GLShaderHelper.CreateShaders(vvs, ffs, ggs, "","", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.resMgmt.shader_programs[5]);

            //Main Shader
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.resMgmt.shader_programs[0]);

            //Texture Mixing Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/pass_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/pass_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.resMgmt.shader_programs[3]);

            //GBuffer Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Gbuffer_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.resMgmt.shader_programs[9]);

            //Decal Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/decal_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Decal_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", "", "", out vertex_shader_ob,
                    out fragment_shader_ob, out Util.resMgmt.shader_programs[10]);

            //Locator Shaders
            GLShaderHelper.CreateShaders(Resources.locator_vert, Resources.locator_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.resMgmt.shader_programs[1]);
            
            //Joint Shaders
            GLShaderHelper.CreateShaders(Resources.joint_vert, Resources.joint_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.resMgmt.shader_programs[2]);

            //Text Shaders
            GLShaderHelper.CreateShaders(Resources.text_vert, Resources.text_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.resMgmt.shader_programs[4]);

            //Picking Shaders
            GLShaderHelper.CreateShaders(Resources.pick_vert, Resources.pick_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.resMgmt.shader_programs[6]);

            //Light Shaders
            GLShaderHelper.CreateShaders(Resources.light_vert, Resources.light_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.resMgmt.shader_programs[7]);

            //Camera Shaders
            GLShaderHelper.CreateShaders(Resources.camera_vert, Resources.camera_frag, "", "", "", out vertex_shader_ob,
                out fragment_shader_ob, out Util.resMgmt.shader_programs[8]);
            
            
            GMDL.scene scene = new GMDL.scene();
            scene.type = TYPES.SCENE;
            scene.shader_programs = new int[] { Util.resMgmt.shader_programs[1],
                                                Util.resMgmt.shader_programs[5],
                                                Util.resMgmt.shader_programs[6]};


            Util.activeControl = glControl1;
            //Init Gbuffer
            gbuf = new GBuffer();
            Util.gbuf = gbuf;

            Console.WriteLine("GBuffer Setup Done, Last GL Error: " + GL.GetError());

            scene.ID = this.childCounter;


            //Add Frustum cube
            GMDL.Collision cube = new GMDL.Collision();
            //cube.vbo = (new Capsule(new Vector3(), 14.0f, 2.0f)).getVBO();
            cube.main_Vao = (new Box(1.0f, 1.0f, 1.0f)).getVAO();

            //Create model
            GMDL.Collision so = new GMDL.Collision();

            //Remove that after implemented all the different collision types
            cube.shader_programs = new int[] { Util.resMgmt.shader_programs[0],
                                              Util.resMgmt.shader_programs[5],
                                              Util.resMgmt.shader_programs[6]}; //Use Mesh program for collisions
            cube.name = "FRUSTUM";
            cube.collisionType = (int) COLLISIONTYPES.BOX;


            scene.children.Add(cube);

            this.mainScene = scene;
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
                gamepadID = i;
                Console.WriteLine(caps + caps.GetType().Name);
            }
            //Setup GamePad Handler
            gpHandler = new GamepadHandler(gamepadID);


            //Load Settings
            Settings.loadSettings();

            //Setup the update timer
            t = new Timer();
            t.Interval = 5;
            t.Tick += new EventHandler(timer_ticker);
            t.Start();

            //Set GEOMMBIN statusStrip
            GEOMMBIN.strip = this.toolStripStatusLabel1;

            //Set Default JMarray
            for (int i = 0; i < 256; i++)
                Util.insertMatToArray(Util.JMarray, i * 16, Matrix4.Identity);

            int maxfloats;
            GL.GetInteger(GetPName.MaxVertexUniformVectors, out maxfloats);
            toolStripStatusLabel1.Text = "Ready";

            //Load default textures
            addDefaultTextures();

            //Load default Lights
            addDefaultLights();

            //Load font
            setupFont();


            //Add 2 Cams
            Camera cam;
            cam = new Camera(50, Util.resMgmt.shader_programs[8], 0, true);
            Util.resMgmt.GLCameras.Add(cam);
            //cam = new Camera(50, ResourceMgmt.shader_programs[8], 0, false);
            //ResourceMgmt.GLCameras.Add(cam);
            activeCam = Util.resMgmt.GLCameras[0];
            activeCam.isActive = true;

            
            //Check if Temp folder exists
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
            
        }

        private void Form1_Close(object sender, EventArgs e)
        {

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

        private void gamepadController()
        {
            //This Method handles and controls the gamepad input
            //Move camera
            //Console.WriteLine(gpHandler.getAxsState(0, 0));
            //Console.WriteLine(gpHandler.getBtnState(1) - gpHandler.getBtnState(0));
            //Console.WriteLine(gpHandler.getAxsState(0, 1));
            for (int i = 0; i < movement_speed; i++)
                activeCam.Move(gpHandler.getAxsState(0, 0),
                               gpHandler.getAxsState(0, 1),
                               (gpHandler.getBtnState(1) - gpHandler.getBtnState(0) ));
            //Rotate Camera
            //for (int i = 0; i < movement_speed; i++)
            activeCam.AddRotation(-3.0f * gpHandler.getAxsState(1, 0), 3.0f *gpHandler.getAxsState(1, 1));



        }


        private void setupFont()
        {
            font = new FontGL();

            //Test BMP Image Class
            //BMPImage bm = new BMPImage("courier.bmp");

            //Create font to memory
            MemoryStream ms = FontGL.createFont();
            BMPImage bm = new BMPImage(ms);

            //Testing some inits
            font.initFromImage(bm);
            font.tex = bm.GLid;
            font.program = Util.resMgmt.shader_programs[4];

            //Set default settings
            float scale = 0.75f;
            font.space = scale * 0.20f;
            font.width = scale * 0.20f;
            font.height = scale * 0.35f;

            //Add some text for rendering
            texObs.Add(font.renderText("Greetings", new Vector2(0.02f, 0.0f), scale));
            texObs.Add(font.renderText(occludedNum.ToString(), new Vector2(1.0f, 0.0f), 1.0f));
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            //Add Default textures
            //White tex
            string texpath = Path.Combine(execpath, "default.dds");
            GMDL.Texture tex = new GMDL.Texture(texpath);
            Util.resMgmt.GLtextures["default.dds"] = tex;
            //Transparent Mask
            texpath = Path.Combine(execpath, "default_mask.dds");
            tex = new GMDL.Texture(texpath);
            Util.resMgmt.GLtextures["default_mask.dds"] = tex;

        }

        //Light Functions

        private void addDefaultLights()
        {
            //Add one and only light for now
            GMDL.Light light = new GMDL.Light();
            light.shader_programs = new int[] { Util.resMgmt.shader_programs[7] };
            light.localPosition = new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0)));

            Util.resMgmt.GLlights.Add(light);
        }

        private void updateLightPosition(int light_id)
        {
            GMDL.Light light = (GMDL.Light)Util.resMgmt.GLlights[light_id];
            light.updatePosition(new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0))));
        }


        //glControl Timer
        private void timer_ticker(object sender, EventArgs e)
        {
            //Handle Gamepad Input
            gpHandler.updateState();
            //Console.WriteLine(gpHandler.getAxsState(0, 0).ToString() + " " +  gpHandler.getAxsState(0, 1).ToString());
            //gpHandler.reportButtons();
            gamepadController(); //Move camera according to input

            //Update common transforms
            activeCam.aspect = (float)glControl1.ClientSize.Width / glControl1.ClientSize.Height;
            activeCam.updateViewMatrix();
            activeCam.updateFrustumPlanes();
            //proj = Matrix4.CreatePerspectiveFieldOfView(-w, w, -h, h , znear, zfar);

            Matrix4 Rotx = Matrix4.CreateRotationX(rot[0] * (float) Math.PI / 180.0f);
            Matrix4 Roty = Matrix4.CreateRotationY(rot[1] * (float) Math.PI / 180.0f);
            Matrix4 Rotz = Matrix4.CreateRotationZ(rot[2] * (float) Math.PI / 180.0f);
            rotMat = Rotz * Roty * Rotx;
            mvp = activeCam.viewMat; //Full mvp matrix
            Util.mvp = mvp;
            occludedNum = 0; //Reset Counter

            //Simply invalidate the gl control
            //glControl1.MakeCurrent();
            glControl1.Invalidate();
        }

        private void render_scene()
        {
            //Console.WriteLine("Rendering Scene Cam Position : {0}", this.activeCam.Position);
            //Console.WriteLine("Rendering Scene Cam Orientation: {0}", this.activeCam.Orientation);
            GL.CullFace(CullFaceMode.Back);

            //Render only the first scene for now
            
            if (this.mainScene != null)
            {
                //Drawing Phase
                traverse_render(this.mainScene, 0);
                //Drawing Debug
                if (RenderOptions.RenderDebug) traverse_render(this.mainScene, 1);
            }

        }

        private void render_info()
        {
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.DepthBufferBit);
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);


            GL.UseProgram(Util.resMgmt.shader_programs[4]);

            //Load uniforms
            int loc;
            loc = GL.GetUniformLocation(Util.resMgmt.shader_programs[4], "w");
            GL.Uniform1(loc, (float)glControl1.Width);
            loc = GL.GetUniformLocation(Util.resMgmt.shader_programs[4], "h");
            GL.Uniform1(loc, (float)glControl1.Height);

            fps();
            texObs[1]=font.renderText(occludedNum.ToString(), new Vector2(1.0f, 0.0f), 0.75f);
            //Render Text Objects
            foreach (GLText t in texObs)
                t.render();

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

        }

        private void render_decals()
        {
            //gbuf.dump();
            GL.Enable(EnableCap.DepthTest);
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.DepthFunc(DepthFunction.Gequal);


            int active_program = Util.resMgmt.shader_programs[10];
            GL.UseProgram(active_program);
            int loc;
            Matrix4 temp;

            gbuf.dump();

            //Upload inverse decat world matrix
            //for ( int i = 0;i< Math.Min(1, Util.resMgmt.GLDecals.Count); i++)
            foreach (GMDL.model decal in Util.resMgmt.GLDecals)
            {
                //GMDL.Decal decal = (GMDL.Decal) Util.resMgmt.GLDecals[i];
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
            int active_program = Util.resMgmt.shader_programs[7];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref mvp);
            
            foreach (GMDL.model light in Util.resMgmt.GLlights)
                light.render(0);
        }

        private void render_cameras()
        {
            int active_program = Util.resMgmt.shader_programs[8];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            Matrix4 cam_mvp = activeCam.viewMat;
            GL.UniformMatrix4(loc, false, ref cam_mvp);
            //Send object world Matrix to all shaders

            foreach (Camera cam in Util.resMgmt.GLCameras)
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
            int tex_w = glControl1.Width;
            int tex_h = glControl1.Height;

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

            if (this.mainScene != null) traverse_render(this.mainScene, 2);

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
            traverse_oblist_rs(this.mainScene, "selected", 0);
            
            //Try to find object
            selectedOb = (GMDL.meshModel) traverse_oblist_field<int>(this.mainScene, ob_id, "selected", 1);
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
            glControl1.Invalidate();

            
        }

        //Set Camera FOV
        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            
            activeCam.setFOV((int)Math.Max(1, numericUpDown1.Value));
            glControl1.Invalidate();
        }

        //Znear
        private void numericUpDown4_ValueChanged(object sender, EventArgs e)
        {
            activeCam.zNear = (float)this.numericUpDown4.Value;
            glControl1.Invalidate();
        }

        //Zfar
        private void numericUpDown5_ValueChanged(object sender, EventArgs e)
        {
            activeCam.zFar = (float)this.numericUpDown5.Value;
            glControl1.Invalidate();
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

            glControl1.Invalidate();
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
            light_distance = (float)numericUpDown2.Value;
            updateLightPosition(0);
            glControl1.Invalidate();
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
            if (ob.jointModel.Count > 0)
            {
                ob.traverse_joints(ob.jointModel);
                this.animScenes.Add(ob); //Add scene to animscenes
            }



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
                    GL.Uniform3(loc, Util.resMgmt.GLlights[0].localPosition);

                    //Upload Light Intensity
                    loc = GL.GetUniformLocation(active_program, "intensity");
                    GL.Uniform1(loc, light_intensity);

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

            glControl1.MakeCurrent();

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
                Util.resMgmt = n.resMgmt;
                n.resMgmt.shader_programs = this.resMgmt.shader_programs; //Copy the same shader programs
                //n.resMgmt.GLgeoms = this.resMgmt.GLgeoms;
                //readd textures
                addDefaultLights();
                addDefaultTextures();
                n.resMgmt.GLCameras = this.resMgmt.GLCameras;
                
                //----PROC GENERATION START----
                List<string> parts = new List<string>();
                ModelProcGen.parse_descriptor(ref parts, root);

                Console.WriteLine(String.Join(" ", parts.ToArray()));
                GMDL.model m;
                m = ModelProcGen.get_procgen_parts(ref parts, this.mainScene);
                //----PROC GENERATION END----

                n.rootObject = m;
                n.shader_programs = Util.resMgmt.shader_programs;

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

        private void resumeTicker(object sender, EventArgs e)
        {
            Form1 f = ((ProcGenForm) sender).parentForm;
            //Start the timer
            f.t.Start();
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
            glControl1.Focus();
        }

        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            foreach (GMDL.model s in animScenes)
                if (s.animMeta != null) s.animate();
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
            movement_speed = (int) s.Value;
        }


        //GLCONTROL METHODS
        private void glControl_Load(object sender, EventArgs e)
        {
            GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.DepthTest);
            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            Console.WriteLine("GL Cleared");
            Console.WriteLine(GL.GetError());

            this.glloaded = true;

            //Set mouse pos
            mouse_x = 0;
            mouse_y = 0;

            glControl1.Invalidate();
        }

        private void setup_GLControl()
        {
            //glControl1 = new GLControl(new OpenTK.Graphics.GraphicsMode(32, 24, 0, 16));
            glControl1 = new GLControl();
            glControl1.Size = new System.Drawing.Size(976, 645);
            this.glControl1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.glControl1.BackColor = System.Drawing.Color.Black;
            this.glControl1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.glControl1.Location = new System.Drawing.Point(0, 0);
            this.glControl1.MinimumSize = new System.Drawing.Size(256, 256);
            this.glControl1.VSync = true;
            this.glControl1.Load += new System.EventHandler(this.glControl_Load);
            this.glControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.glControl1_Paint);
            this.glControl1.MouseHover += new System.EventHandler(this.glControl1_MouseHover);
            this.glControl1.MouseMove += new System.Windows.Forms.MouseEventHandler(this.glControl1_MouseMove);
            this.glControl1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.glControl1_MouseClick);
            this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.glControl1.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.glControl1_KeyDown);
            this.glControl1.Resize += new System.EventHandler(this.glControl1_Resize);
            this.glControl1.Enter += new System.EventHandler(this.glControl1_Enter);
            this.glControl1.Leave += new System.EventHandler(this.glControl1_Leave);

        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (!this.glloaded)
                return;
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            gbuf.start();

            //Console.WriteLine(active_fbo);
            render_scene();

            //Store the dumps
            gbuf.dump();
            render_decals();

            //render_cameras();
            //render_lights();
            render_info();

            //gbuf.stop();

            //Render Deferred
            //gbuf.render();
            gbuf.blit();

            glControl1.SwapBuffers();
            
            //translate_View();
            ////Draw scene
            //Update Joystick 

            //Console.WriteLine("Painting Control");
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;
            if (glControl1.ClientSize.Height == 0)
                glControl1.ClientSize = new System.Drawing.Size(glControl1.ClientSize.Width, 1);
            Console.WriteLine("GLControl Resizing");
            gbuf.resize(glControl1.ClientSize.Width, glControl1.ClientSize.Height);
            GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
            
            //GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
        }

        private void glControl1_KeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            //Console.WriteLine("Key pressed {0}",e.KeyCode);
            switch (e.KeyCode)
            {
                //Translations
                //X Axis
                //case (Keys.Right):
                //    this.eye.X += 0.1f;
                //    break;
                //case Keys.Left:
                //    this.eye.X -= 0.1f;
                //    break;
                //Z Axis
                //Local Transformation
                case Keys.Q:
                    for (int i = 0; i < movement_speed; i++)
                        this.rot.Y -= 4.0f;
                    break;
                case Keys.E:
                    for (int i = 0; i < movement_speed; i++)
                        this.rot.Y += 4.0f;
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
                        activeCam.Move(0.0f, 0.01f, 0.0f);
                    break;
                case Keys.S:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, -0.01f, 0.0f);
                    break;
                case (Keys.D):
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.01f, 0.0f, 0.0f);
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
                    if (Util.resMgmt.GLCameras[0].isActive)
                    {
                        activeCam.isActive = false;
                        activeCam = Util.resMgmt.GLCameras[1];
                    }
                    else
                    {
                        activeCam.isActive = false;
                        activeCam = Util.resMgmt.GLCameras[0];
                    }
                        
                    activeCam.isActive = true;

                    //Set info of active cam to the controls
                    numericUpDown1.ValueChanged -= this.numericUpDown1_ValueChanged;
                    numericUpDown1.Value =   (decimal) (180.0f * activeCam.fov / (float) Math.PI);
                    numericUpDown1.ValueChanged += this.numericUpDown1_ValueChanged;

                    numericUpDown4.ValueChanged -= this.numericUpDown4_ValueChanged;
                    numericUpDown4.Value = (decimal) activeCam.zNear;
                    numericUpDown4.ValueChanged += this.numericUpDown4_ValueChanged;

                    numericUpDown5.ValueChanged -= this.numericUpDown5_ValueChanged;
                    numericUpDown5.Value = (decimal) activeCam.zFar;
                    numericUpDown5.ValueChanged += this.numericUpDown5_ValueChanged;

                    //Console.WriteLine(activeCam.GetViewMatrix());
                    
                    break;

                default:
                    //Console.WriteLine("Not Implemented Yet");
                    return;
            }
            //glControl1.Invalidate();

        }

        private void glControl1_MouseMove(object sender, MouseEventArgs e)
        {
            //int delta_x = (int) (Math.Pow(cam.fov, 4) * (e.X - mouse_x));
            //int delta_y = (int) (Math.Pow(cam.fov, 4) * (e.Y - mouse_y));
            int delta_x = (e.X - mouse_x);
            int delta_y = (e.Y - mouse_y);

            delta_x = Math.Min(Math.Max(delta_x, -10), 10);
            delta_y = Math.Min(Math.Max(delta_y, -10), 10);

            if (e.Button == MouseButtons.Left)
            {
                //Console.WriteLine("Deltas {0} {1} {2}", delta_x, delta_y, e.Button);
                activeCam.AddRotation(delta_x, delta_y);
            }

            mouse_x = e.X;
            mouse_y = e.Y;

        }

        private void glControl1_Scroll(object sender, MouseEventArgs e)
        {
            if (Math.Abs(e.Delta) > 0)
            {
                //Console.WriteLine("Wheel Delta {0}", e.Delta);
                int sign = e.Delta / Math.Abs(e.Delta);
                int newval = (int)numericUpDown1.Value + sign;
                newval = (int)Math.Min(Math.Max(newval, numericUpDown1.Minimum), numericUpDown1.Maximum);
                activeCam.setFOV(newval);
                numericUpDown1.Value = newval;

                //eye.Z += e.Delta * 0.2f;
                glControl1.Invalidate();
            }

        }
        
        private void glControl1_MouseHover(object sender, EventArgs e)
        {
            glControl1.Focus();
            //glControl1.Invalidate();
        }

        private void glControl1_Enter(object sender, EventArgs e)
        {
            //Start Timer when the glControl gets focus
            Console.WriteLine("ENtered Focus");
            t.Start();
        }

        private void glControl1_Leave(object sender, EventArgs e)
        {
            //Don't update the control when its not focused
            Console.WriteLine("Left Focus");
            if (newButton1.status)
                t.Stop();
        }

        //GLCONTROL Context Menu
        private void glControl1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                mainglcontrolContext.Show(Control.MousePosition);
            }else if ((e.Button == MouseButtons.Left) && (ModifierKeys == Keys.Control))
            {
                selectObject(e.Location);
            }

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
            findGeoms(mainScene, obj, ref index);

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
            this.mainScene.Dispose();
            Util.resMgmt.Cleanup();
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            Console.WriteLine("Clicked");
            selectedOb = ((MyTreeNode)e.Node).model;
            loadSelectedObect();
            //Deselect everything first
            traverse_oblist_rs(this.mainScene, "selected", 0);
            
            //Try to find object
            selectedOb = traverse_oblist_field<int>(this.mainScene, selectedOb.ID, "selected", 1);


        }

        private void getObjectTexturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //This function tries to save to disk the selected objects textures - if any
            gbuf.dump();


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


        //Light Intensity
        private void l_intensity_nud_ValueChanged(object sender, EventArgs e)
        {
            light_intensity = (float) this.l_intensity_nud.Value;
            updateLightPosition(0);
            glControl1.Invalidate();
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
            program = Util.resMgmt.shader_programs[9];
            
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

            Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            int[] rbufs = new int[2];
            GL.Ext.GenRenderbuffers(2, rbufs);
            depth_rbo = rbufs[1];
            diff_rbo = rbufs[0];

            Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //Bind color renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, diff_rbo);
            //Normal Version
            //GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.Rgba8, size[0], size[1]);
            //Multisampling version
            GL.Ext.RenderbufferStorageMultisample(RenderbufferTarget.RenderbufferExt, msaa_samples, RenderbufferStorage.Rgb8, size[0], size[1]);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, RenderbufferTarget.RenderbufferExt, diff_rbo);
            
            Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());


            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //Bind depth renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, depth_rbo);
            //Normal Version
            //GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.DepthComponent, size[0], size[1]);
            //Multisampling version
            GL.Ext.RenderbufferStorageMultisample(RenderbufferTarget.RenderbufferExt, msaa_samples, RenderbufferStorage.DepthComponent, size[0], size[1]);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, RenderbufferTarget.RenderbufferExt, depth_rbo);

            Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

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
