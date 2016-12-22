using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK;

namespace Model_Viewer
{
    public class Camera
    {
        public Vector3 Position = new Vector3(0.0f, 0.0f, -5.0f);
        public Vector3 Orientation = new Vector3(0.0f, 0f, 0f);
        public float MoveSpeed = 0.2f;
        public float MouseSensitivity = 0.01f;
        public bool isActive = false;
        //Projection variables Set defaults
        public float fov;
        public float zNear = 0.5f;
        public float zFar = 1000.0f;
        public float aspect = 1.0f;

        //Matrices
        public Matrix4 projMat;
        public Matrix4 lookMat;

        //Rendering Stuff
        public GMDL.customVBO vbo;
        public int program;

        public Camera(int angle, int program)
        {
            //Set fov on init
            this.setFOV(angle);
            vbo = (new Box(1.0f, 1.0f, 1.0f)).getVBO();
            this.program = program;
        }
        
        public Matrix4 GetViewMatrix()
        {
            Vector3 lookat = new Vector3();

            lookat.X = (float)(Math.Sin((float) Orientation.X) * Math.Cos((float) Orientation.Y));
            lookat.Y = (float) Math.Sin((float) Orientation.Y);
            lookat.Z = (float)(Math.Cos((float) Orientation.X) * Math.Cos((float) Orientation.Y));

            lookMat = Matrix4.LookAt(Position, Position + lookat, Vector3.UnitY);
            //projMat = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar);
            //Call Custom
            projMat = this.ComputeFOVProjection();

            return lookMat * projMat;
        }

        public void Move(float x, float y, float z)
        {
            Vector3 offset = new Vector3();

            Vector3 forward = new Vector3((float)Math.Sin((float)Orientation.X), 0, (float)Math.Cos((float)Orientation.X));
            Vector3 right = new Vector3(-forward.Z, 0, forward.X);

            offset += x * right;
            offset += y * forward;
            offset.Y += z;

            offset.NormalizeFast();
            offset = Vector3.Multiply(offset, MoveSpeed);

            Position += offset;
        }

        public void AddRotation(float x, float y)
        {
            x = x * MouseSensitivity;
            y = y * MouseSensitivity;

            Orientation.X = (Orientation.X + x) % ((float)Math.PI * 2.0f);
            Orientation.Y = Math.Max(Math.Min(Orientation.Y + y, (float)Math.PI / 2.0f - 0.1f), (float)-Math.PI / 2.0f + 0.1f);
        }

        public void setFOV(int angle)
        {
            this.fov = (float) Math.PI * angle / 180.0f;
        }

        public Matrix4 ComputeFOVProjection()
        {
            Matrix4 proj = new Matrix4();
            //
            // General form of the Projection Matrix
            //
            // uh = Cot( fov/2 ) == 1/Tan(fov/2)
            // uw / uh = 1/aspect
            // 
            //   uw         0       0       0
            //    0        uh       0       0
            //    0         0      f/(f-n)  1
            //    0         0    -fn/(f-n)  0
            //
            // Make result to be identity first

            // check for bad parameters to avoid divide by zero:
            // if found, assert and return an identity matrix.
            float frustumDepth = zFar - zNear;
            float oneOverDepth = 1 / frustumDepth;

            proj[1, 1] = 1.0f / (float) Math.Tan(0.5f * fov);
            proj[0, 0] = -1.0f * proj[1, 1] / (aspect);
            proj[2, 2] = zFar * oneOverDepth;
            proj[3, 2] = (-zFar * zNear) * oneOverDepth;
            proj[2, 3] = 1;
            proj[3, 3] = 0;

            //Debug.WriteLine(proj);
            return proj;
        }


        public void render()
        {
            GL.UseProgram(program);

            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo.vertex_buffer_object);

            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vbo.element_buffer_object);
            GL.PointSize(10.0f);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, vbo.vCount,
                vbo.iCount, vbo.iType, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
        }

    }


   


}
