using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using System.Diagnostics;

namespace Viewer_Unit_Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void Int_Rev_2_10_10_10_DECODE_TEST()
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            byte[] a32 = new byte[] {0xB3, 0x61, 0x43, 0x76 };
            uint value = BitConverter.ToUInt32(a32, 0);
            //Convert Values
            int i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
            int i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
            int i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
            //int i4 = _2sComplement.toInt((value >> 30) & 0x003, 10);
            float norm = (float)Math.Sqrt(i1 * i1 + i2 * i2 + i3 * i3);

            //Convert Values
            //i4 = _2sComplement.toInt((value >> 00) & 0x003, 02);
            //i3 = _2sComplement.toInt((value >> 02) & 0x3FF, 10);
            //i2 = _2sComplement.toInt((value >> 12) & 0x3FF, 10);
            //i1 = _2sComplement.toInt((value >> 22) & 0x3FF, 10);
            //Debug.WriteLine("{0}, {1}, {2}", i1, i2, i3);


            Vector4 vN = new Vector4(Convert.ToSingle(i1) / norm,
                             Convert.ToSingle(i2) / norm,
                             Convert.ToSingle(i3) / norm,
                             1.0f);
            //(Convert.ToSingle(v4) - 1.5f) / 1.5f);
            Trace.WriteLine(vN);
        }
    }
}
