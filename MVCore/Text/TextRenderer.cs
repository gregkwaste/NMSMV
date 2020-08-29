using System;
using OpenTK.Graphics.OpenGL4;
using MVCore.Common;
using GLSLHelper;
using MVCore.GMDL;

namespace MVCore.Text
{
    public class TextRenderer
    {
        private ResourceManager resMgr;

        public TextRenderer(ResourceManager mgr)
        {
            resMgr = mgr;
        }

        public void render()
        {
            GL.Enable(EnableCap.Blend);
            GL.Disable(EnableCap.DepthTest);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //Render info right on the 0 buffer

            GLSLShaderConfig shader = resMgr.GLShaders[SHADER_TYPE.TEXT_SHADER];
            GL.UseProgram(shader.program_id);

#if (DEBUG)
            //Upload test options to the shader
            //GL.Uniform1(shader.uniformLocations["edge"], RenderState.renderSettings.testOpt1);
            //GL.Uniform1(shader.uniformLocations["width"], RenderState.renderSettings.testOpt2);
#endif
            
            GL.Disable(EnableCap.CullFace);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            //Render texts included in Text manager
            foreach (Text t in resMgr.txtMgr.texts)
            {
                GLMeshVao m = t.meshVao;
                
                //Render Start
                //GL.Uniform1(shader.uniformLocations["size"], m.material.CustomPerMaterialUniforms["size"].Vec.X);
                GL.Uniform1(shader.uniformLocations["fontSize"], (float) t.font.Size);
                GL.Uniform1(shader.uniformLocations["textSize"], t.lineHeight);
                GL.Uniform2(shader.uniformLocations["offset"], t.pos);
                GL.Uniform3(shader.uniformLocations["color"], t.color);
                //GL.Uniform2(shader.uniformLocations["textDim"], t.size);
                m.render(shader, RENDERPASS.FORWARD);
            }
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.Enable(EnableCap.CullFace);

            GL.PolygonMode(MaterialFace.FrontAndBack, RenderState.renderSettings.RENDERMODE);
            GL.Enable(EnableCap.DepthTest);
            GL.Disable(EnableCap.Blend);

        }
    }
}
