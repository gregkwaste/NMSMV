using System;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;

namespace MVCore
{
    //Shadow map of the key light (light 0), which the lighting treats as a directional light.
    //Shadow casting meshes are drawn into a depth texture from the light, fitted around the scene,
    //and the deferred light pass compares every pixel against it
    public class ShadowRenderer : IDisposable
    {
        public const int SHADOW_MAP_SIZE = 2048;

        //Depth only framebuffer
        public int depth_fbo;
        //Depth texture, sampled with hardware depth comparison (sampler2DShadow)
        public int depth_tex_id;

        //Rotated scene space to light clip space
        public Matrix4 lightViewProj = Matrix4.Identity;
        //World size of one shadow map texel, scales the normal offset of the lookups
        public float texelWorldSize = 0.0f;

        public ShadowRenderer()
        {
            depth_tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, depth_tex_id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.DepthComponent32f,
                SHADOW_MAP_SIZE, SHADOW_MAP_SIZE, 0, PixelFormat.DepthComponent, PixelType.Float, IntPtr.Zero);

            //Linear filtering with the compare mode on gives 2x2 hardware PCF per lookup
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareMode, (int) TextureCompareMode.CompareRefToTexture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureCompareFunc, (int) DepthFunction.Lequal);
            //Outside the map nothing is shadowed
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.ClampToBorder);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBorderColor, new float[] { 1.0f, 1.0f, 1.0f, 1.0f });
            GL.BindTexture(TextureTarget.Texture2D, 0);

            depth_fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, depth_fbo);
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthAttachment,
                TextureTarget.Texture2D, depth_tex_id, 0);
            GL.DrawBuffer(DrawBufferMode.None);
            GL.ReadBuffer(ReadBufferMode.None);

            FramebufferErrorCode status = GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer);
            if (status != FramebufferErrorCode.FramebufferComplete)
                Common.CallBacks.Log("Shadow framebuffer incomplete: {0}", status);

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        //Fits an orthographic projection looking down the light direction around the scene bounding sphere.
        //lightDir points from the scene towards the light. Returns false when there is nothing to fit
        public bool update(Vector3 lightDir, Vector3 center, float radius)
        {
            if (radius <= 0.0f)
                return false;

            //Same fallback as the shader for a zero key light position
            Vector3 L = lightDir.LengthSquared > 1e-8f ? lightDir.Normalized() : new Vector3(1.0f).Normalized();
            float r = 1.05f * radius;
            Vector3 eye = center + 2.0f * r * L;
            Vector3 up = Math.Abs(L.Y) < 0.99f ? Vector3.UnitY : Vector3.UnitX;

            Matrix4 view = Matrix4.LookAt(eye, center, up);
            Matrix4 proj = Matrix4.CreateOrthographic(2.0f * r, 2.0f * r, r, 3.0f * r);
            lightViewProj = view * proj;
            texelWorldSize = 2.0f * r / SHADOW_MAP_SIZE;
            return true;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    GL.DeleteTexture(depth_tex_id);
                    GL.DeleteFramebuffer(depth_fbo);
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion

    }
}
