using System;
using System.Collections.Generic;
using System.ComponentModel;
using OpenTK;
using libMBIN.NMS.Toolkit;
using System.Collections.ObjectModel;
using MVCore.Utils;
using System.Linq;


namespace MVCore.GMDL
{
    public abstract class Model : IDisposable, INotifyPropertyChanged
    {
        public bool renderable; //Used to toggle visibility from the UI
        public bool active; //Used internally
        public bool occluded; //Used by the occluder
        public bool debuggable;
        public int selected;
        //public GLSLHelper.GLSLShaderConfig[] shader_programs;
        public int ID;
        public TYPES type;
        public string name;
        public ulong nameHash;
        public List<Model> children = new List<Model>();
        public Dictionary<string, Dictionary<string, Vector3>> palette;
        public bool procFlag; //This is used to define procgen usage
        public TkSceneNodeData nms_template;
        public GLMeshVao meshVao;
        public int instanceId = -1;

        //Transformation Parameters
        public Vector3 worldPosition;
        public Matrix4 worldMat;
        public Matrix4 normMat;
        public Matrix4 localMat;

        public Vector3 __localPosition; //Original Position
        public Vector3 __localScale; //Original Scale
        public Matrix4 __localRotation; //Original Rotation
        public Vector3 _localPosition;
        public Vector3 _localScale;
        public Vector3 _localRotationAngles;
        public Matrix4 _localRotation;
        public Matrix4 _localPoseMatrix;

        public Model parent;
        public int cIndex = 0;
        public bool updated = true; //Making it public just for the Joints

        //Components
        public Scene parentScene;
        public List<Component> _components = new List<Component>();
        public int animComponentID;
        public int animPoseComponentID;
        public int actionComponentID;

        //LOD
        public float[] _LODDistances = new float[5];
        public int _LODNum = 1; //Default value of 1 LOD per model

        //Bounding Volume
        public Vector3 AABBMIN = new Vector3();
        public Vector3 AABBMAX = new Vector3();

        //Disposable Stuff
        public bool disposed = false;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }


        //Properties
        public Vector3 localPosition
        {
            get { return _localPosition; }
            set { _localPosition = value; updated = true; }
        }

        public Matrix4 localRotation
        {
            get { return _localRotation; }
            set { _localRotation = value; updated = true; }
        }

        public Vector3 localScale
        {
            get { return _localScale; }
            set { _localScale = value; updated = true; }
        }

        public int LODNumber
        {
            get { return _LODNum; }
        }
        public List<float> LODDistances
        {
            get
            {
                List<float> l = new List<float>();
                for (int i = 0; i < _LODDistances.Length; i++)
                {
                    if (_LODDistances[i] > 0)
                        l.Add(_LODDistances[i]);
                }
                return l;
            }
        }

        public void updateRotationFromAngles(float x, float y, float z)
        {

        }

        public virtual Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {

            //Default shit
            //Create assimp node
            Assimp.Node node = new Assimp.Node(Name);
            node.Transform = MathUtils.convertMatrix(localMat);

            //Handle animations maybe?
            int animComponentId = hasComponent(typeof(AnimComponent));
            if (animComponentId > -1)
            {
                AnimComponent cmp = (AnimComponent)_components[animComponentId];
                cmp.assimpExport(ref scn);

            }

            foreach (Model child in children)
            {
                Assimp.Node c = child.assimpExport(ref scn, ref meshImportStatus);
                node.Children.Add(c);
            }


            return node;
        }

        public string Name
        {
            get { return name; }
            set { name = value; }
        }
        public string Type
        {
            get { return type.ToString(); }
        }

        public virtual bool IsRenderable
        {
            get
            {
                return renderable;
            }
            set
            {
                renderable = value;
                updated = true;
                foreach (var child in Children)
                    child.IsRenderable = value;
                //meshVao?.setInstanceOccludedStatus(instanceId, !renderable);
                NotifyPropertyChanged("IsRenderable"); //Make sure to update the UI because of the subsequent changes
            }
        }

        public List<Component> Components
        {
            get
            {
                return _components;
            }
        }


        //Methods
        public void recalculateAABB()
        {

            //Revert back to the original values
            AABBMIN = meshVao.metaData.AABBMIN;
            AABBMAX = meshVao.metaData.AABBMAX;

            //Generate all 8 points from the AABB
            List<Vector4> vecs = new List<Vector4>
            {
                new Vector4(AABBMIN.X, AABBMIN.Y, AABBMIN.Z, 1.0f),
                new Vector4(AABBMAX.X, AABBMIN.Y, AABBMIN.Z, 1.0f),
                new Vector4(AABBMIN.X, AABBMAX.Y, AABBMIN.Z, 1.0f),
                new Vector4(AABBMAX.X, AABBMAX.Y, AABBMIN.Z, 1.0f),

                new Vector4(AABBMIN.X, AABBMIN.Y, AABBMAX.Z, 1.0f),
                new Vector4(AABBMAX.X, AABBMIN.Y, AABBMAX.Z, 1.0f),
                new Vector4(AABBMIN.X, AABBMAX.Y, AABBMAX.Z, 1.0f),
                new Vector4(AABBMAX.X, AABBMAX.Y, AABBMAX.Z, 1.0f)
            };

            //Transform all Vectors using the worldMat
            for (int i = 0; i < 8; i++)
                vecs[i] = vecs[i] * worldMat;

            //Init vectors to max
            AABBMIN = new Vector3(float.MaxValue);
            AABBMAX = new Vector3(float.MinValue);

            //Align values

            for (int i = 0; i < 8; i++)
            {
                AABBMIN.X = Math.Min(AABBMIN.X, vecs[i].X);
                AABBMIN.Y = Math.Min(AABBMIN.Y, vecs[i].Y);
                AABBMIN.Z = Math.Min(AABBMIN.Z, vecs[i].Z);

                AABBMAX.X = Math.Max(AABBMAX.X, vecs[i].X);
                AABBMAX.Y = Math.Max(AABBMAX.Y, vecs[i].Y);
                AABBMAX.Z = Math.Max(AABBMAX.Z, vecs[i].Z);
            }
        }

        public bool intersects(Vector3 ray_start, Vector3 ray, ref float distance)
        {
            //Calculate bound box center
            float radius = 0.5f * (AABBMIN - AABBMAX).Length;
            Vector3 bsh_center = AABBMIN + 0.5f * (AABBMAX - AABBMIN);

            //Move sphere to object's root position
            bsh_center = (new Vector4(bsh_center, 1.0f)).Xyz;

            //Calculate factors of the point equation
            float a = ray.LengthSquared;
            float b = 2.0f * Vector3.Dot(ray, ray_start - bsh_center);
            float c = (ray_start - bsh_center).LengthSquared - radius * radius;

            float D = b * b - 4 * a * c;

            if (D >= 0.0f)
            {
                //Make sure that the calculated l is positive so that intersections are
                //checked only forward
                float l2 = (-b + (float)Math.Sqrt(D)) / (2.0f * a);
                float l1 = (-b - (float)Math.Sqrt(D)) / (2.0f * a);

                if (l2 > 0.0f || l1 > 0.0f)
                {
                    float d = (float)Math.Min((ray * l1).Length, (ray * l2).Length);

                    if (d < distance)
                    {
                        distance = d;
                        return true;
                    }
                }
            }

            return false;
        }

        public abstract Model Clone();

        public virtual void updateLODDistances()
        {
            foreach (Model s in children)
                s.updateLODDistances();
        }

        public virtual void setupSkinMatrixArrays()
        {
            foreach (Model s in children)
                s.setupSkinMatrixArrays();
        }

        public virtual void updateMeshInfo(bool lod_filter = false)
        {
            foreach (Model child in children)
            {
                child.updateMeshInfo(lod_filter);
            }
        }

        public virtual void update()
        {

            //if (changed)
            {
                //Create scaling matrix
                Matrix4 scale = Matrix4.Identity;
                scale[0, 0] = _localScale.X;
                scale[1, 1] = _localScale.Y;
                scale[2, 2] = _localScale.Z;

                localMat = _localPoseMatrix * scale * _localRotation * Matrix4.CreateTranslation(_localPosition);
            }

            //Finally Update world Transformation Matrix
            if (parent != null)
            {
                worldMat = localMat * parent.worldMat;
            }

            else
                worldMat = localMat;

            //Update worldPosition
            if (parent != null)
            {
                //Add Translation as well
                worldPosition = (Vector4.Transform(new Vector4(0.0f, 0.0f, 0.0f, 1.0f), this.worldMat)).Xyz;
            }
            else
                worldPosition = localPosition;

            //Trigger the position update of all children nodes
            foreach (GMDL.Model child in children)
            {
                child.update();
            }

            updated = true; //Transform changed, trigger mesh updates
        }


        public void findNode(string name, ref Model m)
        {
            if (Name == name)
            {
                m = this;
                return;
            }
                
            foreach (Model child in children)
            {
                child.findNode(name, ref m);
            }
        }


        //Properties for Data Binding
        public ObservableCollection<Model> Children
        {
            get
            {
                return new ObservableCollection<Model>(children.OrderBy(i => i.Name));
            }
        }


        //TODO: Consider converting all such attributes using properties
        public void updatePosition(Vector3 newPosition)
        {
            localPosition = newPosition;
        }

        public void init(float[] trans)
        {
            //Get Local Position
            Vector3 rotation;
            _localPosition = new Vector3(trans[0], trans[1], trans[2]);
            __localPosition = _localPosition;

            //Save raw rotations
            rotation.X = MathUtils.radians(trans[3]);
            rotation.Y = MathUtils.radians(trans[4]);
            rotation.Z = MathUtils.radians(trans[5]);

            _localRotationAngles = new Vector3(trans[3], trans[4], trans[5]);
            //IF PARSED SEPARATELY USING THE AXIS ANGLES
            //OpenTK.Quaternion qx = OpenTK.Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f), rotation.X);
            //OpenTK.Quaternion qy = OpenTK.Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f), rotation.Y);
            //OpenTK.Quaternion qz = OpenTK.Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f), rotation.Z);

            //OpenTK.Quaternion q = qy * qz * qx; //ALWAYS YZX
            //OpenTK.Quaternion q = qx * qz * qy; //ALWAYS YZX
            //OpenTK.Quaternion q_euler = OpenTK.Quaternion.FromEulerAngles(MathUtils.radians(trans[3]),
            //                                            MathUtils.radians(trans[4]), MathUtils.radians(trans[5]));

            Matrix4 rotx = Matrix4.CreateRotationX(rotation.X);
            Matrix4 roty = Matrix4.CreateRotationY(rotation.Y);
            Matrix4 rotz = Matrix4.CreateRotationZ(rotation.Z);
            _localRotation = rotz * rotx * roty;
            __localRotation = _localRotation;

            //Get Local Scale
            _localScale = new Vector3(trans[6], trans[7], trans[8]);
            __localScale = _localScale;

            //Set paths
            if (parent != null)
                this.cIndex = this.parent.children.Count;
        }

        //Default Constructor
        protected Model()
        {
            renderable = true;
            active = true;
            debuggable = false;
            occluded = false;
            updated = true;
            selected = 0;
            ID = -1;
            name = "";
            procFlag = false;    //This is used to define procgen usage

            //Transformation Parameters
            worldPosition = new Vector3(0.0f, 0.0f, 0.0f);
            worldMat = Matrix4.Identity;
            normMat = Matrix4.Identity;
            localMat = Matrix4.Identity;

            _localPosition = new Vector3(0.0f, 0.0f, 0.0f);
            _localScale = new Vector3(1.0f, 1.0f, 1.0f);
            _localRotationAngles = new Vector3(0.0f, 0.0f, 0.0f);
            _localRotation = Matrix4.Identity;
            _localPoseMatrix = Matrix4.Identity;

            cIndex = 0;

            //Component Init
            _components = new List<Component>();
            animComponentID = -1;
            animPoseComponentID = -1;
            actionComponentID = -1;
        }


        public virtual void copyFrom(Model input)
        {
            this.renderable = input.renderable; //Override Renderability
            this.debuggable = input.debuggable;
            this.selected = 0;
            this.type = input.type;
            this.name = input.name;
            this.ID = input.ID;
            this.updated = input.updated;
            this.cIndex = input.cIndex;
            //MESHVAO AND INSTANCE IDS SHOULD BE HANDLED EXPLICITLY

            //Clone transformation
            _localPosition = input._localPosition;
            _localRotationAngles = input._localRotationAngles;
            _localRotation = input._localRotation;
            _localScale = input._localScale;
            _localPoseMatrix = input._localPoseMatrix;

            this.localMat = input.localMat;
            this.worldMat = input.worldMat;
            this.normMat = input.normMat;

            //Clone LOD Info
            this._LODNum = input._LODNum;
            for (int i = 0; i < 5; i++)
                this._LODDistances[i] = input._LODDistances[i];

            //Component Stuff
            this.animComponentID = input.animComponentID;
            this.animPoseComponentID = input.animPoseComponentID;

            //Clone components
            for (int i = 0; i < input.Components.Count; i++)
            {
                this.Components.Add(input.Components[i].Clone());
            }
        }

        //Copy Constructor
        public Model(Model input)
        {
            this.copyFrom(input);
            foreach (GMDL.Model child in input.children)
            {
                GMDL.Model nChild = child.Clone();
                nChild.parent = this;
                this.children.Add(nChild);
            }
        }

        //NMSTEmplate Export

        public virtual TkSceneNodeData ExportTemplate(bool keepRenderable)
        {
            //Copy main info
            TkSceneNodeData cpy = new TkSceneNodeData();

            cpy.Transform = nms_template.Transform;
            cpy.Attributes = nms_template.Attributes;
            cpy.Type = nms_template.Type;
            cpy.Name = nms_template.Name;
            cpy.NameHash = nms_template.NameHash;

            if (children.Count > 0)
                cpy.Children = new List<TkSceneNodeData>();

            foreach (Model child in children)
            {
                if (!child.renderable && keepRenderable)
                    continue;
                else if (child.nms_template != null)
                    cpy.Children.Add(child.ExportTemplate(keepRenderable));
            }

            return cpy;
        }



        #region ComponentQueries
        public int hasComponent(Type ComponentType)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                Component temp = _components[i];
                if (temp.GetType() == ComponentType)
                    return i;
            }

            return -1;
        }

        #endregion


        #region AnimationComponent

        public virtual void setParentScene(Scene scene)
        {
            parentScene = scene;
            foreach (Model child in children)
            {
                child.setParentScene(scene);
            }
        }

        #endregion

        #region AnimPoseComponent
        //TODO: It would be nice if I didn't have to do make the method public, but it needs a lot of work on the 
        //AnimPoseComponent class to temporarily store the selected pose frames, while also in the model.update method

        //Locator Animation Stuff

        public Dictionary<string, Matrix4> loadPose()
        {

            if (animPoseComponentID < 0)
                return new Dictionary<string, Matrix4>();

            AnimPoseComponent apc = _components[animPoseComponentID] as AnimPoseComponent;
            Dictionary<string, Matrix4> posematrices = new Dictionary<string, Matrix4>();

            foreach (TkAnimNodeData node in apc._poseFrameData.NodeData)
            {
                List<OpenTK.Quaternion> quats = new List<OpenTK.Quaternion>();
                List<Vector3> translations = new List<Vector3>();
                List<Vector3> scales = new List<Vector3>();

                //We should interpolate frame shit over all the selected Pose Data

                //Gather all the transformation data for all the pose factors
                for (int i = 0; i < apc._poseData.Count; i++)
                //for (int i = 0; i < 1; i++)
                {
                    //Get Pose Frame
                    int poseFrameIndex = apc._poseData[i].PActivePoseFrame;

                    Vector3 v_t, v_s;
                    OpenTK.Quaternion lq;
                    //Fetch Rotation Quaternion
                    lq = NMSUtils.fetchRotQuaternion(node, apc._poseFrameData, poseFrameIndex);
                    v_t = NMSUtils.fetchTransVector(node, apc._poseFrameData, poseFrameIndex);
                    v_s = NMSUtils.fetchScaleVector(node, apc._poseFrameData, poseFrameIndex);

                    quats.Add(lq);
                    translations.Add(v_t);
                    scales.Add(v_s);
                }

                float fact = 1.0f / quats.Count;
                Quaternion fq = new Quaternion();
                Vector3 f_vt = new Vector3();
                Vector3 f_vs = new Vector3();


                fq = quats[0];
                f_vt = translations[0];
                f_vs = scales[0];

                //Interpolate all data
                for (int i = 1; i < quats.Count; i++)
                {
                    //Method A: Interpolate
                    //Quaternion.Slerp(fq, quats[i], 0.5f);
                    //Vector3.Lerp(f_vt, translations[i], 0.5f);
                    //Vector3.Lerp(f_vs, scales[i], 0.5f);

                    //Addup
                    f_vs *= scales[i];
                }

                //Generate Transformation Matrix
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq) * Matrix4.CreateTranslation(f_vt);
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq);
                Matrix4 poseMat = Matrix4.CreateScale(f_vs);
                posematrices[node.Node] = poseMat;

            }

            return posematrices;

        }

        public virtual void applyPoses(Dictionary<string, Matrix4> poseMatrices)
        {

        }


        #endregion


        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                if (children != null)
                    foreach (Model c in children) c.Dispose();
                children.Clear();

                //Free textureManager
            }

            //Free unmanaged resources

            disposed = true;
        }

#if DEBUG
        ~Model()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            System.Diagnostics.Debug.Fail("Undisposed lock. Object Type " + type);
        }
#endif


    }

}
