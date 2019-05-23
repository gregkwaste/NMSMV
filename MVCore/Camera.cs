using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using MVCore;
using MathNet.Numerics;

namespace MVCore.GMDL
{
    public class Camera
    {
        public Vector3 Position = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 Movement = new Vector3(0.0f, 0.0f, 0.0f);


        public float pitch = 0; //Radians rotation on X axis
        public float yaw = (float) Math.PI/2.0f; //Radians rotation on Y axis
        public float roll = 0; //Radians rotation on Z axis

        public Vector3 Orientation = new Vector3(0.0f, 0f, 0f);
        public float MoveSpeed = 0.02f;
        public float MouseSensitivity = 0.001f;
        public bool isActive = false;
        //Projection variables Set defaults
        public float fov;
        public float zNear = 0.05f;
        public float zFar = 15000.0f;
        public float aspect = 1.0f;

        //Matrices
        public Matrix4 projMat;
        public Matrix4 lookMat;
        public Matrix4 viewMat = Matrix4.Identity;
        public int type;
        public bool culling;

        //Camera Frustum Planes
        private Frustum extFrustum = new Frustum();
        public Vector4[] frPlanes = new Vector4[6];


        //Rendering Stuff
        public GMDL.mainVAO vao;
        public int program;

        public Camera(int angle, int program, int mode, bool cull)
        {
            //Set fov on init
            this.setFOV(angle);
            vao = (new Primitives.Box(1.0f, 1.0f, 1.0f)).getVAO();
            this.program = program;
            this.type = mode;
            this.culling = cull;

            updateOrientation();
            //Initialize the viewmat
            this.updateViewMatrix();
        
        }
        
        public void updateViewMatrix()
        {
            lookMat = Matrix4.LookAt(Position, Position + Orientation, Vector3.UnitY);
            //lookMat = Matrix4.LookAt(new Vector3(0.0f,0.0f,0.0f), lookat, Vector3.UnitY);

            if (type == 0) {
                //projMat = Matrix4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar);
                //Call Custom
                //projMat = this.ComputeFOVProjection();
                float w, h;
                float tangent = (float) Math.Tan(fov / 2.0f);   // tangent of half fovY
                h = zNear * tangent;  // half height of near plane
                w = h * aspect;       // half width of near plane

                //projMat = Matrix4.CreatePerspectiveOffCenter(-w, w, -h, h, zNear, zFar);
                Matrix4.CreatePerspectiveFieldOfView(fov, aspect, zNear, zFar, out projMat);
                
                viewMat = lookMat * projMat;
            }
            else
            {
                //Create orthographic projection
                Matrix4.CreateOrthographic(aspect * 2.0f, 2.0f, zNear, zFar, out projMat);
                //projMat.Transpose();
                //Create scale matrix based on the fov
                Matrix4 scaleMat = Matrix4.CreateScale(0.8f * fov);
                viewMat = scaleMat * lookMat * projMat;
            }


            updateFrustumPlanes();
        }

        public void Move(float x, float y, float z)
        {
            Vector3 offset = new Vector3();

            //Vector3 right = new Vector3(-forward.Z, 0, forward.X);
            Vector3 right = Vector3.Cross(Orientation, new Vector3(0.0f, 1.0f, 0.0f)).Normalized();

            offset += x * right;
            offset += y * Orientation;
            offset.Y += z;

            //offset.NormalizeFast();
            //Movement speed is accumulated in x,y,z
            //offset = Vector3.Multiply(offset, MoveSpeed);

            Position += offset;
        }

        public void AddRotation(float x, float y)
        {
            yaw -= x * MouseSensitivity;
            pitch += y * MouseSensitivity;

            yaw = yaw %  (2.0f * (float) Math.PI);
            pitch = pitch % (2.0f * (float) Math.PI);
            
            //yaw = MathUtils.clamp(yaw, (float)-Math.PI, (float)Math.PI);
            //pitch = MathUtils.clamp(pitch, (float)-Math.PI, (float)Math.PI);

            //Console.WriteLine("{0} {1}", yaw, pitch);

            updateOrientation();
            
            //x = x * MouseSensitivity;
            //y = y * MouseSensitivity;

            //Orientation.X = (Orientation.X + x) % ((float)Math.PI * 2.0f);
            //Orientation.Y = Math.Max(Math.Min(Orientation.Y + y, (float)Math.PI / 2.0f - 0.1f), (float)-Math.PI / 2.0f + 0.1f);
        }

        public void updateOrientation()
        {
            //Recalculate orientation vector
            Orientation.X = (float)Math.Cos(yaw) * (float)Math.Cos(pitch);
            Orientation.Y = (float)Math.Sin(pitch);
            Orientation.Z = (float)Math.Sin(yaw) * (float)Math.Cos(pitch);
        }

        public void setFOV(int angle)
        {
            this.fov = MathUtils.radians(angle);
        }

        public void updateFrustumPlanes()
        {
            //projMat.Transpose();
            //extFrustum.CalculateFrustum(projMat, lookMat); //Old Method
            extFrustum.CalculateFrustum(viewMat); // New Method
            return;
            Matrix4 mat = viewMat;
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

            Vector4 p1 = new Vector4(cand.Bbox[0], 1.0f);
            Vector4 p2 = new Vector4(cand.Bbox[1], 1.0f);
            p1 = p1 * cand.worldMat * transform;
            p2 = p2 * cand.worldMat * transform;
            float radius = 0.5f * (p1 - p2).Length;

            Vector4 bsh_center = p1 + p2;
            bsh_center *= 0.5f;

            //This is not accurate for some fucking reason
            //return extFrustum.AABBVsFrustum(cand.Bbox, cand.worldMat * transform);
            
            
            //In the future I should add the original AABB as well, spheres look to work like a charm for now   
            return extFrustum.SphereVsFrustum(bsh_center.Xyz, radius);
        }

        public void render()
        {
            GL.UseProgram(program);

            //Keep manual rendering for the camera because it needs vertex updates
            //Init Arrays

            Int32[] indices = new Int32[] { 0,1,2,
                                            2,1,3,
                                            1,5,3,
                                            5,7,3,
                                            5,4,6,
                                            5,6,7,
                                            0,2,4,
                                            2,6,4,
                                            3,6,2,
                                            7,6,3,
                                            0,4,5,
                                            1,0,5 };

            int b_size = extFrustum._frustum_points.Length * sizeof(float);
            byte[] verts_b = new byte[b_size];

            int i_size = indices.Length * sizeof(Int32);
            byte[] indices_b = new byte[i_size];

            System.Buffer.BlockCopy(extFrustum._frustum_points, 0, verts_b, 0, b_size);
            System.Buffer.BlockCopy(indices, 0, indices_b, 0, i_size);

            //Generate OpenGL buffers
            int vertex_buffer_object;
            int element_buffer_object;

            GL.GenBuffers(1, out vertex_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) b_size, verts_b, BufferUsageHint.StaticDraw);
            
            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) i_size, indices_b, BufferUsageHint.StaticDraw);

            //Step 1: Upload Uniforms
            int loc;

            //Render Elements
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            int vpos = GL.GetAttribLocation(program, "vPosition");
            GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(vpos);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);
            GL.PointSize(10.0f);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            //GL.DrawElements(PrimitiveType.Triangles, 36, DrawElementsType.UnsignedShort, indices_b);
            GL.DrawRangeElements(PrimitiveType.Triangles, 0, 36,
                36, DrawElementsType.UnsignedInt, (IntPtr) 0);
            //GL.DrawArrays(PrimitiveType.Points, 0, 8); //Works - renders points
            
            //Debug.WriteLine("Locator Object {2} vpos {0} cpos {1} prog {3}", vpos, cpos, this.name, this.shader_program);
            //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vertex_buffer_object,this.color_buffer_object);

            GL.DisableVertexAttribArray(vpos);
            indices = null;
            verts_b = null;
            indices_b = null;
        }
    }

    public class Frustum
    {
        private float[] _clipMatrix = new float[16];
        private float[,] _frustum = new float[6, 4];
        public float[,] _frustum_points = new float[8, 3];
        
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
            float magnitude = 1.0f / (float)Math.Sqrt((frustum[side, 0] * frustum[side, 0]) + (frustum[side, 1] * frustum[side, 1])
                                                + (frustum[side, 2] * frustum[side, 2]));
            frustum[side, 0] *= magnitude;
            frustum[side, 1] *= magnitude;
            frustum[side, 2] *= magnitude;
            frustum[side, 3] *= magnitude;
        }

        public bool PointVsFrustum(float x, float y, float z)
        {
            for (int p = 0; p < 6; p++)
            {
                if (_frustum[p, 0] * x + _frustum[p, 1] * y + _frustum[p, 2] * z + _frustum[p, 3] <= 0.0f)
                {
                    Console.WriteLine("Point vs Frustum, Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p, x, y, z);
                    return false;
                }
                    
            }
            return true;
        }

        public bool PointVsFrustum(Vector3 location)
        {
            return PointVsFrustum(location.X, location.Y, location.Z);
        }


        public bool AABBVsFrustum(Vector3[] AABB, Matrix4 transform)
        {
            //Transform points from local to model space
            Vector4[] tr_AABB = new Vector4[2];

            tr_AABB[0] = new Vector4(AABB[0], 1.0f) * transform;
            tr_AABB[1] = new Vector4(AABB[1], 1.0f) * transform;

            //Generate all 8 points from the AABB
            float[] verts = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };

            //Check if all points are outside one of the planes
            for (int p = 0; p < 6; p++)
            {
                //Check all 8 points
                int i;
                for (i = 0; i < 8; i++)
                {
                    if (_frustum[p, 0] * verts[3 * i] + _frustum[p, 1] * verts[3 * i + 1] + _frustum[p, 2] * verts[3 * i + 2] + _frustum[p, 3] > 0.0f)
                        break;
                }

                if (i == 8)
                {
                    Console.WriteLine("Plane {0} failed", (ClippingPlane) p);
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
                    //Console.WriteLine("Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p,
                    //    x, y, z);
                    return false;
                }
            }
            return true;
        }

        public bool SphereVsFrustum(Vector3 location, float radius)
        {
            return SphereVsFrustum(location.X, location.Y, location.Z, radius);
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
            return VolumeVsFrustum(location.X, location.Y, location.Z, width, height, length);
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


        public void CalculateFrustum(Matrix4 mvp)
        {
            //Front Plane
            this._frustum[(int)ClippingPlane.Front, 0] = -mvp.M13; //mvp.M14 + mvp.M13;
            this._frustum[(int)ClippingPlane.Front, 1] = -mvp.M23; //mvp.M24 + mvp.M23;
            this._frustum[(int)ClippingPlane.Front, 2] = -mvp.M33; //mvp.M34 + mvp.M33;
            this._frustum[(int)ClippingPlane.Front, 3] = -mvp.M43; //mvp.M44 + mvp.M43;

            //Back Plane
            this._frustum[(int)ClippingPlane.Back, 0] = mvp.M13 - mvp.M14;
            this._frustum[(int)ClippingPlane.Back, 1] = mvp.M23 - mvp.M24;
            this._frustum[(int)ClippingPlane.Back, 2] = mvp.M33 - mvp.M34;
            this._frustum[(int)ClippingPlane.Back, 3] = mvp.M43 - mvp.M44;

            //Left Plane
            this._frustum[(int)ClippingPlane.Left, 0] = -mvp.M14 - mvp.M11;
            this._frustum[(int)ClippingPlane.Left, 1] = -mvp.M24 - mvp.M21;
            this._frustum[(int)ClippingPlane.Left, 2] = -mvp.M34 - mvp.M31;
            this._frustum[(int)ClippingPlane.Left, 3] = -mvp.M44 - mvp.M41;

            //Right Plane
            this._frustum[(int)ClippingPlane.Right, 0] = mvp.M11 - mvp.M14;
            this._frustum[(int)ClippingPlane.Right, 1] = mvp.M21 - mvp.M24;
            this._frustum[(int)ClippingPlane.Right, 2] = mvp.M31 - mvp.M34;
            this._frustum[(int)ClippingPlane.Right, 3] = mvp.M41 - mvp.M44;

            //Top Plane
            this._frustum[(int)ClippingPlane.Top, 0] = mvp.M12 - mvp.M14;
            this._frustum[(int)ClippingPlane.Top, 1] = mvp.M22 - mvp.M24;
            this._frustum[(int)ClippingPlane.Top, 2] = mvp.M32 - mvp.M34;
            this._frustum[(int)ClippingPlane.Top, 3] = mvp.M42 - mvp.M44;

            //Bottom Plane
            this._frustum[(int)ClippingPlane.Bottom, 0] = -mvp.M14 - mvp.M12;
            this._frustum[(int)ClippingPlane.Bottom, 1] = -mvp.M24 - mvp.M22;
            this._frustum[(int)ClippingPlane.Bottom, 2] = -mvp.M34 - mvp.M32;
            this._frustum[(int)ClippingPlane.Bottom, 3] = -mvp.M44 - mvp.M42;

            

            //Invert everything to bring it to the original values
            for (int i = 0; i < 6; i++)
                for (int j = 0; j < 4; j++)
                    _frustum[i, j] = -_frustum[i, j];
            
            NormalizePlane(_frustum, (int)ClippingPlane.Right);
            NormalizePlane(_frustum, (int)ClippingPlane.Left);
            NormalizePlane(_frustum, (int)ClippingPlane.Top);
            NormalizePlane(_frustum, (int)ClippingPlane.Bottom);
            NormalizePlane(_frustum, (int)ClippingPlane.Front);
            NormalizePlane(_frustum, (int)ClippingPlane.Back);

            /*
            
            //Find Frustum Points by solving all the systems
            float[] p;
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Left, (int)ClippingPlane.Bottom);
            _frustum_points[0, 0] = p[0]; _frustum_points[0, 1] = p[1]; _frustum_points[0, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Left, (int)ClippingPlane.Top);
            _frustum_points[1, 0] = p[0]; _frustum_points[1, 1] = p[1]; _frustum_points[1, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Right, (int)ClippingPlane.Bottom);
            _frustum_points[2, 0] = p[0]; _frustum_points[2, 1] = p[1]; _frustum_points[2, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Right, (int)ClippingPlane.Top);
            _frustum_points[3, 0] = p[0]; _frustum_points[3, 1] = p[1]; _frustum_points[3, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Left, (int)ClippingPlane.Bottom);
            _frustum_points[4, 0] = p[0]; _frustum_points[4, 1] = p[1]; _frustum_points[4, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Left, (int)ClippingPlane.Top);
            _frustum_points[5, 0] = p[0]; _frustum_points[5, 1] = p[1]; _frustum_points[5, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Right, (int)ClippingPlane.Bottom);
            _frustum_points[6, 0] = p[0]; _frustum_points[6, 1] = p[1]; _frustum_points[6, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Right, (int)ClippingPlane.Top);
            _frustum_points[7, 0] = p[0]; _frustum_points[7, 1] = p[1]; _frustum_points[7, 2] = p[2];

            */
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

            Matrix4 clipMatrix = modelViewMatrix * projectionMatrix;

            //_frustum[(int)ClippingPlane.Right, 0] = _clipMatrix[3] - _clipMatrix[0];
            //_frustum[(int)ClippingPlane.Right, 1] = _clipMatrix[7] - _clipMatrix[4];
            //_frustum[(int)ClippingPlane.Right, 2] = _clipMatrix[11] - _clipMatrix[8];
            //_frustum[(int)ClippingPlane.Right, 3] = _clipMatrix[15] - _clipMatrix[12];
            _frustum[(int)ClippingPlane.Right, 0] = clipMatrix.M14 - clipMatrix.M11;
            _frustum[(int)ClippingPlane.Right, 1] = clipMatrix.M24 - clipMatrix.M21;
            _frustum[(int)ClippingPlane.Right, 2] = clipMatrix.M34 - clipMatrix.M31;
            _frustum[(int)ClippingPlane.Right, 3] = clipMatrix.M44 - clipMatrix.M41;
            NormalizePlane(_frustum, (int)ClippingPlane.Right);

            //_frustum[(int)ClippingPlane.Left, 0] = _clipMatrix[3] + _clipMatrix[0];
            //_frustum[(int)ClippingPlane.Left, 1] = _clipMatrix[7] + _clipMatrix[4];
            //_frustum[(int)ClippingPlane.Left, 2] = _clipMatrix[11] + _clipMatrix[8];
            //_frustum[(int)ClippingPlane.Left, 3] = _clipMatrix[15] + _clipMatrix[12];
            _frustum[(int)ClippingPlane.Left, 0] = clipMatrix.M14 + clipMatrix.M11;
            _frustum[(int)ClippingPlane.Left, 1] = clipMatrix.M24 + clipMatrix.M21;
            _frustum[(int)ClippingPlane.Left, 2] = clipMatrix.M34 + clipMatrix.M31;
            _frustum[(int)ClippingPlane.Left, 3] = clipMatrix.M44 + clipMatrix.M41;
            NormalizePlane(_frustum, (int)ClippingPlane.Left);

            //_frustum[(int)ClippingPlane.Bottom, 0] = _clipMatrix[3] + _clipMatrix[1];
            //_frustum[(int)ClippingPlane.Bottom, 1] = _clipMatrix[7] + _clipMatrix[5];
            //_frustum[(int)ClippingPlane.Bottom, 2] = _clipMatrix[11] + _clipMatrix[9];
            //_frustum[(int)ClippingPlane.Bottom, 3] = _clipMatrix[15] + _clipMatrix[13];
            _frustum[(int)ClippingPlane.Bottom, 0] = clipMatrix.M14 + clipMatrix.M12;
            _frustum[(int)ClippingPlane.Bottom, 1] = clipMatrix.M24 + clipMatrix.M22;
            _frustum[(int)ClippingPlane.Bottom, 2] = clipMatrix.M34 + clipMatrix.M32;
            _frustum[(int)ClippingPlane.Bottom, 3] = clipMatrix.M44 + clipMatrix.M42;
            NormalizePlane(_frustum, (int)ClippingPlane.Bottom);

            //_frustum[(int)ClippingPlane.Top, 0] = _clipMatrix[3] - _clipMatrix[1];
            //_frustum[(int)ClippingPlane.Top, 1] = _clipMatrix[7] - _clipMatrix[5];
            //_frustum[(int)ClippingPlane.Top, 2] = _clipMatrix[11] - _clipMatrix[9];
            //_frustum[(int)ClippingPlane.Top, 3] = _clipMatrix[15] - _clipMatrix[13];
            _frustum[(int)ClippingPlane.Top, 0] = clipMatrix.M14 - clipMatrix.M12;
            _frustum[(int)ClippingPlane.Top, 1] = clipMatrix.M24 - clipMatrix.M22;
            _frustum[(int)ClippingPlane.Top, 2] = clipMatrix.M34 - clipMatrix.M32;
            _frustum[(int)ClippingPlane.Top, 3] = clipMatrix.M44 - clipMatrix.M42;
            NormalizePlane(_frustum, (int)ClippingPlane.Top);

            //_frustum[(int)ClippingPlane.Back, 0] = _clipMatrix[3] - _clipMatrix[2];
            //_frustum[(int)ClippingPlane.Back, 1] = _clipMatrix[7] - _clipMatrix[6];
            //_frustum[(int)ClippingPlane.Back, 2] = _clipMatrix[11] - _clipMatrix[10];
            //_frustum[(int)ClippingPlane.Back, 3] = _clipMatrix[15] - _clipMatrix[14];
            _frustum[(int)ClippingPlane.Back, 0] = clipMatrix.M14 - clipMatrix.M13;
            _frustum[(int)ClippingPlane.Back, 1] = clipMatrix.M24 - clipMatrix.M23;
            _frustum[(int)ClippingPlane.Back, 2] = clipMatrix.M34 - clipMatrix.M33;
            _frustum[(int)ClippingPlane.Back, 3] = clipMatrix.M44 - clipMatrix.M43;
            NormalizePlane(_frustum, (int)ClippingPlane.Back);

            //_frustum[(int)ClippingPlane.Front, 0] = _clipMatrix[3] + _clipMatrix[2];
            //_frustum[(int)ClippingPlane.Front, 1] = _clipMatrix[7] + _clipMatrix[6];
            //_frustum[(int)ClippingPlane.Front, 2] = _clipMatrix[11] + _clipMatrix[10];
            //_frustum[(int)ClippingPlane.Front, 3] = _clipMatrix[15] + _clipMatrix[14];
            _frustum[(int)ClippingPlane.Front, 0] = clipMatrix.M14 + clipMatrix.M13;
            _frustum[(int)ClippingPlane.Front, 1] = clipMatrix.M24 + clipMatrix.M23;
            _frustum[(int)ClippingPlane.Front, 2] = clipMatrix.M34 + clipMatrix.M33;
            _frustum[(int)ClippingPlane.Front, 3] = clipMatrix.M44 + clipMatrix.M43;
            NormalizePlane(_frustum, (int)ClippingPlane.Front);

            //Find Frustum Points by solving all the systems
            float[] p;
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Left, (int)ClippingPlane.Bottom);
            _frustum_points[0, 0] = p[0]; _frustum_points[0, 1] = p[1]; _frustum_points[0, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Left, (int)ClippingPlane.Top);
            _frustum_points[1, 0] = p[0]; _frustum_points[1, 1] = p[1]; _frustum_points[1, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Right, (int)ClippingPlane.Bottom);
            _frustum_points[2, 0] = p[0]; _frustum_points[2, 1] = p[1]; _frustum_points[2, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Back, (int)ClippingPlane.Right, (int)ClippingPlane.Top);
            _frustum_points[3, 0] = p[0]; _frustum_points[3, 1] = p[1]; _frustum_points[3, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Left, (int)ClippingPlane.Bottom);
            _frustum_points[4, 0] = p[0]; _frustum_points[4, 1] = p[1]; _frustum_points[4, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Left, (int)ClippingPlane.Top);
            _frustum_points[5, 0] = p[0]; _frustum_points[5, 1] = p[1]; _frustum_points[5, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Right, (int)ClippingPlane.Bottom);
            _frustum_points[6, 0] = p[0]; _frustum_points[6, 1] = p[1]; _frustum_points[6, 2] = p[2];
            p = solvePlaneSystem((int)ClippingPlane.Front, (int)ClippingPlane.Right, (int)ClippingPlane.Top);
            _frustum_points[7, 0] = p[0]; _frustum_points[7, 1] = p[1]; _frustum_points[7, 2] = p[2];


            /*
            //Print equations and find points on each
            for (int pl = 0; pl < 6; pl++)
            {
                Console.WriteLine("Plane {4} Equation ({0})x + ({1})y + ({2})z + ({3})",
                    _frustum[pl, 0], _frustum[pl, 1], _frustum[pl, 2], _frustum[pl, 3], pl);
                Console.WriteLine("Point A ({0}, {1}, {2})",
                    0,0, -_frustum[pl, 3] / _frustum[pl, 2]);
                Console.WriteLine("Point B ({0}, {1}, {2})",
                    0, -_frustum[pl, 3] / _frustum[pl, 1], 0);
                Console.WriteLine("Point C ({0}, {1}, {2})",
                    -_frustum[pl, 3] / _frustum[pl, 0], 0, 0);
            }
            */
        
        }

        float[] solvePlaneSystem(int p1, int p2, int p3)
        {
            //Setup Matrix
            var A = MathNet.Numerics.LinearAlgebra.Matrix<float>.Build.DenseOfArray(new float[,]
            {
                { _frustum[p1, 0], _frustum[p1, 1], _frustum[p1, 2] },
                { _frustum[p2, 0], _frustum[p2, 1], _frustum[p2, 2] },
                { _frustum[p3, 0], _frustum[p3, 1], _frustum[p3, 2] }
            });

            //Setup Right Hand Side
            var b = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.Dense(new float[]
            { _frustum[p1, 3], _frustum[p2, 3], _frustum[p3, 3] });


            var x = A.Solve(b);

            float[] ret_x = new float[3];
            ret_x[0] = x[0];
            ret_x[1] = x[1];
            ret_x[2] = x[2];

            return ret_x;

        }

    }



}
