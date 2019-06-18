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
        private int shader_program;

        private Context context;

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

        private List<Texture> diffTextures = new List<Texture>(8);
        private List<Texture> maskTextures = new List<Texture>(8);
        private float[] baseLayersUsed = new float[8];
        private float[] alphaLayersUsed = new float[8];
        private List<float[]> reColours = new List<float[]>(8);

        //Default Textures
        private Texture dDiff;
        private Texture dMask;

        //Rendering Options
        private float hasAlphaChannel = 0.0f;
        private int  renderMode = 0;

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

            //Init the Texture lists
            for (int i = 0; i < 8; i++)
            {
                diffTextures.Add(null);
                maskTextures.Add(null);
                baseLayersUsed[i] = 0.0f;
                alphaLayersUsed[i] = 0.0f;
                reColours.Add(new float[] { 1.0f, 1.0f, 1.0f, 0.0f });
            }

            //Init Utils
            MVCore.FileUtils.dirpath = ""; // Init to empty string

        }
        
        private void glControl1_Paint(object sender, PaintEventArgs e)
        {
            if (!this.glloaded)
                return;
            glControl1.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //GL.ClearColor(System.Drawing.Color.Black);
            glControl1_Render();
            
            //context.Draw();
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
            GL.Viewport(0, 0, glControl1.ClientSize.Width, glControl1.ClientSize.Height);
            GL.ClearColor(System.Drawing.Color.Black);
            GL.Enable(EnableCap.DepthTest);
            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            Debug.WriteLine("GL Cleared");
            Debug.WriteLine(GL.GetError());

            this.glloaded = true;


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
            string vvs, ffs;
            int vertex_shader_ob, fragment_shader_ob;
            vvs = GLSL_Preprocessor.Parser("Shaders/pass_VS.glsl");
            ffs = GLSL_Preprocessor.Parser("Shaders/pass_FS.glsl");
            //Compile Texture Shaders
            CreateShaders(vvs, ffs, out vertex_shader_ob,
                    out fragment_shader_ob, out shader_program);


            //Setup default program
            GL.UseProgram(shader_program);

            //Init Default Textures
            dDiff = new Texture("default.dds");
            dMask = new Texture("default_mask.dds");

            context = new Context(Tw.GraphicsAPI.OpenGL);
            ////Add stuff to context
            //var configsBar = new Bar(context);
            //configsBar.Label = "Configuration";
            //configsBar.Contained = true;

            //var thresholdVar = new FloatVariable(configsBar, 0.0f);
            //thresholdVar.Label = "Convergence";

            glControl1.Invalidate();
            
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

            MVCore.GMDL.mainVAO vao = new MVCore.Primitives.Quad().getVAO();

            //BIND TEXTURES
            Texture tex;
            int loc;

            //If there are samples defined, there are diffuse textures for sure

            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            //NEW WAY OF TEXTURE BINDING

            //DIFFUSE TEXTURES
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.ConstantAlpha, BlendingFactorDest.OneMinusConstantAlpha);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                loc = GL.GetUniformLocation(shader_program, "d_lbaseLayersUsed[" + i.ToString() + "]");
                GL.Uniform1(loc, baseLayersUsed[i]);
                if (baseLayersUsed[i] > 0.0f)
                    baseLayerIndex = i;
            }

            for (int i = 0; i < 8; i++)
            {

                if (diffTextures[i] != null)
                    tex = diffTextures[i];
                else
                    tex = dMask;
                
                //Upload diffuse Texture
                string sem = "diffuseTex[" + i.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(shader_program, sem);
                GL.Uniform1(loc, i); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                //Upload average Color
                loc = GL.GetUniformLocation(shader_program, "lAverageColors[" + i.ToString() + "]");
                GL.Uniform4(loc, tex.avgColor.X, tex.avgColor.Y, tex.avgColor.Z, 1.0f); 

                GL.ActiveTexture((OpenTK.Graphics.OpenGL4.TextureUnit)(tex0Id + i));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);
            
            }

            //TESTING MASKS
            //SETTING HASALPHACHANNEL FLAG TO FALSE
            loc = GL.GetUniformLocation(shader_program, "hasAlphaChannel");
            GL.Uniform1(loc, hasAlphaChannel);

            loc = GL.GetUniformLocation(shader_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);

            //Toggle mask-diffuse
            loc = GL.GetUniformLocation(shader_program, "mode");
            GL.Uniform1(loc, renderMode);


            //MASKS
            //Upload alpha Layers Used
            for (int i = 0; i < 8; i++)
            {
                loc = GL.GetUniformLocation(shader_program, "lalphaLayersUsed[" + i.ToString() + "]");
                GL.Uniform1(loc, alphaLayersUsed[i]);
            }

            //Upload Mask Textures -- Alpha Masks???
            for (int i = 0; i < 8; i++)
            {
                if (maskTextures[i] != null)
                    tex = maskTextures[i];
                else
                    tex = dDiff;
                

                //Upload diffuse Texture
                string sem = "maskTex[" + i.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(shader_program, sem);
                GL.Uniform1(loc, 8 + i); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                //Upload PaletteColor
                //loc = GL.GetUniformLocation(pass_program, "palColors[" + i.ToString() + "]");
                //Use Texture paletteOpt and object palette to load the palette color
                //GL.Uniform3(loc, palette[tex.palOpt.PaletteName][tex.palOpt.ColorName]);

                GL.ActiveTexture((OpenTK.Graphics.OpenGL4.TextureUnit)(tex0Id + 8 + i));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);

            }

            //Upload Recolouring Information
            for (int i = 0; i < 8; i++)
            {
                loc = GL.GetUniformLocation(shader_program, "lRecolours[" + i.ToString() + "]");
                GL.Uniform4(loc, reColours[i][0], reColours[i][1], reColours[i][2], reColours[i][3]);
            }


            //RENDERING PHASE
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
            GL.BindVertexArray(vao.vao_id);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
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
                if (diffTextures[index] != null)
                    GL.DeleteTexture(diffTextures[index].bufferID);

                diffTextures[index] = tex;
                lbox.Items[index].Text = tex.name;

                baseLayersUsed[index] = 1.0f;
            }
            else
            {
                if (maskTextures[index] != null)
                    GL.DeleteTexture(maskTextures[index].bufferID);

                maskTextures[index] = tex;
                lbox.Items[index].Text = tex.name;

                alphaLayersUsed[index] = 1.0f;
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
                reColours[index] = new float[] { (int)coldiag.Color.R / 256.0f, (int)coldiag.Color.G / 256.0f, (int)coldiag.Color.B / 256.0f, 1.0f };
                
            }
            else
            {
                lbox.Items[index].BackColor = System.Drawing.Color.White;
                reColours[index] = new float[] { 1.0f, 1.0f, 1.0f, 0.0f };
            }

            Console.WriteLine("RGB: {0} {1} {2}", reColours[index][0], reColours[index][1], reColours[index][2]);
            Vector3 hsv = RGBToHSV(new Vector3(reColours[index][0], reColours[index][1], reColours[index][2]));
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


        //Shader Creation
        private void CreateShaders(string vs, string fs, out int vertexObject,
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
    }


    

    public class customStrip: System.Windows.Forms.ContextMenuStrip
    {
        public ListView lBox;
        
        public customStrip(System.ComponentModel.IContainer components) : base(components)
        {
            
        }
    }
}
