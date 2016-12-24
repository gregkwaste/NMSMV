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
        public float zNear = 2.0f;
        public float zFar = 1000.0f;
        public float aspect = 1.0f;

        //Matrices
        public Matrix4 projMat;
        public Matrix4 lookMat;
        public int type;
        public bool culling;

        //Camera Frustum Planes
        private Frustum extFrustum = new Frustum();
        public Vector4[] frPlanes = new Vector4[6];


        //Rendering Stuff
        public GMDL.customVBO vbo;
        public int program;

        public Camera(int angle, int program, int mode, bool cull)
        {
            //Set fov on init
            this.setFOV(angle);
            vbo = (new Box(1.0f, 1.0f, 1.0f)).getVBO();
            this.program = program;
            this.type = mode;
            this.culling = cull;
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
                float tangent = (float) Math.Tan(fov / 2.0f);   // tangent of half fovY
                h = zNear * tangent;  // half height of near plane
                w = h * aspect;       // half width of near plane

                projMat = Matrix4.CreatePerspectiveOffCenter(-w, w, -h, h, zNear, zFar);
                //projMat.Transpose();
                //projMat = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar);
                
                return (lookMat * projMat);
            }
            else
            {
                //Create orthographic projection
                projMat = Matrix4.CreateOrthographic(aspect * 2.0f, 2.0f, zNear, zFar);
                //projMat.Transpose();
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

        public void updateFrustumPlanes()
        {
            //projMat.Transpose();
            //extFrustum.CalculateFrustum(projMat, lookMat);
            //return;

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
            if (!culling) return true;
            //transform is the local transformation that may be applied additionally
            Matrix4 mat = cand.worldMat * GetViewMatrix();
            ////Matrix4 mat = mvp;
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


            //Vector4 v;
            //int result = 0;
            //v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[0].Y, cand.Bbox[0].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;
            //v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[0].Y, cand.Bbox[0].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;
            //v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[1].Y, cand.Bbox[0].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;
            //v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[1].Y, cand.Bbox[0].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;

            //v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[0].Y, cand.Bbox[1].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;
            //v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[0].Y, cand.Bbox[1].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;
            //v = Vector4.Transform(new Vector4(cand.Bbox[0].X, cand.Bbox[1].Y, cand.Bbox[1].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;
            //v = Vector4.Transform(new Vector4(cand.Bbox[1].X, cand.Bbox[1].Y, cand.Bbox[1].Z, 1.0f), mat);
            ////v = (1.0f / v.W) * v;
            //if (!extFrustum.PointVsFrustum(v.Xyz)) result++;


            //if (result == 8)
            //{
            //    //Before discarding check the bounding Sphere
            //    Vector4 bsh_center = (new Vector4 ((cand.Bbox[0] + cand.Bbox[1])));
            //    bsh_center = 0.5f * bsh_center;
            //    bsh_center.W = 1.0f;

            //    float radius = (0.5f * (cand.Bbox[1] - cand.Bbox[0])).Length;

            //    //Now bring it to clip space
            //    v = Vector4.Transform(bsh_center, mat);
            //    //v = (1.0f / v.W) * v;

            //    //Now calculate distances from the planes
            //    for (int i = 0; i < 6; i++)
            //    {
            //        if (!extFrustum.SphereVsFrustum(bsh_center.Xyz, radius)) return false;
            //    }


            //    return false;
            //}
                


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
        }

        
    }

    public class Frustum
    {
        private float[] _clipMatrix = new float[16];
        private float[,] _frustum = new float[6, 4];

        public const int A = 0;
        public const int B = 1;
        public const int C = 2;
        public const int D = 3;

        public enum ClippingPlane : int
        {
            Right = 0,
            Left = 1,
            Bottom = 2,
            Top = 3,
            Back = 4,
            Front = 5
        }

        private void NormalizePlane(float[,] frustum, int side)
        {
            float magnitude = (float)Math.Sqrt((frustum[side, 0] * frustum[side, 0]) + (frustum[side, 1] * frustum[side, 1])
                                                + (frustum[side, 2] * frustum[side, 2]));
            frustum[side, 0] /= magnitude;
            frustum[side, 1] /= magnitude;
            frustum[side, 2] /= magnitude;
            frustum[side, 3] /= magnitude;
        }

        public bool PointVsFrustum(float x, float y, float z)
        {
            for (int i = 0; i < 6; i++)
            {
                if (this._frustum[i, 0] * x + this._frustum[i, 1] * y + this._frustum[i, 2] * z + this._frustum[i, 3] <= 0.0f)
                {
                    return false;
                }
            }
            return true;
        }

        public bool PointVsFrustum(Vector3 location)
        {
            for (int i = 0; i < 6; i++)
            {
                if (this._frustum[i, 0] * location.X + this._frustum[i, 1] * location.Y + this._frustum[i, 2] * location.Z + this._frustum[i, 3] <= 0.0f)
                {
                    return false;
                }
            }
            return true;
        }

        public bool SphereVsFrustum(float x, float y, float z, float radius)
        {
            float d = 0;
            for (int p = 0; p < 6; p++)
            {
                d = _frustum[p, 0] * x + _frustum[p, 1] * y + _frustum[p, 2] * z + _frustum[p, 3];
                if (d <= -radius)
                {
                    return false;
                }
            }
            return true;
        }

        public bool SphereVsFrustum(Vector3 location, float radius)
        {
            float d = 0;
            for (int p = 0; p < 6; p++)
            {
                d = _frustum[p, 0] * location.X + _frustum[p, 1] * location.Y + _frustum[p, 2] * location.Z + _frustum[p, 3];
                if (d <= -radius)
                {
                    return false;
                }
            }
            return true;
        }

        public bool VolumeVsFrustum(float x, float y, float z, float width, float height, float length)
        {
            for (int i = 0; i < 6; i++)
            {
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y - height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + width) + _frustum[i, B] * (y + height) + _frustum[i, C] * (z + length) + _frustum[i, D] > 0)
                    continue;
                return false;
            }
            return true;
        }

        public bool VolumeVsFrustum(Vector3 location, float width, float height, float length)
        {
            for (int i = 0; i < 6; i++)
            {
                if (_frustum[i, A] * (location.X - width) + _frustum[i, B] * (location.Y - height) + _frustum[i, C] * (location.Z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (location.X + width) + _frustum[i, B] * (location.Y - height) + _frustum[i, C] * (location.Z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (location.X - width) + _frustum[i, B] * (location.Y + height) + _frustum[i, C] * (location.Z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (location.X + width) + _frustum[i, B] * (location.Y + height) + _frustum[i, C] * (location.Z - length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (location.X - width) + _frustum[i, B] * (location.Y - height) + _frustum[i, C] * (location.Z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (location.X + width) + _frustum[i, B] * (location.Y - height) + _frustum[i, C] * (location.Z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (location.X - width) + _frustum[i, B] * (location.Y + height) + _frustum[i, C] * (location.Z + length) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (location.X + width) + _frustum[i, B] * (location.Y + height) + _frustum[i, C] * (location.Z + length) + _frustum[i, D] > 0)
                    continue;
                return false;
            }
            return true;
        }

        public bool CubeVsFrustum(float x, float y, float z, float size)
        {
            for (int i = 0; i < 6; i++)
            {
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z - size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y - size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x - size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                if (_frustum[i, A] * (x + size) + _frustum[i, B] * (y + size) + _frustum[i, C] * (z + size) + _frustum[i, D] > 0)
                    continue;
                return false;
            }
            return true;
        }

        public float distanceFromPlane(int id, Vector3 point)
        {
            Vector3 planeNormal = new Vector3(_frustum[id, 0], _frustum[id, 1], _frustum[id, 2]);
            return Vector3.Dot(planeNormal, point) / planeNormal.Length;
        }

        public void CalculateFrustum(Matrix4 projectionMatrix, Matrix4 modelViewMatrix)
        {
            _clipMatrix[0] = (modelViewMatrix.M11 * projectionMatrix.M11) + (modelViewMatrix.M12 * projectionMatrix.M21) + (modelViewMatrix.M13 * projectionMatrix.M31) + (modelViewMatrix.M14 * projectionMatrix.M41);
            _clipMatrix[1] = (modelViewMatrix.M11 * projectionMatrix.M12) + (modelViewMatrix.M12 * projectionMatrix.M22) + (modelViewMatrix.M13 * projectionMatrix.M32) + (modelViewMatrix.M14 * projectionMatrix.M42);
            _clipMatrix[2] = (modelViewMatrix.M11 * projectionMatrix.M13) + (modelViewMatrix.M12 * projectionMatrix.M23) + (modelViewMatrix.M13 * projectionMatrix.M33) + (modelViewMatrix.M14 * projectionMatrix.M43);
            _clipMatrix[3] = (modelViewMatrix.M11 * projectionMatrix.M14) + (modelViewMatrix.M12 * projectionMatrix.M24) + (modelViewMatrix.M13 * projectionMatrix.M34) + (modelViewMatrix.M14 * projectionMatrix.M44);

            _clipMatrix[4] = (modelViewMatrix.M21 * projectionMatrix.M11) + (modelViewMatrix.M22 * projectionMatrix.M21) + (modelViewMatrix.M23 * projectionMatrix.M31) + (modelViewMatrix.M24 * projectionMatrix.M41);
            _clipMatrix[5] = (modelViewMatrix.M21 * projectionMatrix.M12) + (modelViewMatrix.M22 * projectionMatrix.M22) + (modelViewMatrix.M23 * projectionMatrix.M32) + (modelViewMatrix.M24 * projectionMatrix.M42);
            _clipMatrix[6] = (modelViewMatrix.M21 * projectionMatrix.M13) + (modelViewMatrix.M22 * projectionMatrix.M23) + (modelViewMatrix.M23 * projectionMatrix.M33) + (modelViewMatrix.M24 * projectionMatrix.M43);
            _clipMatrix[7] = (modelViewMatrix.M21 * projectionMatrix.M14) + (modelViewMatrix.M22 * projectionMatrix.M24) + (modelViewMatrix.M23 * projectionMatrix.M34) + (modelViewMatrix.M24 * projectionMatrix.M44);

            _clipMatrix[8] = (modelViewMatrix.M31 * projectionMatrix.M11) + (modelViewMatrix.M32 * projectionMatrix.M21) + (modelViewMatrix.M33 * projectionMatrix.M31) + (modelViewMatrix.M34 * projectionMatrix.M41);
            _clipMatrix[9] = (modelViewMatrix.M31 * projectionMatrix.M12) + (modelViewMatrix.M32 * projectionMatrix.M22) + (modelViewMatrix.M33 * projectionMatrix.M32) + (modelViewMatrix.M34 * projectionMatrix.M42);
            _clipMatrix[10] = (modelViewMatrix.M31 * projectionMatrix.M13) + (modelViewMatrix.M32 * projectionMatrix.M23) + (modelViewMatrix.M33 * projectionMatrix.M33) + (modelViewMatrix.M34 * projectionMatrix.M43);
            _clipMatrix[11] = (modelViewMatrix.M31 * projectionMatrix.M14) + (modelViewMatrix.M32 * projectionMatrix.M24) + (modelViewMatrix.M33 * projectionMatrix.M34) + (modelViewMatrix.M34 * projectionMatrix.M44);

            _clipMatrix[12] = (modelViewMatrix.M41 * projectionMatrix.M11) + (modelViewMatrix.M42 * projectionMatrix.M21) + (modelViewMatrix.M43 * projectionMatrix.M31) + (modelViewMatrix.M44 * projectionMatrix.M41);
            _clipMatrix[13] = (modelViewMatrix.M41 * projectionMatrix.M12) + (modelViewMatrix.M42 * projectionMatrix.M22) + (modelViewMatrix.M43 * projectionMatrix.M32) + (modelViewMatrix.M44 * projectionMatrix.M42);
            _clipMatrix[14] = (modelViewMatrix.M41 * projectionMatrix.M13) + (modelViewMatrix.M42 * projectionMatrix.M23) + (modelViewMatrix.M43 * projectionMatrix.M33) + (modelViewMatrix.M44 * projectionMatrix.M43);
            _clipMatrix[15] = (modelViewMatrix.M41 * projectionMatrix.M14) + (modelViewMatrix.M42 * projectionMatrix.M24) + (modelViewMatrix.M43 * projectionMatrix.M34) + (modelViewMatrix.M44 * projectionMatrix.M44);

            _frustum[(int)ClippingPlane.Right, 0] = _clipMatrix[3] - _clipMatrix[0];
            _frustum[(int)ClippingPlane.Right, 1] = _clipMatrix[7] - _clipMatrix[4];
            _frustum[(int)ClippingPlane.Right, 2] = _clipMatrix[11] - _clipMatrix[8];
            _frustum[(int)ClippingPlane.Right, 3] = _clipMatrix[15] - _clipMatrix[12];
            NormalizePlane(_frustum, (int)ClippingPlane.Right);

            _frustum[(int)ClippingPlane.Left, 0] = _clipMatrix[3] + _clipMatrix[0];
            _frustum[(int)ClippingPlane.Left, 1] = _clipMatrix[7] + _clipMatrix[4];
            _frustum[(int)ClippingPlane.Left, 2] = _clipMatrix[11] + _clipMatrix[8];
            _frustum[(int)ClippingPlane.Left, 3] = _clipMatrix[15] + _clipMatrix[12];
            NormalizePlane(_frustum, (int)ClippingPlane.Left);

            _frustum[(int)ClippingPlane.Bottom, 0] = _clipMatrix[3] + _clipMatrix[1];
            _frustum[(int)ClippingPlane.Bottom, 1] = _clipMatrix[7] + _clipMatrix[5];
            _frustum[(int)ClippingPlane.Bottom, 2] = _clipMatrix[11] + _clipMatrix[9];
            _frustum[(int)ClippingPlane.Bottom, 3] = _clipMatrix[15] + _clipMatrix[13];
            NormalizePlane(_frustum, (int)ClippingPlane.Bottom);

            _frustum[(int)ClippingPlane.Top, 0] = _clipMatrix[3] - _clipMatrix[1];
            _frustum[(int)ClippingPlane.Top, 1] = _clipMatrix[7] - _clipMatrix[5];
            _frustum[(int)ClippingPlane.Top, 2] = _clipMatrix[11] - _clipMatrix[9];
            _frustum[(int)ClippingPlane.Top, 3] = _clipMatrix[15] - _clipMatrix[13];
            NormalizePlane(_frustum, (int)ClippingPlane.Top);

            _frustum[(int)ClippingPlane.Back, 0] = _clipMatrix[3] - _clipMatrix[2];
            _frustum[(int)ClippingPlane.Back, 1] = _clipMatrix[7] - _clipMatrix[6];
            _frustum[(int)ClippingPlane.Back, 2] = _clipMatrix[11] - _clipMatrix[10];
            _frustum[(int)ClippingPlane.Back, 3] = _clipMatrix[15] - _clipMatrix[14];
            NormalizePlane(_frustum, (int)ClippingPlane.Back);

            _frustum[(int)ClippingPlane.Front, 0] = _clipMatrix[3] + _clipMatrix[2];
            _frustum[(int)ClippingPlane.Front, 1] = _clipMatrix[7] + _clipMatrix[6];
            _frustum[(int)ClippingPlane.Front, 2] = _clipMatrix[11] + _clipMatrix[10];
            _frustum[(int)ClippingPlane.Front, 3] = _clipMatrix[15] + _clipMatrix[14];
            NormalizePlane(_frustum, (int)ClippingPlane.Front);
        }

        /// <summary>
        /// Draw the frustum for debugging purposes.
        /// </summary>
        public void Draw()
        {
            // Implement drawing code here.
        }
    }



}
