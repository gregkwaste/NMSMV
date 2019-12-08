using System;
using System.IO;
using OpenTK.Graphics.OpenGL4;
using System.Collections.Generic;
using System.Reflection;
using MVCore;

namespace GLSLHelper { 

    //Delegate function for sending requests to the active GL control
    public delegate void GLSLShaderModRequest(GLSLShaderConfig config, GLSLShaderText shaderText);
    public delegate void GLSLShaderCompileRequest(GLSLShaderConfig config);
    
    public enum SHADER_TYPE
    {
        NULL_SHADER = 0x0,
        MESH_FORWARD_SHADER,
        MESH_DEFERRED_SHADER,
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

    public class GLSLShaderText
    {
        public string[] strings;
        public int[] string_lengths;
        public string[] filepaths;
        public int string_num = 0;
        private int max_strings_num = 10;
        public string resolved_text = ""; //Full shader text after resolving
        public int shader_object_id;
        public ShaderType shader_type;

        public string compilation_log = ""; //Keep track of the generated log during shader compilation
        
        public List<GLSLShaderConfig> parentShaders = new List<GLSLShaderConfig>(); //Keeps track of all the Shaders that the current text belongs to

        //Static random generator used in temp file name generation
        private static Random rand_gen = new Random(999991);

        //FileSystemWatcher lists
        private Dictionary<FileSystemWatcher, int> FileWatcherDict = new Dictionary<FileSystemWatcher, int>();

        public GLSLShaderText(ShaderType type)
        {
            shader_type = type; //Set shader type
            strings = new string[max_strings_num];
            string_lengths = new int[max_strings_num];
            filepaths = new string[max_strings_num];
            //Add the version string by default
            addString(GLSLShaderConfig.version);
        }

        public void addString(string s)
        {
            strings[string_num] = Parser(s, true);
            string_lengths[string_num] = s.Length;
            string_num++;
        }

        private void addFileWatcher(string filepath)
        {
            FileSystemWatcher fw = new FileSystemWatcher();
            fw.Changed += new FileSystemEventHandler(file_changed);
            fw.Path = Path.GetDirectoryName(filepath);
            fw.Filter = Path.GetFileName(filepath);
            fw.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size;
            fw.EnableRaisingEvents = true;
            FileWatcherDict[fw] = string_num;
        }

        public void addStringFromFile(string filepath)
        {
            filepaths[string_num] = filepath; //Save filepath
            addString(filepath);
        }

        
        public void compile()
        {
            compilation_log = ""; //Reset compilation log
            shader_object_id = GL.CreateShader(shader_type);
            string info, actual_shader_source;
            int status_code, actual_shader_length;
            
            //Compile Shader
            GL.ShaderSource(shader_object_id, string_num, strings, (int[]) null);
            
            //Get resolved shader text
            GL.GetShaderSource(shader_object_id, 4096, out actual_shader_length, out actual_shader_source);
            resolved_text = actual_shader_source; //Store full shader code
            
            GL.CompileShader(shader_object_id);
            GL.GetShaderInfoLog(shader_object_id, out info);

            compilation_log += info + "\n";
            GL.GetShader(shader_object_id, ShaderParameter.CompileStatus, out status_code);
            if (status_code != 1)
                GLShaderHelper.throwCompilationError(compilation_log);
        }

        public void resolve()
        {
            //Here we should retrieve the full text of the shader
            
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

            data = Parser(data, false);
            string old_data = strings[FileWatcherDict[fw]];


            //Checksum the shader source before sending a request
            if (old_data.GetHashCode() != data.GetHashCode())
            {
                //Replace data
                strings[FileWatcherDict[fw]] = data;

                //Send shader modification request for all the parrent shaders
                //They just need to link the new shader_object into their programs
                foreach (GLSLShaderConfig shader in parentShaders)
                {
                    shader.modifyShader(shader, this);
                }
            }
        }

        private string Parser(string path, bool initWatchers)
        {
            //Make sure that the input file is indeed a file
            StreamReader sr;
            string[] split;
            string relpath = "";
            string text = "";
            string tmp_file = "tmp_" + rand_gen.Next().ToString();
            Console.WriteLine("Using temp file {0}", tmp_file);
            bool use_tmp_file = false;
            if (path.EndsWith(".glsl"))
            {
                string execPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                path = Path.Combine(execPath, path);
                Console.WriteLine(path);
                //Check if file exists
                if (!File.Exists(path))
                {
                    //Because of shader files coming either in raw or path format, I should check for resources in
                    //the local Shaders folder as well
                    string basename = Path.GetFileName(path);
                    string dirname = Path.GetDirectoryName(path);
                    path = Path.Combine(dirname, "Shaders", basename);
                    if (!File.Exists(path))
                        throw new ApplicationException("Preprocessor: File not found. Check the input filepath");
                }

                //Add filewatcher
                if (initWatchers)
                    addFileWatcher(path);

                split = Path.GetDirectoryName(path).Split(Path.PathSeparator);
                relpath = split[split.Length - 1];

                //FileStream fs = new FileStream(path, FileMode.Open);
                sr = new StreamReader(path);
            }
            else
            {
                //Shader has been provided in a raw string
                //Save it to a temp file
                File.WriteAllText(tmp_file, path);
                sr = new StreamReader(tmp_file);
                use_tmp_file = true;
            }

            string line;
            while ((line = sr.ReadLine()) != null)
            {
                //string line = sr.ReadLine();
                string outline = line;
                line = line.TrimStart(new char[] { ' ' });

                //Check for preprocessor directives
                if (line.StartsWith("#include"))
                {
                    split = line.Split(' ');

                    if (split.Length != 2)
                        throw new ApplicationException("Wrong Usage of #include directive");

                    //get included filepath
                    string npath = split[1].Trim('"');
                    npath = npath.TrimStart('/');
                    npath = Path.Combine(relpath, npath);
                    outline = Parser(npath, initWatchers);
                }
                //Skip Comments
                else if (line.StartsWith("///")) continue;

                //Finally append the parsed text
                text += outline + '\n';
                //sw.WriteLine(outline);
            }
            //CLose readwrites

            sr.Close();
            if (use_tmp_file)
            {
                File.Delete(tmp_file);
            }
            return text;
        }

    }

    

    public class GLSLShaderConfig
    {
        public string name = "";
        //Store the raw shader text objects temporarily
        public GLSLShaderText vs_text;
        public GLSLShaderText fs_text;
        public GLSLShaderText gs_text;
        public GLSLShaderText tcs_text;
        public GLSLShaderText tes_text;

        
        //Store the raw shader text temporarily
        public SHADER_TYPE shader_type = SHADER_TYPE.NULL_SHADER;

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

        //Default shader versions
        public static string version = "#version 450\n #extension GL_ARB_explicit_uniform_location : enable\n" +
                                       "#extension GL_ARB_separate_shader_objects : enable\n"+
                                       "#extension GL_ARB_texture_query_lod : enable\n"+
                                       "#extension GL_ARB_gpu_shader5 : enable\n";
        
        public GLSLShaderConfig(GLSLShaderText vvs, GLSLShaderText ffs, GLSLShaderText ggs, GLSLShaderText ttcs, GLSLShaderText ttes, SHADER_TYPE type)
        {
            shader_type = type; //Set my custom shader type for recognition
            //Store objects
            fs_text = ffs;
            gs_text = ggs;
            vs_text = vvs;
            tes_text = ttes;
            tcs_text = ttcs;

            //Set parents to the shader objects
            ffs?.parentShaders.Add(this);
            ggs?.parentShaders.Add(this);
            vvs?.parentShaders.Add(this);
            ttes?.parentShaders.Add(this);
            ttcs?.parentShaders.Add(this);
        
        }


        public int getLocation(string name)
        {
            if (uniformLocations.ContainsKey(name))
                return uniformLocations[name];
            return -1;
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
        static public void CreateShaders(GLSLShaderConfig config)
        {
            int status_code;
            string info;
            bool gsflag = false;
            bool tsflag = false;

            if (!(config.gs_text == null)) gsflag = true;
            if (!((config.tcs_text == null) & (config.tes_text == null))) tsflag = true;

            //Compile vertex shader
            
            if (config.vs_text != null)
            {
                config.vs_text.compile();
                config.log += NumberLines(config.vs_text.resolved_text) + "\n";
            }

            if (config.fs_text != null)
            {
                config.fs_text.compile();
                config.log += NumberLines(config.fs_text.resolved_text) + "\n";
            }

            if (config.gs_text != null)
            {
                config.gs_text.compile();
                config.log += NumberLines(config.gs_text.resolved_text) + "\n";
            }

            if (config.tes_text != null)
            {
                config.tes_text.compile();
                config.log += NumberLines(config.tes_text.resolved_text) + "\n";
            }

            if (config.tcs_text != null)
            {
                config.tcs_text.compile();
                config.log += NumberLines(config.tcs_text.resolved_text) + "\n";
            }
            
            //Create new program
            config.program_id = GL.CreateProgram();

            //Attach shaders to program
            GL.AttachShader(config.program_id, config.vs_text.shader_object_id);
            GL.AttachShader(config.program_id, config.fs_text.shader_object_id);

            if (gsflag)
                GL.AttachShader(config.program_id, config.gs_text.shader_object_id);
            
            if (tsflag)
            {
                GL.AttachShader(config.program_id, config.tcs_text.shader_object_id);
                GL.AttachShader(config.program_id, config.tes_text.shader_object_id);
            }

            GL.LinkProgram(config.program_id);
            
            //Check Linking
            GL.GetProgramInfoLog(config.program_id, out info);
            config.log += info + "\n";
            GL.GetProgram(config.program_id, GetProgramParameterName.LinkStatus, out status_code);
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
        
        static public void modifyShader(GLSLShaderConfig shader_conf, GLSLShaderText shaderText)
        {
            Console.WriteLine("Actually Modifying Shader");

            int[] attached_shaders = new int[20];
            int count;
            int status_code;
            string info;
            GL.GetAttachedShaders(shader_conf.program_id, 20, out count, attached_shaders);

            for (int i = 0; i < count; i++)
            {
                int[] shader_params = new int[10];
                GL.GetShader(attached_shaders[i], OpenTK.Graphics.OpenGL4.ShaderParameter.ShaderType, shader_params);

                if (shader_params[0] == (int) shaderText.shader_type)
                {
                    Console.WriteLine("Found modified shader");

                    //Trying to compile shader
                    shaderText.compile();

                    //Attach new shader back to program
                    GL.DetachShader(shader_conf.program_id, attached_shaders[i]);
                    GL.AttachShader(shader_conf.program_id, shaderText.shader_object_id);
                    GL.LinkProgram(shader_conf.program_id);

                    GL.GetProgram(shader_conf.program_id, GetProgramParameterName.LinkStatus, out status_code);
                    if (status_code != 1)
                    {
                        Console.WriteLine("Unable to link the new shader. Reverting to the old shader");
                        return;
                    }

                    //Delete old shader and reload uniforms
                    loadActiveUniforms(shader_conf); //Re-load active uniforms
                    Console.WriteLine("Shader was modified successfully");
                    break;
                }
            }
            Console.WriteLine("Shader was not found...");
        }


        public static void throwCompilationError(string log)
        {
            //Lock execution until the file is available
            string log_file = "shader_compilation_log.out";

            if (!File.Exists(log_file))
                File.Create(log_file);
            
            while (!FileUtils.IsFileReady(log_file)) { };
            
            StreamWriter sr = new StreamWriter(log_file);
            sr.Write(log);
            sr.Close();
            Console.WriteLine(log);
            throw new ApplicationException("Shader Compilation Failed. Check Log");
        }
    }
}


