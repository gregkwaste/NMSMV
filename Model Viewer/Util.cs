using System;
using System.Collections.Generic;
using System.IO;
using OpenTK;
using System.Xml;
using System.Diagnostics;

namespace Model_Viewer
{
    public static class Util
    {
        public static readonly Random randgen = new Random();
        public static float[] JMarray = new float[256 * 16];

        public static ResourceMgmt activeResMgmt;

        //Current GLControl Handle
        public static CGLControl activeControl;

        //Temporarily store mvp matrix
        public static Matrix4 mvp;

        //Current Gbuffer
        public static GBuffer gbuf;

        //Active Gamepad ID
        public static int gamepadID;

        public static string dirpath;
        public static int procGenNum;
        public static bool forceProcGen;

        public static float[] mulMatArrays(float[] lmat1, float[] lmat2, int count)
        {
            float[] res = new float[count * 16];
            for (int i = 0; i < count; i++)
            {
                int off = 16 * i;
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 4; k++)
                        for (int m = 0; m < 4; m++)
                            res[off + 4 * j + k] += lmat1[off + 4 * j + m] * lmat2[off + 4 * m + k];
            }

            return res;
        }

        public static void mulMatArrays(ref float[] dest, float[] lmat1, float[] lmat2, int count)
        {
            for (int i = 0; i < count; i++)
            {
                int off = 16 * i;
                for (int j = 0; j < 4; j++)
                    for (int k = 0; k < 4; k++)
                        for (int m = 0; m < 4; m++)
                            dest[off + 4 * j + k] += lmat1[off + 4 * j + m] * lmat2[off + 4 * m + k];
            }

        }

        //Add matrix to JMArray
        public static void insertMatToArray16(float[] array, int offset, Matrix4 mat)
        {
            //mat.Transpose();//Transpose Matrix Testing
            array[offset + 0] = mat.M11;
            array[offset + 1] = mat.M12;
            array[offset + 2] = mat.M13;
            array[offset + 3] = mat.M14;
            array[offset + 4] = mat.M21;
            array[offset + 5] = mat.M22;
            array[offset + 6] = mat.M23;
            array[offset + 7] = mat.M24;
            array[offset + 8] = mat.M31;
            array[offset + 9] = mat.M32;
            array[offset + 10] = mat.M33;
            array[offset + 11] = mat.M34;
            array[offset + 12] = mat.M41;
            array[offset + 13] = mat.M42;
            array[offset + 14] = mat.M43;
            array[offset + 15] = mat.M44;
        }

        public static void insertMatToArray12Trans(float[] array, int offset, Matrix4 mat)
        {
            //mat.Transpose();//Transpose Matrix Testing
            array[offset + 0] = mat.M11;
            array[offset + 1] = mat.M21;
            array[offset + 2] = mat.M31;
            array[offset + 3] = mat.M41;
            array[offset + 4] = mat.M12;
            array[offset + 5] = mat.M22;
            array[offset + 6] = mat.M32;
            array[offset + 7] = mat.M42;
            array[offset + 8] = mat.M13;
            array[offset + 9] = mat.M23;
            array[offset + 10] = mat.M33;
            array[offset + 11] = mat.M43;
        }


        //Check files
        public static bool compareFileSizes(string filepath1, string filepath2)
        {
            if (!File.Exists(filepath1)) return false;
            if (!File.Exists(filepath2)) return false;

            var f1 = new FileStream(filepath1, FileMode.Open);
            var f2 = new FileStream(filepath2, FileMode.Open);
            var l1 = f1.Length;
            var l2 = f2.Length;
            f1.Close();
            f2.Close();

            if (l1 != l2) return false;

            return true;
        }

        public static bool compareFilesHash(string filepath1, string filepath2)
        {
            using (var reader1 = new System.IO.FileStream(filepath1, System.IO.FileMode.Open, System.IO.FileAccess.Read))
            {
                using (var reader2 = new System.IO.FileStream(filepath2, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    byte[] hash1;
                    byte[] hash2;

                    using (var md51 = new System.Security.Cryptography.MD5CryptoServiceProvider())
                    {
                        md51.ComputeHash(reader1);
                        hash1 = md51.Hash;
                    }

                    using (var md52 = new System.Security.Cryptography.MD5CryptoServiceProvider())
                    {
                        md52.ComputeHash(reader2);
                        hash2 = md52.Hash;
                    }

                    int j = 0;
                    for (j = 0; j < hash1.Length; j++)
                    {
                        if (hash1[j] != hash2[j])
                        {
                            break;
                        }
                    }

                    if (j == hash1.Length)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
            }
        }




        //Convert Path to EXML
        public static string getExmlPath(string path)
        {
            //Fix Path incase of reference
            path = path.Replace('/', '\\');
            string[] split = path.Split('.');
            string newpath = "";
            //for (int i = 0; i < split.Length - 1; i++)
            //    newpath += split[i]+ "." ;
            //Get main name
            string[] pathsplit = split[0].Split('\\');
            newpath = pathsplit[pathsplit.Length - 1] + "." + split[split.Length - 2] + ".exml";

            return "Temp\\" + newpath;
        }

        public static string getFullExmlPath(string path)
        {
            //Get Relative path again
            string tpath = path.Replace(Util.dirpath, "").TrimStart('\\');

            //Fix Path incase of reference
            tpath = tpath.Replace('/', '\\');
            string[] split = tpath.Split('\\');
            string filename = split[split.Length - 1];

            string[] f_name_split = filename.Split('.');
            //Assemble new f_name
            string newfilename = "";
            for (int i = 0; i < f_name_split.Length - 1; i++)
            {
                newfilename += f_name_split[i] + ".";
            }
            newfilename += "exml";

            string newpath = "";
            //Get main name
            for (int i = 0; i < split.Length - 1; i++)
                newpath += split[i] + "_";

            newpath += newfilename;

            return "Temp\\" + newpath;
        }
        //MbinCompiler Caller
        public static void MbinToExml(string path, string output)
        {
            Process proc = new System.Diagnostics.Process();
            proc.StartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            proc.StartInfo.FileName = "MBINCompiler.exe";
            proc.StartInfo.Arguments = " \"" + path + "\" " + " \"" + output + "\" ";
            proc.Start();
            proc.WaitForExit();
        }
        //Update Status strip
        public static void setStatus(string status, System.Windows.Forms.ToolStripStatusLabel strip)
        {
            strip.Text = status;
            strip.Invalidate();
            strip.GetCurrentParent().Refresh();
        }

        //Parse a string of n and terminate if it is a null terminated string
        public static String read_string(BinaryReader br, int n)
        {
            string s = "";
            bool exit = true;
            long off = br.BaseStream.Position;
            while (exit)
            {
                Char c = br.ReadChar();
                if (c == 0)
                    exit = false;
                else
                    s += c;
            }

            br.BaseStream.Seek(off + 0x80, SeekOrigin.Begin);
            return s;
        }

        //XMLElement Documents
        public static XmlElement GetChildWithProp(XmlElement root, string field, string value)
        {
            for (int i = 0; i < root.ChildNodes.Count; i++)
            {
                XmlElement test = (XmlElement)root.ChildNodes[i].SelectSingleNode("Property[@name='" + field + "']");
                if (test != null)
                {
                    if (test.GetAttribute("value") == value)
                        return (XmlElement)root.ChildNodes[i];
                }
            }

            return null;
        }

        public static string GetPropValue(XmlElement root, string field)
        {
            XmlElement test = (XmlElement)root.SelectSingleNode("Property[@name='" + field + "']");
            return test.GetAttribute("value");
        }


        //
    }

}
