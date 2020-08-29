using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using OpenTK;
using OpenTK.Graphics.OpenGL4;


namespace MVCore.Engine
{
    public class FBO
    {
        public int fbo = -1;
        public int[] channels;
        public int depth_channel = -1;
        private readonly int channel_num = -1;
        private readonly TextureTarget tex_target;

        //Buffer Specs
        public int size_x;
        public int size_y;
        public int msaa_samples = 8;

        public FBO(TextureTarget type, int num, int x, int y, bool setup_depth)
        {
            //Setup properties
            size_x = x;
            size_y = y;
            channel_num = num;

            tex_target = type;
            setup(setup_depth);
        }

        public void setup(bool setup_depth)
        {
            //Init the main FBO
            fbo = GL.GenFramebuffer();
            channels = new int[channel_num];

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Setup Attachments
            for (int i = 0; i < channel_num; i++)
                setup_texture(ref channels[i], tex_target, fbo, FramebufferAttachment.ColorAttachment0 + i, PixelInternalFormat.Rgba16f);
            
            if (setup_depth)
                setup_texture(ref depth_channel, tex_target, fbo, FramebufferAttachment.DepthAttachment, PixelInternalFormat.DepthComponent);
            
            //Revert Back the default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }

        public void setup_texture(ref int handle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id, PixelInternalFormat format)
        {
            handle = GL.GenTexture();

            if (textarget == TextureTarget.Texture2DMultisample)
            {
                GL.BindTexture(TextureTarget.Texture2DMultisample, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size_x, size_y, true);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

                GL.BindFramebuffer(FramebufferTarget.FramebufferExt, attach_to_fbo);
                GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, attachment_id, TextureTarget.Texture2DMultisample, handle, 0);
            }
            else if (textarget == TextureTarget.Texture2D)
            {
                GL.BindTexture(TextureTarget.Texture2D, handle);

                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                //GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);

                switch (format)
                {
                    case PixelInternalFormat.DepthComponent:
                        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size_x, size_y, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                        break;
                    default:
                        GL.TexImage2D(TextureTarget.Texture2D, 0, format, size_x, size_y, 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);
                        break;
                }

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);


                GL.BindFramebuffer(FramebufferTarget.FramebufferExt, attach_to_fbo);
                GL.FramebufferTexture2D(FramebufferTarget.FramebufferExt, attachment_id, TextureTarget.Texture2D, handle, 0);

                //Check
                if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                    Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            }
            else
            {
                throw new Exception("Unsupported texture target " + textarget);
            }


        }


        public void resize(int w, int h)
        {
            size_x = w;
            size_y = h;

            Cleanup();
            setup(depth_channel != -1);
        }

        public void Cleanup()
        {

            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            //Delete Buffer
            GL.DeleteFramebuffer(fbo);

            for (int i = 0; i < channel_num; i++)
                GL.DeleteTexture(channels[i]);
            if (depth_channel != -1)
                GL.DeleteTexture(depth_channel);
        }

        //STATIC HELPER METHODS

        public static void copyChannel(int from_fbo, int to_fbo, int sourceSizeX, int sourceSizeY, int destSizeX, int destSizeY,
            ReadBufferMode from_channel, DrawBufferMode to_channel)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from_fbo);
            GL.ReadBuffer(from_channel); //Read color
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            GL.DrawBuffer(to_channel); //Write to blur1

            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, destSizeX, destSizeY,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Linear);
            
        }

        public static void copyDepthChannel(int from_fbo, int to_fbo, int sourceSizeX, int sourceSizeY, int destSizeX, int destSizeY)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, from_fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, to_fbo);
            
            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, destSizeX, destSizeY,
            ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }

        public static void copyChannel(int fbo, int sourceSizeX, int sourceSizeY, ReadBufferMode from_channel, DrawBufferMode to_channel)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.ReadBuffer(from_channel); //Read color
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            GL.DrawBuffer(to_channel); //Write to blur1

            //Method 1: Use Blitbuffer
            GL.BlitFramebuffer(0, 0, sourceSizeX, sourceSizeY, 0, 0, sourceSizeX, sourceSizeY,
            ClearBufferMask.ColorBufferBit, BlitFramebufferFilter.Nearest);
        }

    }

    public class PBuffer
    {
        public int fbo = -1;
        
        //Pixel Bufffer Textures
        public int color = -1;
        public int blur1 = -1;
        public int blur2 = -1;
        public int composite = -1;
        public int depth = -1;

        //Buffer Specs
        public int[] size;
        public int msaa_samples = 8;


        public PBuffer(int x, int y)
        {
            size = new int[] { x, y };
            setup();
        }

        public void setup()
        {
            //Init the main FBO
            fbo = GL.GenFramebuffer();

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Setup color texture
            setup_texture(ref color, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(color, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment0);
            //Setup blur1 texture
            setup_texture(ref blur1, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(blur1, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment1);
            //Setup blur2 texture
            setup_texture(ref blur2, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(blur2, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment2);
            //Setup composite texture
            setup_texture(ref composite, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(composite, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment3);
            
            //Setup depth texture
            setup_texture(ref depth, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true);
            bindTextureToFBO(depth, TextureTarget.Texture2D, fbo, FramebufferAttachment.DepthAttachment);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Revert Back the default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }


        public void setup_texture(ref int handle, TextureTarget textarget, PixelInternalFormat format, bool isDepth)
        {
            handle = GL.GenTexture();
            GL.BindTexture(textarget, handle);

            if (textarget == TextureTarget.Texture2DMultisample)
            {
                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            }
            else if (textarget == TextureTarget.Texture2D)
            {
                if (isDepth)
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                else
                    GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);


                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            }
            else
            {
                throw new Exception("Unsupported texture target " + textarget);
            }
        }

        //TODO: Organize this function a bit
        public void bindTextureToFBO(int texHandle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, textarget, texHandle, 0);
        }

        public void Cleanup()
        {

            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            //Delete Buffer
            GL.DeleteFramebuffer(fbo);

            //Delete textures
            GL.DeleteTexture(color);
            GL.DeleteTexture(blur1);
            GL.DeleteTexture(blur2);
            GL.DeleteTexture(composite);
        }

        public void resize(int w, int h)
        {
            size[0] = w;
            size[1] = h;

            Cleanup();
            setup();
        }


        

    }

    public class GBuffer : IDisposable
    {
        public int fbo = -1;

        //Textures
        public int albedo = -1;
        public int normals = -1;
        public int info = -1;
        public int depth = -1;
        public int depth_dump = -1;

        //Buffer Specs
        public int[] size;
        public int msaa_samples = 8;

        public GBuffer(int x, int y)
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
            fbo = GL.GenFramebuffer();
            
            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Setup diffuse texture
            setup_texture(ref albedo, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(albedo, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment0);
            //Setup normals texture
            setup_texture(ref normals, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(normals, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment1);
            //Setup info texture
            setup_texture(ref info, TextureTarget.Texture2D, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(info, TextureTarget.Texture2D, fbo, FramebufferAttachment.ColorAttachment2);
            //Setup Depth texture
            setup_texture(ref depth, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true);
            bindTextureToFBO(depth, TextureTarget.Texture2D, fbo, FramebufferAttachment.DepthAttachment);
            
            //Setup depth backup  texture
            setup_texture(ref depth_dump, TextureTarget.Texture2D, PixelInternalFormat.DepthComponent, true);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Revert Back the default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }

        public void setup_texture(ref int handle, TextureTarget textarget, PixelInternalFormat format, bool isDepth)
        {
            handle = GL.GenTexture();
            GL.BindTexture(textarget, handle);

            if (textarget == TextureTarget.Texture2DMultisample)
            {
                //GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                GL.TexImage2DMultisample(TextureTargetMultisample.Texture2DMultisample, msaa_samples, format, size[0], size[1], true);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            
            }
            else if (textarget == TextureTarget.Texture2D)
            {
                if (isDepth)
                    GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
                else
                    GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            
            }
            else
            {
                throw new Exception("Unsupported texture target " + textarget);
            }
        }

        //TODO: Organize this function a bit
        public void bindTextureToFBO(int texHandle, TextureTarget textarget, int attach_to_fbo, FramebufferAttachment attachment_id)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, attach_to_fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, textarget, texHandle, 0);
        }

        
        public void init()
        {
            GL.Viewport(0, 0, size[0], size[1]);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            //Main flags
            //GL.Enable(EnableCap.Multisample);
            GL.Enable(EnableCap.Texture2D);
            
            //Geometry Shader Parameters
            GL.PatchParameter(PatchParameterFloat.PatchDefaultInnerLevel, new float[] { 2.0f });
            GL.PatchParameter(PatchParameterFloat.PatchDefaultOuterLevel, new float[] { 4.0f, 4.0f, 4.0f });
            GL.PatchParameter(PatchParameterInt.PatchVertices, 3);
        }

        public void bind()
        {
            //Bind Gbuffer fbo
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, fbo);
            GL.DrawBuffers(3, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0,
                                                      DrawBuffersEnum.ColorAttachment1,
                                                      DrawBuffersEnum.ColorAttachment2} );
        }

        public void clearColor(Vector4 col)
        {
            GL.ClearColor(col.X, col.Y, col.Z, col.W);
        }

        public void clear(ClearBufferMask mask)
        {
            GL.Clear(mask);
        }

        public void stop()
        {
            //Blit can replace the render & stop funtions
            //Simply resolves and copies the ms offscreen fbo to the default framebuffer without any need to render the textures and to any other post proc effects
            //I guess that I don't need the textures as well, when I'm rendering like this
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);
            //Blit
            GL.BlitFramebuffer(0, 0, size[0], size[1],
                                   0, 0, size[0], size[1],
                                   ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit,
                                   BlitFramebufferFilter.Nearest);
        }

        public void dump()
        {
            //Bind Buffers
            //Resolving Buffers
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, fbo);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, 0);

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
            GL.DeleteTexture(normals);
            GL.DeleteTexture(info);
            GL.DeleteTexture(depth);
            GL.DeleteTexture(depth_dump);
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
