using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenTK;
using MathNet.Numerics.LinearAlgebra;
using System.Diagnostics;

namespace Viewer_Unit_Tests
{
    [TestClass]
    public class UnitTest2
    {
        [TestMethod]
        public void MatrixMultOpenTK()
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));

            //Step A: Init an OpenTK MAtrix
            Matrix4 otk_A = Matrix4.Identity;
            Matrix4 otk_B = Matrix4.Identity;
            Matrix4 otk_C = Matrix4.Identity;

            //Setup some values
            randInitMatrix(otk_A);
            randInitMatrix(otk_B);


            int EXP_NUM = 1000000;
            Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();

            for (int i = 0; i < EXP_NUM; i++)
                otk_C = otk_A * otk_B;

            sw.Stop();
            Trace.WriteLine("Time taken: " + sw.Elapsed.TotalMilliseconds.ToString());
        }

        [TestMethod]
        public void MatrixMultMathNet()
        {
            Trace.Listeners.Add(new TextWriterTraceListener(Console.Out));
            //Step A: Init a Math.Net MAtrix
            Matrix<float> mn_A = Matrix<float>.Build.Dense(4, 4);
            Matrix<float> mn_B = Matrix<float>.Build.Dense(4, 4);
            Matrix<float> mn_C = Matrix<float>.Build.Dense(4, 4);
            
            //Setup some values
            randInitMatrix(mn_A);
            randInitMatrix(mn_B);


            int EXP_NUM = 1000000;
            Stopwatch sw = Stopwatch.StartNew();

            for (int i = 0; i < EXP_NUM; i++)
                mn_A.Multiply(mn_B, mn_C);
   
            sw.Stop();
            Trace.WriteLine("Time taken: " +  sw.Elapsed.TotalMilliseconds.ToString());
        }

        private void randInitMatrix(Matrix4 mat)
        {
            Random randgen = new Random();

            for (int i=0;i<4;i++)
                for (int j = 0; j < 4; j++)
                    mat[i, j] = randgen.Next(-100, 100) / (float)200;
        }
        private void randInitMatrix(Matrix<float> mat)
        {
            Random randgen = new Random();

            for (int i = 0; i < 4; i++)
                for (int j = 0; j < 4; j++)
                    mat[i, j] = randgen.Next(-100, 100) / (float)200;
        }
    }
}
