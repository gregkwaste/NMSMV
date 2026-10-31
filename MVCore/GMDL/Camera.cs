using System;
using OpenTK.Mathematics;
using MVCore;
using MVCore.Utils;
using System.ComponentModel;
using System.Drawing.Design;

namespace MVCore.GMDL
{
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

        //Move speed in scene radii per second
        public const float MinSpeed = 0.01f;
        public const float MaxSpeed = 100.0f;

        public float Speed 
        {
            get
            {
                return _speed;
            }

            set
            {
                if (float.IsNaN(value))
                    return;
                _speed = Math.Clamp(value, MinSpeed, MaxSpeed);
                NotifyPropertyChanged("Speed");
                NotifyPropertyChanged("SpeedLog");
            }
        }

        //log10 of Speed for the UI slider, so every step changes the speed by the same ratio
        [Newtonsoft.Json.JsonIgnore]
        public double SpeedLog
        {
            get
            {
                return Math.Log10(_speed);
            }

            set
            {
                Speed = (float) Math.Pow(10.0, value);
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

        //Scene bounds (world space, before the scene rotation). A radius of 0 means no scene,
        //then the near/far planes come from the settings
        public Vector3 sceneCenter = Vector3.Zero;
        public float sceneRadius = 0.0f;
        //Move speed multiplier, follows the scene size so every model takes the same time to cross
        public float speedScale = 1.0f;
        //Near/far planes actually used by the projection
        public float zNear;
        public float zFar;

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

        public Camera(int angle, int mode, bool cull)
        {
            //Set fov on init
            settings.FOV = angle;
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

            updateClipPlanes();
            
            if (type == 0) {
                Matrix4.CreatePerspectiveFieldOfView(settings._fovRadians, aspect, zNear, zFar, out projMat);
                //projMat.Transpose();
                viewMat = lookMat * projMat;
            }
            else
            {
                //Create orthographic projection
                Matrix4.CreateOrthographic(aspect * 2.0f, 2.0f, zNear, zFar, out projMat);
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

        //Fit the depth range tightly around the scene, so the depth buffer precision is spent on the model
        private void updateClipPlanes()
        {
            if (sceneRadius <= 0.0f)
            {
                zNear = settings.ZNear;
                zFar = settings.ZFar;
                return;
            }

            float dist = (getRotatedSceneCenter() - Position).Length;
            zFar = dist + 2.0f * sceneRadius;
            //Inside the sphere the nearest geometry can be anywhere, so fall back to a fixed ratio
            zNear = Math.Max(0.5f * (dist - sceneRadius), Math.Max(sceneRadius * 0.001f, zFar * 1e-5f));
        }

        private Vector3 getRotatedSceneCenter()
        {
            //Meshes are drawn with worldMat * rotMat
            return (new Vector4(sceneCenter, 1.0f) * Common.RenderState.rotMat).Xyz;
        }

        //Union of the world AABBs of all visible meshes. Returns false if there are none
        public static bool getSceneBounds(Model root, out Vector3 center, out float radius)
        {
            Vector3 min = new Vector3(float.MaxValue);
            Vector3 max = new Vector3(float.MinValue);
            bool found = false;
            addBounds(root, ref min, ref max, ref found);

            center = found ? 0.5f * (min + max) : Vector3.Zero;
            radius = found ? 0.5f * (max - min).Length : 0.0f;
            return found && radius > 0.0f;
        }

        private static void addBounds(Model node, ref Vector3 min, ref Vector3 max, ref bool found)
        {
            if (node == null || !node.renderable)
                return;

            if (node is Mesh m && m.type == TYPES.MESH && (m.LodLevel == 0 || !Common.RenderState.renderSettings.LODFiltering))
            {
                min = Vector3.ComponentMin(min, m.AABBMIN);
                max = Vector3.ComponentMax(max, m.AABBMAX);
                found = true;
            }

            foreach (Model child in node.Children)
                addBounds(child, ref min, ref max, ref found);
        }

        //Store the scene bounds for the clip planes and speed. With moveCamera, also back the camera off
        //along its current view direction until the whole bounding sphere fits in the view
        public void frameScene(Model root, bool moveCamera)
        {
            if (!getSceneBounds(root, out Vector3 center, out float radius))
            {
                sceneRadius = 0.0f;
                speedScale = 1.0f;
                updateViewMatrix();
                return;
            }

            sceneCenter = center;
            sceneRadius = radius;
            speedScale = radius;

            if (moveCamera)
            {
                float fovY = settings._fovRadians;
                float fovX = 2.0f * (float)Math.Atan(Math.Tan(fovY / 2.0f) * aspect);
                float halfFov = Math.Max(Math.Min(fovY, fovX) / 2.0f, 0.01f);
                float dist = 1.1f * radius / (float)Math.Sin(halfFov);
                Position = getRotatedSceneCenter() - Front * dist;
            }

            updateViewMatrix();
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

        public void updateFrustumPlanes()
        {
            //projMat.Transpose();
            //extFrustum.CalculateFrustum(projMat, lookMat); //Old Method
            extFrustum.CalculateFrustum(viewMat); // New Method
        }

        public bool frustum_occlude(Vector3 AABBMIN, Vector3 AABBMAX, Matrix4 transform)
        {
            if (!Common.RenderState.renderSettings.UseFrustumCulling)
                return true;

            float radius = 0.5f * (AABBMIN - AABBMAX).Length;
            Vector3 bsh_center = AABBMIN + 0.5f * (AABBMAX - AABBMIN);

            //The radius is in local units, so it has to grow with the largest scale of the transform
            radius *= Math.Max(transform.Row0.Xyz.Length, Math.Max(transform.Row1.Xyz.Length, transform.Row2.Xyz.Length));

            //Move sphere to object's root position
            bsh_center = (new Vector4(bsh_center, 1.0f) * transform).Xyz;

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

    }

    public class Frustum
    {
        private Vector4[] _frustum = new Vector4[6];

        public enum ClippingPlane : int
        {
            Right = 0,
            Left = 1,
            Bottom = 2,
            Top = 3,
            Back = 4,
            Front = 5
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

        public void CalculateFrustum(Matrix4 mvp)
        {
            //Front Plane (OpenGL clip space puts the near plane at z = -w, z = 0 is the Direct3D one)
            _frustum[(int)ClippingPlane.Front] = new Vector4(-mvp.M13 - mvp.M14, -mvp.M23 - mvp.M24,
                                                             -mvp.M33 - mvp.M34,
                                                             -mvp.M43 - mvp.M44);

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

            //Normalize planes by the length of their normal so that dot(plane, point) is a real distance.
            //The sphere test compares it against the radius, so normalizing all 4 components is wrong.
            for (int i = 0; i < 6; i++)
                _frustum[i] /= _frustum[i].Xyz.Length;

        }

    }



}
