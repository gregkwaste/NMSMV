using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.IO;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Collections.Generic;
using System.Xml;

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
        private int[] shader_programs;
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
        int vertex_shader_loc;
        int fragment_shader_loc;
        int shader_program_loc;

        private List<GMDL.GeomObject> geomobjects = new List<GMDL.GeomObject>();
        private List<GMDL.model> vboobjects = new List<GMDL.model>();
        private GMDL.model rootObject;
        private XmlDocument xmlDoc;
        private Dictionary<string, int> index_dict = new Dictionary<string, int>();
        private Dictionary<string, GMDL.model> joint_dict = new Dictionary<string, GMDL.model>();
        private treeviewCheckStatus tvchkstat = treeviewCheckStatus.Children;

        //Animation Meta
        public GMDL.AnimeMetaData meta = new GMDL.AnimeMetaData();

        //Joint Array for shader
        public float[] JMArray = new float[60 * 16];



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

            var split = filename.Split('.');
            var ext = split[split.Length - 1].ToUpper();
            Debug.WriteLine(ext);

            //if (ext != "MBIN")
            //{
            //    Debug.WriteLine("Not an MBIN file");
            //    return;
            //}

            var fs = new FileStream(filename, FileMode.Open);
            //geomobjects.Add(GEOMMBIN.Parse(fs));
            XmlDocument xml = new XmlDocument();
            Debug.WriteLine("Parsing SCENE XML");
            this.xmlDoc = SCENEMBIN.Parse(fs);
            
            //Store path locally for now
            //string dirpath = "J:\\Installs\\Steam\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";

            this.rootObject = GEOMMBIN.LoadObjects(Util.dirpath, this.xmlDoc, shader_programs);
            this.rootObject.index = this.childCounter;
            this.childCounter++;

            //Debug.WriteLine("Objects Returned: {0}",oblist.Count);
            TreeNode node = new TreeNode("ROOT_LOC");
            node.Checked = true;
            //Clear index dictionary
            index_dict.Clear();
            this.childCounter = 0;
            //Add root to dictionary
            index_dict["ROOT_LOC"] = this.childCounter;
            this.childCounter += 1;
            //Set indices and TreeNodes 
            traverse_oblist(this.rootObject, node);
            //Add root to treeview
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(node);

            //vboobjects.Add(new GMDL.customVBO(GEOMMBIN.Parse(fs)));
            glControl1.Invalidate();
            fs.Close();
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

            //glControl1.Invalidate();
            //Debug.WriteLine("Painting Control");
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;
            //Compile Object Shaders
            using (StreamReader vs = new StreamReader("Shaders/Simple_VS.glsl"))
            using (StreamReader fs = new StreamReader("Shaders/Simple_FS.glsl"))
                CreateShaders(vs.ReadToEnd(), fs.ReadToEnd(), out vertex_shader_ob,
                    out fragment_shader_ob, out shader_program_ob);
            //Compile Locator Shaders
            using (StreamReader vs = new StreamReader("Shaders/locator_VS.glsl"))
            using (StreamReader fs = new StreamReader("Shaders/locator_FS.glsl"))
                CreateShaders(vs.ReadToEnd(), fs.ReadToEnd(), out vertex_shader_loc,
                    out fragment_shader_loc, out shader_program_loc);

            //Populate shader list
            this.shader_programs = new int[2] { this.shader_program_ob, this.shader_program_loc };
            Debug.WriteLine("Programs {0} {1}", shader_programs[0], shader_programs[1]);
            this.rootObject = new GMDL.locator();
            this.rootObject.shader_program = shader_programs[1];
            this.rootObject.index = this.childCounter;
            this.childCounter++;
            TreeNode node = new TreeNode("ORIGIN");
            node.Checked = true;
            treeView1.Nodes.Add(node);

            //Set to current cam fov
            numericUpDown1.Value = 35;
            numericUpDown2.Value = (decimal) 5.0;
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
                    this.rot.Y -= 4.0f;
                    break;
                case Keys.E:
                    this.rot.Y += 4.0f;
                    break;
                case Keys.Z:
                    this.rot.X -= 4.0f;
                    break;
                case Keys.C:
                    this.rot.X += 4.0f;
                    break;
                //Camera Movement
                case Keys.W:
                    cam.Move(0.0f, 0.1f, 0.0f);
                    break;
                case Keys.S:
                    cam.Move(0.0f, -0.1f, 0.0f);
                    break;
                case (Keys.D):
                    cam.Move(+0.1f, 0.0f, 0.0f);
                    break;
                case Keys.A:
                    cam.Move(-0.1f, 0.0f, 0.0f);
                    break;
                case (Keys.R):
                    cam.Move(0.0f, 0.0f, 0.1f);
                    break;
                case Keys.F:
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
                default:
                    Debug.WriteLine("Not Implemented Yet");
                    break;
            }
            glControl1.Invalidate();
            
        }

        private void render_scene()
        {
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //Debug.WriteLine("Rendering Scene Cam Position : {0}", this.cam.Position);
            //Debug.WriteLine("Rendering Scene Cam Orientation: {0}", this.cam.Orientation);

            if (this.rootObject != null)
            {
                traverse_render(this.rootObject);
            }
            
            
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
            traverse_oblist_rs(this.rootObject, this.index_dict[e.Node.Text], e.Node.Checked);
            //Handle Children in treeview
            if (this.tvchkstat == treeviewCheckStatus.Children)
            {
                foreach (TreeNode node in e.Node.Nodes)
                    node.Checked = e.Node.Checked;
            }
            
            glControl1.Invalidate();
        }

        private void numericUpDown2_ValueChanged(object sender, EventArgs e)
        {
            light_distance = (float) numericUpDown2.Value;
            glControl1.Invalidate();
        }

        private void traverse_oblist(GMDL.model ob, TreeNode parent)
        {
            if (ob.children.Count > 0)
            {
                List<GMDL.model> duplicates = new List<GMDL.model>();
                foreach (GMDL.model child in ob.children)
                {
                    //Keep only Meshes, Locators and Joints
                    if (child.type != "MESH" & child.type != "LOCATOR" & child.type !="JOINT")
                        continue;
                    //Check if Shape object
                    //if (child.Name.Contains("Shape"))
                    //    continue;
                    

                    //Don't Save Duplicates
                    if (!index_dict.ContainsKey(child.name))
                    {
                        //Set object index
                        child.index = this.childCounter;
                        this.index_dict.Add(child.name.ToUpper(), child.index);
                        //Add only joints to joint dictionary
                        if (child.type == "JOINT")
                        {
                            GMDL.Joint temp = (GMDL.Joint) child;
                            this.joint_dict.Add(child.name.ToUpper(), child);
                            insertMatToArray(this.JMArray, temp.jointIndex*16, temp.worldMat);
                        }
                            
                        this.childCounter++;
                        TreeNode node = new TreeNode(child.name);
                        
                        //Debug.WriteLine("Testing Geom {0}  Node {1}", child.Index, node.Index);
                        node.Checked = true;
                        parent.Nodes.Add(node);
                        traverse_oblist(child, node);
                    }
                    else
                    {
                        Debug.WriteLine("Duplicate {0} {1}", child.name,ob.children.Count);
                        duplicates.Add(child);
                    }
                }
                //Remove duplicates
                foreach (GMDL.model dupl in duplicates)
                {
                    ob.children.Remove(dupl);
                }
            }
        }

        private void traverse_oblist_rs(GMDL.model root, int index, bool status)
        {
            if (root.index == index)
            {
                //If you found the index toggle all children
                //Debug.WriteLine("Toggling Renderability on {0}", root.name);
                root.renderable = status;

                //foreach (GMDL.model child in root.children)
                //{
                //    child.Renderable = !child.Renderable;
                //    Debug.WriteLine("Toggling Renderability on {0}", child.name);
                //    traverse_oblist_rs(child, index);
                //}
            }
            else
            {
                //If not continue traversing the children
                foreach (GMDL.model child in root.children)
                {
                    traverse_oblist_rs(child, index, status);
                }
            }
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
            //Send World Position to all shaders
            loc = GL.GetUniformLocation(root.shader_program, "worldTrans");
            GL.Uniform3(loc, root.worldPosition);
            //Send local Rotation Matrix to all shaders
            loc = GL.GetUniformLocation(root.shader_program, "worldRot");
            Matrix4 wMat = root.worldMat;
            GL.UniformMatrix4(loc, false, ref wMat);


            //Send projection matrix to all shaders
            loc = GL.GetUniformLocation(root.shader_program, "proj");
            GL.UniformMatrix4(loc, false, ref proj);
            //Send theta to all shaders
            loc = GL.GetUniformLocation(root.shader_program, "theta");
            GL.Uniform3(loc, this.rot);

            if (root.shader_program == shader_programs[0])
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

                //Upload joint data
                loc = GL.GetUniformLocation(root.shader_program, "jMs");
                GL.UniformMatrix4(loc, 60, false, JMArray);


            } else if (root.shader_program == shader_programs[1])
            {
                //Locator Program
            }
            GL.ClearColor(System.Drawing.Color.Black);
            if (this.index_dict.ContainsKey(root.name))
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
                    GMDL.model part = collectPart(this.rootObject.children, parts[i]);
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
                    CGLControl n = new CGLControl();
                    n.objects = allparts[i * table.ColumnCount + j];
                    n.shader_programs = shader_programs;
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
            glControl1.Invalidate();
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
                //Check if there is a rotation for that node
                if (node.rotIndex < frame.rotations.Count - 1)
                {
                    if (joint_dict.ContainsKey(node.name))
                    {
                        Matrix4 newrot = Matrix4.CreateFromQuaternion(frame.rotations[node.rotIndex]);
                        joint_dict[node.name].localMat = newrot;
                    }
                
                }
                //Debug.WriteLine("Node " + node.name+ " {0} {1} {2}",node.rotIndex,node.transIndex,node.scaleIndex);
            }

            //Update JMArrays
            foreach (GMDL.model joint in joint_dict.Values)
            {
                GMDL.Joint j = (GMDL.Joint) joint;
                insertMatToArray(JMArray, j.jointIndex * 16, j.worldMat);
            }


            glControl1.Invalidate();

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
                System.Threading.Thread.Sleep(100);
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

        //Add matrix to JMArray
        private void insertMatToArray(float[] array, int offset, Matrix4 mat)
        {
            array[offset + 0] = mat.M11;
            array[offset + 1] = mat.M12;
            array[offset + 2] = mat.M13;
            array[offset + 3] = mat.M14;
            array[offset + 4] = mat.M21;
            array[offset + 5] = mat.M22;
            array[offset + 6] = mat.M23;
            array[offset + 7] = mat.M24;
            array[offset + 8] = mat.M31;
            array[offset + 9] = mat.M32;
            array[offset + 10] = mat.M33;
            array[offset + 11] = mat.M34;
            array[offset + 12] = mat.M41;
            array[offset + 13] = mat.M42;
            array[offset + 14] = mat.M43;
            array[offset + 15] = mat.M44;
        }

    }
}
