using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL;

namespace Model_Viewer
{

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
            program = Util.activeResMgmt.shader_programs[9];

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

            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            int[] rbufs = new int[2];
            GL.Ext.GenRenderbuffers(2, rbufs);
            depth_rbo = rbufs[1];
            diff_rbo = rbufs[0];

            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //Bind color renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, diff_rbo);
            //Normal Version
            //GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.Rgba8, size[0], size[1]);
            //Multisampling version
            GL.Ext.RenderbufferStorageMultisample(RenderbufferTarget.RenderbufferExt, msaa_samples, RenderbufferStorage.Rgb8, size[0], size[1]);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, RenderbufferTarget.RenderbufferExt, diff_rbo);

            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());


            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //Bind depth renderbuffer
            GL.Ext.BindRenderbuffer(RenderbufferTarget.RenderbufferExt, depth_rbo);
            //Normal Version
            //GL.Ext.RenderbufferStorage(RenderbufferTarget.RenderbufferExt, RenderbufferStorage.DepthComponent, size[0], size[1]);
            //Multisampling version
            GL.Ext.RenderbufferStorageMultisample(RenderbufferTarget.RenderbufferExt, msaa_samples, RenderbufferStorage.DepthComponent, size[0], size[1]);
            GL.Ext.FramebufferRenderbuffer(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, RenderbufferTarget.RenderbufferExt, depth_rbo);

            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

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
            if (dump_fbo != 0)
            {
                GL.DeleteFramebuffer(dump_fbo);
                GL.DeleteTexture(dump_diff);
                GL.DeleteTexture(dump_pos);
                GL.DeleteTexture(dump_depth);
            }

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


            GL.ActiveTexture((TextureUnit)tex0_Id);
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
            size = new int[] { w, h };

            Cleanup();
            setup();
        }


    }

}
