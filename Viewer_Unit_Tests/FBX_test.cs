using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Fbx;
using System.IO;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;

namespace Viewer_Unit_Tests
{
    public struct FBXTimeStamp
    {
        public int Version;
        public int Year;
        public int Month;
        public int Day;
        public int Hour;
        public int Minute;
        public int Second;
        public int Millisecond;

        public FbxNode getFBXNode()
        {
            FbxNode t = new FbxNode();
            t.Name = "CreationTimeStamp";
            FbxNode t1 = new FbxNode();
            t1.Name = "Version";
            t1.Value = Version;
            t.Nodes.Add(t);
            t1 = new FbxNode();
            t1.Name = "Year";
            t1.Value = Year;
            t.Nodes.Add(t1);

            return t;
        }
    }

    public struct FbxHeaderExtension
    {
        public int FBXHeaderVersion;
        public int FBXVersion;
        public FBXTimeStamp CreationTimeStamp;

        public FbxHeaderExtension(int headerversion, int version)
        {
            FBXHeaderVersion = headerversion;
            FBXVersion = version;
            CreationTimeStamp = new FBXTimeStamp();
            CreationTimeStamp.Day = 3;
            CreationTimeStamp.Year = 2020;
        }
        public FbxNode getFBXNode()
        {
            FbxNode t = new FbxNode();
            t.Name = "FBXHeaderExtension";
            FbxNode t1 = new FbxNode();
            t1.Name = "FBXHeaderVersion";
            t1.Value = FBXHeaderVersion;
            t.Nodes.Add(t1);
            t1 = new FbxNode();
            t1.Name = "FBXVersion";
            t1.Value = FBXVersion;
            t.Nodes.Add(t1);
            t1 = CreationTimeStamp.getFBXNode();
            t1.Name = "CreationTimeStamp";

            return t;
        }
    }



    [TestClass]
    public class UnitTest4
    {
        [TestMethod]
        public void FbxEporter()
        {
            //Generate an FBX document
            FbxDocument fbxDoc = new FbxDocument();
            FbxNode t;
            
            //FBX Header Extension
            FbxHeaderExtension he = new FbxHeaderExtension();
            t = he.getFBXNode();
            fbxDoc.Nodes.Add(t);

            t = new FbxNode();
            t.Name = "Test";
            t.Value = (new List<int>() { 0, 1, 2, 3, 4, 5, 6 }).ToArray();

            fbxDoc.Nodes.Add(t);

            

            


            //Try to export
            FileStream fs = new FileStream("test.fbx", FileMode.Create);
            FbxAsciiWriter writer = new FbxAsciiWriter(fs);
            writer.Write(fbxDoc);
            fs.Close();
            
        }
    }
}
