using System;
using OpenTK.Graphics.OpenGL4;


namespace MVCore.Engine
{
    public static class FramebufferBlitter
    {
        public static void CopyDepth(int sourceFramebuffer, int destinationFramebuffer,
                                     int sourceWidth, int sourceHeight,
                                     int destinationWidth, int destinationHeight)
        {
            GL.BindFramebuffer(FramebufferTarget.ReadFramebuffer, sourceFramebuffer);
            GL.BindFramebuffer(FramebufferTarget.DrawFramebuffer, destinationFramebuffer);
            GL.BlitFramebuffer(0, 0, sourceWidth, sourceHeight,
                               0, 0, destinationWidth, destinationHeight,
                               ClearBufferMask.DepthBufferBit, BlitFramebufferFilter.Nearest);
        }
    }

    public sealed class PBuffer : IDisposable
    {
        public int fbo = -1;
        
        //Pixel Bufffer Textures
        public int color = -1;
        public int blur1 = -1;
        public int blur2 = -1;
        public int depth = -1;

        //Buffer Specs
        public int[] size;
        public PBuffer(int x, int y)
        {
            size = new int[] { x, y };
            setup();
        }

        private void setup()
        {
            //Init the main FBO
            fbo = GL.GenFramebuffer();

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Common.CallBacks.Log("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Setup color texture
            setup_texture(ref color, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(color, FramebufferAttachment.ColorAttachment0);
            //Setup blur1 texture
            setup_texture(ref blur1, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(blur1, FramebufferAttachment.ColorAttachment1);
            //Setup blur2 texture
            setup_texture(ref blur2, PixelInternalFormat.Rgba16f, false);
            bindTextureToFBO(blur2, FramebufferAttachment.ColorAttachment2);
            //Setup depth texture
            setup_texture(ref depth, PixelInternalFormat.DepthComponent, true);
            bindTextureToFBO(depth, FramebufferAttachment.DepthAttachment);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Common.CallBacks.Log("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Revert Back the default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }


        private void setup_texture(ref int handle, PixelInternalFormat format, bool isDepth)
        {
            handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);
            if (isDepth)
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            else
                GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
        }

        private void bindTextureToFBO(int texHandle, FramebufferAttachment attachment_id)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, TextureTarget.Texture2D, texHandle, 0);
        }

        private void Cleanup()
        {

            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            //Delete Buffer
            GL.DeleteFramebuffer(fbo);

            //Delete textures
            GL.DeleteTexture(color);
            GL.DeleteTexture(blur1);
            GL.DeleteTexture(blur2);
            GL.DeleteTexture(depth);
        }

        public void resize(int w, int h)
        {
            size[0] = w;
            size[1] = h;

            Cleanup();
            setup();
        }

        public void Dispose()
        {
            Cleanup();
        }

    }

    public sealed class GBuffer : IDisposable
    {
        public int fbo = -1;

        //Textures
        public int albedo = -1;
        public int normals = -1;
        public int info = -1;
        public int depth = -1;

        //Buffer Specs
        public int[] size;
        public GBuffer(int x, int y)
        {
            //Setup all stuff
            //Init size to the current GLcontrol size
            size = new int[] { x, y };

            init();
            setup();
        }

        private void setup()
        {
            //Init the main FBO
            fbo = GL.GenFramebuffer();
            
            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Common.CallBacks.Log("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Setup diffuse texture
            setup_texture(ref albedo, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
            bindTextureToFBO(albedo, FramebufferAttachment.ColorAttachment0);
            //Setup normals texture
            setup_texture(ref normals, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
            bindTextureToFBO(normals, FramebufferAttachment.ColorAttachment1);
            //Setup info texture 
            setup_texture(ref info, PixelInternalFormat.Rgba16f, false, TextureMagFilter.Linear, TextureMinFilter.Linear);
            bindTextureToFBO(info, FramebufferAttachment.ColorAttachment2);
            //Setup Depth texture
            setup_texture(ref depth, PixelInternalFormat.DepthComponent, true, TextureMagFilter.Nearest, TextureMinFilter.Nearest);
            bindTextureToFBO(depth, FramebufferAttachment.DepthAttachment);
            
            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt) != FramebufferErrorCode.FramebufferComplete)
                Common.CallBacks.Log("MALAKIES STO FRAMEBUFFER tou GBuffer" + GL.CheckFramebufferStatus(FramebufferTarget.FramebufferExt));

            //Revert Back the default fbo
            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
        }

        private void setup_texture(ref int handle, PixelInternalFormat format, bool isDepth, TextureMagFilter texMagFilter, TextureMinFilter texMinFilter)
        {
            handle = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, handle);
            if (isDepth)
                GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent, size[0], size[1], 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);
            else
                GL.TexImage2D(TextureTarget.Texture2D, 0, format, size[0], size[1], 0, PixelFormat.Rgba, PixelType.Float, IntPtr.Zero);

            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)texMagFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)texMinFilter);
        }

        private void bindTextureToFBO(int texHandle, FramebufferAttachment attachment_id)
        {
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, attachment_id, TextureTarget.Texture2D, texHandle, 0);
        }

        
        private void init()
        {
            GL.Viewport(0, 0, size[0], size[1]);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            
            //Main flags
            //GL.Enable(EnableCap.Multisample);
            
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

        public void clear(ClearBufferMask mask)
        {
            GL.Clear(mask);
        }

        private void Cleanup()
        {

            GL.BindFramebuffer(FramebufferTarget.FramebufferExt, 0);
            //Delete Buffer
            GL.DeleteFramebuffer(fbo);

            //Delete textures
            GL.DeleteTexture(albedo);
            GL.DeleteTexture(normals);
            GL.DeleteTexture(info);
            GL.DeleteTexture(depth);
        }

        public void resize(int w, int h)
        {
            size[0] = w;
            size[1] = h;
            
            Cleanup();
            setup();
        }


        

        public void Dispose()
        {
            Cleanup();
        }


    }

}
