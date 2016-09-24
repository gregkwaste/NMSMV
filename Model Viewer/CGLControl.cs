using System;
using System.Collections.Generic;
using System.Windows.Forms;
using OpenTK;
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.IO;

namespace Model_Viewer
{
    class CGLControl : GLControl
    {
        public List<GMDL.model> objects = new List<GMDL.model>();
        public GMDL.model rootObject;


        private Vector3 rot = new Vector3(0.0f, 0.0f, 0.0f);
        private Vector3 target = new Vector3(0.0f, 0.0f, 0.0f);
        private Camera cam = new Camera(60);

        private float light_angle_y = 0.0f;
        private float light_angle_x = 0.0f;
        private float light_distance = 5.0f;
        private float scale = 1.0f;
        public int[] shader_programs;
        //Mouse Pos
        private int mouse_x;
        private int mouse_y;
        //Control Identifier
        private int index;

        //Custom Palette
        private Dictionary<string,Dictionary<string,Vector3>> palette;

        //Animation Stuff
        private int currentFrame = 0;
        private bool animationStatus = false;
        //Animation Meta
        public GMDL.AnimeMetaData meta = new GMDL.AnimeMetaData();

        //Init-GUI Related
        private ContextMenuStrip contextMenuStrip1;
        private System.ComponentModel.IContainer components;
        private ToolStripMenuItem exportToObjToolStripMenuItem;
        private ToolStripMenuItem loadAnimationToolStripMenuItem;
        private OpenFileDialog openFileDialog1;
        private System.ComponentModel.BackgroundWorker backgroundWorker1;

        //Joint Array for shader
        public float[] JMArray = new float[128 * 16];
        public Dictionary<string,GMDL.model> joint_dict = new Dictionary<string,GMDL.model>();
        //public float[] JColors = new float[128 * 3];

        //Constructor
        public CGLControl(int index)
        {
            this.Load += new System.EventHandler(this.genericLoad);
            this.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.Resize += new System.EventHandler(this.genericResize);
            this.MouseHover += new System.EventHandler(this.hover);
            this.MouseMove += new System.Windows.Forms.MouseEventHandler(this.genericMouseMove);
            this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.CGLControl_MouseClick);
            this.PreviewKeyDown += new System.Windows.Forms.PreviewKeyDownEventHandler(this.generic_KeyDown);
            //Set properties
            this.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            this.MinimumSize = new System.Drawing.Size(128, 128);
            this.MaximumSize = new System.Drawing.Size(640, 480);


            //Set Camera position
            for (int i = 0; i < 20; i++)
                cam.Move(0.0f, -0.1f, 0.0f);
            this.rot.Y = 131;
            this.light_angle_y = 190;

            //Set Control Identifiers
            this.index = index;

            //Assign new palette to GLControl
            palette = Model_Viewer.Palettes.createPalette();
        }

        public void SetupItems()
        {
            //This function is used to setup all necessary additional parameters on the objects.
            traverse_oblistPalette(rootObject, palette);
        }

        public void traverse_oblistPalette(GMDL.model root,Dictionary<string,Dictionary<string,Vector3>> palette)
        {
            foreach (GMDL.model m in root.children)
            {
                m.palette = palette;
                if (m.children.Count != 0)
                    traverse_oblistPalette(m, palette);
            }
        }

        private void render_scene()
        {
            this.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            if (this.rootObject != null)
                traverse_render(this.rootObject);

        }

        private void traverse_render(GMDL.model m)
        {
            this.MakeCurrent();
            GL.UseProgram(m.shader_program);
            if (m.shader_program == -1)
            {
                Debug.WriteLine("Shit program, Exiting");
                //throw new ApplicationException("Shit program");
                return;
            }
                
            Matrix4 look = cam.GetViewMatrix();
            //Matrix4 look = Matrix4.Identity;
            float aspect = (float)this.ClientSize.Width / this.ClientSize.Height;
            Matrix4 proj = Matrix4.CreatePerspectiveFieldOfView(cam.fov, aspect,
                                                                0.1f, 300.0f);
            int loc;
            //Send LookAt matrix to all shaders
            loc = GL.GetUniformLocation(m.shader_program, "look");
            GL.UniformMatrix4(loc, false, ref look);
            //Send projection matrix to all shaders
            loc = GL.GetUniformLocation(m.shader_program, "proj");
            GL.UniformMatrix4(loc, false, ref proj);
            //Send theta to all shaders
            loc = GL.GetUniformLocation(m.shader_program, "theta");
            GL.Uniform3(loc, this.rot);

            if (m.shader_program == shader_programs[0])
            {
                //Object program
                //Local Transformation is the same for all objects 
                //Pending - Personalize local matrix on each object
                loc = GL.GetUniformLocation(m.shader_program, "scale");
                GL.Uniform1(loc, this.scale);

                loc = GL.GetUniformLocation(m.shader_program, "light");

                GL.Uniform3(loc, new Vector3((float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Sin(this.light_angle_y * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Sin(this.light_angle_x * Math.PI / 180.0)),
                                                (float)(light_distance * Math.Cos(this.light_angle_x * Math.PI / 180.0) *
                                                            Math.Cos(this.light_angle_y * Math.PI / 180.0))));
                    
                //Upload joint transform data
                //Multiply matrices before sending them
                float[] skinmats = Util.mulMatArrays(((GMDL.sharedVBO)m).vbo.invBMats, JMArray, 128);
                loc = GL.GetUniformLocation(m.shader_program, "skinMats");
                GL.UniformMatrix4(loc, 128, false, skinmats);

            }
            else if (m.shader_program == shader_programs[1])
            {
                //Locator Program
            }
            GL.ClearColor(System.Drawing.Color.Black);
            //Render Object
            m.render();
            //Render Children
            if (m.children!= null)
                foreach (GMDL.model child in m.children)
                    traverse_render(child);
        }

        private void genericLoad(object sender, EventArgs e)
        {

            this.InitializeComponent();
            this.Size = new System.Drawing.Size(640, 480);
            this.MakeCurrent();
            GL.Viewport(0, 0, this.ClientSize.Width, this.ClientSize.Height);
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.DepthTest);
            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            //Debug.WriteLine("GL Cleared");
            //Debug.WriteLine(GL.GetError());
        }

        private void genericPaint(object sender, EventArgs e)
        {
            this.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            render_scene();

            this.SwapBuffers();
        }

        private void hover(object sender, EventArgs e)
        {
            this.Focus();
            this.Invalidate();
        }

        private void genericMouseMove(object sender, MouseEventArgs e)
        {
            //Debug.WriteLine("Mouse moving on {0}", this.TabIndex);
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
                this.Invalidate();
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
            this.Invalidate();
        }

        private void genericResize(object sender, EventArgs e)
        {
            if (this.ClientSize.Height == 0)
                this.ClientSize = new System.Drawing.Size(this.ClientSize.Width, 1);
            //Debug.WriteLine("GLControl {0} Resizing {1}x{2}",this.index, this.ClientSize.Width, this.ClientSize.Height);
            this.MakeCurrent();
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
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.exportToObjToolStripMenuItem,
            this.loadAnimationToolStripMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(160, 48);
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
                contextMenuStrip1.Show(System.Windows.Forms.Control.MousePosition);
            }
        }

        private void exportToObjToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Exporting to obj");
            Debug.WriteLine("Not Implemented YET");
        }

        private void loadAnimationToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Loading Animation");
            //Opening Animation File
            Debug.WriteLine("Opening Animation File");

            DialogResult res = openFileDialog1.ShowDialog();
            var filename = openFileDialog1.FileName;

            if (res == DialogResult.Cancel)
                return;

            FileStream fs = new FileStream(filename, FileMode.Open);
            meta = new GMDL.AnimeMetaData();
            meta.Load(fs);

            fs.Close();



        }


        //Animation Playback
        private void backgroundWorker1_DoWork(object sender, System.ComponentModel.DoWorkEventArgs e)
        {
            int val = currentFrame;
            int max = meta.frameCount;
            int i = val;
            while (true)
            {
                //Reset
                if (i == max) i = 0;
                System.Threading.Thread.Sleep(50);
                backgroundWorker1.ReportProgress(i);
                i += 1;

                if (backgroundWorker1.CancellationPending)
                {
                    e.Cancel = true;
                    return;
                }
            }

        }

        private void backgroundWorker1_ProgressChanged(object sender, System.ComponentModel.ProgressChangedEventArgs e)
        {
            updateAnim(e.ProgressPercentage);
        }

        private void updateAnim(int frameIndex)
        {
            //Debug.WriteLine("Setting Frame Index {0}", frameIndex);
            GMDL.AnimNodeFrameData frame = new GMDL.AnimNodeFrameData();
            frame = meta.frameData.frames[frameIndex];

            foreach (GMDL.AnimeNode node in meta.nodeData.nodeList)
            {
                if (joint_dict.ContainsKey(node.name))
                {
                    //Check if there is a rotation for that node
                    if (node.rotIndex < frame.rotations.Count - 1)
                        joint_dict[node.name].localRotation = Matrix3.CreateFromQuaternion(frame.rotations[node.rotIndex]);

                    //Matrix4 newrot = Matrix4.CreateFromQuaternion(frame.rotations[node.rotIndex]);
                    if (node.transIndex < frame.translations.Count - 1)
                        joint_dict[node.name].localPosition = frame.translations[node.transIndex];

                }
                //Debug.WriteLine("Node " + node.name+ " {0} {1} {2}",node.rotIndex,node.transIndex,node.scaleIndex);
            }

            //Update JMArrays
            foreach (GMDL.model joint in joint_dict.Values)
            {
                GMDL.Joint j = (GMDL.Joint)joint;
                Util.insertMatToArray(JMArray, j.jointIndex * 16, j.worldMat);
            }

            this.Invalidate();

        }


    }


}
