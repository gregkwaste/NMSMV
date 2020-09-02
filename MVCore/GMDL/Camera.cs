using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK;
using MVCore;
using MVCore.Utils;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra.Complex;
using System.ComponentModel;
using System.Drawing.Design;
using System.Security.Permissions;

namespace MVCore.GMDL
{
    public struct CameraPos
    {
        public Vector3 PosImpulse;
        public Vector3 Rotation;

        public void Reset()
        {
            PosImpulse = new Vector3(0.0f);
            Rotation = new Vector3(0.0f);
        }
    }

    public class Camera
    {
        //Base Coordinate System
        public Vector3 BaseRight = new Vector3(1.0f, 0.0f, 0.0f);
        public Vector3 BaseFront = new Vector3(0.0f, 0.0f, -1.0f);
        public Vector3 BaseUp = new Vector3(0.0f, 1.0f, 0.0f);

        //Prev Vectors
        public Quaternion PrevDirection = new Quaternion(new Vector3(0.0f, (float)Math.PI / 2.0f, 0.0f));
        public Vector3 PrevPosition = new Vector3(0.0f, 0.0f, 0.0f);
        
        //Target Vectors
        public Quaternion TargetDirection = new Quaternion(new Vector3(0.0f, (float)Math.PI / 2.0f, 0.0f));
        public Vector3 TargetPosition = new Vector3(0.0f, 0.0f, 0.0f);

        //Current Vectors
        public Vector3 Right = new Vector3(1.0f, 0.0f, 0.0f);
        public Vector3 Front = new Vector3(0.0f, 0.0f, -1.0f);
        public Vector3 Up = new Vector3(0.0f, 1.0f, 0.0f);
        public Quaternion Direction = new Quaternion(new Vector3(0.0f, (float)Math.PI / 2.0f, 0.0f));
        public Vector3 Position = new Vector3(0.0f, 0.0f, 0.0f);

        //Movement Time
        private float t_pos_move = 100.0f;
        private float t_rot_move = 100.0f;
        private float t_start = 0.0f;

        public float Speed = 1.0f; //Speed in Units/Sec
        public float SpeedPower = 1.0f; //Coefficient to which speed is raised
        public float Sensitivity = 0.001f;
        public bool isActive = false;
        //Projection variables Set defaults
        public float fov;
        public float zNear = 0.05f;
        public float zFar = 15000.0f;
        public float aspect = 1.0f;

        //Matrices
        public Matrix4 projMat;
        public Matrix4 projMatInv;
        public Matrix4 lookMat;
        public Matrix4 lookMatInv;
        public Matrix4 viewMat = Matrix4.Identity;
        public int type;
        public bool culling;

        //Camera Frustum Planes
        private Frustum extFrustum = new Frustum();
        public Vector4[] frPlanes = new Vector4[6];

        //Rendering Stuff
        public GMDL.GLMeshVao vao;
        public int program;

        public Camera(int angle, int program, int mode, bool cull)
        {
            //Set fov on init
            this.setFOV(angle);
            vao = new GLMeshVao();
            vao.vao = (new Primitives.Box(1.0f, 1.0f, 1.0f, new Vector3(1.0f), true)).getVAO();
            this.program = program;
            this.type = mode;
            this.culling = cull;

            //calcCameraOrientation(ref Front, ref Right, ref Up, 0, 0);
            
            //Initialize the viewmat
            this.updateViewMatrix();
        
        }
        
        public void updateViewMatrix()
        {
            lookMat = Matrix4.LookAt(Position, Position + Front, Up);
            
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
            
            //Calculate invert Matrices
            lookMatInv = Matrix4.Invert(lookMat);
            projMatInv = Matrix4.Invert(projMat);
            
            updateFrustumPlanes();
        }

        private void calcCameraOrientation(ref Vector3 front, ref Vector3 right, ref Vector3 up, 
            float yaw, float pitch)
        {
            //Recalculate front vector
            front.X = (float) Math.Cos(yaw) * (float)Math.Cos(pitch);
            front.Y = (float) Math.Sin(pitch);
            front.Z = (float) Math.Sin(yaw) * (float)Math.Cos(pitch);
            front.Normalize();

            //Recalculate right vector
            right = Vector3.Cross(front, new Vector3(0.0f, 1.0f, 0.0f)).Normalized();
            up = Vector3.Cross(right, front).Normalized();
        
        }

        public void updateTarget(CameraPos target, float interval)
        {
            //Interval is the update interval of the movement defined in the control camera timer
            
            //Cache current Position + Orientation
            PrevPosition = Position;
            PrevDirection = Direction;

            
            //Rotate Direction
            Quaternion rx = Quaternion.FromAxisAngle(Up, -target.Rotation.X * Sensitivity);
            Quaternion ry = Quaternion.FromAxisAngle(Right, -target.Rotation.Y * Sensitivity); //Looks OK
            //Quaternion rz = Quaternion.FromAxisAngle(Front, 0.0f); //Looks OK

            TargetDirection = Direction * rx * ry;

            float actual_speed = (float) Math.Pow(Speed, SpeedPower);
            

            float step = 0.001f;
            Vector3 offset = new Vector3();
            offset += step * actual_speed * target.PosImpulse.X * Right;
            offset += step * actual_speed * target.PosImpulse.Y * Front;
            offset += step * actual_speed * target.PosImpulse.Z * Up;

            //Update final vector
            TargetPosition += offset;

            //Calculate Time for movement
            
            /*
            Console.WriteLine("TargetPos {0} {1} {2}",
                TargetPosition.X, TargetPosition.Y, TargetPosition.Z);
            Console.WriteLine("PrevPos {0} {1} {2}",
                PrevPosition.X, PrevPosition.Y, PrevPosition.Z);
            Console.WriteLine("TargetRotation {0} {1} {2} {3}",
                TargetDirection.X, TargetDirection.Y, TargetDirection.Z, TargetDirection.W);
            Console.WriteLine("PrevRotation {0} {1} {2} {3}",
                PrevDirection.X, PrevDirection.Y, PrevDirection.Z, PrevDirection.W);
            */

            float eff_speed = interval * actual_speed / 1000.0f;
            t_pos_move = (TargetPosition - PrevPosition).Length / eff_speed;
            t_rot_move = (TargetDirection - PrevDirection).Length / eff_speed;
            t_start = 0.0f; //Reset time_counter

            //Console.WriteLine("t_pos {0}, t_rot {1}", t_pos_move, t_rot_move);

        }

        public void Move(double dt)
        {
             //calculate interpolation coeff
            t_start += (float) dt;
            float pos_lerp_coeff, rot_lerp_coeff;

            
            pos_lerp_coeff = t_start / (float) Math.Max(t_pos_move, 1e-4);
            pos_lerp_coeff = MathUtils.clamp(pos_lerp_coeff, 0.0f, 1.0f);
            
            
            rot_lerp_coeff = t_start / (float)Math.Max(t_rot_move, 1e-4);
            rot_lerp_coeff = MathUtils.clamp(rot_lerp_coeff, 0.0f, 1.0f);
            

            //Interpolate Quaternions/Vectors
            Direction = PrevDirection * (1.0f - rot_lerp_coeff) +
                        TargetDirection * rot_lerp_coeff;
            Position = PrevPosition * (1.0f - pos_lerp_coeff) +
                    TargetPosition * pos_lerp_coeff;

            //Update Base Axis
            Quaternion newFront = MathUtils.conjugate(Direction) * new Quaternion(BaseFront, 0.0f) * Direction;
            Front = newFront.Xyz.Normalized();
            Right = Vector3.Cross(Front, BaseUp).Normalized();
            Up = Vector3.Cross(Right, Front).Normalized();
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

        public bool frustum_occlude(Vector3 AABBMIN, Vector3 AABBMAX, Matrix4 transform)
        {
            if (!Common.RenderState.renderSettings.UseFrustumCulling)
                return true;

            float radius = 0.5f * (AABBMIN - AABBMAX).Length;
            Vector3 bsh_center = AABBMIN + 0.5f * (AABBMAX - AABBMIN);

            //Move sphere to object's root position
            bsh_center = (new Vector4(bsh_center, 1.0f) * transform).Xyz;

            //This is not accurate for some fucking reason
            //return extFrustum.AABBVsFrustum(cand.Bbox, cand.worldMat * transform);

            //In the future I should add the original AABB as well, spheres look to work like a charm for now   
            return extFrustum.SphereVsFrustum(bsh_center, radius);
        }


        public bool frustum_occlude(GMDL.GLMeshVao meshVao, Matrix4 transform)
        {
            if (!culling) return true;

            Vector4 v1, v2;

            v1 = new Vector4(meshVao.metaData.AABBMIN, 1.0f);
            v2 = new Vector4(meshVao.metaData.AABBMAX, 1.0f);
            
            return frustum_occlude(v1.Xyz, v2.Xyz, transform);
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
        private Vector4[] _frustum = new Vector4[6];
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

        public bool PointVsFrustum(Vector4 point)
        {
            for (int i = 0; i < 6; i++)
            {
                if (Vector4.Dot(_frustum[i],point) <= 0.0f)
                {
                    //Console.WriteLine("Point vs Frustum, Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p, x, y, z);
                    return false;
                }
                    
            }
            return true;
        }

        public bool PointVsFrustum(Vector3 location)
        {
            return PointVsFrustum(new Vector4(location, 1.0f));
        }


        public bool AABBVsFrustum(Vector3[] AABB)
        {
            //Transform points from local to model space
            Vector4[] tr_AABB = new Vector4[2];

            tr_AABB[0] = new Vector4(AABB[0], 1.0f);
            tr_AABB[1] = new Vector4(AABB[1], 1.0f);


            Vector4[] verts = new Vector4[8];
            verts[0] = new Vector4(tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z, 1.0f);
            verts[1] = new Vector4(tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z, 1.0f);
            verts[2] = new Vector4(tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z, 1.0f);
            verts[3] = new Vector4(tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z, 1.0f);
            verts[4] = new Vector4(tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z, 1.0f);
            verts[5] = new Vector4(tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z, 1.0f);
            verts[6] = new Vector4(tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z, 1.0f);
            verts[7] = new Vector4(tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z, 1.0f);

            
            //Check if all points are outside one of the planes
            for (int p = 0; p < 6; p++)
            {
                //Check all 8 points
                int i;
                for (i = 0; i < 8; i++)
                {
                    if (Vector4.Dot(_frustum[p], verts[i]) > 0.0f)
                        return true;
                }

            }

            return false;
        }


        public bool SphereVsFrustum(Vector4 center, float radius)
        {
            float d = 0;
            for (int p = 0; p < 6; p++)
            {
                d = Vector4.Dot(_frustum[p], center);
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
            return SphereVsFrustum(new Vector4(location, 1.0f), radius);
        }

        public bool VolumeVsFrustum(float x, float y, float z, float width, float height, float length)
        {
            /* TO BE REPAIRED
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
            */
            return true;
        }

        public bool VolumeVsFrustum(Vector3 location, float width, float height, float length)
        {
            return VolumeVsFrustum(location.X, location.Y, location.Z, width, height, length);
        }

        public bool CubeVsFrustum(float x, float y, float z, float size)
        {
            /*
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
            */
            return true;
        }

        public float distanceFromPlane(int id, Vector4 point)
        {
            return Vector4.Dot(_frustum[id], point) / _frustum[id].Length;
        }


        public void CalculateFrustum(Matrix4 mvp)
        {
            //Front Plane
            _frustum[(int)ClippingPlane.Front] = new Vector4(-mvp.M13, -mvp.M23, -mvp.M33, -mvp.M43);

            //Back Plane
            _frustum[(int)ClippingPlane.Back] = new Vector4(mvp.M13 - mvp.M14, mvp.M23 - mvp.M24, mvp.M33 - mvp.M34,
                mvp.M43 - mvp.M44);

            //Left Plane
            _frustum[(int)ClippingPlane.Left] = new Vector4(-mvp.M14 - mvp.M11, -mvp.M24 - mvp.M21,
                                                            -mvp.M34 - mvp.M31,
                                                            -mvp.M44 - mvp.M41);

            //Right Plane
            _frustum[(int)ClippingPlane.Right] = new Vector4(mvp.M11 - mvp.M14, mvp.M21 - mvp.M24,
                                                             mvp.M31 - mvp.M34,
                                                             mvp.M41 - mvp.M44);

            //Top Plane
            _frustum[(int)ClippingPlane.Top] = new Vector4(mvp.M12 - mvp.M14, mvp.M22 - mvp.M24,
                                                             mvp.M32 - mvp.M34,
                                                             mvp.M42 - mvp.M44);

            //Bottom Plane
            _frustum[(int)ClippingPlane.Bottom] = new Vector4(  -mvp.M14 - mvp.M12,
                                                                -mvp.M24 - mvp.M22,
                                                                -mvp.M34 - mvp.M32,
                                                                -mvp.M44 - mvp.M42);

            //Invert everything to bring it to the original values
            for (int i = 0; i < 6; i++)
                _frustum[i] *= -1.0f;

            //Normalize planes (NOT SURE IF I NEED THAT)
            for (int i = 0; i < 6; i++)
                _frustum[i].Normalize();

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


        float[] solvePlaneSystem(int p1, int p2, int p3)
        {
            //Setup Matrix
            var A = MathNet.Numerics.LinearAlgebra.Matrix<float>.Build.DenseOfArray(new float[,]
            {
                { _frustum[p1].X, _frustum[p1].Y, _frustum[p1].Z },
                { _frustum[p2].X, _frustum[p2].Y, _frustum[p2].Z },
                { _frustum[p3].X, _frustum[p3].Y, _frustum[p3].Z }
            });

            //Setup Right Hand Side
            var b = MathNet.Numerics.LinearAlgebra.Vector<float>.Build.Dense(new float[]
            { _frustum[p1].W, _frustum[p2].W, _frustum[p3].W });

            var x = A.Solve(b);

            float[] ret_x = new float[3];
            ret_x[0] = x[0];
            ret_x[1] = x[1];
            ret_x[2] = x[2];

            return ret_x;

        }

    }



}
