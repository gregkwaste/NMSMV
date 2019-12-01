using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Generic;


namespace GLSLHelper { 

    //Delegate function for sending requests to the active GL control
    public delegate void GLSLShaderModRequest(GLSLShaderConfig config, string shaderText, ShaderType shadertype);
    public delegate void GLSLShaderCompileRequest(GLSLShaderConfig config);

    public enum SHADER_TYPE
    {
        NULL_SHADER = 0x0,
        MESH_SHADER,
        DECAL_SHADER,
        DEBUG_MESH_SHADER,
        PICKING_SHADER,
        BBOX_SHADER,
        LOCATOR_SHADER,
        JOINT_SHADER,
        CAMERA_SHADER,
        TEXTURE_MIX_SHADER,
        PASSTHROUGH_SHADER,
        LIGHT_SHADER,
        TEXT_SHADER,
        GBUFFER_SHADER,
        GAUSSIAN_BLUR_SHADER
    }

    public class GLSLShaderConfig
    {
        public string name = "";
        //Store the raw shader text temporarily
        public string vs = "";
        public string gs = "";
        public string tcs = "";
        public string tes = "";
        public string fs = "";
        public SHADER_TYPE shader_type = SHADER_TYPE.NULL_SHADER;

        //FileSystemWatcher lists
        private Dictionary<FileSystemWatcher, string> FileWatcherDict = new Dictionary<FileSystemWatcher, string>();
        
        //Publically hold the filepaths of the shaders
        //For now I am keeping the paths in the filewatcher
        
        //Program ID
        public int program_id = -1;
        //Shader Compilation log
        public string log = "";

        //Keep active uniforms
        public Dictionary<string, int> uniformLocations = new Dictionary<string, int>();

        public GLSLShaderModRequest modifyShader;
        public GLSLShaderCompileRequest compileShader;
        
        public GLSLShaderConfig(string vvs, string ffs, string ggs, string ttcs, string ttes, SHADER_TYPE type)
        {
            shader_type = type;
            
            //Vertex Shader
            if (vvs != "")
            {
                //Fetch data and store it to vs if a file
                if (vvs.EndsWith(".glsl"))
                {
                    FileSystemWatcher fw = new FileSystemWatcher();
                    fw.Changed += new FileSystemEventHandler(file_changed);
                    fw.Path = Path.GetDirectoryName(vvs);
                    fw.Filter = Path.GetFileName(vvs);
                    fw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                    fw.EnableRaisingEvents = true;
                    vs = GLSL_Preprocessor.Parser(File.ReadAllText(vvs)); //Fetch data 
                    FileWatcherDict[fw] = "vs"; //Set entry in dictionary
                }
                else
                    vs = GLSL_Preprocessor.Parser(vvs);
            }

            //Fragment Shader
            if (ffs != "")
            {
                //Fetch data and store it to vs if a file
                if (ffs.EndsWith(".glsl"))
                {
                    //Fetch data and store it to fs
                    FileSystemWatcher fw = new FileSystemWatcher();
                    fw.Path = Path.GetDirectoryName(ffs);
                    fw.Filter = Path.GetFileName(ffs);
                    fw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
                    fw.EnableRaisingEvents = true;
                    fs = GLSL_Preprocessor.Parser(File.ReadAllText(ffs)); //Fetch data 
                    fw.Changed += new FileSystemEventHandler(file_changed);
                    FileWatcherDict[fw] = "fs"; //Set entry in dictionary
                }
                else
                    fs = GLSL_Preprocessor.Parser(ffs); ;
            }

            //Geometry Shader
            if (ggs != "")
            {
                //Fetch data and store it to vs if a file
                if (ggs.EndsWith(".glsl"))
                {
                    //Fetch data and store it to gs
                    FileSystemWatcher fw = new FileSystemWatcher();
                    fw.Path = Path.GetDirectoryName(ggs);
                    fw.Filter = Path.GetFileName(ggs);
                    fw.NotifyFilter =   NotifyFilters.LastWrite | NotifyFilters.Size;
                    fw.EnableRaisingEvents = true;
                    gs = GLSL_Preprocessor.Parser(File.ReadAllText(ggs)); //Fetch data 
                    fw.Changed += new FileSystemEventHandler(file_changed);
                    FileWatcherDict[fw] = "gs"; //Set entry in dictionary
                }
                else
                    gs = GLSL_Preprocessor.Parser(ggs);
            }

        }


        public int getLocation(string name)
        {
            if (uniformLocations.ContainsKey(name))
                return uniformLocations[name];
            return -1;
        }

        private void file_changed(object sender, FileSystemEventArgs e)
        {
            FileSystemWatcher fw = (FileSystemWatcher) sender;
            string path = Path.Combine(fw.Path, fw.Filter);
            Console.WriteLine("Reloading {0}", path);
            string data = "";
            bool islocked = true;
            while (islocked)
            {
                try
                {
                    data = File.ReadAllText(path);
                    islocked = false;
                }
                catch (System.IO.IOException)
                {
                    continue;
                }

            }

            data = GLSL_Preprocessor.Parser(data);
            string old_data = "";

            //Set data
            ShaderType typ = ShaderType.VertexShader; //default value, SHOULD change

            switch (FileWatcherDict[fw])
            {
                case "vs":
                    old_data = vs;
                    vs = data;
                    typ = ShaderType.VertexShader;
                    break;
                case "fs":
                    old_data = fs;
                    fs = data;
                    typ = ShaderType.FragmentShader;
                    break;
                case "gs":
                    old_data = gs;
                    gs = data;
                    typ = ShaderType.GeometryShader;
                    break;
                case "tes":
                    old_data = tes;
                    tes = data;
                    typ = ShaderType.TessEvaluationShader;
                    break;
                case "tcs":
                    old_data = tcs;
                    tcs = data;
                    typ = ShaderType.TessControlShader;
                    break;
            }

            //Checksum the shader source before sending a request
            if (old_data.GetHashCode() != data.GetHashCode())
            {
                //Send shader modification request
                modifyShader(this, data, typ);
            }
            
        }
    }


    public static class GLShaderHelper
    {
        static private string NumberLines(string s)
        {
            if (s == "")
                return s;
                
            string n_s = "";
            string[] split = s.Split('\n');

            for (int i = 0; i < split.Length; i++)
                n_s += (i + 1).ToString() + ": " + split[i] + "\n";
            
            return n_s;
        }
        
        //Shader Creation
        static public void CreateShaders(GLSLShaderConfig config, out int vertexObject,
            out int fragmentObject, out int program)
        {
            int status_code;
            string info;
            bool gsflag = false;
            bool tsflag = false;
            if (!(config.gs == "")) gsflag = true;
            if (!((config.tcs == "") & (config.tes == ""))) tsflag = true;

            //Write Shader strings
            
            config.log += NumberLines(config.vs) + "\n";
            config.log += NumberLines(config.fs) + "\n";
            config.log += NumberLines(config.gs) + "\n";
            config.log += NumberLines(config.tcs) + "\n";
            config.log += NumberLines(config.tes) + "\n";
            
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
            GL.ShaderSource(vertexObject, config.vs);
            GL.CompileShader(vertexObject);
            GL.GetShaderInfoLog(vertexObject, out info);
            config.log += info + "\n";
            GL.GetShader(vertexObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throwCompilationError(config.log);

            //Compile fragment Shader
            GL.ShaderSource(fragmentObject, config.fs);
            GL.CompileShader(fragmentObject);
            GL.GetShaderInfoLog(fragmentObject, out info);
            config.log += info + "\n";
            GL.GetShader(fragmentObject, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                throwCompilationError(config.log);

            //Compile Geometry Shader
            if (gsflag)
            {
                GL.ShaderSource(geometryObject, config.gs);
                GL.CompileShader(geometryObject);
                GL.GetShaderInfoLog(geometryObject, out info);
                config.log += info + "\n";
                GL.GetShader(geometryObject, ShaderParameter.CompileStatus, out status_code);
                if (status_code != 1)
                    throwCompilationError(config.log);
            }

            //Compile Tesselation Shaders
            if (tsflag)
            {
                //Control Shader
                GL.ShaderSource(tcsObject, config.tcs);
                GL.CompileShader(tcsObject);
                GL.GetShaderInfoLog(tcsObject, out info);
                config.log += info + "\n";
                GL.GetShader(tcsObject, ShaderParameter.CompileStatus, out status_code);
                if (status_code != 1)
                    throwCompilationError(config.log);

                GL.ShaderSource(tesObject, config.tes);
                GL.CompileShader(tesObject);
                GL.GetShaderInfoLog(tesObject, out info);
                config.log += info + "\n";
                GL.GetShader(tesObject, ShaderParameter.CompileStatus, out status_code);
                if (status_code != 1)
                    throwCompilationError(config.log);
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
            config.log += info + "\n";
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out status_code);
            if (status_code != 1)
                throwCompilationError(config.log);

            loadActiveUniforms(config);
        }


        static private void loadActiveUniforms(GLSLShaderConfig shader_conf)
        {
            int active_uniforms_count;
            GL.GetProgram(shader_conf.program_id, GetProgramParameterName.ActiveUniforms, out active_uniforms_count);

            shader_conf.uniformLocations.Clear(); //Reset locataions
            shader_conf.log += "Active Uniforms: " + active_uniforms_count.ToString() + "\n";
            for (int i = 0; i < active_uniforms_count; i++)
            {
                int size;
                int bufSize = 64;
                int length;
                string name;
                ActiveUniformType type;
                int loc;

                GL.GetActiveUniform(shader_conf.program_id, i, bufSize, out size, out length, out type, out name);
                loc = GL.GetUniformLocation(shader_conf.program_id, name);
                string info_string = String.Format("Uniform # {0} Location: {1} Type: {2} Name: {3} Length: {4} Size: {5}",
                    i, loc, type.ToString(), name, length, size);

                shader_conf.uniformLocations[name] = loc; //Store location
                shader_conf.log += info_string + "\n";
            }
        }
        
        static public void modifyShader(GLSLShaderConfig shader_conf, string shaderText, OpenTK.Graphics.OpenGL4.ShaderType shadertype)
        {
            Console.WriteLine("Actually Modifying Shader");

            int[] attached_shaders = new int[20];
            int count;
            GL.GetAttachedShaders(shader_conf.program_id, 20, out count, attached_shaders);

            for (int i = 0; i < count; i++)
            {
                int[] shader_params = new int[10];
                GL.GetShader(attached_shaders[i], OpenTK.Graphics.OpenGL4.ShaderParameter.ShaderType, shader_params);

                if (shader_params[0] == (int)shadertype)
                {
                    Console.WriteLine("Found modified shader");

                    string info;
                    int status_code;
                    int new_shader_ob = GL.CreateShader(shadertype);
                    GL.ShaderSource(new_shader_ob, shaderText);
                    GL.CompileShader(new_shader_ob);
                    GL.GetShaderInfoLog(new_shader_ob, out info);
                    GL.GetShader(new_shader_ob, OpenTK.Graphics.OpenGL4.ShaderParameter.CompileStatus, out status_code);
                    if (status_code != 1)
                    {
                        Console.WriteLine("Shader Compilation Failed, Aborting...");
                        Console.WriteLine(info);
                        return;
                    }

                    //Attach new shader back to program
                    GL.DetachShader(shader_conf.program_id, attached_shaders[i]);
                    GL.AttachShader(shader_conf.program_id, new_shader_ob);
                    GL.LinkProgram(shader_conf.program_id);

                    GL.GetProgram(shader_conf.program_id, GetProgramParameterName.LinkStatus, out status_code);
                    if (status_code != 1)
                    {
                        Console.WriteLine("Unable to link the new shader. Reverting to the old shader");
                        Console.WriteLine(info);

                        //Relink the old shader
                        GL.DetachShader(shader_conf.program_id, new_shader_ob);
                        GL.AttachShader(shader_conf.program_id, attached_shaders[i]);
                        GL.LinkProgram(shader_conf.program_id);
                        return;
                    }

                    //Delete old shader and reload uniforms
                    GL.DeleteShader(attached_shaders[i]);
                    loadActiveUniforms(shader_conf); //Re-load active uniforms

                    Console.WriteLine("Shader was modified successfully");
                    break;
                }
            }
            Console.WriteLine("Shader was not found...");
        }


        private static void throwCompilationError(string log)
        {
            StreamWriter sr = new StreamWriter("shader_compilation_log_" +DateTime.Now.ToFileTime() + "_log.out");
            sr.Write(log);
            sr.Close();
            Console.WriteLine(log);
            throw new ApplicationException("Shader Compilation Failed. Check Log");
        }
    }
}


