using System;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
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

    public class CameraJSONSettings
    {
        public CameraSettings settings { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float Pitch { get; set; }
        public float Yaw { get; set; }

        public CameraJSONSettings(Camera cam)
        {
            settings = cam.settings;
            PosX = cam.Position.X;
            PosY = cam.Position.Y;
            PosZ = cam.Position.Z;
            Pitch = cam.pitch;
            Yaw = cam.yaw;
        }

        public CameraJSONSettings()
        {
            settings = new CameraSettings();
            PosX = 0.0f;
            PosY = 0.0f;
            PosZ = 0.0f;
            Pitch = -90.0f;
            Yaw = 0.0f;
        }
    }


    public class CameraSettings : INotifyPropertyChanged
    {
        public float _fovRadians = MathUtils.radians(90);
        private float _znear = 0.005f;
        private float _zfar = 15000.0f;
        private float _speed = 1.0f;
        private float _sensitivity = 0.8f;

        
        //Properties
        public int FOV 
        {
            get
            {
                return (int) MathUtils.degrees(_fovRadians);
            }

            set
            {
                _fovRadians = MathUtils.radians(value);
                NotifyPropertyChanged("FOV");
            }
        }

        public float ZNear 
        {
            get
            {
                return _znear;
            }

            set
            {
                _znear = value;
                NotifyPropertyChanged("ZNear");
            }
        }

        public float ZFar 
        {
            get
            {
                return _zfar;
            }

            set
            {
                _zfar = value;
                NotifyPropertyChanged("ZFar");
            }
        }

        public float Speed 
        {
            get
            {
                return _speed;
            }

            set
            {
                _speed = value;
                NotifyPropertyChanged("Speed");
            }
        }

        public float Sensitivity
        {
            get
            {
                return _sensitivity;
            }

            set
            {
                _sensitivity = value;
                NotifyPropertyChanged("Sensitivity");
            }
        }
        
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }
    }

    public class Camera
    {
        //Base Coordinate System
        public static Vector3 BaseRight = new Vector3(1.0f, 0.0f, 0.0f);
        public static Vector3 BaseFront = new Vector3(0.0f, 0.0f, -1.0f);
        public static Vector3 BaseUp = new Vector3(0.0f, 1.0f, 0.0f);
        public Vector3 Front = BaseFront;
        public Vector3 Right = BaseRight;

        //Current Vectors
        public Vector3 Position = new Vector3(0.0f, 0.0f, 0.0f);
        public float yaw = 0.0f;
        public float pitch = -90.0f;
        
        public bool isActive = false;
        //Projection variables Set defaults
        public float aspect = 1.0f;
        
        public CameraSettings settings = new CameraSettings();

        
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
        public GLMeshVao vao;
        public int program;

        public Camera(int angle, int prgrm, int mode, bool cull)
        {
            //Set fov on init
            settings.FOV = angle;
            vao = new GLMeshVao();
            vao.vao = (new Primitives.Box(1.0f, 1.0f, 1.0f, new Vector3(1.0f), true)).getVAO();
            program = prgrm;
            type = mode;
            culling = cull;

            //calcCameraOrientation(ref Front, ref Right, ref Up, 0, 0);
            
            //Initialize the viewmat
            updateViewMatrix();
        
        }
        
        public void updateViewMatrix()
        {
            Front.X = (float)Math.Cos(MathHelper.DegreesToRadians(Math.Clamp(yaw, -89, 89))) * (float)Math.Cos(MathHelper.DegreesToRadians(pitch));
            Front.Y = (float)Math.Sin(MathHelper.DegreesToRadians(Math.Clamp(yaw, -89, 89)));
            Front.Z = (float)Math.Cos(MathHelper.DegreesToRadians(Math.Clamp(yaw, -89, 89))) * (float)Math.Sin(MathHelper.DegreesToRadians(pitch));
            Front = Vector3.Normalize(Front);
            
            Right = Vector3.Cross(Front, BaseUp).Normalized();

            lookMat = Matrix4.LookAt(Position, Position + Front, Vector3.Cross(Right, Front));
            
            if (type == 0) {
                Matrix4.CreatePerspectiveFieldOfView(settings._fovRadians, aspect, settings.ZNear, settings.ZFar, out projMat);
                //projMat.Transpose();
                viewMat = lookMat * projMat;
            }
            else
            {
                //Create orthographic projection
                Matrix4.CreateOrthographic(aspect * 2.0f, 2.0f, settings.ZNear, settings.ZFar, out projMat);
                //projMat.Transpose();
                //Create scale matrix based on the fov
                Matrix4 scaleMat = Matrix4.CreateScale(0.8f * settings._fovRadians);
                viewMat = scaleMat * lookMat * projMat;
            }

            //Calculate invert Matrices
            lookMatInv = Matrix4.Invert(lookMat);
            projMatInv = Matrix4.Invert(projMat);
            
            updateFrustumPlanes();
        }

        public CameraJSONSettings GetSettings()
        {
            return new CameraJSONSettings()
            {
                settings = settings,
                
                PosX = Position.X,
                PosY = Position.Y,
                PosZ = Position.Z
            };
        }

        public static void SetCameraPosition(ref Camera cam, Vector3 pos)
        {
            //Position
            cam.Position = pos;
        }

        public static void SetCameraDirection(ref Camera cam, Quaternion quat)
        {
            //TODO Convert Quaternion to yaw pitch
        }

        public static void SetCameraSettings(ref Camera cam, CameraSettings settings)
        {
            cam.settings = settings;
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
                    //Common.CallBacks.Log("Point vs Frustum, Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p, x, y, z);
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
                    //Common.CallBacks.Log("Plane {0} Failed. Failed Vector {1} {2} {3}", (ClippingPlane)p,
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
