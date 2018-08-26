using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;


namespace GLSLHelper
{
    public static class GLShaderHelper
    {
        //Shader Creation
        static public void CreateShaders(string vs, string fs, string gs, string tcs, string tes, out int vertexObject,
            out int fragmentObject, out int program, ref string log)
        {
            int status_code;
            string info;
            bool gsflag = false;
            bool tsflag = false;
            if (!(gs == "")) gsflag = true;
            if (!((tcs == "") & (tes == ""))) tsflag = true;

            //Write Shader strings
            log += vs + "\n";
            log += fs + "\n";
            log += gs + "\n";
            log += tcs + "\n";
            log += tes + "\n";
            
            vertexObject = GL.CreateShader(ShaderType.VertexShader);
            fragmentObject = GL.CreateShader(ShaderType.FragmentShader);
            int geometryObject = -1;
            int tcsObject = -1;
            int tesObject = -1;
            if (gsflag) geometryObject = GL.CreateShader(ShaderType.GeometryShader);
            if (tsflag)
            {
                tcsObject = GL.CreateShader(ShaderType.TessControlShader);
                tesObject = GL.CreateShader(ShaderType.TessEvaluationShader);
            }

            //Compile vertex Shader
            GL.ShaderSource(vertexObject, vs);
            GL.CompileShader(vertexObject);
            GL.GetShaderInfoLog(vertexObject, out info);
            log += info + "\n";
            GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throwCompilationError(log);

            //Compile fragment Shader
            GL.ShaderSource(fragmentObject, fs);
            GL.CompileShader(fragmentObject);
            GL.GetShaderInfoLog(fragmentObject, out info);
            log += info + "\n";
            GL.GetShader(fragmentObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throwCompilationError(log);

            //Compile Geometry Shader
            if (gsflag)
            {
                GL.ShaderSource(geometryObject, gs);
                GL.CompileShader(geometryObject);
                GL.GetShaderInfoLog(geometryObject, out info);
                log += info + "\n";
                GL.GetShader(geometryObject, ShaderParameter.CompileStatus, out status_code);
                if (status_code != 1)
                    throwCompilationError(log);
            }

            //Compile Tesselation Shaders
            if (tsflag)
            {
                //Control Shader
                GL.ShaderSource(tcsObject, tcs);
                GL.CompileShader(tcsObject);
                GL.GetShaderInfoLog(tcsObject, out info);
                log += info + "\n";
                GL.GetShader(tcsObject, ShaderParameter.CompileStatus, out status_code);
                if (status_code != 1)
                    throwCompilationError(log);

                GL.ShaderSource(tesObject, tes);
                GL.CompileShader(tesObject);
                GL.GetShaderInfoLog(tesObject, out info);
                log += info + "\n";
                GL.GetShader(tesObject, ShaderParameter.CompileStatus, out status_code);
                if (status_code != 1)
                    throwCompilationError(log);
            }

            program = GL.CreateProgram();
            GL.AttachShader(program, fragmentObject);
            GL.AttachShader(program, vertexObject);
            if (gsflag) GL.AttachShader(program, geometryObject);
            if (tsflag)
            {
                GL.AttachShader(program, tcsObject);
                GL.AttachShader(program, tesObject);
            }
            GL.LinkProgram(program);

            //Check Linking
            GL.GetProgramInfoLog(program, out info);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out status_code);
            if (status_code != 1)
                throwCompilationError(log);


        }

        private static void throwCompilationError(string log)
        {
            StreamWriter sr = new StreamWriter("shader_compilation_log_" +DateTime.Now.ToFileTime() + "_log.out");
            sr.Write(log);
            sr.Close();
            throw new ApplicationException("Shader Compilation Failed. Check Log");
        }
    }
}


