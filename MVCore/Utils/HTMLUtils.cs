using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace MVCore.Utils
{
    public static class HTMLUtils
    {

        public static string request(Uri uri)
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:79.0) Gecko/20100101 Firefox/79.0";
            request.Accept = "text/ html,application / xhtml + xml,application / xml; q = 0.9,image / webp,*/*;q=0.8";
            request.Headers.Add(HttpRequestHeader.AcceptEncoding, "gzip, deflate, br");
            request.Headers.Add(HttpRequestHeader.AcceptLanguage, "en-US,en;q=0.5");
            request.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            request.Proxy = new WebProxy();

            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            using (Stream stream = response.GetResponseStream())
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        public static bool fetchLibMBINDLL()
        {
            Uri uri = new Uri("https://api.github.com/repos/monkeyman192/MBINCompiler/releases/latest");
            string resp = request(uri);

            Dictionary<string, dynamic> data = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(resp);

            var assets = data["assets"];
            var libMBINVersion = data["tag_name"];
            //Iterate in assets
            //Dictionary<string, dynamic> assets = JsonConvert.DeserializeObject<Dictionary<string, dynamic>>(data["assets"]);

            string downloadUrl = "";
            foreach (var k in assets)
            {
                if (k["name"] == "libMBIN-dotnet4.dll")
                {
                    downloadUrl = k["browser_download_url"];
                    break;
                }
            }

            //local attributes
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string assemblyLoc = Path.Combine(assemblyDir, "libMBIN.dll");
            string assemblyVersion = Assembly.LoadFile(assemblyLoc).GetName().Version.ToString();


            //fetch online version to temp

            if (downloadUrl == "")
                return false;

            
            if (File.Exists("_templibMBIN.dll"))
                File.Delete("_templibMBIN.dll");

            Common.CallBacks.Log("Downloading latest libMBIN release");
            using (var client = new WebClient())
            {
                client.DownloadFile(downloadUrl, "_templibMBIN.dll");
            }
            
            //Fetch version of temp assembly
            string tempAssemblyLoc = Path.Combine(assemblyDir, "_templibMBIN.dll");
            string tempAssemblyVersion = Assembly.LoadFile(tempAssemblyLoc).GetName().Version.ToString();

            DialogResult res = MessageBox.Show("Old Version: " + assemblyVersion + " Online Version: " + tempAssemblyVersion + "\nDo you want to update?", "Info", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

            if (res == DialogResult.Yes)
            {
                if (File.Exists("libMBIN_old.dll"))
                    File.Delete("libMBIN_old.dll");
                File.Move("libMBIN.dll", "libMBIN_old.dll");
                File.Move("_templibMBIN.dll", "libMBIN.dll");

                //MessageBox.Show("Please restart the app to re-load the assembly", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);

                return true;
            
            }

            return false;
        }
    }
}
