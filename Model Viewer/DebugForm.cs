using System;
using System.IO;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Windows.Forms;
using MVCore.GMDL;
using MVCore;

namespace Model_Viewer
{
    public class DebugForm : Form
    {
        public GLControl cgl;
        public model part = null;
        private ResourceMgr resMgr;


        public DebugForm()
        {
            InitializeComponent();
            this.cgl = new GLControl();
            this.resMgr = new ResourceMgr();
            setupCgl();

            this.Controls.Add(cgl);
            this.Show();
        }

        private void setupCgl()
        {
            this.cgl.Load += new System.EventHandler(this.genericLoad);
            this.cgl.Paint += new System.Windows.Forms.PaintEventHandler(this.genericPaint);
            this.cgl.MouseHover += new System.EventHandler(this.hover);
            this.cgl.MouseMove += new System.Windows.Forms.MouseEventHandler(this.genericMouseMove);

            //this.Resize += new System.EventHandler(this.genericResize);
        }

        private void genericLoad(object sender, EventArgs e)
        {

            this.cgl.MakeCurrent();
            this.cgl.Size = new System.Drawing.Size(512, 512);

            GL.Viewport(0, 0, this.cgl.ClientSize.Width, this.cgl.ClientSize.Height);
            GL.ClearColor(System.Drawing.Color.Red);
            GL.Enable(EnableCap.DepthTest);

            //glControl1.SwapBuffers();
            //glControl1.Invalidate();
            //Debug.WriteLine("GL Cleared");
            //Debug.WriteLine(GL.GetError());
        }

        private void genericMouseMove(object sender, MouseEventArgs e)
        {
            /*
             * DOING ABSOLUTELY NOTHING
             */
            //Debug.WriteLine("Moving Mouse in debug");
            this.cgl.Invalidate();
            

        }

        private void hover(object sender, EventArgs e)
        {
            //Debug.WriteLine("Hovering Mouse in debug");
            this.cgl.Focus();
            this.cgl.Invalidate();
        }

        private void genericPaint(object sender, EventArgs e)
        {
            this.cgl.MakeCurrent();
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            this.renderquad();
            this.cgl.SwapBuffers();
        }

        private void renderquad()
        {
            GL.UseProgram(this.resMgr.shader_programs[3]);
            int quad_vbo;
            int quad_ebo;

            //Define Quad
            float[] quad = new float[6 * 3] {
                -1.0f, -1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                -1.0f,  1.0f, 0.0f,
                1.0f, -1.0f, 0.0f,
                1.0f,  1.0f, 0.0f };

            float[] quadcolors = new float[6 * 3]
            {
                1.0f, 0.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                1.0f,  1.0f, 0.0f,
                0.0f, 1.0f, 0.0f,
                0.0f,  0.0f, 1.0f
            };

            //Indices
            int[] indices = new Int32[2 * 3] { 0, 1, 2, 3, 4, 5 };

            //Generate OpenGL buffers
            int arraysize = sizeof(float) * 6 * 3;
            GL.GenBuffers(1, out quad_vbo);
            GL.GenBuffers(1, out quad_ebo);

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


            // Attach to Shaders
            
            //Vertex attribute
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
            //vPosition #0
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);
            
            //vColor #1
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
            GL.EnableVertexAttribArray(1);

            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, quad_ebo);


            //Create Texture to save to
            int out_tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, out_tex);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //NULL means reserve texture memory, but texels are undefined
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 512, 512, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            
            
            //Create New RenderBuffer
            int fb = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, fb);
            //Attach Texture to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, TextureTarget.Texture2D, out_tex, 0);
            
            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Debug.WriteLine("MALAKIES STO FRAMEBUFFER");

            if (part != null)
            {
                renderTextures();
                System.Threading.Thread.Sleep(60);
            }

            //Render to the FBO
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            //Store Framebuffer to Disk
            byte[] pixels = new byte[4 * 512 * 512];
            //GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
            GL.ReadPixels(0, 0, 512, 512, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            if (part != null)
            {
                FileStream fs = new FileStream("framebuffer_raw_" + part.material.name, FileMode.Create);
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(pixels);
                fs.Flush();
                fs.Close();
            }
            
            //Render to Screen as well
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
            GL.DeleteBuffer(quad_vbo);
            GL.DeleteBuffer(quad_ebo);
        }

        public void setPart(model part)
        {
            this.part = part;
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();
            // 
            // DebugForm
            // 
            this.ClientSize = new System.Drawing.Size(520, 520);
            this.Name = "DebugForm";
            this.Text = "GL Debug";
            this.ResumeLayout(false);

        }

        private void renderTextures()
        {

            int pass_program = this.resMgr.shader_programs[3];

            //BIND TEXTURES
            Material material = part.material;
            Texture tex;
            int loc;

            Debug.WriteLine("Rendering Textures of : " + part.name);
            //If there are samples defined, there are diffuse textures for sure

            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            //NEW WAY OF TEXTURE BINDING
            
            //DIFFUSE TEXTURES

            //Upload base Layers Used
            for (int i = 0; i < 8; i++)
            {
                loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed[" + i.ToString() + "]");
                GL.Uniform1(loc, material.baseLayersUsed[i]);
            }

            for (int i = 0; i < 8; i++)
            {
                if (material.difftextures[i] != null)
                {
                    tex = material.difftextures[i];

                    //Upload diffuse Texture
                    string sem = "diffuseTex[" + i.ToString() + "]";
                    //Get Texture location
                    loc = GL.GetUniformLocation(pass_program, sem);
                    GL.Uniform1(loc, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    //Upload PaletteColor
                    //loc = GL.GetUniformLocation(pass_program, "palColors[" + i.ToString() + "]");
                    //Use Texture paletteOpt and object palette to load the palette color
                    //GL.Uniform3(loc, palette[tex.palOpt.PaletteName][tex.palOpt.ColorName]);

                    GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + i));
                    GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);
                }
            }

            //TESTING MASKS
            //SETTING HASALPHACHANNEL FLAG TO FALSE
            loc = GL.GetUniformLocation(pass_program, "hasAlphaChannel");
            GL.Uniform1(loc, 0.0f);


            //MASKS
            //Upload alpha Layers Used
            for (int i = 0; i < 8; i++)
            {
                loc = GL.GetUniformLocation(pass_program, "lalphaLayersUsed[" + i.ToString() + "]");
                GL.Uniform1(loc, material.alphaLayersUsed[i]);
            }

            //Upload Mask Textures -- Alpha Masks???
            for (int i = 0; i < 8; i++)
            {
                if (material.masktextures[i] == null) continue;
                    
                tex = material.masktextures[i];

                //Upload diffuse Texture
                string sem = "maskTex[" + i.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(pass_program, sem);
                GL.Uniform1(loc, 8 + i); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                //Upload PaletteColor
                //loc = GL.GetUniformLocation(pass_program, "palColors[" + i.ToString() + "]");
                //Use Texture paletteOpt and object palette to load the palette color
                //GL.Uniform3(loc, palette[tex.palOpt.PaletteName][tex.palOpt.ColorName]);

                GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + 8 + i));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);
                
            }

            //Upload Recolouring Information
            for (int i = 0; i < 8; i++)
            {
                loc = GL.GetUniformLocation(pass_program, "lRecolours[" + i.ToString() + "]");
                GL.Uniform4(loc, material.reColourings[i][0], material.reColourings[i][1], material.reColourings[i][2], 1.0f);
            }

        }

            
        
    }
}
