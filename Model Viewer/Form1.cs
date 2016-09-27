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
        int shader_program_ob;
        //Shader locators
        //int vertex_shader_loc;
        //int fragment_shader_loc;
        //int shader_program_loc;

        //Setup Timer to invalidate the viewport
        private Timer t;
        

        private List<GMDL.GeomObject> geomobjects = new List<GMDL.GeomObject>();
        private List<GMDL.model> vboobjects = new List<GMDL.model>();
        //private GMDL.model rootObject;
        private List<GMDL.model> scenes = new List<GMDL.model>();
        private XmlDocument xmlDoc = new XmlDocument();
        private Dictionary<string, int> index_dict = new Dictionary<string, int>();
        private OrderedDictionary joint_dict = new OrderedDictionary();
        private treeviewCheckStatus tvchkstat = treeviewCheckStatus.Children;

        //Animation Meta
        public GMDL.AnimeMetaData meta = new GMDL.AnimeMetaData();

        //Joint Array for shader
        public float[] JMArray = new float[128 * 16];
        public float[] JColors = new float[128 * 3];

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

            if (ext != "MBIN")
            {
                Debug.WriteLine("Not an MBIN file");
                return;
            }

            string exmlPath = Util.getExmlPath(filename);

            //Parse the Scene XML file
            Debug.WriteLine("Parsing SCENE XML");
            
            //Convert only if file does not exist
            if (!File.Exists(exmlPath))
            {
                Debug.WriteLine("Exml does not exist");
                Util.MbinToExml(filename);
            }

            //Open exml
            this.xmlDoc.Load(exmlPath);
            

            //Initialize Palettes
            Model_Viewer.Palettes.set_palleteColors();
            
            splitContainer1.Panel2.Controls.Clear();
            //Clear opengl
            ResourceMgmt.GLtextures.Clear();
            setup_GLControl();
            splitContainer1.Panel2.Controls.Add(glControl1);

            glControl1.Update();
            glControl1.MakeCurrent();
            GMDL.model scene;
            scene = GEOMMBIN.LoadObjects(this.xmlDoc);
            scene.index = this.childCounter;
            this.scenes.Clear();
            this.scenes.Add(scene);
            this.childCounter++;

            //Debug.WriteLine("Objects Returned: {0}",oblist.Count);
            TreeNode node = new TreeNode(scene.name);
            node.Checked = true;
            //Clear index dictionary
            index_dict.Clear();
            joint_dict.Clear();
            GC.Collect();
            this.childCounter = 0;
            //Add root to dictionary
            index_dict[scene.name] = this.childCounter;
            this.childCounter += 1;
            //Set indices and TreeNodes 
            traverse_oblist(this.scenes[0], node);
            //Add root to treeview
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(node);

            //vboobjects.Add(new GMDL.customVBO(GEOMMBIN.Parse(fs)));
            glControl1.Invalidate();
        }

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
            this.glControl1.MouseWheel += new System.Windows.Forms.MouseEventHandler(this.glControl1_Scroll);
            this.glControl1.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.glControl1_KeyDown);
            this.glControl1.Resize += new System.EventHandler(this.glControl1_Resize);
        }

        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (!this.glloaded)
                return;
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit| ClearBufferMask.DepthBufferBit);
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

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;

            //Populate shader list
            ResourceMgmt.shader_programs = new int[3];

            //Compile Object Shaders
            using (StreamReader vs = new StreamReader("Shaders/Simple_VS.glsl"))
            using (StreamReader fs = new StreamReader("Shaders/Simple_FS.glsl"))
                CreateShaders(vs.ReadToEnd(), fs.ReadToEnd(), out vertex_shader_ob,
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

            Debug.WriteLine("Programs {0} {1} {2} ", ResourceMgmt.shader_programs[0],
                                                     ResourceMgmt.shader_programs[1],
                                                     ResourceMgmt.shader_programs[2]);

            GMDL.model scene = new GMDL.locator();
            scene.shader_program = ResourceMgmt.shader_programs[1];
            scene.index = this.childCounter;

            this.scenes.Add(scene);
            this.childCounter++;
            TreeNode node = new TreeNode("ORIGIN");
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

            //Setup the timer
            t = new Timer();
            t.Interval = 20;
            t.Tick += new EventHandler(timer_ticker);
            t.Start();
            
        }


        private void timer_ticker(object sender, EventArgs e)
        {
            //SImply invalidate the gl control
            glControl1.Invalidate();
        }

        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;
            if (glControl1.ClientSize.Height == 0)
                glControl1.ClientSize = new System.Drawing.Size(glControl1.ClientSize.Width, 1);
            Debug.WriteLine("GLControl Resizing");
            Debug.WriteLine(this.eye_pos.X.ToString() + " "+ this.eye_pos.Y.ToString());
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
                    for (int i=0;i<movement_speed;i++)
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
                default:
                    Debug.WriteLine("Not Implemented Yet");
                    break;
            }
            //glControl1.Invalidate();
            
        }

        private void render_scene()
        {
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //Debug.WriteLine("Rendering Scene Cam Position : {0}", this.cam.Position);
            //Debug.WriteLine("Rendering Scene Cam Orientation: {0}", this.cam.Orientation);

            //Render only the first scene for now
            if (this.scenes[0] != null)
                traverse_render(this.scenes[0]);
            
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
                newval = (int) Math.Min(Math.Max(newval, numericUpDown1.Minimum), numericUpDown1.Maximum);
                cam.setFOV(newval);
                numericUpDown1.Value = newval;
                
                //eye.Z += e.Delta * 0.2f;
                glControl1.Invalidate();
            }

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
            
            //glControl1.Invalidate();
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
            //glControl1.Invalidate();
        }

        private void traverse_oblist(GMDL.model ob, TreeNode parent)
        {
            if (ob.children.Count > 0)
            {
                List<GMDL.model> duplicates = new List<GMDL.model>();
                foreach (GMDL.model child in ob.children)
                {
                    //Keep only Meshes, Locators and Joints
                    //if (child.type != TYPES.MESH & child.type != TYPES.LOCATOR & child.type != TYPES.JOINT & child.type != TYPES.SCENE)
                    //    continue;
                    //Check if Shape object
                    //if (child.Name.Contains("Shape"))
                    //    continue;
                    

                    //Don't Save Duplicates
                    //if (!index_dict.ContainsKey(child.name))
                    //{
                        //Set object index
                        child.index = this.childCounter;
                        //this.index_dict.Add(child.name, child.index);
                        //Add only joints to joint dictionary
                        if (child.type == TYPES.JOINT)
                        {
                            GMDL.Joint temp = (GMDL.Joint) child;
                            if (!joint_dict.Contains(child.name))
                                this.joint_dict.Add(child.name, child);
                            Util.insertMatToArray(this.JMArray, temp.jointIndex*16, temp.worldMat);
                            //Insert color to joint color array
                            JColors[temp.jointIndex * 3 + 0] = temp.color.X;
                            JColors[temp.jointIndex * 3 + 1] = temp.color.Y;
                            JColors[temp.jointIndex * 3 + 2] = temp.color.Z;
                        }
                            
                        this.childCounter++;
                        MyTreeNode node = new MyTreeNode(child.name);
                        node.model = child; //Reference model
                        
                        //Debug.WriteLine("Testing Geom {0}  Node {1}", child.Index, node.Index);
                        node.Checked = true;
                        parent.Nodes.Add(node);
                        traverse_oblist(child, node);
                    //}
                    //else
                    //{
                    //    Debug.WriteLine("Duplicate {0} {1}", child.name,ob.children.Count);
                    //    //duplicates.Add(child);
                    //}
                }
                //Remove duplicates
                foreach (GMDL.model dupl in duplicates)
                {
                    ob.children.Remove(dupl);
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
                //Upload firstskinmat
                loc = GL.GetUniformLocation(root.shader_program, "firstskinmat");
                GL.Uniform1(loc, ((GMDL.sharedVBO)root).firstskinmat);

                //Upload joint transform data
                //Multiply matrices before sending them
                float[] skinmats = Util.mulMatArrays(((GMDL.sharedVBO) root).vbo.invBMats,JMArray,128);
                loc = GL.GetUniformLocation(root.shader_program, "skinMats");
                GL.UniformMatrix4(loc, 128, false, skinmats);

                //loc = GL.GetUniformLocation(root.shader_program, "jMs");
                //GL.UniformMatrix4(loc, 60, false, JMArray);
                
                //Upload joint colors
                loc = GL.GetUniformLocation(root.shader_program, "jColors");
                GL.Uniform3(loc, 60, JColors);


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
            GC.Collect();
            t.Stop();
            //Construct Descriptor Path
            string[] split = mainFilePath.Split('.');
            string descrpath = "";
            for (int i = 0; i< split.Length-2; i++)
                descrpath = Path.Combine(descrpath, split[i]);
            descrpath += ".DESCRIPTOR.MBIN";

            string exmlPath = Util.getExmlPath(descrpath);
            Debug.WriteLine("Opening " + descrpath);

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
            ProcGenForm vpwin = new ProcGenForm();
            vpwin.parentForm = this; //Set parent to this form
            vpwin.FormClosed += new FormClosedEventHandler(this.resumeTicker);
            vpwin.Text = "Procedural Generated Models";
            // no smaller than design time size
            vpwin.MinimumSize = new System.Drawing.Size(5 * 300, 3 * 256);
            // no larger than screen size
            vpwin.MaximumSize = new System.Drawing.Size(1920, 1080);
            vpwin.FormBorderStyle = FormBorderStyle.Sizable;
            //vpwin.AutoSize = true;
            //vpwin.AutoSizeMode = AutoSizeMode.GrowAndShrink;

            TableLayoutPanel table = new TableLayoutPanel();
            table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            table.RowCount = 3;
            table.ColumnCount = 5;
            table.Dock = DockStyle.Fill;
            //table.Anchor= AnchorStyles.Bottom
            //table.Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Left;

            //Fix RowStyles
            for (int i = 0; i < table.RowCount; i++)
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100F / table.RowCount));

            //Fix ColumnStyles
            for (int i = 0; i < table.ColumnCount; i++)
                table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F / table.ColumnCount));


            for (int i = 0; i < table.RowCount; i++)
            {
                for (int j = 0; j < table.ColumnCount; j++)
                {
                    //Create New GLControl
                    CGLControl n = new CGLControl(i * table.ColumnCount + j);
                    n.MakeCurrent(); //Make current

                    //----PROC GENERATION----
                    List<string> parts = new List<string>();
                    ModelProcGen.parse_descriptor(ref parts, root);

                    Debug.WriteLine(String.Join(" ", parts.ToArray()));
                    GMDL.model m;
                    m = ModelProcGen.get_procgen_parts(ref parts, this.scenes[0]);
                    //----PROC GENERATION----

                    n.rootObject = m;
                    n.shader_programs = ResourceMgmt.shader_programs;

                    //Send animation data
                    n.JMArray = (float[])JMArray.Clone();

                    Dictionary<string, GMDL.model> clonedDict = new Dictionary<string, GMDL.model>();
                    try
                    {
                        cloneJointDict(ref clonedDict, ((GMDL.model) joint_dict[0]).Clone());
                        n.joint_dict = clonedDict;
                    }
                    catch (ArgumentOutOfRangeException ex)
                    {
                        //Debug.WriteLine("Omitting Joint Dict, There are no joints");
                    }

                    n.SetupItems();
                    table.Controls.Add(n, j, i);
                    n.Invalidate();
                }
            }

            vpwin.Controls.Add(table);
            vpwin.Show();
            
        }

        private void resumeTicker(object sender, EventArgs e)
        {
            Form1 f = ((ProcGenForm) sender).parentForm;
            //Start the timer
            f.t.Start();
        }

        private void cloneJointDict(ref Dictionary<string,GMDL.model> jointdict, GMDL.model root)
        {
            jointdict[root.name] = root;
            foreach (GMDL.model child in root.children)
                cloneJointDict(ref jointdict, child);
        }

        private void randomgenerator_Click(object sender, EventArgs e)
        {
            XmlNode level0 = this.xmlDoc.SelectSingleNode("./ROOT/SECTIONS");
            List<Selector> sellist = ModelProcGen.parse_level(level0);
            List<List<GMDL.model>> allparts = new List<List<GMDL.model>>();
            //Create 12 random instances
            for (int k = 0; k < 15; k++)
            {
                List<string> parts = new List<string>();
                for (int i = 0; i < sellist.Count; i++)
                    ModelProcGen.parse_selector(sellist[i], ref parts);

                Debug.WriteLine(String.Join(" ", parts.ToArray()));
                //Make list of active parts
                List<GMDL.model> vboParts = new List<GMDL.model>();
                for (int i = 0; i < parts.Count; i++)
                {
                    GMDL.model part = collectPart(this.scenes[0].children, parts[i]);
                    GMDL.model npart = (GMDL.model)part.Clone();
                    npart.children.Clear();
                    vboParts.Add(npart);
                }

                allparts.Add(vboParts);
            }
            
            /* This code renders changes to the main viewport
            //Reset all nodes
            foreach (TreeNode node in treeView1.Nodes[0].Nodes)
                node.Checked = false;
            
            //Temporarity swap tvscheckstatus
            this.tvchkstat = treeviewCheckStatus.Single;
            for (int i = 0; i < parts.Count; i++)
            {
                TreeNode node = findNodeFromText(treeView1.Nodes, parts[i]);
                if (node !=null)
                    node.Checked = true;
            }

            //Bring it back
            this.tvchkstat = treeviewCheckStatus.children;

            */
            
            Form vpwin = new Form();
            vpwin.AutoSize = true;
            //vpwin.Size = new System.Drawing.Size(800, 600);

            TableLayoutPanel table = new TableLayoutPanel();
            table.AutoSize = true;
            table.RowCount = 3;
            table.ColumnCount = 5;
            table.Dock = DockStyle.Fill;
            //table.Anchor = AnchorStyles.Bottom | AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Left;

            List<CGLControl> ctlist = new List<CGLControl>();

            for (int i = 0; i < table.RowCount; i++)
            {
                for (int j = 0; j < table.ColumnCount; j++)
                {
                    CGLControl n = new CGLControl(i * table.ColumnCount + j);
                    n.objects = allparts[i * table.ColumnCount + j];
                    n.shader_programs = ResourceMgmt.shader_programs;
                    table.Controls.Add(n, j, i);
                    ctlist.Add(n);
                }
            }

            vpwin.Controls.Add(table);

            
            vpwin.Show();

            foreach (GLControl ctl in ctlist)
            {
                ctl.Invalidate();
            }

        }

        private void glControl1_MouseHover(object sender, EventArgs e)
        {
            glControl1.Focus();
            //glControl1.Invalidate();
        }

        private void openAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Opening Animation File
            Debug.WriteLine("Opening File");

            DialogResult res = openFileDialog1.ShowDialog();
            var filename = openFileDialog1.FileName;

            if (res == DialogResult.Cancel)
                return;
            
            FileStream fs = new FileStream(filename,FileMode.Open);
            meta = new GMDL.AnimeMetaData();
            meta.Load(fs);

            //Update GUI
            frameBox.Enabled = true;
            frameBox.Minimum = 0;
            frameBox.Maximum = meta.frameCount - 1;


            fs.Close();

        }

        private void frameBox_ValueChanged(object sender, EventArgs e)
        {
            //Get FrameIndex
            int frameIndex = (int) frameBox.Value;
            //Debug.WriteLine("Setting Frame Index {0}", frameIndex);
            GMDL.AnimNodeFrameData frame = new GMDL.AnimNodeFrameData();
            frame = meta.frameData.frames[frameIndex];
            
            foreach (GMDL.AnimeNode node in meta.nodeData.nodeList)
            {
                if (joint_dict.Contains(node.name))
                {
                    //Check if there is a rotation for that node
                    if (node.rotIndex < frame.rotations.Count - 1)
                        ((GMDL.model) joint_dict[node.name]).localRotation = Matrix3.CreateFromQuaternion(frame.rotations[node.rotIndex]);
                    
                    //Matrix4 newrot = Matrix4.CreateFromQuaternion(frame.rotations[node.rotIndex]);
                    if (node.transIndex < frame.translations.Count - 1)
                        ((GMDL.model)joint_dict[node.name]).localPosition = frame.translations[node.transIndex];

                }
                //Debug.WriteLine("Node " + node.name+ " {0} {1} {2}",node.rotIndex,node.transIndex,node.scaleIndex);
            }

            //Update JMArrays
            foreach (GMDL.model joint in joint_dict.Values)
            {
                GMDL.Joint j = (GMDL.Joint) joint;
                Util.insertMatToArray(JMArray, j.jointIndex * 16, j.worldMat);
            }

            //glControl1.Invalidate();

        }



        //Animation Playback
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            int val = (int) frameBox.Value;
            int max = (int) frameBox.Maximum;
            int i = val;
            while (true)
            {
                //Reset
                if (i >= max) i = 0;
                System.Threading.Thread.Sleep(50);
                i += 1;

                backgroundWorker1.ReportProgress(i);

                if (backgroundWorker1.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
            }
            
        }
        
        private void newButton1_Click(object sender, EventArgs e)
        {
            if (newButton1.status)
            {
                backgroundWorker1.RunWorkerAsync();
            } else
            {
                backgroundWorker1.CancelAsync();
            }
            newButton1.status = !newButton1.status;
            newButton1.Invalidate();
        }

        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            frameBox.Value = e.ProgressPercentage;
        }

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

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
    }

    //Class Which will store all the texture resources for better memory management
    public static class ResourceMgmt
    {
        public static Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();

        public static int[] shader_programs;
    }
}
