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
        private Camera cam = new Camera(35);
        
        private float light_angle_y = 0.0f;
        private float light_angle_x = 0.0f;
        private float light_distance = 5.0f;
        private float light_intensity = 2.0f;

        private float scale = 1.0f;
        private int movement_speed = 1;
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;

        public int childCounter = 0;
        //private float rot_x = 0.0f;
        //private float rot_y = 0.0f;

        //Shader objects
        int vertex_shader_ob;
        int fragment_shader_ob;
        
        //Setup Timer to invalidate the viewport
        private Timer t;
        

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

        //Path
        private string mainFilePath;
        //Private Settings Window
        private SettingsForm Settings = new SettingsForm(); 
        

        public Form1()
        {
            InitializeComponent();
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

            if (ext != "MBIN" & ext !="EXML")
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
                    Util.MbinToExml(filename);
                }
            }
            

            //Open exml
            this.xmlDoc.Load(exmlPath);
            
            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();
            
            splitContainer1.Panel2.Controls.Clear();
            //Clear Resources
            ResourceMgmt.GLtextures.Clear();
            ResourceMgmt.GLmaterials.Clear();
            //Add Defaults
            addDefaultTextures();

            setup_GLControl();
            splitContainer1.Panel2.Controls.Add(glControl1);

            glControl1.Update();
            glControl1.MakeCurrent();
            GMDL.scene scene;
            scene = GEOMMBIN.LoadObjects(this.xmlDoc);
            scene.index = this.childCounter;
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

#if DEBUG
            //DEBUG write the jmarray to disk
            using (System.IO.StreamWriter file =
            new System.IO.StreamWriter(@"jmarray.txt"))
            {
                for (int i = 0; i < JMArray.Length / 4; i++)
                {
                    file.WriteLine(String.Join(" ", new string[] { JMArray[4 * i].ToString(),
                                                                   JMArray[4 * i + 1].ToString(),
                                                                   JMArray[4 * i + 2].ToString(),
                                                                   JMArray[4 * i + 3].ToString()}));
                }
            }

#endif

            glControl1.Invalidate();
            Util.setStatus("Ready", this.toolStripStatusLabel1);
        }

        

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;

            //Populate shader list
            ResourceMgmt.shader_programs = new int[4];

            //Create Preprocessor object

            //Compile Object Shaders
            //using (StreamReader vs = new StreamReader("Shaders/Simple_VS.glsl"))
            //using (StreamReader fs = new StreamReader("Shaders/Simple_FS.glsl"))
            string vvs = GLSL_Preprocessor.Parser("Shaders/Simple_VS.glsl");
            string ffs = GLSL_Preprocessor.Parser("Shaders/Simple_FS.glsl");

            //FileStream test = new FileStream("preproc_out", FileMode.Create);
            //StreamWriter sw = new StreamWriter(test);
            //sw.Write(ffs);
            //test.Close();

            CreateShaders(vvs, ffs, out vertex_shader_ob,
                    out fragment_shader_ob, out ResourceMgmt.shader_programs[0]);
            //Compile Locator Shaders
            using (StreamReader vs = new StreamReader("Shaders/locator_VS.glsl"))
            using (StreamReader fs = new StreamReader("Shaders/locator_FS.glsl"))
                CreateShaders(vs.ReadToEnd(), fs.ReadToEnd(), out vertex_shader_ob,
                    out fragment_shader_ob, out ResourceMgmt.shader_programs[1]);
            //Compile Joint Shaders
            using (StreamReader vs = new StreamReader("Shaders/joint_VS.glsl"))
            using (StreamReader fs = new StreamReader("Shaders/joint_FS.glsl"))
                CreateShaders(vs.ReadToEnd(), fs.ReadToEnd(), out vertex_shader_ob,
                    out fragment_shader_ob, out ResourceMgmt.shader_programs[2]);

            vvs = GLSL_Preprocessor.Parser("Shaders/pass_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/pass_FS.glsl");
            //Compile Texture Shaders
            CreateShaders(vvs, ffs, out vertex_shader_ob,
                    out fragment_shader_ob, out ResourceMgmt.shader_programs[3]);

            Debug.WriteLine("Programs {0} {1} {2} {3} ", ResourceMgmt.shader_programs[0],
                                                     ResourceMgmt.shader_programs[1],
                                                     ResourceMgmt.shader_programs[2],
                                                     ResourceMgmt.shader_programs[3]);

            GMDL.scene scene = new GMDL.scene();
            scene.shader_program = ResourceMgmt.shader_programs[1];
            scene.index = this.childCounter;

            this.mainScene = scene;
            this.childCounter++;
            MyTreeNode node = new MyTreeNode("ORIGIN");
            node.model = scene;
            node.Checked = true;
            treeView1.Nodes.Add(node);

            //Set to current cam fov
            numericUpDown1.Value = 35;
            numericUpDown2.Value = (decimal) 5.0;

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
            t.Interval = 20;
            t.Tick += new EventHandler(timer_ticker);
            t.Start();

            //Set GEOMMBIN statusStrip
            GEOMMBIN.strip = this.toolStripStatusLabel1;

            //Set Default JMarray
            for (int i = 0; i < 128; i++)
                Util.insertMatToArray(Util.JMarray, i * 16, Matrix4.Identity);

            int maxfloats;
            GL.GetInteger(GetPName.MaxVertexUniformVectors,out maxfloats);
            toolStripStatusLabel1.Text = "Ready";

            //Query GL Extensions
            Debug.WriteLine("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringName.Extensions).Split(' ');
            foreach (string s in ext)
                Debug.WriteLine(s);

            addDefaultTextures();
            
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


        //glControl Timer
        private void timer_ticker(object sender, EventArgs e)
        {
            //SImply invalidate the gl control
            glControl1.MakeCurrent();
            glControl1.Invalidate();
        }

        
        private void render_scene()
        {
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //Debug.WriteLine("Rendering Scene Cam Position : {0}", this.cam.Position);
            //Debug.WriteLine("Rendering Scene Cam Orientation: {0}", this.cam.Orientation);

            //Render only the first scene for now
            if (this.mainScene != null)
                traverse_render(this.mainScene);
            
            //Render Info

            ////Clear Matrices
            //GL.MatrixMode(MatrixMode.Modelview);
            //GL.LoadIdentity();
            //GL.MatrixMode(MatrixMode.Projection);
            //GL.LoadIdentity();
            
            
            //GL.MatrixMode(MatrixMode.Projection);
            //GL.LoadIdentity();
            //glControl1.SwapBuffers();
        }

        //private bool render_object(GMDL.customVBO vbo)
        //{
        //    Debug.WriteLine("Rendering VBO Object here");

        //    //Bind vertex buffer
        //    GL.BindBuffer(BufferTarget.ArrayBuffer, vbo.vertex_buffer_object);
        //    GL.VertexPointer(3, VertexPointerType.HalfFloat, vbo.vx_size, vbo.vx_stride);

        //    int vpos;
        //    //Vertex attribute
        //    vpos = GL.GetAttribLocation(this.shader_program,"vPosition");
        //    GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.HalfFloat,false, vbo.vx_size, vbo.vx_stride);
        //    GL.EnableVertexAttribArray(vpos);

        //    //Normal Attribute
        //    vpos = GL.GetAttribLocation(this.shader_program, "nPosition");
        //    GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.HalfFloat, false, vbo.vx_size, vbo.n_stride);
        //    GL.EnableVertexAttribArray(vpos);

        //    //Render Elements
        //    GL.DrawElements(BeginMode.Triangles, vbo.iCount, DrawElementsType.UnsignedShort, 0);

        //    return true;
        //}
        
        private void CreateShaders(string vs,string fs, out int vertexObject, 
            out int fragmentObject, out int program)
        {
            int status_code;
            string info;

            vertexObject = GL.CreateShader(ShaderType.VertexShader);
            fragmentObject = GL.CreateShader(ShaderType.FragmentShader);

            //Compile vertex Shader
            GL.ShaderSource(vertexObject, vs);
            GL.CompileShader(vertexObject);
            GL.GetShaderInfoLog(vertexObject, out info);
            GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throw new ApplicationException(info);

            //Compile fragment Shader
            GL.ShaderSource(fragmentObject, fs);

            //HANDLE INCLUDES
            /*

            DEPRECATED SECTION BECAUSE OF THE PREPROCESSOR IMPLEMENTATION
            
            //string commonCode;
            //using (StreamReader cs = new StreamReader("Shaders/common.glsl"))
            //    commonCode = cs.ReadToEnd();
            //string[] common = { "/common.glsl" };
            //int[] length = null;
            //GL.Arb.NamedString(ArbShadingLanguageInclude.ShaderIncludeArb, common[0].Length, common[0], commonCode.Length, commonCode);
            //Debug.WriteLine(GL.Arb.IsNamedString(common[0].Length, common[0]));
            //GL.Arb.CompileShaderInclude(fragmentObject, 1, common, length);

            */
            GL.CompileShader(fragmentObject);
            GL.GetShaderInfoLog(fragmentObject, out info);
            GL.GetShader(fragmentObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throw new ApplicationException(info);

            program = GL.CreateProgram();
            GL.AttachShader(program, fragmentObject);
            GL.AttachShader(program, vertexObject);
            GL.LinkProgram(program);
            //GL.UseProgram(program);

        }

        private void numericUpDown1_ValueChanged(object sender, EventArgs e)
        {
            //Set Camera FOV
            cam.setFOV((int) Math.Max(1, numericUpDown1.Value));
            glControl1.Invalidate();
        }

        private void treeView1_AfterCheck(object sender, TreeViewEventArgs e)
        {
            //Debug.WriteLine("{0} {1}", e.Node.Checked, e.Node.Index);
            //Toggle Renderability of node
            traverse_oblist_rs(((MyTreeNode) e.Node).model, e.Node.Text, e.Node.Checked);
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

            FieldInfo[] fields= t.GetFields();
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

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            light_distance = (float) numericUpDown2.Value;
            glControl1.Invalidate();
        }


        private void traverse_oblist_altid(ref List<string> alt , TreeNode parent)
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
            ob.index = this.childCounter;
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

        private void traverse_oblist_rs(GMDL.model root, string name, bool status)
        {
            setObjectField<bool>("renderable", (GMDL.model)root, status);
            foreach (GMDL.model child in root.children)
                traverse_oblist_rs(child, name, status);
        }

        private void traverse_render(GMDL.model root)
        {
            //GL.LinkProgram(root.ShaderProgram);
            GL.UseProgram(root.shader_program);
            if (root.shader_program == -1)
                throw new ApplicationException("Shit program");

            Matrix4 look = cam.GetViewMatrix();
            float aspect = (float)glControl1.ClientSize.Width / glControl1.ClientSize.Height;
            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(cam.fov, aspect,
                                                                0.1f, 300.0f);
            int loc;
            //Send LookAt matrix to all shaders
            loc = GL.GetUniformLocation(root.shader_program, "look");
            GL.UniformMatrix4(loc, false, ref look);
            //Send object world Matrix to all shaders

            loc = GL.GetUniformLocation(root.shader_program, "worldMat");
            Matrix4 wMat = root.worldMat;
            GL.UniformMatrix4(loc, false, ref wMat);

            //Send projection matrix to all shaders
            loc = GL.GetUniformLocation(root.shader_program, "proj");
            GL.UniformMatrix4(loc, false, ref proj);
            //Send theta to all shaders
            loc = GL.GetUniformLocation(root.shader_program, "theta");
            GL.Uniform3(loc, this.rot);

            if (root.shader_program == ResourceMgmt.shader_programs[0])
            {
                //Object program
                //Local Transformation is the same for all objects 
                //Pending - Personalize local matrix on each object
                loc = GL.GetUniformLocation(root.shader_program, "scale");
                GL.Uniform1(loc, this.scale);

                loc = GL.GetUniformLocation(root.shader_program, "light");

                GL.Uniform3(loc, new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                             (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                             (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0))));

                //Upload Light Intensity
                loc = GL.GetUniformLocation(root.shader_program, "intensity");
                GL.Uniform1(loc, light_intensity);
                
                //Upload camera position as the light
                //GL.Uniform3(loc, cam.Position);

                //Upload firstskinmat
                loc = GL.GetUniformLocation(root.shader_program, "firstskinmat");
                GL.Uniform1(loc, ((GMDL.sharedVBO)root).firstskinmat);

                //loc = GL.GetUniformLocation(root.shader_program, "jMs");
                //GL.UniformMatrix4(loc, 60, false, JMArray);
                
                //Upload joint colors
                //loc = GL.GetUniformLocation(root.shader_program, "jColors");
                //GL.Uniform3(loc, 60, JColors);


            } else if (root.shader_program == ResourceMgmt.shader_programs[1])
            {
                //Locator Program
                //TESTING
            }
            GL.ClearColor(System.Drawing.Color.Black);
            //if (this.index_dict.ContainsKey(root.name))
            root.render();
            //Render children
            foreach (GMDL.model child in root.children){
                this.glControl1.MakeCurrent();
                traverse_render(child);
            }
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
                Util.MbinToExml(descrpath);
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

        //Deprecated
        //private void randomgenerator_Click(object sender, EventArgs e)
        //{
        //    XmlNode level0 = this.xmlDoc.SelectSingleNode("./ROOT/SECTIONS");
        //    List<Selector> sellist = ModelProcGen.parse_level(level0);
        //    List<List<GMDL.model>> allparts = new List<List<GMDL.model>>();
        //    //Create 12 random instances
        //    for (int k = 0; k < 15; k++)
        //    {
        //        List<string> parts = new List<string>();
        //        for (int i = 0; i < sellist.Count; i++)
        //            ModelProcGen.parse_selector(sellist[i], ref parts);

        //        Debug.WriteLine(String.Join(" ", parts.ToArray()));
        //        //Make list of active parts
        //        List<GMDL.model> vboParts = new List<GMDL.model>();
        //        for (int i = 0; i < parts.Count; i++)
        //        {
        //            GMDL.model part = collectPart(this.mainScene.children, parts[i]);
        //            GMDL.model npart = (GMDL.model)part.Clone();
        //            npart.children.Clear();
        //            vboParts.Add(npart);
        //        }

        //        allparts.Add(vboParts);
        //    }
            
        //    /* This code renders changes to the main viewport
        //    //Reset all nodes
        //    foreach (TreeNode node in treeView1.Nodes[0].Nodes)
        //        node.Checked = false;
            
        //    //Temporarity swap tvscheckstatus
        //    this.tvchkstat = treeviewCheckStatus.Single;
        //    for (int i = 0; i < parts.Count; i++)
        //    {
        //        TreeNode node = findNodeFromText(treeView1.Nodes, parts[i]);
        //        if (node !=null)
        //            node.Checked = true;
        //    }

        //    //Bring it back
        //    this.tvchkstat = treeviewCheckStatus.children;

        //    */
            
        //    Form vpwin = new Form();
        //    vpwin.AutoSize = true;
        //    //vpwin.Size = new System.Drawing.Size(800, 600);

        //    TableLayoutPanel table = new TableLayoutPanel();
        //    table.AutoSize = true;
        //    table.RowCount = 3;
        //    table.ColumnCount = 5;
        //    table.Dock = DockStyle.Fill;
        //    //table.Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Left;

        //    List<CGLControl> ctlist = new List<CGLControl>();

        //    for (int i = 0; i < table.RowCount; i++)
        //    {
        //        for (int j = 0; j < table.ColumnCount; j++)
        //        {
        //            CGLControl n = new CGLControl(i * table.ColumnCount + j);
        //            n.objects = allparts[i * table.ColumnCount + j];
        //            n.shader_programs = ResourceMgmt.shader_programs;
        //            table.Controls.Add(n, j, i);
        //            ctlist.Add(n);
        //        }
        //    }

        //    vpwin.Controls.Add(table);

            
        //    vpwin.Show();

        //    foreach (GLControl ctl in ctlist)
        //        ctl.Invalidate();
        //}

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
            render_scene();
            //GL.ClearColor(System.Drawing.Color.Black);
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
                        cam.Move(0.0f, 0.1f, 0.0f);
                    break;
                case Keys.S:
                    for (int i = 0; i < movement_speed; i++)
                        cam.Move(0.0f, -0.1f, 0.0f);
                    break;
                case (Keys.D):
                    for (int i = 0; i < movement_speed; i++)
                        cam.Move(+0.1f, 0.0f, 0.0f);
                    break;
                case Keys.A:
                    for (int i = 0; i < movement_speed; i++)
                        cam.Move(-0.1f, 0.0f, 0.0f);
                    break;
                case (Keys.R):
                    for (int i = 0; i < movement_speed; i++)
                        cam.Move(0.0f, 0.0f, 0.1f);
                    break;
                case Keys.F:
                    for (int i = 0; i < movement_speed; i++)
                        cam.Move(0.0f, 0.0f, -0.1f);
                    break;
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
                //Toggle Wireframe
                case Keys.I:
                    if (RenderOptions.RENDERMODE == PolygonMode.Fill)
                        RenderOptions.RENDERMODE = PolygonMode.Line;
                    else
                        RenderOptions.RENDERMODE = PolygonMode.Fill;
                    break;
                //Toggle Texture Render
                case Keys.O:
                    RenderOptions.UseTextures = !RenderOptions.UseTextures;
                    break;
                //Toggle Small Render
                case Keys.P:
                    RenderOptions.RenderSmall = !RenderOptions.RenderSmall;
                    break;
                //Toggle Collisions Render
                case Keys.OemOpenBrackets:
                    RenderOptions.RenderCollisions = !RenderOptions.RenderCollisions;
                    break;
                default:
                    Debug.WriteLine("Not Implemented Yet");
                    break;
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
                cam.AddRotation(delta_x, delta_y);
                glControl1.Invalidate();
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
                cam.setFOV(newval);
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
            if (m.type == TYPES.MESH)
            {
                //Get converted text
                GMDL.sharedVBO me = (GMDL.sharedVBO)m;
                me.writeGeomToStream(s, ref index);

            }
            foreach (GMDL.model c in m.children)
                if (c.renderable) findGeoms(c, s, ref index);
        }

        private void l_intensity_nud_ValueChanged(object sender, EventArgs e)
        {
            light_intensity = (float) this.l_intensity_nud.Value;
            glControl1.Invalidate();
        }
    }

    //Class Which will store all the texture resources for better memory management
    public static class ResourceMgmt
    {
        public static Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        public static Dictionary<string, GMDL.Material> GLmaterials = new Dictionary<string, GMDL.Material>();
        public static int[] shader_programs;
        public static DebugForm DebugWin = new DebugForm();
    }
}
