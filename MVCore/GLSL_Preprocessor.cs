using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;


namespace GLSLHelper
{
    public static class GLSL_Preprocessor
    {
        static public string Parser(string path)
        {
            //Make sure that the input file is indeed a file
            StreamReader sr;
            string[] split;
            string relpath = "";
            string text = "";
            string tmp_file = "tmp_" + (new Random()).Next().ToString();
            bool use_tmp_file = false;
            if (path.EndsWith(".glsl"))
            {
                string execPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                path = Path.Combine(execPath, path);
                Debug.WriteLine(path);
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
            while ((line=sr.ReadLine()) != null)
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

                    outline = Parser(npath);
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
                File.Delete(tmp_file);
            return text;

        }

    }
}
