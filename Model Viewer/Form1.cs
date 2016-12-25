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
        private Matrix4 rotMat, mvp;
        private int occludedNum = 0;

        private float scale = 1.0f;
        private int movement_speed = 1;
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;

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
        public float[] JMArray = new float[128 * 16];
        //public float[] JColors = new float[128 * 3];

        //Selected Object
        public GMDL.sharedVBO selectedOb;

        //Path
        private string mainFilePath;
        //Private Settings Window
        private SettingsForm Settings = new SettingsForm();


        public Form1()
        {
            //Custom stuff
            this.xyzControl1 = new XYZControl("WorldPosition");
            this.xyzControl2 = new XYZControl("LocalPosition");

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
            Debug.WriteLine("Opening File");
            DialogResult res = openFileDialog1.ShowDialog();
            if (res == DialogResult.Cancel)
                return;

            var filename = openFileDialog1.FileName;
            mainFilePath = openFileDialog1.FileName;

            var split = filename.Split('.');
            var ext = split[split.Length - 1].ToUpper();
            Debug.WriteLine(ext);

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
                Debug.WriteLine("Parsing SCENE XML");

                //Convert only if file does not exist
                if (!File.Exists(exmlPath))
                {
                    Debug.WriteLine("Exml does not exist");
                    Util.MbinToExml(filename, exmlPath);
                }
            }


            //Open exml
            this.xmlDoc.Load(exmlPath);

            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();

            RightSplitter.Panel1.Controls.Clear();
            //Clear Resources
            ResourceMgmt.Cleanup();
            //Add Defaults
            addDefaultTextures();

            setup_GLControl();
            RightSplitter.Panel1.Controls.Add(glControl1);

            glControl1.Update();
            glControl1.MakeCurrent();
            GMDL.scene scene;
            scene = GEOMMBIN.LoadObjects(this.xmlDoc);
            scene.ID = this.childCounter;
            this.mainScene = null;
            this.mainScene = scene;
            this.childCounter++;

            Util.setStatus("Creating Nodes...", this.toolStripStatusLabel1);

            //Debug.WriteLine("Objects Returned: {0}",oblist.Count);
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
            Util.setStatus("Ready", this.toolStripStatusLabel1);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;

            //Populate shader list
            ResourceMgmt.shader_programs = new int[9];
            string vvs, ggs, ffs;
            //Geometry Shader
            //Compile Object Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VSEmpty.glsl");
            ggs = GLSL_Preprocessor.Parser("Shaders/Simple_GS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FSEmpty.glsl");

            GLShaderHelper.CreateShaders(vvs, ffs, ggs, out vertex_shader_ob,
                    out fragment_shader_ob, out ResourceMgmt.shader_programs[5]);

            //Main Shader
            vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", out vertex_shader_ob,
                    out fragment_shader_ob, out ResourceMgmt.shader_programs[0]);

            //Texture Mixing Shaders
            vvs = GLSL_Preprocessor.Parser("Shaders/pass_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/pass_FS.glsl");
            GLShaderHelper.CreateShaders(vvs, ffs, "", out vertex_shader_ob,
                    out fragment_shader_ob, out ResourceMgmt.shader_programs[3]);

            //Locator Shaders
            GLShaderHelper.CreateShaders(Resources.locator_vert, Resources.locator_frag, "", out vertex_shader_ob,
                out fragment_shader_ob, out ResourceMgmt.shader_programs[1]);
            
            //Joint Shaders
            GLShaderHelper.CreateShaders(Resources.joint_vert, Resources.joint_frag, "", out vertex_shader_ob,
                out fragment_shader_ob, out ResourceMgmt.shader_programs[2]);

            //Text Shaders
            GLShaderHelper.CreateShaders(Resources.text_vert, Resources.text_frag, "", out vertex_shader_ob,
                out fragment_shader_ob, out ResourceMgmt.shader_programs[4]);

            //Picking Shaders
            GLShaderHelper.CreateShaders(Resources.pick_vert, Resources.pick_frag, "", out vertex_shader_ob,
                out fragment_shader_ob, out ResourceMgmt.shader_programs[6]);

            //Light Shaders
            GLShaderHelper.CreateShaders(Resources.light_vert, Resources.light_frag, "", out vertex_shader_ob,
                out fragment_shader_ob, out ResourceMgmt.shader_programs[7]);

            //Camera Shaders
            GLShaderHelper.CreateShaders(Resources.camera_vert, Resources.camera_frag, "", out vertex_shader_ob,
                out fragment_shader_ob, out ResourceMgmt.shader_programs[8]);
            
            GMDL.scene scene = new GMDL.scene();
            scene.type = TYPES.SCENE;
            scene.shader_programs = new int[] { ResourceMgmt.shader_programs[1],
                                                ResourceMgmt.shader_programs[5],
                                                ResourceMgmt.shader_programs[6]};
            scene.ID = this.childCounter;


            //Add Frustum cube
            GMDL.Collision cube = new GMDL.Collision();
            cube.vbo = (new Box(2.0f, 2.0f, 2.0f)).getVBO();

            //Create model
            GMDL.Collision so = new GMDL.Collision();

            //Remove that after implemented all the different collision types
            cube.shader_programs = new int[] { ResourceMgmt.shader_programs[0],
                                              ResourceMgmt.shader_programs[5],
                                              ResourceMgmt.shader_programs[6]}; //Use Mesh program for collisions
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
                Debug.WriteLine(caps);
            }

            //Load Settings
            Settings.loadSettings();

            //Setup the update timer
            t = new Timer();
            t.Interval = 16;
            t.Tick += new EventHandler(timer_ticker);
            t.Start();

            //Set GEOMMBIN statusStrip
            GEOMMBIN.strip = this.toolStripStatusLabel1;

            //Set Default JMarray
            for (int i = 0; i < 128; i++)
                Util.insertMatToArray(Util.JMarray, i * 16, Matrix4.Identity);

            int maxfloats;
            GL.GetInteger(GetPName.MaxVertexUniformVectors, out maxfloats);
            toolStripStatusLabel1.Text = "Ready";

            //Query GL Extensions
            Debug.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
                Debug.WriteLine(s);


            //Load default textures
            addDefaultTextures();

            //Load default Lights
            addDefaultLights();

            //Load font
            setupFont();


            //Add 2 Cams
            Camera cam;
            cam = new Camera(50, ResourceMgmt.shader_programs[8], 0, true);
            ResourceMgmt.GLCameras.Add(cam);
            cam = new Camera(50, ResourceMgmt.shader_programs[8], 0, false);
            ResourceMgmt.GLCameras.Add(cam);
            activeCam = ResourceMgmt.GLCameras[0];
            activeCam.isActive = true;

            
            //Check if Temp folder exists
            if (!Directory.Exists("Temp")) Directory.CreateDirectory("Temp");
            
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
            font.program = ResourceMgmt.shader_programs[4];

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
            Model_Viewer.ResourceMgmt.GLtextures["default.dds"] = tex;
            //Transparent Mask
            texpath = Path.Combine(execpath, "default_mask.dds");
            tex = new GMDL.Texture(texpath);
            Model_Viewer.ResourceMgmt.GLtextures["default_mask.dds"] = tex;

        }

        //Light Functions

        private void addDefaultLights()
        {
            //Add one and only light for now
            GMDL.Light light = new GMDL.Light();
            light.shader_programs = new int[] { ResourceMgmt.shader_programs[7] };
            light.localPosition = new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0)));

            ResourceMgmt.GLlights.Add(light);
        }

        private void updateLightPosition(int light_id)
        {
            GMDL.Light light = (GMDL.Light) ResourceMgmt.GLlights[light_id];
            light.updatePosition(new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0))));
        }


        //glControl Timer
        private void timer_ticker(object sender, EventArgs e)
        {
            //Update common transforms
            activeCam.aspect = (float)glControl1.ClientSize.Width / glControl1.ClientSize.Height;
            //proj = Matrix4.CreatePerspectiveFieldOfView(-w, w, -h, h , znear, zfar);

            Matrix4 Rotx = Matrix4.CreateRotationX(rot[0] * (float) Math.PI / 180.0f);
            Matrix4 Roty = Matrix4.CreateRotationY(rot[1] * (float) Math.PI / 180.0f);
            Matrix4 Rotz = Matrix4.CreateRotationZ(rot[2] * (float) Math.PI / 180.0f);
            rotMat = Rotz * Roty * Rotx;
            mvp = activeCam.GetViewMatrix(); //Full mvp matrix
            
            activeCam.updateFrustumPlanes();
            
            occludedNum = 0; //Reset Counter

            //Simply invalidate the gl control
            glControl1.MakeCurrent();
            glControl1.Invalidate();
        }

        private void render_scene()
        {
            //Debug.WriteLine("Rendering Scene Cam Position : {0}", this.cam.Position);
            //Debug.WriteLine("Rendering Scene Cam Orientation: {0}", this.cam.Orientation);
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
            GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);


            GL.UseProgram(ResourceMgmt.shader_programs[4]);

            //Load uniforms
            int loc;
            loc = GL.GetUniformLocation(ResourceMgmt.shader_programs[4], "w");
            GL.Uniform1(loc, (float)glControl1.Width);
            loc = GL.GetUniformLocation(ResourceMgmt.shader_programs[4], "h");
            GL.Uniform1(loc, (float)glControl1.Height);

            texObs[1]=font.renderText(occludedNum.ToString(), new Vector2(1.0f, 0.0f), 0.75f);
            //Render Text Objects
            foreach (GLText t in texObs)
                t.render();

            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.DepthTest);

        }

        private void render_lights()
        {
            int active_program = ResourceMgmt.shader_programs[7];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix
            loc = GL.GetUniformLocation(active_program, "mvp");
            GL.UniformMatrix4(loc, false, ref mvp);
            
            //Send theta to all shaders
            loc = GL.GetUniformLocation(active_program, "theta");
            GL.Uniform3(loc, this.rot);

            foreach (GMDL.model light in ResourceMgmt.GLlights)
                light.render(0);
        }

        private void render_cameras()
        {
            int active_program = ResourceMgmt.shader_programs[8];

            GL.UseProgram(active_program);
            int loc;
            //Send mvp matrix to all shaders
            loc = GL.GetUniformLocation(active_program, "mvp");
            Matrix4 cam_mvp = activeCam.GetViewMatrix();
            GL.UniformMatrix4(loc, false, ref cam_mvp);
            //Send object world Matrix to all shaders

            foreach (Camera cam in ResourceMgmt.GLCameras)
            {
                //Upload uniforms
                loc = GL.GetUniformLocation(active_program, "self_mvp");
                Matrix4 self_mvp = cam.GetViewMatrix();
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


            int[] rbufs = new int[2];
            GL.Ext.GenRenderbuffers(2, rbufs);
            int depth_rb = rbufs[1];
            int color_rb = rbufs[0];

            Console.WriteLine("Last GL Error: " + GL.GetError());

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);
            //Bind color renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, color_rb);
            GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.Rgba8, tex_w, tex_h);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, RenderbufferTarget.RenderbufferExt, color_rb);

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);
            //Bind depth renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, depth_rb);
            GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.DepthComponent, tex_w, tex_h);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, RenderbufferTarget.RenderbufferExt, depth_rb);

            //Draw Scene
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);//Now render objects with the picking program
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Debug.WriteLine("MALAKIES STO FRAMEBUFFER" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            if (this.mainScene != null) traverse_render(this.mainScene, 2);

            //Store Framebuffer to Disk
            byte[] pixels = new byte[4 * tex_w * tex_h];
            
#if DEBUG
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Debug.WriteLine("Saving Picking Buffer. Dimensions: " + tex_w +" "+ tex_h);
            //GL.ReadPixels(0, 0, tex_w, tex_h, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            FileStream fs = new FileStream("pickBuffer", FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write(pixels);
            fs.Flush();
            fs.Close();
#endif

            //Pick object here :)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Debug.WriteLine("Selecting Object at Position: " + p.X + " " + (tex_h -p.Y));
            byte[] buffer = new byte[4];
            GL.ReadPixels(p.X, tex_h - p.Y, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, buffer);

            //Convert color to id
            int ob_id = (buffer[1] << 8) | buffer[0];

            //Deselect everything first
            traverse_oblist_rs(this.mainScene, "selected", 0);
            
            //Try to find object
            selectedOb = (GMDL.sharedVBO) traverse_oblist_field<int>(this.mainScene, ob_id, "selected", 1);
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
            selMatName.Text = selectedOb.material.name;
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
            //Debug.WriteLine("{0} {1}", e.Node.Checked, e.Node.Index);
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

                    //Debug.WriteLine("Testing Geom {0}  Node {1}", child.Index, node.Index);
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
                Debug.WriteLine("Object Found: " + root.name + " ID: " + root.ID);
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
                GL.Uniform3(loc, ResourceMgmt.GLlights[0].localPosition);

                //Upload Light Intensity
                loc = GL.GetUniformLocation(active_program, "intensity");
                GL.Uniform1(loc, light_intensity);

                //Upload camera position as the light
                //GL.Uniform3(loc, cam.Position);

                //Upload firstskinmat
                loc = GL.GetUniformLocation(active_program, "firstskinmat");
                GL.Uniform1(loc, ((GMDL.sharedVBO)root).firstskinmat);

                //Apply frustum culling only for mesh objects
                //root.render(program);
                if (activeCam.frustum_occlude(root, rotMat)) root.render(program);
                else occludedNum++;
            }
            else if (root.type == TYPES.LOCATOR || root.type == TYPES.SCENE || root.type == TYPES.JOINT || root.type == TYPES.LIGHT || root.type ==TYPES.COLLISION)
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

        private TreeNode findNodeFromText(TreeNodeCollection coll, string text)
        {
            foreach (TreeNode node in coll)
            {
                //Debug.WriteLine(node.Text + " " + text + " {0}", node.Text == text);
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
            Debug.WriteLine("Opening " + descrpath);

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
                Debug.WriteLine("Exml does not exist, Converting...");
                //Convert Descriptor MBIN to exml
                Util.MbinToExml(descrpath, exmlPath);
            }
            
            //Parse exml now
            XmlDocument descrXml = new XmlDocument();
            descrXml.Load(exmlPath);
            XmlElement root = (XmlElement) descrXml.ChildNodes[1].ChildNodes[0];

            //List<GMDL.model> allparts = new List<GMDL.model>();

            //Revise Procgen Creation

            //First Create the form and the table
            Form vpwin = new Form();
            //vpwin.parentForm = this; //Set parent to this form
            //vpwin.FormClosed += new FormClosedEventHandler(this.resumeTicker);
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
                n.MakeCurrent(); //Make current

                //----PROC GENERATION START----
                List<string> parts = new List<string>();
                ModelProcGen.parse_descriptor(ref parts, root);

                Debug.WriteLine(String.Join(" ", parts.ToArray()));
                GMDL.model m;
                m = ModelProcGen.get_procgen_parts(ref parts, this.mainScene);
                //----PROC GENERATION END----

                n.rootObject = m;
                n.shader_programs = ResourceMgmt.shader_programs;

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

            //        Debug.WriteLine(String.Join(" ", parts.ToArray()));
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
            Debug.WriteLine("Opening File");

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
            GL.Enable(EnableCap.DepthTest);
            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            Debug.WriteLine("GL Cleared");
            Debug.WriteLine(GL.GetError());

            this.glloaded = true;

            //Set mouse pos
            mouse_x = 0;
            mouse_y = 0;

            glControl1.Invalidate();
        }

        private void setup_GLControl()
        {
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
            //GL.Enable(EnableCap.DepthTest);
            render_scene();
            render_lights();
            render_cameras();
            render_info();
            

            glControl1.SwapBuffers();
            //translate_View();
            ////Draw scene
            //GL.MatrixMode(MatrixMode.Modelview);
            //Update Joystick 

            //glControl1.Invalidate();
            //Debug.WriteLine("Painting Control");
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;
            if (glControl1.ClientSize.Height == 0)
                glControl1.ClientSize = new System.Drawing.Size(glControl1.ClientSize.Width, 1);
            Debug.WriteLine("GLControl Resizing");
            Debug.WriteLine(this.eye_pos.X.ToString() + " " + this.eye_pos.Y.ToString());
            GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
            //GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
        }

        private void glControl1_KeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            //Debug.WriteLine("Key pressed {0}",e.KeyCode);
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
                        activeCam.Move(0.0f, -0.1f, 0.0f);
                    break;
                case Keys.S:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, 0.1f, 0.0f);
                    break;
                case (Keys.D):
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(-0.1f, 0.0f, 0.0f);
                    break;
                case Keys.A:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.1f, 0.0f, 0.0f);
                    break;
                case (Keys.R):
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, 0.0f, -0.1f);
                    break;
                case Keys.F:
                    for (int i = 0; i < movement_speed; i++)
                        activeCam.Move(0.0f, 0.0f, 0.1f);
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
                    if (ResourceMgmt.GLCameras[0].isActive)
                    {
                        activeCam.isActive = false;
                        activeCam = ResourceMgmt.GLCameras[1];
                    }
                    else
                    {
                        activeCam.isActive = false;
                        activeCam = ResourceMgmt.GLCameras[0];
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

                    //Debug.WriteLine(activeCam.GetViewMatrix());
                    
                    break;

                default:
                    //Debug.WriteLine("Not Implemented Yet");
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
                //Debug.WriteLine("Deltas {0} {1} {2}", delta_x, delta_y, e.Button);
                activeCam.AddRotation(delta_x, delta_y);
            }

            mouse_x = e.X;
            mouse_y = e.Y;

        }

        private void glControl1_Scroll(object sender, MouseEventArgs e)
        {
            if (Math.Abs(e.Delta) > 0)
            {
                //Debug.WriteLine("Wheel Delta {0}", e.Delta);
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
            Debug.WriteLine("ENtered Focus");
            t.Start();
        }

        private void glControl1_Leave(object sender, EventArgs e)
        {
            //Don't update the control when its not focused
            Debug.WriteLine("Left Focus");
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
            Debug.WriteLine(this.treeView1.Nodes[0].Text);
            traverse_oblist_altid(ref altId, this.treeView1.Nodes[0]);
            string finalalt = "";
            for (int i = 0; i < altId.Count; i++)
                finalalt += altId[i] + " ";
            //Debug.WriteLine(finalalt);
            Clipboard.SetText(finalalt);
            Util.setStatus("AltID Copied to clipboard.", toolStripStatusLabel1);
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
            findGeoms(mainScene, obj, ref index);

            obj.Close();

        }

        private void findGeoms(GMDL.model m, StreamWriter s, ref uint index)
        {
            if (m.type == TYPES.MESH || m.type ==TYPES.COLLISION)
            {
                //Get converted text
                GMDL.sharedVBO me = (GMDL.sharedVBO)m;
                me.writeGeomToStream(s, ref index);

            }
            foreach (GMDL.model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }
        
        //Light Intensity
        private void l_intensity_nud_ValueChanged(object sender, EventArgs e)
        {
            light_intensity = (float) this.l_intensity_nud.Value;
            updateLightPosition(0);
            glControl1.Invalidate();
        }
        
    
    }

    //Class Which will store all the texture resources for better memory management
    public static class ResourceMgmt
    {
        public static Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        public static Dictionary<string, GMDL.Material> GLmaterials = new Dictionary<string, GMDL.Material>();
        public static List<GMDL.model> GLlights = new List<GMDL.model>();
        public static List<Camera> GLCameras = new List<Camera>();
        public static int[] shader_programs;
        public static DebugForm DebugWin;
        
        public static void Cleanup()
        {
            GLtextures.Clear();
            GLmaterials.Clear();
        }

    }
}
