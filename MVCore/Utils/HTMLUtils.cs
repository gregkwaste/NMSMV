using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace MVCore.Utils
{
    public static class HTMLUtils
    {
        //Shared client, HttpClient is meant to be reused for the lifetime of the app
        private static readonly HttpClient httpClient = createClient();

        private static HttpClient createClient()
        {
            HttpClient client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(60);
            //GitHub's API requires a User-Agent
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NMSModelViewer", "1.0"));
            return client;
        }

        public static string request(Uri uri)
        {
            using HttpRequestMessage msg = new HttpRequestMessage(HttpMethod.Get, uri);
            msg.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
            using HttpResponseMessage response = httpClient.Send(msg);
            response.EnsureSuccessStatusCode();
            using StreamReader reader = new StreamReader(response.Content.ReadAsStream());
            return reader.ReadToEnd();
        }

        private static void downloadFile(string url, string destination)
        {
            using HttpResponseMessage response = httpClient.Send(new HttpRequestMessage(HttpMethod.Get, url));
            response.EnsureSuccessStatusCode();
            using Stream source = response.Content.ReadAsStream();
            using FileStream target = new FileStream(destination, FileMode.Create, FileAccess.Write);
            source.CopyTo(target);
        }

        public static bool fetchLibMBINDLL()
        {
            //The app is portable, libMBIN.dll lives next to the executable
            string appDir = AppContext.BaseDirectory;
            string assemblyLoc = Path.Combine(appDir, "libMBIN.dll");
            string tempAssemblyLoc = Path.Combine(appDir, "_templibMBIN.dll");
            string oldAssemblyLoc = Path.Combine(appDir, "libMBIN_old.dll");

            try
            {
                Uri uri = new Uri("https://api.github.com/repos/monkeyman192/MBINCompiler/releases/latest");
                JObject data = JObject.Parse(request(uri));

                //Prefer a dotnet6 build of the library if the release has one
                string downloadUrl_net5 = "";
                string downloadUrl_net6 = "";
                foreach (JToken asset in data["assets"] ?? new JArray())
                {
                    string name = (string) asset["name"];
                    if (name == "libMBIN-dotnet6.dll")
                        downloadUrl_net6 = (string) asset["browser_download_url"];
                    else if (name == "libMBIN.dll")
                        downloadUrl_net5 = (string) asset["browser_download_url"];
                }

                string downloadUrl = (downloadUrl_net6 != "") ? downloadUrl_net6 : downloadUrl_net5;
                if (downloadUrl == "")
                {
                    MessageBox.Show("No libMBIN.dll was found in the latest MBINCompiler release (" + data["tag_name"] + ").", "Info", MessageBoxButton.OK);
                    return false;
                }

                Common.CallBacks.Log("Downloading latest libMBIN release " + data["tag_name"]);
                downloadFile(downloadUrl, tempAssemblyLoc);

                //Read the versions from the files without loading them into the running app
                Version assemblyVersion = AssemblyName.GetAssemblyName(assemblyLoc).Version;
                Version tempAssemblyVersion = AssemblyName.GetAssemblyName(tempAssemblyLoc).Version;

                if (assemblyVersion == tempAssemblyVersion)
                {
                    File.Delete(tempAssemblyLoc);
                    MessageBox.Show("libMBIN is already updated.", "Info", MessageBoxButton.OK);
                    return false;
                }

                var res = MessageBox.Show("Old Version: " + assemblyVersion + " Online Version: " + tempAssemblyVersion + "\nDo you want to update?", "Info", MessageBoxButton.YesNo, MessageBoxImage.Information);

                if (res != MessageBoxResult.Yes)
                {
                    File.Delete(tempAssemblyLoc);
                    return false;
                }

                //Windows allows renaming a dll that is in use, so swap the files and restart
                if (File.Exists(oldAssemblyLoc))
                    File.Delete(oldAssemblyLoc);
                File.Move(assemblyLoc, oldAssemblyLoc);
                File.Move(tempAssemblyLoc, assemblyLoc);
                return true;
            }
            catch (Exception ex)
            {
                Common.CallBacks.Log("libMBIN update failed: " + ex);
                MessageBox.Show("libMBIN update failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                if (File.Exists(tempAssemblyLoc))
                    File.Delete(tempAssemblyLoc);
                return false;
            }
        }
    }
}
