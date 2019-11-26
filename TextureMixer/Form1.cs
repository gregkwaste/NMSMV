using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using OpenTK.Graphics.OpenGL4;
using OpenTK;
using KUtility;
using AntTweakBar;
using GLSLHelper;
using MVCore.GMDL;
using Model_Viewer;

namespace TextureMixer
{
    public partial class Form1 : Form
    {
        private bool glloaded;
        private Context context;
        private int shader_program;

        //QUAD GEOMETRY
        private float[] quad = new float[6 * 3] {
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f };

        private float[] quadcolors = new float[6 * 3]
        {
                1.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f,  0.0f, 1.0f
        };

        //Indices
        private int[] indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

        private int quad_vbo, quad_ebo;

        //TEXTURE INFO

        //private List<Texture> diffTextures = new List<Texture>(8);
        //private List<Texture> maskTextures = new List<Texture>(8);
        //private float[] baseLayersUsed = new float[8];
        //private float[] alphaLayersUsed = new float[8];
        private List<float[]> reColours = new List<float[]>(8);

        //Default Textures
        private Texture dDiff;
        private Texture dMask;

        //Rendering Options
        private float hasAlphaChannel = 0;
        private int renderMode = 0;

        public Form1()
        {
            InitializeComponent();

            //CheckBoxes
            this.diffuseList.MouseUp += new System.Windows.Forms.MouseEventHandler(this.List_RightClick);
            this.diffuseList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.List_SelectOnClick);
            this.alphaList.MouseUp += new System.Windows.Forms.MouseEventHandler(this.List_RightClick);
            this.alphaList.MouseDown += new System.Windows.Forms.MouseEventHandler(this.List_SelectOnClick);

            //GL Control
            this.glControl1.Load += new System.EventHandler(this.glControl_Load);
            this.glControl1.Paint += new System.Windows.Forms.PaintEventHandler(this.glControl1_Paint);
            this.glControl1.Resize += new System.EventHandler(this.glControl1_Resize);

            //Init the texture mixer
            MVCore.GMDL.TextureMixer.clear();

            //Init Utils
            MVCore.FileUtils.dirpath = ""; // Init to empty string

        }
        
        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (!this.glloaded)
                return;
            glControl1.MakeCurrent();
            glControl1_Render();
            glControl1.SwapBuffers();
            
            
            //translate_View();
            ////Draw scene
            //GL.MatrixMode(MatrixMode.Modelview);
            //Update Joystick 

            //glControl1.Invalidate();
            //Debug.WriteLine("Painting Control");
        }

        private void glControl_Load(object sender, EventArgs e)
        {
            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            Debug.WriteLine("GL Cleared");
            Debug.WriteLine(GL.GetError());

            this.glloaded = true;

            //Initialize REsource Manager
            MVCore.Common.RenderState.activeResMgr = new MVCore.ResourceManager();

            //Init Default Textures
            dDiff = new Texture("default.dds");
            dDiff.name = "default.dds";
            dMask = new Texture("default_mask.dds");
            dMask.name = "default_mask.dds";

            MVCore.Common.RenderState.activeResMgr.texMgr.addTexture(dDiff);
            MVCore.Common.RenderState.activeResMgr.texMgr.addTexture(dMask);

            //Add default primitives
            addDefaultPrimitives();

            //Generate Geometry VBOs
            GL.GenBuffers(1, out quad_vbo);
            GL.GenBuffers(1, out quad_ebo);

            //Bind Geometry Buffers
            int arraysize = sizeof(float) * 6 * 3;
            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, quad);
            //Add color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, quadcolors);

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * 6), indices, BufferUsageHint.StaticDraw);


            //Compile Shaders
            compileShaders();
            
            //Setup default program
            GL.UseProgram(shader_program);

            context = new Context(Tw.GraphicsAPI.OpenGL);
            ////Add stuff to context
            //var configsBar = new Bar(context);
            //configsBar.Label = "Configuration";
            //configsBar.Contained = true;

            //var thresholdVar = new FloatVariable(configsBar, 0.0f);
            //thresholdVar.Label = "Convergence";

            glControl1.Invalidate();
            
        }

        private void addDefaultPrimitives()
        {
            //Default quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad(1.0f, 1.0f);
            MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_quad"] = q.getVAO();

            //Default render quad
            q = new MVCore.Primitives.Quad();
            MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"] = q.getVAO();

            //Default cross
            MVCore.Primitives.Cross c = new MVCore.Primitives.Cross();
            MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_cross"] = c.getVAO();

            //Default cube
            MVCore.Primitives.Box bx = new MVCore.Primitives.Box(1.0f, 1.0f, 1.0f);
            MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_box"] = bx.getVAO();

            //Default sphere
            MVCore.Primitives.Sphere sph = new MVCore.Primitives.Sphere(new Vector3(0.0f, 0.0f, 0.0f), 100.0f);
            MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_sphere"] = sph.getVAO();
        }

        private void compileShaders()
        {
            //Populate shader list
            string log = "";

            //Texture Mixing Shader
            compileShader("Shaders/pass_VS.glsl",
                            "Shaders/pass_FS.glsl",
                            "", "", "", "TEXTURE_MIXING_SHADER", ref log);

        }

        private void compileShader(string vs, string fs, string gs, string tes, string tcs, string name, ref string log)
        {
            GLSLShaderConfig shader_conf = new GLSLShaderConfig(vs, fs, gs, tcs, tes, name);

            compileShader(shader_conf);
            MVCore.Common.RenderState.activeResMgr.GLShaders[shader_conf.name] = shader_conf;
            log += shader_conf.log; //Append log
        }

        public void compileShader(GLSLShaderConfig config)
        {
            int vertexObject;
            int fragmentObject;

            if (config.program_id != -1)
                GL.DeleteProgram(config.program_id);

            GLShaderHelper.CreateShaders(config, out vertexObject, out fragmentObject, out config.program_id);
        }


        private void glControl1_Resize(object sender, EventArgs e)
        {
            if (!this.glloaded)
                return;
            if (glControl1.ClientSize.Height == 0)
                glControl1.ClientSize = new System.Drawing.Size(glControl1.ClientSize.Width, 1);
            Debug.WriteLine("GLControl Resizing");
            context.HandleResize(ClientSize);
            GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
        }


        private void glControl1_Render()
        {
            Debug.WriteLine("Rendering");
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ClearColor(System.Drawing.Color.Black);

            GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
            
            //TODO use mode selection and choose between mask, normal, diffuse mix methods
            MVCore.GMDL.TextureMixer.mixDiffuseTextures(ClientSize.Width, ClientSize.Height);
        }

        //Context Menus on ListBox
        private void List_RightClick(object sender, MouseEventArgs e)
        {

            ListView lbox = (ListView)sender;

            //Test the click
            var info = lbox.HitTest(e.X, e.Y);

            if (info.Item != null)
            {
                var row = info.Item.Index;
                var col = info.Item.SubItems.IndexOf(info.SubItem);

                if (e.Button == MouseButtons.Right)
                {
                    contextMenuStrip1.lBox = lbox;
                    contextMenuStrip1.Show(Cursor.Position);
                }

            }

            
        }

        private void List_SelectOnClick(object sender, MouseEventArgs e)
        {
            ListView lbox = (ListView) sender;

            //Test the click
            var info = lbox.HitTest(e.X, e.Y);

            //For if the click was on an item select it
            foreach (ListViewItem item in lbox.Items)
                item.Selected = false;

            if (info.Item != null)
            {
                var row = info.Item.Index;
                var col = info.Item.SubItems.IndexOf(info.SubItem);

                info.Item.Selected = true;
            }

        }

        private void importTexturesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Importing Texture from here");

            ToolStripMenuItem sitem = ((ToolStripMenuItem) sender);
            ListView lbox = ((customStrip) sitem.Owner).lBox;

            Debug.WriteLine(lbox.SelectedItems);

            int index = lbox.SelectedIndices[0];

            DialogResult res = openFileDialog1.ShowDialog();

            if (res != DialogResult.OK)
                return;

            //Load the texture 
            Texture tex = new Texture(openFileDialog1.FileName);


            //Destroy texture if i'm about to replace
            if (lbox.Name.Contains("diffuse"))
            {
                if (MVCore.GMDL.TextureMixer.difftextures[index] != null)
                    GL.DeleteTexture(MVCore.GMDL.TextureMixer.difftextures[index].bufferID);

                MVCore.GMDL.TextureMixer.difftextures[index] = tex;
                lbox.Items[index].Text = tex.name;

                MVCore.GMDL.TextureMixer.baseLayersUsed[index] = 1.0f;
            }
            else
            {
                if (MVCore.GMDL.TextureMixer.masktextures[index] != null)
                    GL.DeleteTexture(MVCore.GMDL.TextureMixer.masktextures[index].bufferID);

                MVCore.GMDL.TextureMixer.masktextures[index] = tex;
                lbox.Items[index].Text = tex.name;

                MVCore.GMDL.TextureMixer.alphaLayersUsed[index] = 1.0f;
            }
                
            glControl1.Invalidate();
        }

        private void setReColourToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Debug.WriteLine("Setting ReColor");

            ToolStripMenuItem sitem = ((ToolStripMenuItem)sender);
            ListView lbox = ((customStrip)sitem.Owner).lBox;

            int index = lbox.SelectedIndices[0];

            //Color Dialog
            ColorDialog coldiag = new ColorDialog();

            DialogResult res = coldiag.ShowDialog();
            if (res == DialogResult.OK)
            {
                
                lbox.Items[index].BackColor = coldiag.Color;
                MVCore.GMDL.TextureMixer.reColourings[index] = new float[] { (int)coldiag.Color.R / 256.0f,
                                                                             (int)coldiag.Color.G / 256.0f,
                                                                             (int)coldiag.Color.B / 256.0f,
                                                                             1.0f };
            }
            else
            {
                lbox.Items[index].BackColor = System.Drawing.Color.White;
                MVCore.GMDL.TextureMixer.reColourings[index] = new float[] { 0.0f, 0.0f, 0.0f, 0.0f};
            }

            Console.WriteLine("RGB: {0} {1} {2}", MVCore.GMDL.TextureMixer.reColourings[index][0],
                                                  MVCore.GMDL.TextureMixer.reColourings[index][1], 
                                                  MVCore.GMDL.TextureMixer.reColourings[index][2]);
            Vector3 hsv = RGBToHSV(new Vector3(MVCore.GMDL.TextureMixer.reColourings[index][0],
                                               MVCore.GMDL.TextureMixer.reColourings[index][1], 
                                               MVCore.GMDL.TextureMixer.reColourings[index][2]));
            Console.WriteLine("HSV: {0} {1} {2}", hsv.X, hsv.Y, hsv.Z);
            glControl1.Invalidate();

        }

        private Vector3 RGBToHSV(Vector3 c)
        {
            Vector4 K = new Vector4(0.0f, -1.0f / 3.0f, 2.0f / 3.0f, -1.0f);
            Vector4 p = mix(new Vector4(c.Y, c.Z, K.W, K.Z), new Vector4(c.Z, c.Y, K.X, K.Y), step(c.Y, c.Z));
            Vector4 q = mix(new Vector4(p.X, p.Y, p.W, c.X), new Vector4(c.X, p.Y, p.Z, p.X), step(p.X, c.X));

            float d = q.X - (float) Math.Min(q.W, q.Y);
            float e = 1.0e-10f;
            return new Vector3((float) Math.Abs(q.Z + (q.W - q.Y) / (6.0 * d + e)), d / (q.X + e), q.X);
        }

        private Vector4 mix(Vector4 x, Vector4 y, float a)
        {
            return (1 - a) * x + a * y;
        }

        private float step(float a, float b)
        {
            if (b < a)
                return 0.0f;
            return 1.0f;
        }

        

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chk = (CheckBox)sender;

            if (chk.Checked)
                this.hasAlphaChannel = 1.0f;
            else
                this.hasAlphaChannel = 0.0f;

            glControl1.Invalidate();
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox chk = (CheckBox)sender;

            if (chk.Checked)
                this.renderMode = 1;
            else
                this.renderMode = 0;

            glControl1.Invalidate();
        }

    }


    

    public class customStrip: System.Windows.Forms.ContextMenuStrip
    {
        public ListView lBox;
        
        public customStrip(System.ComponentModel.IContainer components) : base(components)
        {
            
        }
    }
}
