using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK;

namespace Model_Viewer
{
    public class Camera
    {
        public Vector3 Position = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 Movement = new Vector3(0.0f, 0.0f, 0.0f);
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
        public int type;

        //Camera Frustum Planes
        public Vector4[] frPlanes = new Vector4[6];


        //Rendering Stuff
        public GMDL.customVBO vbo;
        public int program;

        public Camera(int angle, int program, int mode)
        {
            //Set fov on init
            this.setFOV(angle);
            vbo = (new Box(1.0f, 1.0f, 1.0f)).getVBO();
            this.program = program;
            this.type = mode;

        }
        
        public Matrix4 GetViewMatrix()
        {
            Vector3 lookat = new Vector3();

            lookat.X = (float)(Math.Sin((float) Orientation.X) * Math.Cos((float) Orientation.Y));
            lookat.Y = (float) Math.Sin((float) Orientation.Y);
            lookat.Z = (float)(Math.Cos((float) Orientation.X) * Math.Cos((float) Orientation.Y));

            lookMat = Matrix4.LookAt(Position, Position + lookat, Vector3.UnitY);

            Matrix4 trans = Matrix4.CreateTranslation(Movement);
            if (type == 0) {
                //projMat = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar);
                //Call Custom
                //projMat = this.ComputeFOVProjection();
                float w, h;
                if (aspect > 1.0f)
                {
                    w = 0.5f;
                    h = w / aspect;
                }
                else
                {
                    h = 0.5f;
                    w = h * aspect;
                }


                //projMat = Matrix4.CreatePerspectiveOffCenter(-w, w, -h, h, zNear, zFar);
                projMat = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar);

                return trans * lookMat * projMat;
            }
            else
            {
                //Create orthographic projection
                projMat = Matrix4.CreateOrthographic(aspect * 2.0f, 2.0f, zNear, zFar);

                //Create scale matrix based on the fov
                Matrix4 scaleMat = Matrix4.CreateScale(0.8f * fov);
                return scaleMat * trans * lookMat * projMat;
            }
            

            
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

            Movement += offset;
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

        public void updateFrustumPlanes()
        {
            Matrix4 mat = GetViewMatrix();
            mat.Transpose();
            //Matrix4 mat = proj;
            //Left
            frPlanes[0] = mat.Row0 + mat.Row3;
            //Right
            frPlanes[1] = mat.Row3 - mat.Row0;
            //Bottom
            frPlanes[2] = mat.Row3 + mat.Row1;
            //Top
            frPlanes[3] = mat.Row3 - mat.Row1;
            //Near
            frPlanes[4] = mat.Row3 + mat.Row2;
            //Far
            frPlanes[5] = mat.Row3 - mat.Row2;


            //Normalize them
            for (int i = 0; i < 6; i++)
            {
                float l = frPlanes[i].Xyz.Length;
                //Normalize
                frPlanes[i].X /= l;
                frPlanes[i].Y /= l;
                frPlanes[i].Z /= l;
                frPlanes[i].W /= l;
            }

        }

        public bool frustum_occlude(GMDL.model cand, Matrix4 transform)
        {
            //transform is the local transformation that may be applied additionally
            Matrix4 mat = cand.worldMat * transform * GetViewMatrix();
            //Matrix4 mat = mvp;
            //mat.Transpose();

            for (int i = 0; i < 6; i++)
            {
                int result = 0;
                Vector4 v;

                v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[0].Y, cand.Bbox[0].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);
                v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[0].Y, cand.Bbox[0].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);
                v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[1].Y, cand.Bbox[0].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);
                v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[1].Y, cand.Bbox[0].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);

                v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[0].Y, cand.Bbox[1].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);
                v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[0].Y, cand.Bbox[1].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);
                v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[1].Y, cand.Bbox[1].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);
                v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[1].Y, cand.Bbox[1].Z, 1.0f), mat);
                result += (Vector3.Dot(frPlanes[i].Xyz, v.Xyz) + frPlanes[i].W < 0.0f ? 1 : 0);

                if (result == 8) return false;
            }

            return true;
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
