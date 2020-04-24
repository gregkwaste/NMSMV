using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using libMBIN.NMS.Toolkit;
using OpenTK;
using OpenTK.Graphics.OpenGL;


namespace MVCore
{
    public class GBuffer : IDisposable
    {
        public int fbo = -1;

        //Dump fbo stuff
        public int dump_fbo = -1;
        public int dump_rgba8_1 = -1;
        public int dump_rgba16f_1 = -1;
        public int dump_rgba32f_1 = -1;
        public int dump_rgba32f_2 = -1;
        public int dump_depth = -1;

        //Textures
        public int albedo = -1;
        public int positions = -1;
        public int normals = -1;
        public int info = -1;
        public int info2 = -1;
        public int final_color = -1;
        public int depth = -1;

        //Buffer Geometry
        public int quad_vao;
        public int program = -1;
        public int[] size;
        private int msaa_samples = 8;

        public GBuffer(ResourceManager mgr, int x, int y)
        {
            //Setup all stuff
            //Init size to the current GLcontrol size
            size = new int[] { x, y };

            init();
            setup();
        }

        public void setup()
        {
            //Init the main FBO
            fbo = GL.Ext.GenFramebuffer();
            
            //Init the 
            //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());
            
            //Check
            if (GL.Ext.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Setup diffuse texture
            setup_texture(ref albedo, TextureTarget.Texture2DMultisample, fbo, FramebufferAttachment.ColorAttachment0Ext, PixelInternalFormat.Rgba16f);
            //Setup positions texture
            setup_texture(ref positions, TextureTarget.Texture2DMultisample, fbo, FramebufferAttachment.ColorAttachment1Ext, PixelInternalFormat.Rgba32f);
            //Setup normals texture
            setup_texture(ref normals, TextureTarget.Texture2DMultisample, fbo, FramebufferAttachment.ColorAttachment2Ext, PixelInternalFormat.Rgba32f);
            //Setup final color texture
            setup_texture(ref final_color, TextureTarget.Texture2DMultisample, fbo, FramebufferAttachment.ColorAttachment3Ext, PixelInternalFormat.Rgba8);
            //Setup info texture
            setup_texture(ref info, TextureTarget.Texture2DMultisample, fbo, FramebufferAttachment.ColorAttachment4Ext, PixelInternalFormat.Rgba32f);
            //Setup info2 texture
            setup_texture(ref info2, TextureTarget.Texture2DMultisample, fbo, FramebufferAttachment.ColorAttachment5Ext, PixelInternalFormat.Rgba32f);
            //Setup Depth texture
            setup_texture(ref depth, TextureTarget.Texture2DMultisample, fbo, FramebufferAttachment.DepthAttachmentExt, PixelInternalFormat.DepthComponent);

            //Check
            if (GL.Ext.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));


            //Setup dump_fbo
            dump_fbo = GL.Ext.GenFramebuffer();
            
            setup_texture(ref dump_rgba8_1, TextureTarget.Texture2DMultisample, dump_fbo, FramebufferAttachment.ColorAttachment0, PixelInternalFormat.Rgba8);
            setup_texture(ref dump_rgba16f_1, TextureTarget.Texture2DMultisample, dump_fbo, FramebufferAttachment.ColorAttachment1, PixelInternalFormat.Rgba16f);
            //setup_texture(ref dump_rgba8_2, TextureTarget.Texture2DMultisample,dump_fbo, FramebufferAttachment.ColorAttachment1, PixelInternalFormat.Rgba8);
            setup_texture(ref dump_rgba32f_1, TextureTarget.Texture2DMultisample, dump_fbo, FramebufferAttachment.ColorAttachment2, PixelInternalFormat.Rgba32f);
            setup_texture(ref dump_rgba32f_2, TextureTarget.Texture2DMultisample, dump_fbo, FramebufferAttachment.ColorAttachment3, PixelInternalFormat.Rgba32f);
            setup_texture(ref dump_depth, TextureTarget.Texture2DMultisample, dump_fbo, FramebufferAttachment.DepthAttachmentExt, PixelInternalFormat.DepthComponent);


            //Check
            if (GL.Ext.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Revert Back the default fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }

        //TODO: Organize this function a bit
        public void setup_texture(ref int handle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id, PixelInternalFormat format)
        {
            handle = GL.Ext.GenTexture();


            if (textarget == TextureTarget.Texture2DMultisample)
            {
                GL.Ext.BindTexture(TextureTarget.Texture2DMultisample, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, attach_to_fbo);
                GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, attachment_id, TextureTarget.Texture2DMultisample, handle, 0);
                //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());
            } else if (textarget == TextureTarget.Texture2D)
            {
                GL.Ext.BindTexture(TextureTarget.Texture2D, handle);

                Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                //GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
                GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());

                GL.Ext.BindFramebuffer(FramebufferTarget.FramebufferExt, attach_to_fbo);
                Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());
                GL.Ext.FramebufferTexture2D(FramebufferTarget.FramebufferExt, attachment_id, TextureTarget.Texture2D, handle, 0);
                Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());
                //Console.WriteLine("GBuffer Setup, Last GL Error: " + GL.GetError());
            } else
            {
                throw new Exception("Unsupported texture target " + textarget);
            }



        }

        
        public void init()
        {
            GL.Viewport(0, 0, size[0], size[1]);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            //Main flags
            GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.Texture2D);
            
            //Geometry Shader Parameters
            GL.PatchParameter(PatchParameterFloat.PatchDefaultInnerLevel, new float[] { 2.0f });
            GL.PatchParameter(PatchParameterFloat.PatchDefaultOuterLevel, new float[] { 4.0f, 4.0f, 4.0f });
            GL.PatchParameter(PatchParameterInt.PatchVertices, 3);
        }

        public void bind()
        {
            //Bind Gbuffer fbo
            GL.Ext.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            GL.DrawBuffers(6, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0,
                                                      DrawBuffersEnum.ColorAttachment1,
                                                      DrawBuffersEnum.ColorAttachment2,
                                                      DrawBuffersEnum.ColorAttachment3,
                                                      DrawBuffersEnum.ColorAttachment4,
                                                      DrawBuffersEnum.ColorAttachment5} );
#if DEBUG
            ErrorCode err = GL.GetError();
            if (err != ErrorCode.NoError)
                Console.WriteLine(err);
#endif

            GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f); //Transparent Clear color
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
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
            GL.DeleteTexture(albedo);
            GL.DeleteTexture(positions);
            GL.DeleteTexture(normals);
            GL.DeleteTexture(depth);
            GL.DeleteTexture(final_color);
            GL.DeleteTexture(info);
            GL.DeleteTexture(info2);

            //Delete dump textures + dump_fbo
            GL.DeleteFramebuffer(dump_fbo);
            GL.DeleteTexture(dump_rgba8_1);
            GL.DeleteTexture(dump_rgba16f_1);
            GL.DeleteTexture(dump_rgba32f_1);
            GL.DeleteTexture(dump_rgba32f_2);
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
