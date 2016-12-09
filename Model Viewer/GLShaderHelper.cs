using System;
using OpenTK.Graphics.OpenGL;


namespace GLHelpers
{
    class GLShaderHelper
    {
        //Shader Creation
        static public void CreateShaders(string vs, string fs, string gs, out int vertexObject,
            out int fragmentObject, out int program)
        {
            int status_code;
            string info;
            bool gsflag = false;
            if (!(gs == "")) gsflag = true;
            

            vertexObject = GL.CreateShader(ShaderType.VertexShader);
            fragmentObject = GL.CreateShader(ShaderType.FragmentShader);
            int geometryObject = -1;
            if (gsflag) geometryObject = GL.CreateShader(ShaderType.GeometryShader);
            
            //Compile vertex Shader
            GL.ShaderSource(vertexObject, vs);
            GL.CompileShader(vertexObject);
            GL.GetShaderInfoLog(vertexObject, out info);
            GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throw new ApplicationException(info);

            //Compile fragment Shader
            GL.ShaderSource(fragmentObject, fs);
            GL.CompileShader(fragmentObject);
            GL.GetShaderInfoLog(fragmentObject, out info);
            GL.GetShader(fragmentObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throw new ApplicationException(info);

            //Compile Geometry Shader
            if (gsflag)
            {
                GL.ShaderSource(geometryObject, gs);
                GL.CompileShader(geometryObject);
                GL.GetShaderInfoLog(geometryObject, out info);
                GL.GetShader(geometryObject, ShaderParameter.CompileStatus, out status_code);
                if (status_code != 1)
                    throw new ApplicationException(info);
            }

            program = GL.CreateProgram();
            GL.AttachShader(program, fragmentObject);
            GL.AttachShader(program, vertexObject);
            if (gsflag) GL.AttachShader(program, geometryObject);
            GL.LinkProgram(program);
            //GL.UseProgram(program);

        }
    }
}
