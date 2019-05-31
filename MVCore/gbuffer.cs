using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using libMBIN.Models.Structs;
using OpenTK;
using OpenTK.Graphics.OpenGL;


namespace MVCore
{
    public class GBuffer : IDisposable
    {
        public int fbo = -1;

        //Dump fbo stuff
        public int dump_fbo;
        public int dump_diff;
        public int dump_pos;
        public int dump_depth;

        //Textures
        public int diffuse = -1;
        public int positions = -1;
        public int normals = -1;
        public int depth = -1;

        public int quad_vao;
        public int program = -1;
        public int[] size;
        private int msaa_samples = 8;

        public GBuffer(ResourceMgr mgr, int x, int y)
        {
            //Create Quad Geometry
            program = mgr.GLShaders["GBUFFER_SHADER"];

            quad_vao = mgr.GLPrimitiveVaos["default_renderquad"].vao_id;

            //Setup all stuff
            //Init size to the current GLcontrol size
            size = new int[] { x, y };

            init();
            setup();

        }

        public void setup()
        {
            //Init the FBO
            fbo = GL.Ext.GenFramebuffer();
            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());
            
            //Check
            if (GL.Ext.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
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

            //Revert Back the default fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }

        public void setup_dump()
        {
            //Create Intermediate Framebuffer
            dump_fbo = GL.GenFramebuffer();
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

            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, dump_fbo);

            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());
            GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment0Ext, TextureTarget.Texture2D, dump_diff, 0);
            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());
            GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.ColorAttachment1Ext, TextureTarget.Texture2D, dump_pos, 0);
            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());
            GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, FramebufferAttachment.DepthAttachmentExt, TextureTarget.Texture2D, dump_depth, 0);
            //Console.WriteLine("Dump, Last GL Error: " + GL.GetError());

        }

        public void setup_texture(ref int handle, int attachment)
        {
            handle = GL.Ext.GenTexture();

            GL.Ext.BindTexture(TextureTarget.Texture2DMultisample, handle);

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

                    GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
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
                    GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
                    GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, t, TextureTarget.Texture2DMultisample, handle, 0);
                    //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

                    break;
                default:
                    t = FramebufferAttachment.ColorAttachment0Ext + attachment;
                    //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, size[0], size[1], 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                    GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, PixelInternalFormat.Rgba8, size[0], size[1], true);

                    GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
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
            //Bind default fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.UseProgram(program);
            GL.BindVertexArray(quad_vao);

            //Upload the GBuffer textures
            //Diffuse
            int tex0_Id = (int)TextureUnit.Texture0;
            int loc = GL.GetUniformLocation(program, "diffuseTex");
            GL.Uniform1(loc, 0);

            GL.ActiveTexture((TextureUnit)(tex0_Id + 0));
            GL.BindTexture(TextureTarget.Texture2DMultisample, diffuse);

            //Positions
            loc = GL.GetUniformLocation(program, "positionTex");
            GL.Uniform1(loc, 1);

            GL.ActiveTexture((TextureUnit)(tex0_Id + 1));
            GL.BindTexture(TextureTarget.Texture2DMultisample, positions);

            //Depth
            loc = GL.GetUniformLocation(program, "depthTex");
            GL.Uniform1(loc, 2);

            GL.ActiveTexture((TextureUnit)(tex0_Id + 2));
            GL.BindTexture(TextureTarget.Texture2DMultisample, depth);

            //Render quad
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr) 0);
            GL.BindVertexArray(0);
        
        }

        public void init()
        {
            GL.Viewport(0, 0, size[0], size[1]);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            //Main flags
            GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.Texture2D);
            GL.Enable(EnableCap.DepthTest);

            //Geometry Shader Parameters
            GL.PatchParameter(PatchParameterFloat.PatchDefaultInnerLevel, new float[] { 2.0f });
            GL.PatchParameter(PatchParameterFloat.PatchDefaultOuterLevel, new float[] { 4.0f, 4.0f, 4.0f });
            GL.PatchParameter(PatchParameterInt.PatchVertices, 3);
        }

        public void start()
        {
            //Draw Scene

            //Bind Gbuffer fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ClearColor(Common.RenderOptions.clearColor);
            
            //GL.ClearTexImage(positions, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
            //GL.ClearTexImage(depth, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            //GL.ClearTexImage(diffuse, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.DrawBuffers(3, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0,
                                                      DrawBuffersEnum.ColorAttachment1,
                                                      DrawBuffersEnum.ColorAttachment2 } );
        }

        public void stop()
        {
            //Blit can replace the render & stop funtions
            //Simply resolves and copies the ms offscreen fbo to the default framebuffer without any need to render the textures and to any other post proc effects
            //I guess that I don't need the textures as well, when I'm rendering like this
            GL.Ext.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.Ext.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            //Blit
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
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, dump_fbo);

            byte[] pixels = new byte[16 * size[0] * size[1]];
            //pixels = new byte[4 * size[0] * size[1]];
            //Console.WriteLine("Dumping Framebuffer textures " + size[0] + " " + size[1]);

#if false
            //Save Depth Texture
            GL.BindTexture(TextureTarget.Texture2D, dump_depth);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.DepthComponent, PixelType.Float, pixels);

            File.WriteAllBytes("dump.depth", pixels);
#endif

#if false
            //Read Color0
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            GL.BlitFramebuffer(0, 0, size[0], size[1],
                                   0, 0, size[0], size[1],
                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
                                   BlitFramebufferFilter.Nearest);


            //Save Diffuse Color
            GL.BindTexture(TextureTarget.Texture2D, dump_diff);
            GL.GetTexImage(TextureTarget.Texture2D, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            //File.WriteAllBytes("dump.color0", pixels);
#endif


            //Rebind Gbuffer fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, fbo);
            //GL.DrawBuffer(DrawBufferMode.ColorAttachment0);
            //GL.ReadBuffer(ReadBufferMode.ColorAttachment0);

        }

        public void Cleanup()
        {

            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            //Delete Buffer
            GL.DeleteFramebuffer(fbo);

            //Delete textures
            GL.DeleteTexture(diffuse);
            GL.DeleteTexture(positions);
            GL.DeleteTexture(normals);
            GL.DeleteTexture(depth);

            //Delete dump textures
            GL.DeleteFramebuffer(dump_fbo);
            GL.DeleteTexture(dump_diff);
            GL.DeleteTexture(dump_pos);
            GL.DeleteTexture(dump_depth);
        }

        public void resize(int w, int h)
        {
            size[0] = w;
            size[1] = h;
            
            Cleanup();
            setup();
        }


        

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Cleanup();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        //~GBuffer()
        //{
        //    Cleanup();
        //    GL.DeleteBuffer(quad_vbo);
        //    GL.DeleteBuffer(quad_ebo);
        //}

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion


    }

}
