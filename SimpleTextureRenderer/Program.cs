using System;
using MVCore.GMDL;
using OpenTK;
using GLSLHelper;
using OpenTK.Graphics.OpenGL4;


namespace SimpleTextureRenderer
{
    public class TextureRenderer : OpenTK.GameWindow
    {
        private int texture_id;
        private GLSLShaderConfig shader_conf;
        private int quad_vao_id;

        public TextureRenderer()
            : base(800, 600, OpenTK.Graphics.GraphicsMode.Default, "Texture Renderer")
        {
            VSync = OpenTK.VSyncMode.On;

        }

        public void compileShader(GLSLShaderConfig config)
        {
            if (config.program_id != -1)
                GL.DeleteProgram(config.program_id);

            GLShaderHelper.CreateShaders(config);
        }


        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            GL.ClearColor(0.1f, 0.2f, 0.5f, 0.0f);
            //GL.Enable(EnableCap.DepthTest);

            //Setup Texture
            //string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\BEAMGRADIENT.DDS";
            string texturepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\TEXTURES\\PLANETS\\BIOMES\\WEIRD\\BEAMSTONE\\SCROLLINGCLOUD.DDS";
            Texture tex = new Texture(texturepath);

            texture_id = tex.bufferID;


            string log = "";
            //Compile Necessary Shaders
            //Pass Shader
            GLSLShaderText gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            GLSLShaderText passthrough_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            passthrough_shader_fs.addStringFromFile("Shaders/PassThrough_FS.glsl");


            shader_conf = new GLSLShaderConfig(gbuffer_shader_vs, passthrough_shader_fs,
                null, null, null, SHADER_TYPE.PASSTHROUGH_SHADER);
            compileShader(shader_conf);

            //Generate Geometry

            //Default render quad
            MVCore.Primitives.Quad q = new MVCore.Primitives.Quad();
            quad_vao_id = q.getVAO().vao_id;
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);
            GL.Viewport(0, 0, 800, 600);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            GL.ClearColor(0.1f, 0.2f, 0.5f, 0.0f);

            GL.UseProgram(shader_conf.program_id);
            Console.WriteLine("1" + GL.GetError());

            GL.BindVertexArray(quad_vao_id);
            Console.WriteLine("2" + GL.GetError());

            //Upload texture
            GL.Uniform1(shader_conf.uniformLocations["InTex"], 0);
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2DArray, texture_id);

            Console.WriteLine("3" + GL.GetError());

            //Render quad
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, (IntPtr)0);
            GL.BindVertexArray(0);

            Console.WriteLine("4" + GL.GetError());

            SwapBuffers();
        }

        [STAThread]
        public void Main()
        {
            using (TextureRenderer tx = new TextureRenderer())
            {
                tx.Run(60.0);
            }

            Console.WriteLine("All Good");
        }
    }
}
