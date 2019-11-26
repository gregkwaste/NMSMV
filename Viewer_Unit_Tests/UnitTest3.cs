using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using libMBIN;
using libMBIN.NMS.Toolkit;


namespace Viewer_Unit_Tests
{
    [TestClass]
    public class UnitTest3
    {
        [TestMethod]
        public void OpenGEOMDATAFilewithlibMBIN()
        {
            string filepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\MODELS\\COMMON\\CHARACTERS\\ASTRONAUT\\ASTRONAUT01.GEOMETRY.MBIN.PC";
            //string filepath = "E:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\MODELS\\COMMON\\CHARACTERS\\ASTRONAUT\\ASTRONAUT01.SCENE.EXML";

            //Try to read EXML
            //TkSceneNodeData t = (TkSceneNodeData)libMBIN.EXmlFile.ReadTemplate(filepath);
            //Console.WriteLine("All Good Loading", t.Name);

            try { 
                libMBIN.MBINFile mbinf = new libMBIN.MBINFile(filepath);
                mbinf.Load();
                NMSTemplate template = mbinf.GetData();
                mbinf.Dispose();
                Console.WriteLine("All Good Loading");
            } catch (Exception ex)
            {
                Console.WriteLine("Something Went Wrong");
                Assert.Fail();
            }

        }
    }
}
