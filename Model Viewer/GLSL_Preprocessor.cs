using System;
using System.Diagnostics;
using System.Reflection;
using System.IO;

namespace Model_Viewer
{
    static class GLSL_Preprocessor
    {
        static public string Parser(string path)
        {
            string execPath = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            path = Path.Combine(execPath, path);
            Debug.WriteLine(path);
            //Check if file exists
            if (!File.Exists(path))
                throw new ApplicationException("Preprocessor: File not found. Check the input filepath");

            string[] split = Path.GetDirectoryName(path).Split(Path.PathSeparator);
            string relpath = split[split.Length - 1];

            string text = "";
            //FileStream fs = new FileStream(path, FileMode.Open);
            StreamReader sr = new StreamReader(path);
            Stream os = new MemoryStream();
            StreamWriter sw = new StreamWriter(os, System.Text.Encoding.UTF8);

            string line;
            while ((line=sr.ReadLine()) != null)
            {
                //string line = sr.ReadLine();
                string outline = line;
                line = line.TrimStart(new char[] { ' ' });

                //Check for preprocessor directives
                if (line.StartsWith("#include")){
                    split = line.Split(' ');

                    if (split.Length != 2)
                        throw new ApplicationException("Wrong Usage of #include directive");

                    //get included filepath
                    string npath = split[1].Trim('"');
                    npath = npath.TrimStart('/');
                    npath = Path.Combine(relpath, npath);

                    outline = Parser(npath);
                }

                //Finally append the parsed text
                text += outline + '\n';
                //sw.WriteLine(outline);
            }
            //CLose readwrites
            
            //sr.Close();

            //os.Seek(0,SeekOrigin.Begin);
            //sr = new StreamReader(os);
            //sw.Close();
            return text;

        }

    }
}
