using System;
using System.IO;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Windows.Forms;

namespace Model_Viewer
{
    public class DebugForm : Form
    {
        public GLControl cgl;
        public GMDL.model part = null;


        public DebugForm()
        {
            InitializeComponent();
            this.cgl = new GLControl();
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
            GL.UseProgram(ResourceMgmt.shader_programs[3]);
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
            int vpos, cpos;

            //Vertex attribute
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, quad_vbo);
            vpos = GL.GetAttribLocation(ResourceMgmt.shader_programs[3], "vPosition");
            GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(vpos);

            cpos = GL.GetAttribLocation(ResourceMgmt.shader_programs[3], "vColor");
            GL.VertexAttribPointer(cpos, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
            GL.EnableVertexAttribArray(cpos);

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
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgb, 512, 512, 0, PixelFormat.Rgb, PixelType.UnsignedByte, IntPtr.Zero);
            
            
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
            byte[] pixels = new byte[3 * 512 * 512];
            //GL.PixelStore(PixelStoreParameter.PackAlignment, 1);
            GL.ReadPixels(0, 0, 512, 512, PixelFormat.Rgb, PixelType.UnsignedByte, pixels);

            if (part != null)
            {
                FileStream fs = new FileStream("framebuffer_raw_" + part.name, FileMode.Create);
                BinaryWriter bw = new BinaryWriter(fs);
                bw.Write(pixels);
                fs.Flush();
                fs.Close();
            }
            
            //Render to Screen as well
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DrawArrays(PrimitiveType.Triangles, 0, 6);
            
            GL.DisableVertexAttribArray(vpos);
            GL.DisableVertexAttribArray(cpos);
            GL.DeleteBuffer(quad_vbo);
            GL.DeleteBuffer(quad_ebo);
        }

        public void setPart(GMDL.model part)
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
            
            int pass_program = ResourceMgmt.shader_programs[3];

            //BIND TEXTURES
            GMDL.Material material = part.material;
            GMDL.Texture tex;
            int loc;

            Debug.WriteLine("Rendering Textures of : " + part.name);
            //If there are samples defined, there are diffuse textures for sure
            if (material.samplers.Count > 0)
            {
                //GL.Enable(EnableCap.Blend);
                //GL.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.One);
                //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

                //Get Diffuse sampler
                GMDL.Sampler sam = material.samplers[0];

                //Upload procedural sampler flag
                loc = GL.GetUniformLocation(pass_program, "procFlag");
                if (sam.proc) GL.Uniform1(loc, 1);
                else GL.Uniform1(loc, 0);

                if (sam.procTextures.Count > 0 & RenderOptions.UseTextures)
                {
                    loc = GL.GetUniformLocation(pass_program, "diffuseFlag");
                    GL.Uniform1(loc, 1.0f);

                    int tex0Id = (int)TextureUnit.Texture0;

                    //Handle ProcGen Sampler
                    loc = GL.GetUniformLocation(pass_program, "diffTexCount");
                    GL.Uniform1(loc, sam.procTextures.Count);

                    //if (this.name == "_Body_Tri" | this.name == "_Head_Tri")
                    //    Debug.WriteLine("Debug");

                    for (int i = 0; i < sam.procTextures.Count; i++)
                    {
                        tex = sam.procTextures[i];

                        //Upload PaletteColor
                        loc = GL.GetUniformLocation(pass_program, "palColors[" + i.ToString() + "]");
                        //Use Texture paletteOpt and object palette to load the palette color
                        GL.Uniform3(loc, part.palette[tex.palOpt.PaletteName][tex.palOpt.ColorName]);

                        //Get Texture location
                        string test = "diffuseTex[" + i.ToString() + "]";
                        loc = GL.GetUniformLocation(pass_program, test);
                        GL.Uniform1(loc, i); // I need to upload the texture unit number

                        GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + i));
                        GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);

                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                        GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, tex.pif,
                            tex.width, tex.height, 0, tex.ddsImage.header.dwPitchOrLinearSize, tex.ddsImage.bdata);

                        //Check if there is a masked texture bound
                        if (tex.mask != null)
                        {
                            //Set mask flag
                            test = "maskFlags[" + i.ToString() + "]";
                            loc = GL.GetUniformLocation(pass_program, test);
                            GL.Uniform1(loc, 1);

                            test = "maskTex[" + i.ToString() + "]";
                            loc = GL.GetUniformLocation(pass_program, test);
                            GL.Uniform1(loc, 8 + i); // I need to upload the texture unit number

                            GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + 8 + i));
                            GL.BindTexture(TextureTarget.Texture2D, tex.mask.bufferID);

                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                            GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, tex.mask.pif,
                                tex.mask.width, tex.mask.height, 0, tex.mask.ddsImage.header.dwPitchOrLinearSize, tex.mask.ddsImage.bdata);


                        }
                        else
                        {
                            //Set mask flag to false
                            test = "maskFlags[" + i.ToString() + "]";
                            loc = GL.GetUniformLocation(pass_program, test);
                            GL.Uniform1(loc, 0);
                        }

                        //Check if there is a normal texture bound
                        if (tex.normal != null)
                        {
                            //Set mask flag
                            test = "normalFlags[" + i.ToString() + "]";
                            loc = GL.GetUniformLocation(pass_program, test);
                            GL.Uniform1(loc, 1);

                            test = "normalTex[" + i.ToString() + "]";
                            loc = GL.GetUniformLocation(pass_program, test);
                            GL.Uniform1(loc, 8 + i); // I need to upload the texture unit number

                            GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + 8 + i));
                            GL.BindTexture(TextureTarget.Texture2D, tex.normal.bufferID);

                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                            GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, tex.normal.pif,
                                tex.normal.width, tex.normal.height, 0, tex.normal.ddsImage.header.dwPitchOrLinearSize, tex.normal.ddsImage.bdata);


                        }
                        else
                        {
                            //Set mask flag to false
                            test = "normalFlags[" + i.ToString() + "]";
                            loc = GL.GetUniformLocation(pass_program, test);
                            GL.Uniform1(loc, 0);
                        }

                    }

                    //Load global material mask
                    if (sam.pathMask != null && !sam.proc)
                    {
                        //Set mask flag
                        string test = "maskFlags[0]";
                        loc = GL.GetUniformLocation(pass_program, test);
                        GL.Uniform1(loc, 1);

                        test = "maskTex[0]";
                        loc = GL.GetUniformLocation(pass_program, test);
                        GL.Uniform1(loc, 8); // I need to upload the texture unit number

                        GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + 8));
                        GL.BindTexture(TextureTarget.Texture2D, sam.procTextures[0].mask.bufferID);

                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                        GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, sam.procTextures[0].mask.pif,
                            sam.procTextures[0].mask.width, sam.procTextures[0].mask.height, 0, sam.procTextures[0].mask.ddsImage.header.dwPitchOrLinearSize,
                            sam.procTextures[0].mask.ddsImage.bdata);
                    }

                    //Load global material normal
                    if (sam.pathNormal != null && !sam.proc)
                    {
                        //Set mask flag
                        string test = "normalFlags[0]";
                        loc = GL.GetUniformLocation(pass_program, test);
                        GL.Uniform1(loc, 1);

                        test = "normalTex[0]";
                        loc = GL.GetUniformLocation(pass_program, test);
                        GL.Uniform1(loc, 16); // I need to upload the texture unit number

                        GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + 16));
                        GL.BindTexture(TextureTarget.Texture2D, sam.procTextures[0].normal.bufferID);

                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                        GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, sam.procTextures[0].normal.pif,
                            sam.procTextures[0].normal.width, sam.procTextures[0].normal.height, 0, sam.procTextures[0].normal.ddsImage.header.dwPitchOrLinearSize,
                            sam.procTextures[0].normal.ddsImage.bdata);
                    }
                
                }
                else
                {
                    //Probably textures not found. Render with random color
                    loc = GL.GetUniformLocation(pass_program, "diffuseFlag");
                    GL.Uniform1(loc, 0.0f);
                }
            }
            else
            {
                loc = GL.GetUniformLocation(pass_program, "diffuseFlag");
                GL.Uniform1(loc, 0.0f);
            }

            //Upload Default Color
            loc = GL.GetUniformLocation(pass_program, "color");
            GL.Uniform3(loc, ((GMDL.sharedVBO) part).color);

        }

    }
}
