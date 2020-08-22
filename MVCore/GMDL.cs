//#define DUMP_TEXTURES

using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL4;
using OpenTK;
//using MathNet.Numerics.LinearAlgebra;
//using MIConvexHull;
using KUtility;
using Model_Viewer;
using System.Linq;
using System.Net.Mime;
using System.Xml;
using libMBIN.NMS.Toolkit;
using System.Reflection;
using System.ComponentModel;
using MVCore;
using ExtTextureFilterAnisotropic = OpenTK.Graphics.ES30.ExtTextureFilterAnisotropic;
using PixelFormat = OpenTK.Graphics.OpenGL4.PixelFormat;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using GLSLHelper;
using System.Windows.Forms;
using System.Security.Permissions;
using SharpFont;
using WPFModelViewer.Properties;
using WPFModelViewer;
//using Matrix4 = MathNet.Numerics.LinearAlgebra.Matrix<float>;
using MVCore.Common;

namespace MVCore.GMDL
{
    public enum RENDERPASS
    {
        DEFERRED = 0x0,
        FORWARD,
        DECAL,
        BHULL,
        BBOX,
        DEBUG,
        PICK,
        COUNT
    }

    public class SimpleSampler
    {
        public string PName { get; set; }
        SimpleSampler()
        {

        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    public abstract class model : IDisposable, INotifyPropertyChanged
    {
        public bool renderable;
        public bool occluded;
        public bool debuggable;
        public int selected;
        //public GLSLHelper.GLSLShaderConfig[] shader_programs;
        public int ID;
        public TYPES type;
        public string name;
        public ulong nameHash;
        public List<model> children = new List<model>();
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

        public Vector3 _localPosition;
        public Vector3 _localScale;
        public Vector3 _localRotationAngles;
        public Matrix4 _localRotation;
        public Matrix4 _localPoseMatrix;

        public model parent;
        public int cIndex = 0;
        public bool updated = true; //Making it public just for the joints

        //Components
        public scene parentScene;
        public List<Component> _components = new List<Component>();
        public int animComponentID;
        public int animPoseComponentID;

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
            get {
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

        public virtual Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus) {

            //Default shit
            //Create assimp node
            Assimp.Node node = new Assimp.Node(Name);
            node.Transform = MathUtils.convertMatrix(localMat);

            //Handle animations maybe?
            int animComponentId = hasComponent(typeof(AnimComponent));
            if (animComponentId > -1)
            {
                AnimComponent cmp = (AnimComponent) _components[animComponentId];
                cmp.assimpExport(ref scn);

            }

            foreach (model child in children)
            {
                Assimp.Node c = child.assimpExport(ref scn, ref meshImportStatus);
                node.Children.Add(c);
            }
                

            return node;
        }

        public string Name
        {
            get { return name; }
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
            get {
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
            List<Vector4> vecs = new List<Vector4>();
            vecs.Add(new Vector4(AABBMIN.X, AABBMIN.Y, AABBMIN.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMIN.Y, AABBMIN.Z, 1.0f));
            vecs.Add(new Vector4(AABBMIN.X, AABBMAX.Y, AABBMIN.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMAX.Y, AABBMIN.Z, 1.0f));

            vecs.Add(new Vector4(AABBMIN.X, AABBMIN.Y, AABBMAX.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMIN.Y, AABBMAX.Z, 1.0f));
            vecs.Add(new Vector4(AABBMIN.X, AABBMAX.Y, AABBMAX.Z, 1.0f));
            vecs.Add(new Vector4(AABBMAX.X, AABBMAX.Y, AABBMAX.Z, 1.0f));

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
            float c = (ray_start - bsh_center).LengthSquared - radius*radius;

            float D = b * b - 4 * a * c;

            if (D >= 0.0f)
            {
                //Make sure that the calculated l is positive so that intersections are
                //checked only forward
                float l2 = (-b + (float) Math.Sqrt(D)) / (2.0f * a);
                float l1 = (-b - (float) Math.Sqrt(D)) / (2.0f * a);

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

        public abstract model Clone();

        public virtual void updateLODDistances()
        {
            foreach (model s in children)
                s.updateLODDistances();
        }

        public virtual void setupSkinMatrixArrays()
        {
            foreach (model s in children)
                s.setupSkinMatrixArrays();
        }

        public virtual void updateMeshInfo()
        {
            foreach (model child in children)
            {
                child.updateMeshInfo();
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
            foreach (GMDL.model child in children)
            {
                child.update();
            }

            updated = true; //Transform changed, trigger mesh updates
        }

        //Properties for Data Binding
        public ObservableCollection<model> Children{
            get
            {
                return new ObservableCollection<model>(children.OrderBy(i=>i.Name));
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
            
            //Get Local Scale
            _localScale = new Vector3(trans[6], trans[7], trans[8]);

            //Set paths
            if (parent!=null)
                this.cIndex = this.parent.children.Count;
        }

        //Default Constructor
        protected model()
        {
            renderable = true;
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
    }


        public virtual void copyFrom(model input)
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
        public model(model input)
        {
            this.copyFrom(input);
            foreach (GMDL.model child in input.children)
            {
                GMDL.model nChild = child.Clone();
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
            
            foreach (model child in children)
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

        public virtual void setParentScene(scene scene)
        {
            parentScene = scene;
            foreach (model child in children)
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
                OpenTK.Quaternion fq = new OpenTK.Quaternion();
                Vector3 f_vt = new Vector3();
                Vector3 f_vs = new Vector3();


                fq = quats[0];
                f_vt = translations[0];
                f_vs = scales[0];

                //Interpolate all data
                for (int i = 1; i < quats.Count; i++)
                {
                    OpenTK.Quaternion.Slerp(fq, quats[i], 0.5f);
                    Vector3.Lerp(f_vt, translations[i], 0.5f);
                    Vector3.Lerp(f_vs, scales[i], 0.5f);
                }

                //Generate Transformation Matrix
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq) * Matrix4.CreateTranslation(f_vt);
                Matrix4 poseMat = Matrix4.CreateScale(f_vs) * Matrix4.CreateFromQuaternion(fq);
                //Matrix4 poseMat = Matrix4.CreateScale(f_vs);
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
                if (children!=null)
                    foreach (model c in children) c.Dispose();
                children.Clear();

                //Free textureManager
            }

            //Free unmanaged resources

            disposed = true;
        }

#if DEBUG
        ~model()
        {
            // If this finalizer runs, someone somewhere failed to
            // call Dispose, which means we've failed to leave
            // a monitor!
            System.Diagnostics.Debug.Fail("Undisposed lock. Object Type " + type);
        }
#endif

       
    }

    public class reference : locator
    {
        public model ref_scene; //holds the referenced scene

        public reference()
        {
            type = TYPES.REFERENCE;
        }

        public reference(reference input)
        {
            //Copy info
            base.copyFrom(input);
            
            ref_scene = input.ref_scene.Clone();
            ref_scene.parent = this;
            children.Add(ref_scene);
        }

        public void copyFrom(reference input)
        {
            base.copyFrom(input); //Copy base stuff
            this.ref_scene = input.ref_scene;
        }

        public override model Clone()
        {
            return new reference(this);
        }

        public override TkSceneNodeData ExportTemplate(bool keepRenderable)
        {
            //Copy main info
            TkSceneNodeData cpy = new TkSceneNodeData();

            cpy.Transform = nms_template.Transform;
            cpy.Attributes = nms_template.Attributes;
            cpy.Type = nms_template.Type;
            cpy.Name = nms_template.Name;
            cpy.NameHash = nms_template.NameHash;

            //The only difference with references is that the first children of the reference object is a copy of the
            //actual scene

            if (children.Count > 1)
                cpy.Children = new List<TkSceneNodeData>();

            for (int i = 1; i < children.Count; i++)
            {
                model child = children[i];
                if (!child.renderable && keepRenderable)
                    continue;
                else if (child.nms_template != null)
                    cpy.Children.Add(child.ExportTemplate(keepRenderable));
            }
            
            return cpy;
        }


        public override void setParentScene(scene animscene)
        {
            //DO NOTHING
        }
        
        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }




    public class scene : locator
    {
        public GeomObject gobject; //Keep GeomObject reference
        public textureManager texMgr;

        //Keep reference of all the animation joints of the scene and the skinmatrices
        public float[] skinMats; //Final Matrices
        public Dictionary<string, Joint> jointDict;
        public int activeLOD = 0;
        
        public scene() {
            type = TYPES.MODEL;
            texMgr = new textureManager();
            //Init Animation Stuff
            skinMats = new float[256 * 16];
            jointDict = new Dictionary<string, Joint>();
        }

        
        public void resetPoses()
        {
            foreach (Joint j in jointDict.Values)
                j.localPoseMatrix = Matrix4.Identity;
            update();
        }

        public override void applyPoses(Dictionary<string, Matrix4> poseMatrices)
        {
            foreach (KeyValuePair<string, Matrix4> kp in poseMatrices)
            {
                string node_name = kp.Key;
                
                if (jointDict.ContainsKey(node_name))
                {
                    Joint j = jointDict[node_name];
                    //j.localPoseMatrix = kp.Value;

                    //Vector3 tr = kp.Value.ExtractTranslation();
                    Vector3 sc = kp.Value.ExtractScale();
                    OpenTK.Quaternion q = kp.Value.ExtractRotation();

                    //j.localRotation = Matrix4.CreateFromQuaternion(q);
                    //j.localPosition = tr;

                    j.localRotation = Matrix4.CreateFromQuaternion(q) * j.localRotation;
                    j.localScale *= sc;

                    //j.localPoseMatrix = kp.Value;
                }
            }

            update();
        }

        //TODO Add button in the UI to toggle that shit
        private void resetAnimation()
        {
            foreach (Joint j in jointDict.Values)
            {
                j._localScale = j.BindMat.ExtractScale();
                j._localRotation = Matrix4.CreateFromQuaternion(j.BindMat.ExtractRotation());
                j._localPosition = j.BindMat.ExtractTranslation();
                j._localPoseMatrix = Matrix4.Identity;
            }
        }

        public override Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int,int> meshImportStatus)
        {

            return base.assimpExport(ref scn, ref meshImportStatus);
        }

        public scene(scene input) :base(input)
        {
            gobject = input.gobject;
        }      

        public void copyFrom(scene input)
        {
            base.copyFrom(input); //Copy base stuff
            gobject = input.gobject;
            texMgr = input.texMgr;
        }

        public override model Clone()
        {
            scene new_s = new scene();
            new_s.copyFrom(this);

            new_s.meshVao = this.meshVao;
            new_s.instanceId = GLMeshBufferManager.addInstance(ref new_s.meshVao, this);
            
            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = new_s;
                new_s.children.Add(new_child);
            }

            //Recursively update parentScene to all the new objects
            new_s.setParentScene(new_s);

            //Initialize jointDictionary
            new_s.jointDict.Clear();
            new_s.setupJointDict(new_s);

            return new_s;
        }

        public void setupJointDict(model m)
        {
            if (m.type == TYPES.JOINT)
                jointDict[m.Name] = (Joint) m;

            foreach (model c in m.children)
                setupJointDict(c);
        }

        public override void updateLODDistances()
        {
            //TODO: Cache the distance elsewhere

            //Set Current LOD Level
            double distance = (worldPosition - Common.RenderState.activeCam.Position).Length;

            //Find active LOD
            activeLOD = _LODNum - 1;
            for (int j = 0; j < _LODNum - 1; j++)
            {
                if (distance < _LODDistances[j])
                {
                    activeLOD = j;
                    break;
                }
            }

            base.updateLODDistances();
        }

        
        public override void updateMeshInfo()
        {
            //Update Skin Matrices
            foreach (Joint j in jointDict.Values)
            {
                Matrix4 jointSkinMat = j.invBMat * j.worldMat;
                MathUtils.insertMatToArray16(skinMats, j.jointIndex * 16, jointSkinMat);
            }

            base.updateMeshInfo();
        }


        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                skinMats = null;
                jointDict.Clear();

                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }

    }

    public class gizmo: model
    {
        public gizmo()
        {
            type = TYPES.GIZMO;
            
            //Assemble geometry in the constructor
            meshVao = Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_translation_gizmo"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
        }

        public override GMDL.model Clone()
        {
            return new gizmo();
        }
    }

    public class locator: model
    {
        public locator()
        {
            //Set type
            type = TYPES.LOCATOR;
            //Set BBOX
            AABBMIN = new Vector3(-0.1f, -0.1f, -0.1f);
            AABBMAX = new Vector3(0.1f, 0.1f, 0.1f);
            
            //Assemble geometry in the constructor
            meshVao = Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_cross"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
        }

        public void copyFrom(locator input)
        {
            base.copyFrom(input); //Copy stuff from base class
        }

        protected locator(locator input) : base(input)
        {
            this.copyFrom(input);
        }

        public override GMDL.model Clone()
        {
            locator new_s = new locator();
            new_s.copyFrom(this);

            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = new_s;
                new_s.children.Add(new_child);
            }

            return new_s;
        }

        public override void update()
        {
            base.update();
            recalculateAABB(); //Update AABB
        }

        public override void updateMeshInfo()
        {
            if (!renderable)
            {
                base.updateMeshInfo();
                return;
            }
            
            bool fr_status = Common.RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
            bool occluded_status = !fr_status && Common.RenderState.renderSettings.UseFrustumCulling;

            //Recalculations && Data uploads
            if (!occluded_status)
            {
                instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
            }

            base.updateMeshInfo();
            updated = false; //All done
        }


        #region IDisposable Support
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    meshVao = null; //VAO will be deleted from the resource manager since it is a common mesh
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
        #endregion

    }

    public class GLVao : IDisposable
    {
        //VAO ID
        public int vao_id;
        //VBO IDs
        public int vertex_buffer_object;
        public int element_buffer_object;

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public GLVao()
        {
            vao_id = -1;
            vertex_buffer_object = -1;
            element_buffer_object = -1;
        }

        public void Dispose()
        {
            Dispose(true);
#if DEBUG
            GC.SuppressFinalize(this);
#endif
        }

        protected void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    if (vao_id > 0)
                    {
                        GL.DeleteVertexArray(vao_id);
                        GL.DeleteBuffer(vertex_buffer_object);
                        GL.DeleteBuffer(element_buffer_object);
                    }
                    
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~GLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }


    public class MeshMetaData
    {
        //Mesh Properties
        public int vertrstart_physics;
        public int vertrend_physics;
        public int vertrstart_graphics;
        public int vertrend_graphics;
        public int batchstart_physics;
        public int batchstart_graphics;
        public int batchcount;
        public int firstskinmat;
        public int lastskinmat;
        public int lodLevel;
        //New stuff Properties
        public int boundhullstart;
        public int boundhullend;
        public Vector3 AABBMIN;
        public Vector3 AABBMAX;
        public ulong Hash;

        public MeshMetaData()
        {
            //Init values to null
            vertrend_graphics = 0;
            vertrstart_graphics = 0;
            vertrend_physics = 0;
            vertrstart_physics = 0;
            batchstart_graphics = 0;
            batchstart_physics = 0;
            batchcount = 0;
            firstskinmat = 0;
            lastskinmat = 0;
            boundhullstart = 0;
            boundhullend = 0;
            Hash = 0xFFFFFFFF;
            AABBMIN = new Vector3();
            AABBMAX= new Vector3();
        }

        public MeshMetaData(MeshMetaData input)
        {
            //Init values to null
            vertrend_graphics = input.vertrend_graphics;
            vertrstart_graphics = input.vertrstart_graphics;
            vertrend_physics = input.vertrend_physics;
            vertrstart_physics = input.vertrstart_physics;
            batchstart_graphics = input.batchstart_graphics;
            batchstart_physics = input.batchstart_physics;
            batchcount = input.batchcount;
            firstskinmat = input.firstskinmat;
            lastskinmat = input.lastskinmat;
            boundhullstart = input.boundhullstart;
            boundhullend = input.boundhullend;
            Hash = input.Hash;
            lodLevel = input.lodLevel;
            AABBMIN = new Vector3(input.AABBMIN);
            AABBMAX = new Vector3(input.AABBMAX);
        }
    }

    public static class GLMeshBufferManager
    {
        public const int color_Float_Offset = 0;
        public const int color_Byte_Offset = 0;

        public const int skinned_Float_Offset = 3;
        public const int skinned_Byte_Offset = 12;

        public const int instanceData_Float_Offset = 4;
        public const int instanceData_Byte_Offset = 16;

        //Relative Instance Offsets

        //public static int instance_Uniforms_Offset = 0;
        public const int instance_Uniforms_Float_Offset = 0;
        //public static int instance_worldMat_Offset = 64;
        public const int instance_worldMat_Float_Offset = 16;
        //public static int instance_normalMat_Offset = 128;
        public const int instance_normalMat_Float_Offset = 32;
        //public static int instance_worldMatInv_Offset = 192;
        public const int instance_worldMatInv_Float_Offset = 48;
        //public static int instance_isOccluded_Offset = 256;
        public const int instance_isOccluded_Float_Offset = 64;
        //public static int instance_isSelected_Offset = 260;
        public const int instance_isSelected_Float_Offset = 65;
        //public static int instance_color_Offset = 264; //TODO make that a vec4
        public const int instance_color_Float_Offset = 66;
        public static int instance_struct_size_bytes = 272;
        public const int instance_struct_size_floats = 68;

        //Instance Data Format:
        //0-16 : instance WorldMatrix
        //16-17: isOccluded
        //17-18: isSelected
        //18-20: padding


        public static int addInstance(ref GLMeshVao mesh, model m)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }
            
            if (instance_id < GLMeshVao.MAX_INSTANCES)
            {
                //Uplod worldMat to the meshVao

                Matrix4 actualWorldMat = m.worldMat;
                Matrix4 actualWorldMatInv = (actualWorldMat).Inverted();
                setInstanceWorldMat(mesh, instance_id, actualWorldMat);
                setInstanceWorldMatInv(mesh, instance_id, actualWorldMatInv);
                setInstanceNormalMat(mesh, instance_id, Matrix4.Transpose(actualWorldMatInv));

                mesh.instanceRefs.Add(m); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        //Overload with transform overrides
        public static int addInstance(GLMeshVao mesh, model m, Matrix4 worldMat, Matrix4 worldMatInv, Matrix4 normMat)
        {
            int instance_id = mesh.instance_count;

            //Expand mesh data buffer if required
            if (instance_id * instance_struct_size_bytes > mesh.dataBuffer.Length)
            {
                float[] newBuffer = new float[mesh.dataBuffer.Length + 256];
                Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
                mesh.dataBuffer = newBuffer;
            }
            
            if (instance_id < GLMeshVao.MAX_INSTANCES)
            {
                setInstanceWorldMat(mesh, instance_id, worldMat);
                setInstanceWorldMatInv(mesh, instance_id, worldMatInv);
                setInstanceNormalMat(mesh, instance_id, normMat);
                
                mesh.instanceRefs.Add(m); //Keep reference
                mesh.instance_count++;
            }

            return instance_id;
        }

        public static void clearInstances(GLMeshVao mesh)
        {
            mesh.instanceRefs.Clear();
            mesh.instance_count = 0;
        }

        public static void removeInstance(GLMeshVao mesh, model m)
        {
            int id = mesh.instanceRefs.IndexOf(m);

            //TODO: Make all the memory shit to push the instances backwards
        }


        public static void setInstanceOccludedStatus(GLMeshVao mesh, int instance_id, bool status)
        {
            mesh.visible_instances += (status ? -1 : 1);
            unsafe
            {
                mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isOccluded_Float_Offset] = status ? 1.0f : 0.0f;
            }
        }

        public static bool getInstanceOccludedStatus(GLMeshVao mesh, int instance_id)
        {
            unsafe
            {
                return mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isOccluded_Float_Offset] > 0.0f;
            }
        }

        public static void setInstanceSelectedStatus(GLMeshVao mesh, int instance_id, bool status)
        {
            unsafe
            {
                mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isSelected_Float_Offset] = status ? 1.0f : 0.0f;
            }
        }

        public static bool getInstanceSelectedStatus(GLMeshVao mesh, int instance_id)
        {
            unsafe
            {
                return mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_isSelected_Float_Offset] > 0.0f;
            }
        }

        public static Matrix4 getInstanceWorldMat(GLMeshVao mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset;
                    return MathUtils.Matrix4FromArray(ar, offset);
                }
            }

        }

        public static Matrix4 getInstanceNormalMat(GLMeshVao mesh, int instance_id)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    return MathUtils.Matrix4FromArray(ar, instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset);
                }
            }
        }

        public static Vector3 getInstanceColor(GLMeshVao mesh, int instance_id)
        {
            float col;
            unsafe
            {
                col = mesh.dataBuffer[instance_id * instance_struct_size_floats + instance_color_Float_Offset];
            }

            return new Vector3(col, col, col);
        }

        public static void setInstanceUniform4(GLMeshVao mesh, int instance_id, string un_name, Vector4 un)
        {
            unsafe
            {
                int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_Uniforms_Float_Offset;
                int uniform_id = 0;
                switch (un_name)
                {
                    case "gUserDataVec4":
                        uniform_id = 0;
                        break;
                }

                offset += uniform_id * 4;

                mesh.dataBuffer[offset] = un.X;
                mesh.dataBuffer[offset + 1] = un.Y;
                mesh.dataBuffer[offset + 2] = un.Z;
                mesh.dataBuffer[offset + 3] = un.W;
            }
        }

        public static Vector4 getInstanceUniform(GLMeshVao mesh, int instance_id, string un_name)
        {
            Vector4 un;
            unsafe
            {
                int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_Uniforms_Float_Offset;
                int uniform_id = 0;
                switch (un_name)
                {
                    case "gUserDataVec4":
                        uniform_id = 0;
                        break;
                }

                offset += uniform_id * 4;

                un.X = mesh.dataBuffer[offset];
                un.Y = mesh.dataBuffer[offset + 1];
                un.Z = mesh.dataBuffer[offset + 2];
                un.W = mesh.dataBuffer[offset + 3];
            }

            return un;
        }

        public static void setInstanceWorldMat(GLMeshVao mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public static void setInstanceWorldMatInv(GLMeshVao mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_worldMatInv_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }

        public static void setInstanceNormalMat(GLMeshVao mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceData_Float_Offset + instance_id * instance_struct_size_floats + instance_normalMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
        }


    }

    public class GLMeshVao : IDisposable
    {
        //Class static properties
        public const int MAX_INSTANCES = 512;
        
        public GLVao vao;
        public GLVao bHullVao;
        public MeshMetaData metaData;
        public float[] dataBuffer = new float[256];

        //Mesh type
        public COLLISIONTYPES collisionType;
        public TYPES type;

        //Instance Data
        public int UBO_aligned_size = 0; //Actual size of the data for the UBO, multiple to 256
        public int UBO_offset = 0; //Offset 

        public int instance_count = 0;
        public int visible_instances = 0;
        public List<model> instanceRefs = new List<model>();
        public float[] instanceBoneMatrices;
        private int instanceBoneMatricesTex;
        private int instanceBoneMatricesTexTBO;

        //Animation Properties
        //TODO : At some point include that shit into the instance data
        public int BoneRemapIndicesCount;
        public int[] BoneRemapIndices;
        //public float[] BoneRemapMatrices = new float[16 * 128];
        public bool skinned = false;


        public DrawElementsType indicesLength = DrawElementsType.UnsignedShort;

        //Material Properties
        public Material material;
        public Vector3 color;

        

        //Constructor
        public GLMeshVao()
        {
            vao = new GLVao();
        }

        public GLMeshVao(MeshMetaData data) 
        {
            vao = new GLVao();
            metaData = new MeshMetaData(data);
        }


        //Geometry Setup
        //BSphere calculator
        public GLVao setupBSphere(int instance_id)
        {
            float radius = 0.5f * (metaData.AABBMIN - metaData.AABBMAX).Length;
            Vector4 bsh_center = new Vector4(metaData.AABBMIN + 0.5f * (metaData.AABBMAX - metaData.AABBMIN), 1.0f);

            Matrix4 t_mat = GLMeshBufferManager.getInstanceWorldMat(this, instance_id);
            bsh_center = bsh_center * t_mat;

            //Create Sphere vbo
            return new Primitives.Sphere(bsh_center.Xyz, radius).getVAO();
        }


        //Rendering Methods

        public void renderBBoxes(int pass)
        {
            for (int i = 0; i > instance_count; i++)
                renderBbox(pass, i);
        }


        public void renderBbox(int pass, int instance_id)
        {
            if (GLMeshBufferManager.getInstanceOccludedStatus(this, instance_id))
                return;

            Matrix4 worldMat = GLMeshBufferManager.getInstanceWorldMat(this, instance_id);
            //worldMat = worldMat.ClearRotation();
            
            Vector4[] tr_AABB = new Vector4[2];
            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 1.0f) * worldMat;
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 1.0f) * worldMat;

            tr_AABB[0] = new Vector4(instanceRefs[instance_id].AABBMIN, 1.0f);
            tr_AABB[1] = new Vector4(instanceRefs[instance_id].AABBMAX, 1.0f);

            //tr_AABB[0] = new Vector4(metaData.AABBMIN, 0.0f);
            //tr_AABB[1] = new Vector4(metaData.AABBMAX, 0.0f);

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };

            //Indices
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
                                            1,0,5};
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * verts1.Length;
            int vb_bbox, eb_bbox;
            GL.GenBuffers(1, out vb_bbox);
            GL.GenBuffers(1, out eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts1);
            
            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render

            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

        }


        public void renderBSphere(GLSLHelper.GLSLShaderConfig shader)
        {
            for (int i = 0; i < instance_count; i++)
            {
                GLVao bsh_Vao = setupBSphere(i);

                //Rendering

                GL.UseProgram(shader.program_id);

                //Step 2 Bind & Render Vao
                //Render Bounding Sphere
                GL.BindVertexArray(bsh_Vao.vao_id);
                GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
                GL.DrawElements(PrimitiveType.Triangles, 600, DrawElementsType.UnsignedInt, (IntPtr)0);

                GL.BindVertexArray(0);
                bsh_Vao.Dispose();
            }


        }

        private void renderMesh()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Triangles, metaData.batchcount, indicesLength,
                IntPtr.Zero, instance_count);
            GL.BindVertexArray(0);
        }

        private void renderLight()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArraysInstanced(PrimitiveType.Lines, 0, 2, instance_count);
            GL.DrawArraysInstanced(PrimitiveType.Points, 0, 2, instance_count); //Draw both points
            GL.BindVertexArray(0);
        }

        private void renderCollision()
        {
            //Step 2: Render Elements
            GL.PointSize(8.0f);
            GL.BindVertexArray(vao.vao_id);
            
            switch (collisionType)
            {
                //Rendering based on the original mesh buffers
                case COLLISIONTYPES.MESH:
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Points, metaData.batchcount,
                        indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
                    GL.DrawElementsInstancedBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        indicesLength, IntPtr.Zero, instance_count, -metaData.vertrstart_physics);
                    break;

                //Rendering custom geometry
                case COLLISIONTYPES.BOX:
                case COLLISIONTYPES.CYLINDER:
                case COLLISIONTYPES.CAPSULE:
                case COLLISIONTYPES.SPHERE:
                    GL.DrawElementsInstanced(PrimitiveType.Points, metaData.batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero, instance_count);
                    GL.DrawElementsInstanced(PrimitiveType.Triangles, metaData.batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero, instance_count);
                    break;
            }

            GL.BindVertexArray(0);
        }

        private void renderLocator()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElementsInstanced(PrimitiveType.Lines, 6, indicesLength, IntPtr.Zero, instance_count); //Use Instancing
            GL.BindVertexArray(0);
        }

        private void renderJoint()
        {
            GL.BindVertexArray(vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, metaData.batchcount);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);
        }

        public virtual void renderMain(GLSLShaderConfig shader)
        {
            //Upload Material Information

            //Upload Custom Per Material Uniforms
            foreach (Uniform un in material.CustomPerMaterialUniforms.Values)
            {
                if (shader.uniformLocations.Keys.Contains(un.Name))
                    GL.Uniform4(shader.uniformLocations[un.Name], un.vec.vec4);
            }

            //BIND TEXTURES
            //Diffuse Texture
            foreach (Sampler s in material.PSamplers.Values)
            {
                if (shader.uniformLocations.ContainsKey(s.Name) && s.Map != "")
                {
                    GL.Uniform1(shader.uniformLocations[s.Name], MyTextureUnit.MapTexUnitToSampler[s.Name]);
                    GL.ActiveTexture(s.texUnit.texUnit);
                    GL.BindTexture(s.tex.target, s.tex.texID);
                }
            }

            //BIND TEXTURE Buffer
            if (skinned)
            {
                GL.Uniform1(shader.uniformLocations["mpCustomPerMaterial.skinMatsTex"], 6);
                GL.ActiveTexture(TextureUnit.Texture6);
                GL.BindTexture(TextureTarget.TextureBuffer, instanceBoneMatricesTex);
                GL.TexBuffer(TextureBufferTarget.TextureBuffer,
                    SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            }

            //if (instance_count > 100)
            //    Console.WriteLine("Increase the buffers");

            switch (type)
            {
                case TYPES.GIZMO:
                case TYPES.GIZMOPART:
                case TYPES.MESH:
                    renderMesh();
                    break;
                case TYPES.LOCATOR:
                case TYPES.MODEL:
                    renderLocator();
                    break;
                case TYPES.JOINT:
                    renderJoint();
                    break;
                case TYPES.COLLISION:
                    renderCollision();
                    break;
                case TYPES.LIGHT:
                    renderLight();
                    break;
            }
        }

        private void renderBHull(GLSLHelper.GLSLShaderConfig shader)
        {
            if (bHullVao == null) return;
            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(8.0f);
            GL.BindVertexArray(bHullVao.vao_id);
            
            GL.DrawElementsBaseVertex(PrimitiveType.Points, metaData.batchcount,
                        indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, metaData.batchcount,
                        indicesLength, IntPtr.Zero, -metaData.vertrstart_physics);
            GL.BindVertexArray(0);
        }

        public virtual void renderDebug(int pass)
        {
            GL.UseProgram(pass);
            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            for (int i = 0; i < material.Flags.Count; i++)
                GL.Uniform1(loc + (int) material.Flags[i].MaterialFlag, 1.0f);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            /*
            Util.mulMatArrays(ref skinMats, gobject.invBMats, scene.JMArray, 256);
            loc = GL.GetUniformLocation(pass, "skinMats");
            GL.UniformMatrix4(loc, 256, false, skinMats);
            */

            //Step 2: Render VAO
            GL.BindVertexArray(vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles, metaData.batchcount, DrawElementsType.UnsignedShort, (IntPtr)0);
            GL.BindVertexArray(0);
        }



        //Default render method
        public bool render(GLSLShaderConfig shader, RENDERPASS pass)
        {
            //Render Object
            switch (pass)
            {
                //Render Main
                case RENDERPASS.DEFERRED:
                case RENDERPASS.FORWARD:
                case RENDERPASS.DECAL:
                    renderMain(shader);
                    break;
                case RENDERPASS.BBOX:
                case RENDERPASS.BHULL:
                    //renderBbox(shader.program_id, 0);
                    //renderBSphere(shader);
                    renderBHull(shader);
                    break;
                //Render Debug
                case RENDERPASS.DEBUG:
                    //renderDebug(shader.program_id);
                    break;
                //Render for Picking
                case RENDERPASS.PICK:
                    //renderDebug(shader.program_id);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }

            return true;
        }



        public void setSkinMatrices(scene animScene, int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;


            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                Array.Copy(animScene.skinMats, BoneRemapIndices[i] * 16, instanceBoneMatrices, instance_offset + i * 16, 16);
            }
        }

        public void setDefaultSkinMatrices(int instance_id)
        {
            int instance_offset = 128 * 16 * instance_id;
            for (int i = 0; i < BoneRemapIndicesCount; i++)
            {
                MathUtils.insertMatToArray16(instanceBoneMatrices, instance_offset + i * 16, Matrix4.Identity);
            }
                
        }

        public void initializeSkinMatrices(scene animScene)
        {
            if (instance_count == 0)
                return;
            int jointCount = animScene.jointDict.Values.Count;

            //TODO: Use the jointCount to adaptively setup the instanceBoneMatrices
            //Console.WriteLine("MAX : 128  vs Effective : " + jointCount.ToString());

            //Re-initialize the array based on the number of instances
            instanceBoneMatrices = new float[instance_count * 128 * 16];
            int bufferSize = instance_count * 128 * 16 * 4;

            //Setup the TBO
            instanceBoneMatricesTex = GL.GenTexture();
            instanceBoneMatricesTexTBO = GL.GenBuffer();

            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            GL.BufferData(BufferTarget.TextureBuffer, bufferSize, instanceBoneMatrices, BufferUsageHint.StreamDraw);
            GL.TexBuffer(TextureBufferTarget.TextureBuffer, SizedInternalFormat.Rgba32f, instanceBoneMatricesTexTBO);
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);

        }

        public void uploadSkinningData()
        {
            GL.BindBuffer(BufferTarget.TextureBuffer, instanceBoneMatricesTexTBO);
            int bufferSize = instance_count * 128 * 16 * 4;
            GL.BufferSubData(BufferTarget.TextureBuffer, IntPtr.Zero, bufferSize, instanceBoneMatrices);
            //Console.WriteLine(GL.GetError());
            GL.BindBuffer(BufferTarget.TextureBuffer, 0);
        }


#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls







        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    BoneRemapIndices = null;
                    instanceBoneMatrices = null;
                    
                    vao?.Dispose();

                    if (instanceBoneMatricesTex > 0)
                    {
                        GL.DeleteTexture(instanceBoneMatricesTex);
                        GL.DeleteBuffer(instanceBoneMatricesTexTBO);
                    }
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~mainGLVao()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
#endregion

    }


    public class meshModel : model
    {
        public int LodLevel
        {
            get
            {
                return metaData.lodLevel;
            }
            
        }

        public ulong Hash
        {
            get
            {
                return metaData.Hash;
            }
        }
        
        public MeshMetaData metaData = new MeshMetaData();
        public Vector3 color = new Vector3(); //Per instance
        public bool hasLOD = false;
        public bool Skinned { 
            get
            {
                if (meshVao.material != null)
                {
                    return meshVao.material.has_flag(TkMaterialFlags.UberFlagEnum._F02_SKINNED);
                }
                return false;
            }
        }
        
        public GLVao bHull_Vao;
        public GeomObject gobject; //Ref to the geometry shit
        
        public Material material
        {
            get
            {
                return meshVao.material;
            }
        }

        private static List<string> supportedCommonPerMeshUniforms = new List<string>() { "gUserDataVec4" };

        private Dictionary<string, Uniform> _CommonPerMeshUniforms = new Dictionary<string, Uniform>();

        public Dictionary<string, Uniform> CommonPerMeshUniforms
        {
            get
            {
                return _CommonPerMeshUniforms;
            }
        }

        //Constructor
        public meshModel() : base()
        {
            type = TYPES.MESH;
            metaData = new MeshMetaData();

            //Init MeshModel Uniforms
            foreach (string un in supportedCommonPerMeshUniforms)
            {
                Uniform my_un = new Uniform(un);
                _CommonPerMeshUniforms[my_un.Name] = my_un;
            }
        }

        public meshModel(meshModel input) : base(input)
        {
            //Copy attributes
            this.metaData = new MeshMetaData(input.metaData);
            
            //Copy Vao Refs
            this.meshVao = input.meshVao;
            
            //Material Stuff
            this.color = input.color;
            
            this.palette = input.palette;
            this.gobject = input.gobject; //Leave geometry file intact, no need to copy anything here
        }

        public void copyFrom(meshModel input)
        {
            //Copy attributes
            metaData = new MeshMetaData(input.metaData);
            hasLOD = input.hasLOD;

            //Copy Vao Refs
            meshVao = input.meshVao;

            //Material Stuff
            color = input.color;

            palette = input.palette;
            gobject = input.gobject;

            base.copyFrom(input);
        }

        public override model Clone()
        {
            meshModel new_m = new meshModel();
            new_m.copyFrom(this);

            new_m.meshVao = this.meshVao;
            new_m.instanceId = GLMeshBufferManager.addInstance(ref new_m.meshVao, new_m);
            
            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = new_m;
                new_m.children.Add(new_child);
            }
            
            return new_m;
        }

        public override void update()
        {
            base.update();
            recalculateAABB(); //Update AABB
        }

        public override void setupSkinMatrixArrays()
        {
            meshVao?.initializeSkinMatrices(parentScene);

            base.setupSkinMatrixArrays();
        
        }

        public override void updateMeshInfo()
        {
            if (instanceId < 0)
                Console.WriteLine("test");
            if (meshVao.BoneRemapIndicesCount > 128)
                Console.WriteLine("test");

            if (!renderable)
            {
                base.updateMeshInfo();
                Common.RenderStats.occludedNum += 1;
                return;
            }

            bool fr_status = Common.RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
            bool occluded_status = !fr_status && Common.RenderState.renderSettings.UseFrustumCulling;
            
            //Recalculations && Data uploads
            if (!occluded_status)
            {
                /*
                //Apply LOD filtering
                if (hasLOD && Common.RenderOptions.LODFiltering)
                //if (false)
                {
                    //Console.WriteLine("Active LoD {0}", parentScene.activeLOD);
                    if (parentScene.activeLOD != LodLevel)
                    {
                        meshVao.setInstanceOccludedStatus(instanceId, true);
                        base.updateMeshInfo();
                        return;
                    }
                }
                */

                instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);

                //Upload commonperMeshUniforms
                GLMeshBufferManager.setInstanceUniform4(meshVao, instanceId, 
                    "gUserDataVec4", CommonPerMeshUniforms["gUserDataVec4"].Vec.Vec);
                
                if (Skinned)
                {
                    //Update the mesh remap matrices and continue with the transform updates
                    meshVao.setSkinMatrices(parentScene, instanceId);
                    //Fallback
                    //main_Vao.setDefaultSkinMatrices();
                }

            } else
            {
                Common.RenderStats.occludedNum += 1;
            }

            //meshVao.setInstanceOccludedStatus(instanceId, occluded_status);
            base.updateMeshInfo();
        }

        public override Assimp.Node assimpExport(ref Assimp.Scene scn, ref Dictionary<int, int> meshImportStatus)
        {
            Assimp.Mesh amesh = new Assimp.Mesh();
            Assimp.Node node;
            amesh.Name = name;

            int meshHash = meshVao.GetHashCode();

            //TESTING
            if (scn.MeshCount > 20)
            {
                node = base.assimpExport(ref scn, ref meshImportStatus);
                return node;
             }

            if (!meshImportStatus.ContainsKey(meshHash))
            //if (false)
            {
                meshImportStatus[meshHash] = scn.MeshCount;

                int vertcount = metaData.vertrend_graphics - metaData.vertrstart_graphics + 1;
                MemoryStream vms = new MemoryStream(gobject.meshDataDict[metaData.Hash].vs_buffer);
                MemoryStream ims = new MemoryStream(gobject.meshDataDict[metaData.Hash].is_buffer);
                BinaryReader vbr = new BinaryReader(vms);
                BinaryReader ibr = new BinaryReader(ims);


                //Initialize Texture Component Channels
                if (gobject.bufInfo[1] != null)
                {
                    List<Assimp.Vector3D> textureChannel = new List<Assimp.Vector3D>();
                    amesh.TextureCoordinateChannels.Append(textureChannel);
                    amesh.UVComponentCount[0] = 2;
                }

                //Generate bones only for the joints related to the mesh
                Dictionary<int, Assimp.Bone> localJointDict = new Dictionary<int, Assimp.Bone>();

                //Export Bone Structure
                if (Skinned)
                //if (false)
                {
                    for (int i = 0; i < meshVao.BoneRemapIndicesCount; i++)
                    {
                        int joint_id = meshVao.BoneRemapIndices[i];
                        //Fetch name
                        Joint relJoint = null;

                        foreach (Joint jnt in parentScene.jointDict.Values)
                        {
                            if (jnt.jointIndex == joint_id)
                            {
                                relJoint = jnt;
                                break;
                            }

                        }

                        //Generate bone
                        Assimp.Bone b = new Assimp.Bone();
                        if (relJoint != null)
                        {
                            b.Name = relJoint.name;
                            b.OffsetMatrix = MathUtils.convertMatrix(relJoint.invBMat);
                        }
                        

                        localJointDict[i] = b;
                        amesh.Bones.Add(b);
                    }
                }
                

                
                //Write geometry info

                vbr.BaseStream.Seek(0, SeekOrigin.Begin);
                for (int i = 0; i < vertcount; i++)
                {
                    Assimp.Vector3D v, vN;

                    for (int j = 0; j < gobject.bufInfo.Count; j++)
                    {
                        bufInfo buf = gobject.bufInfo[j];
                        if (buf is null)
                            continue;

                        switch (buf.semantic)
                        {
                            case 0: //vPosition
                                {
                                    switch (buf.type)
                                    {
                                        case VertexAttribPointerType.HalfFloat:
                                            uint v1 = vbr.ReadUInt16();
                                            uint v2 = vbr.ReadUInt16();
                                            uint v3 = vbr.ReadUInt16();
                                            uint v4 = vbr.ReadUInt16();

                                            //Transform vector with worldMatrix
                                            v = new Assimp.Vector3D(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3));
                                            break;
                                        case VertexAttribPointerType.Float: //This is used in my custom vbos
                                            float f1 = vbr.ReadSingle();
                                            float f2 = vbr.ReadSingle();
                                            float f3 = vbr.ReadSingle();
                                            //Transform vector with worldMatrix
                                            v = new Assimp.Vector3D(f1, f2, f3);
                                            break;
                                        default:
                                            throw new Exception("Unimplemented Vertex Type");
                                    }
                                    amesh.Vertices.Add(v);
                                    break;
                                }

                            case 1: //uvPosition
                                {
                                    Assimp.Vector3D uv;
                                    uint v1 = vbr.ReadUInt16();
                                    uint v2 = vbr.ReadUInt16();
                                    uint v3 = vbr.ReadUInt16();
                                    uint v4 = vbr.ReadUInt16();
                                    //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                                    uv = new Assimp.Vector3D(Half.decompress(v1), Half.decompress(v2), 0.0f);

                                    amesh.TextureCoordinateChannels[0].Add(uv); //Add directly to the first channel
                                    break;
                                }
                            case 2: //nPosition
                            case 3: //tPosition
                                {
                                    switch (buf.type)
                                    {
                                        case (VertexAttribPointerType.Float):
                                            float f1, f2, f3;
                                            f1 = vbr.ReadSingle();
                                            f2 = vbr.ReadSingle();
                                            f3 = vbr.ReadSingle();
                                            vN = new Assimp.Vector3D(f1, f2, f3);
                                            break;
                                        case (VertexAttribPointerType.HalfFloat):
                                            uint v1, v2, v3;
                                            v1 = vbr.ReadUInt16();
                                            v2 = vbr.ReadUInt16();
                                            v3 = vbr.ReadUInt16();
                                            vN = new Assimp.Vector3D(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3));
                                            break;
                                        case (VertexAttribPointerType.Int2101010Rev):
                                            int i1, i2, i3;
                                            uint value;
                                            byte[] a32 = new byte[4];
                                            a32 = vbr.ReadBytes(4);

                                            value = BitConverter.ToUInt32(a32, 0);
                                            //Convert Values
                                            i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
                                            i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
                                            i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
                                            //int i4 = _2sComplement.toInt((value >> 30) & 0x003, 10);
                                            float norm = (float)Math.Sqrt(i1 * i1 + i2 * i2 + i3 * i3);

                                            vN = new Assimp.Vector3D(Convert.ToSingle(i1) / norm,
                                                             Convert.ToSingle(i2) / norm,
                                                             Convert.ToSingle(i3) / norm);

                                            //Debug.WriteLine(vN);
                                            break;
                                        default:
                                            throw new Exception("UNIMPLEMENTED NORMAL TYPE. PLEASE REPORT");
                                    }

                                    if (j == 2)
                                        amesh.Normals.Add(vN);
                                    else if (j == 3)
                                    {
                                        amesh.Tangents.Add(vN);
                                        amesh.BiTangents.Add(new Assimp.Vector3D(0.0f, 0.0f, 1.0f));
                                    }
                                    break;
                                }
                            case 4: //bPosition
                                vbr.ReadBytes(4); // skip
                                break;
                            case 5: //BlendIndices + BlendWeights
                                {
                                    int[] joint_ids = new int[4];
                                    float[] weights = new float[4];

                                    for (int k = 0; k < 4; k++)
                                    {
                                        joint_ids[k] = vbr.ReadByte();
                                    }
                                        

                                    for (int k = 0; k < 4; k++)
                                        weights[k] = Half.decompress(vbr.ReadUInt16());

                                    if (Skinned)
                                    //if (false)
                                    {
                                        for (int k = 0; k < 4; k++)
                                        {
                                            int joint_id = joint_ids[k];

                                            Assimp.VertexWeight vw = new Assimp.VertexWeight();
                                            vw.VertexID = i;
                                            vw.Weight = weights[k];
                                            localJointDict[joint_id].VertexWeights.Add(vw);

                                        }

                                        
                                    }
                                   

                                    break;
                                }
                            case 6:
                                break; //Handled by 5
                            default:
                                {
                                    throw new Exception("UNIMPLEMENTED BUF Info. PLEASE REPORT");
                                    break;
                                }

                        }
                    }

                }

                //Export Faces
                //Get indices
                ibr.BaseStream.Seek(0, SeekOrigin.Begin);
                bool start = false;
                int fstart = 0;
                for (int i = 0; i < metaData.batchcount / 3; i++)
                {
                    int f1, f2, f3;
                    //NEXT models assume that all gstream meshes have uint16 indices
                    f1 = ibr.ReadUInt16();
                    f2 = ibr.ReadUInt16();
                    f3 = ibr.ReadUInt16();

                    if (!start && this.type != TYPES.COLLISION)
                    { fstart = f1; start = true; }
                    else if (!start && this.type == TYPES.COLLISION)
                    {
                        fstart = 0; start = true;
                    }

                    int f11, f22, f33;
                    f11 = f1 - fstart;
                    f22 = f2 - fstart;
                    f33 = f3 - fstart;


                    Assimp.Face face = new Assimp.Face();
                    face.Indices.Add(f11);
                    face.Indices.Add(f22);
                    face.Indices.Add(f33);


                    amesh.Faces.Add(face);
                }

                scn.Meshes.Add(amesh);
               
            }

            node = base.assimpExport(ref scn, ref meshImportStatus);
            node.MeshIndices.Add(meshImportStatus[meshHash]);

            return node;
        }

        public void writeGeomToStream(StreamWriter s, ref uint index)
        {
            int vertcount = metaData.vertrend_graphics - metaData.vertrstart_graphics + 1;
            MemoryStream vms = new MemoryStream(gobject.meshDataDict[metaData.Hash].vs_buffer);
            MemoryStream ims = new MemoryStream(gobject.meshDataDict[metaData.Hash].is_buffer);
            BinaryReader vbr = new BinaryReader(vms);
            BinaryReader ibr = new BinaryReader(ims);
            //Start Writing
            //Object name
            s.WriteLine("o " + name);
            //Get Verts

            //Preset Matrices for faster export
            Matrix4 wMat = this.worldMat;
            Matrix4 nMat = Matrix4.Invert(Matrix4.Transpose(wMat));

            vbr.BaseStream.Seek(0, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector4 v;
                VertexAttribPointerType ntype = gobject.bufInfo[0].type;
                int v_section_bytes = 0;

                switch (ntype)
                {
                    case VertexAttribPointerType.HalfFloat:
                        uint v1 = vbr.ReadUInt16();
                        uint v2 = vbr.ReadUInt16();
                        uint v3 = vbr.ReadUInt16();
                        //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());

                        //Transform vector with worldMatrix
                        v = new Vector4(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3), 1.0f);
                        v_section_bytes = 6;
                        break;
                    case VertexAttribPointerType.Float: //This is used in my custom vbos
                        float f1 = vbr.ReadSingle();
                        float f2 = vbr.ReadSingle();
                        float f3 = vbr.ReadSingle();
                        //Transform vector with worldMatrix
                        v = new Vector4(f1, f2, f3, 1.0f);
                        v_section_bytes = 12;
                        break;
                    default:
                        throw new Exception("Unimplemented Vertex Type");
                }


                v = Vector4.Transform(v, this.worldMat);

                //s.WriteLine("v " + Half.decompress(v1).ToString() + " "+ Half.decompress(v2).ToString() + " " + Half.decompress(v3).ToString());
                s.WriteLine("v " + v.X.ToString() + " " + v.Y.ToString() + " " + v.Z.ToString());
                vbr.BaseStream.Seek(gobject.vx_size - v_section_bytes, SeekOrigin.Current);
            }
            //Get Normals

            vbr.BaseStream.Seek(gobject.offsets[2] + 0, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector4 vN;
                VertexAttribPointerType ntype = gobject.bufInfo[2].type;
                int n_section_bytes = 0;

                switch (ntype)
                {
                    case (VertexAttribPointerType.Float):
                        float f1, f2, f3;
                        f1 = vbr.ReadSingle();
                        f2 = vbr.ReadSingle();
                        f3 = vbr.ReadSingle();
                        vN = new Vector4(f1, f2, f3, 1.0f);
                        n_section_bytes = 12;
                        break;
                    case (VertexAttribPointerType.HalfFloat):
                        uint v1, v2, v3;
                        v1 = vbr.ReadUInt16();
                        v2 = vbr.ReadUInt16();
                        v3 = vbr.ReadUInt16();
                        vN = new Vector4(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3), 1.0f);
                        n_section_bytes = 6;
                        break;
                    case (VertexAttribPointerType.Int2101010Rev):
                        int i1, i2, i3;
                        uint value;
                        byte[] a32 = new byte[4];
                        a32 = vbr.ReadBytes(4);

                        value = BitConverter.ToUInt32(a32, 0);
                        //Convert Values
                        i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
                        i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
                        i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
                        //int i4 = _2sComplement.toInt((value >> 30) & 0x003, 10);
                        float norm = (float)Math.Sqrt(i1 * i1 + i2 * i2 + i3 * i3);

                        vN = new Vector4(Convert.ToSingle(i1) / norm,
                                         Convert.ToSingle(i2) / norm,
                                         Convert.ToSingle(i3) / norm,
                                         1.0f);

                        n_section_bytes = 4;
                        //Debug.WriteLine(vN);
                        break;
                    default:
                        throw new Exception("UNIMPLEMENTED NORMAL TYPE. PLEASE REPORT");
                }

                //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                //Transform normal with normalMatrix


                vN = Vector4.Transform(vN, nMat);

                s.WriteLine("vn " + vN.X.ToString() + " " + vN.Y.ToString() + " " + vN.Z.ToString());
                vbr.BaseStream.Seek(gobject.vx_size - n_section_bytes, SeekOrigin.Current);
            }
            //Get UVs, only for mesh objects

            vbr.BaseStream.Seek(Math.Max(gobject.offsets[1], 0) + gobject.vx_size * metaData.vertrstart_graphics, SeekOrigin.Begin);
            for (int i = 0; i < vertcount; i++)
            {
                Vector2 uv;
                int uv_section_bytes = 0;
                if (gobject.offsets[1] != -1) //Check if uvs exist
                {
                    uint v1 = vbr.ReadUInt16();
                    uint v2 = vbr.ReadUInt16();
                    uint v3 = vbr.ReadUInt16();
                    //uint v4 = Convert.ToUInt16(vbr.ReadUInt16());
                    uv = new Vector2(Half.decompress(v1), Half.decompress(v2));
                    uv_section_bytes = 0x6;
                }
                else
                {
                    uv = new Vector2(0.0f, 0.0f);
                    uv_section_bytes = gobject.vx_size;
                }

                s.WriteLine("vt " + uv.X.ToString() + " " + (1.0 - uv.Y).ToString());
                vbr.BaseStream.Seek(gobject.vx_size - uv_section_bytes, SeekOrigin.Current);
            }


            //Some Options
            s.WriteLine("usemtl(null)");
            s.WriteLine("s off");

            //Get indices
            ibr.BaseStream.Seek(0, SeekOrigin.Begin);
            bool start = false;
            uint fstart = 0;
            for (int i = 0; i < metaData.batchcount / 3; i++)
            {
                uint f1, f2, f3;
                //NEXT models assume that all gstream meshes have uint16 indices
                f1 = ibr.ReadUInt16();
                f2 = ibr.ReadUInt16();
                f3 = ibr.ReadUInt16();

                if (!start && this.type != TYPES.COLLISION)
                { fstart = f1; start = true; }
                else if (!start && this.type == TYPES.COLLISION)
                {
                    fstart = 0; start = true;
                }

                uint f11, f22, f33;
                f11 = f1 - fstart + index;
                f22 = f2 - fstart + index;
                f33 = f3 - fstart + index;


                s.WriteLine("f " + f11.ToString() + "/" + f11.ToString() + "/" + f11.ToString() + " "
                                + f22.ToString() + "/" + f22.ToString() + "/" + f22.ToString() + " "
                                + f33.ToString() + "/" + f33.ToString() + "/" + f33.ToString() + " ");


            }
            index += (uint)vertcount;
        }



#region IDisposable Support

        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {

                    // TODO: dispose managed state (managed objects).
                    //if (material != null) material.Dispose();
                    //NOTE: No need to dispose material, because the materials reside in the resource manager
                    base.Dispose(disposing);
                }
            }
        }

#endregion

    }

    [StructLayout(LayoutKind.Explicit)]
    struct CustomPerMaterialUniforms
    {
        [FieldOffset(0)] //256 Bytes
        public unsafe fixed int matflags[64];
        [FieldOffset(256)] //64 Bytes
        public int diffuseTex;
        [FieldOffset(260)] //4 bytes
        public int maskTex;
        [FieldOffset(264)] //4 bytes
        public int normalTex;
        [FieldOffset(276)] //16 bytes
        public Vector4 gMaterialColourVec4;
        [FieldOffset(292)] //16 bytes
        public Vector4 gMaterialParamsVec4;
        [FieldOffset(308)] //16 bytes
        public Vector4 gMaterialSFXVec4;
        [FieldOffset(324)] //16 bytes
        public Vector4 gMaterialSFXColVec4;
        [FieldOffset(340)] //16 bytes
        public Vector4 gDissolveDataVec4;
        [FieldOffset(356)] //16 bytes
        public Vector4 gCustomParams01Vec4;
        
        public static readonly int SizeInBytes = 360;
    };

    public class Collision : model
    {
        public COLLISIONTYPES collisionType;
        public GeomObject gobject;
        public MeshMetaData metaData = new MeshMetaData();
        
        //Custom constructor
        public Collision()
        {
            
        }

        public override model Clone()
        {
            Collision new_m = new Collision();
            new_m.collisionType = collisionType;
            new_m.copyFrom(this);

            new_m.meshVao = this.meshVao;
            new_m.instanceId = GLMeshBufferManager.addInstance(ref new_m.meshVao, new_m);
            
            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = new_m;
                new_m.children.Add(new_child);
            }

            return new_m;
        }
        
        
        protected Collision(Collision input) : base(input)
        {
            collisionType = input.collisionType;
        }

        public override void update()
        {
            base.update();

        }

        public override void updateMeshInfo()
        {
            if (renderable)
            {
                instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
                base.updateMeshInfo();
                return;
            }

            base.updateMeshInfo();
        }

    }


    public class GeomObject : IDisposable
    {
        public string mesh_descr;
        public string small_mesh_descr;

        public bool interleaved;
        public int vx_size;
        public int small_vx_size;

        //Counters
        public int indicesCount=0;
        public int indicesLength = 0;
        public DrawElementsType indicesLengthType;
        public int vertCount = 0;

        //make sure there are enough buffers for non interleaved formats
        public byte[] ibuffer;
        public int[] ibuffer_int;
        public byte[] vbuffer;
        public byte[] small_vbuffer;
        public byte[] cbuffer;
        public byte[] nbuffer;
        public byte[] ubuffer;
        public byte[] tbuffer;
        public List<int[]> bIndices = new List<int[]>();
        public List<float[]> bWeights = new List<float[]>();
        public List<bufInfo> bufInfo = new List<GMDL.bufInfo>();
        public int[] offsets; //List to save strides according to meshdescr
        public int[] small_offsets; //Same thing for the small description
        public short[] boneRemap;
        public List<Vector3[]> bboxes = new List<Vector3[]>();
        public List<Vector3> bhullverts = new List<Vector3>();
        public List<int> bhullstarts = new List<int>();
        public List<int> bhullends = new List<int>();
        public List<int[]> bhullindices = new List<int[]>();
        public List<int> vstarts = new List<int>();
        public Dictionary<ulong, geomMeshMetaData> meshMetaDataDict = new Dictionary<ulong, geomMeshMetaData>();
        public Dictionary<ulong, geomMeshData> meshDataDict = new Dictionary<ulong, geomMeshData>();

        //Joint info
        public int jointCount;
        public List<JointBindingData> jointData = new List<JointBindingData>();
        public float[] invBMats = new float[256 * 16];

        //Dictionary with the compiled VAOs belonging on this gobject
        private Dictionary<ulong, GMDL.GLVao> GLVaos = new Dictionary<ulong, GLVao>();
        //Dictionary to index 
        private Dictionary<ulong, Dictionary<string, GLMeshVao>> GLMeshVaos = new Dictionary<ulong, Dictionary<string, GLMeshVao>>();



        public Vector3 get_vec3_half(BinaryReader br)
        {
            Vector3 temp;
            //Get Values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            uint val3 = br.ReadUInt16();
            //Convert Values
            temp.X = Half.decompress(val1);
            temp.Y = Half.decompress(val2);
            temp.Z = Half.decompress(val3);
            //Console.WriteLine("half {0} {1} {2}", temp[0],temp[1],temp[2]);
            return temp;
        }

        public Vector2 get_vec2_half(BinaryReader br)
        {
            Vector2 temp;
            //Get values
            uint val1 = br.ReadUInt16();
            uint val2 = br.ReadUInt16();
            //Convert Values
            temp.X = Half.decompress(val1);
            temp.Y = Half.decompress(val2);
            return temp;
        }





        //Fetch Meshvao from dictionary
        public GLMeshVao findGLMeshVao(string material_name, ulong hash)
        {
            if (GLMeshVaos.ContainsKey(hash))
                if (GLMeshVaos[hash].ContainsKey(material_name))
                    return GLMeshVaos[hash][material_name];
                
            return null;
        }

        //Fetch Meshvao from dictionary
        public GLVao findVao(ulong hash)
        {
            if (GLVaos.ContainsKey(hash))
                return GLVaos[hash];
            return null;
        }

        //Save GLMeshVAO to gobject
        public bool saveGLMeshVAO(ulong hash, string matname, GLMeshVao meshVao)
        {
            if (GLMeshVaos.ContainsKey(hash))
            {
                if (GLMeshVaos[hash].ContainsKey(matname))
                {
                    Console.WriteLine("MeshVao already in the dictionary, nothing to do...");
                    return false;
                }
            }
            else
                GLMeshVaos[hash] = new Dictionary<string, GLMeshVao>();
                
            GLMeshVaos[hash][matname] = meshVao;

            return true;

        }

        //Save VAO to gobject
        public bool saveVAO(ulong hash, GLVao vao)
        {
            //Double check tha the VAO is not already in the dictinary
            if (GLVaos.ContainsKey(hash))
            {
                Console.WriteLine("Vao already in the dictinary, nothing to do...");
                return false;
            }
                
            //Save to dictionary
            GLVaos[hash] = vao;
            return true;
        }

        //Fetch main VAO
        public GLVao generateVAO(meshModel so)
        {
            //Generate VAO
            GLVao vao = new GLVao();
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);
            
            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];
            
            //Bind vertex buffer
            int size;
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            //Upload Vertex Buffer
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) meshMetaDataDict[so.metaData.Hash].vs_size,
                meshDataDict[so.metaData.Hash].vs_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vx_size * (so.metaData.vertrend_graphics + 1))
            {
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
                Util.showError("Mesh metadata does not match the vertex buffer size from the geometry file", "Error");
            }
                
            Common.RenderStats.vertNum += so.metaData.vertrend_graphics + 1; //Accumulate settings

            //Assign VertexAttribPointers
            for (int i = 0; i < 7; i++)
            {
                if (bufInfo[i] == null) continue;
                bufInfo buf = bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, buf.normalize, vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) meshMetaDataDict[so.metaData.Hash].is_size, 
                meshDataDict[so.metaData.Hash].is_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != meshMetaDataDict[so.metaData.Hash].is_size)
            {
                Util.showError("Mesh metadata does not match the index buffer size from the geometry file", "Error");
                //throw new ApplicationException(String.Format("Problem with vertex buffer"));
            }

            RenderStats.trisNum += (int) (so.metaData.batchcount / 3); //Accumulate settings

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
        }

        public GLVao getCollisionMeshVao(MeshMetaData metaData)
        {
            //Collision Mesh isn't used anywhere else.
            //No need to check for hashes and shit

            float[] vx_buffer_float = new float[(metaData.boundhullend - metaData.boundhullstart) * 3];

            for (int i = 0; i < metaData.boundhullend - metaData.boundhullstart; i++)
            {
                Vector3 v = bhullverts[i + metaData.boundhullstart];
                vx_buffer_float[3 * i + 0] = v.X;
                vx_buffer_float[3 * i + 1] = v.Y;
                vx_buffer_float[3 * i + 2] = v.Z;
            }

            //Generate intermediate geom
            GeomObject temp_geom = new GeomObject();

            //Set main Geometry Info
            temp_geom.vertCount = vx_buffer_float.Length / 3;
            temp_geom.indicesCount = metaData.batchcount;
            temp_geom.indicesLength = indicesLength; 

            //Set Strides
            temp_geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each

            //Set Buffer Offsets
            temp_geom.offsets = new int[7];
            temp_geom.bufInfo = new List<GMDL.bufInfo>();

            for (int i = 0; i < 7; i++)
            {
                temp_geom.bufInfo.Add(null);
                temp_geom.offsets[i] = -1;
            }

            temp_geom.mesh_descr = "vn";
            temp_geom.offsets[0] = 0;
            temp_geom.offsets[2] = 0;
            temp_geom.bufInfo[0] = new GMDL.bufInfo(0, VertexAttribPointerType.Float, 3, 0, "vPosition", false);
            temp_geom.bufInfo[2] = new GMDL.bufInfo(2, VertexAttribPointerType.Float, 3, 0, "nPosition", false);

            //Set Buffers
            temp_geom.ibuffer = new byte[temp_geom.indicesLength * metaData.batchcount];
            temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];

            System.Buffer.BlockCopy(ibuffer, metaData.batchstart_physics * temp_geom.indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
            System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);


            return temp_geom.generateVAO();
        }

        public GLVao generateVAO()
        {

            GLVao vao = new GLVao();

            //Generate VAO
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);
            
            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            ErrorCode err = GL.GetError();
            if (err != ErrorCode.NoError)
                Console.WriteLine(GL.GetError());
            
            //Bind vertex buffer
            int size;
            //Upload Vertex Buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vao.vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, vbuffer.Length,
                vbuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vbuffer.Length)
                throw new ApplicationException(String.Format("Problem with vertex buffer"));

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, ibuffer.Length,
                ibuffer, BufferUsageHint.StaticDraw);

            //Assign VertexAttribPointers
            for (int i = 0; i < 7; i++)
            {
                if (this.bufInfo[i] == null) continue;
                bufInfo buf = this.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
        }


#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    ibuffer = null;
                    vbuffer = null;
                    small_vbuffer = null;
                    offsets = null;
                    small_offsets = null;
                    boneRemap = null;
                    invBMats = null;
                    
                    bIndices.Clear();
                    bWeights.Clear();
                    bufInfo.Clear();
                    bboxes.Clear();
                    bhullverts.Clear();
                    vstarts.Clear();
                    jointData.Clear();

                    //Clear buffers
                    foreach (KeyValuePair<ulong, geomMeshMetaData> pair in meshMetaDataDict)
                        meshDataDict[pair.Key] = null;

                    meshDataDict.Clear();
                    meshMetaDataDict.Clear();

                    //Clear Vaos
                    foreach (GLVao p in GLVaos.Values)
                        p.Dispose();
                    GLVaos.Clear();

                    //Dispose GLmeshes
                    foreach (Dictionary<string, GLMeshVao> p in GLMeshVaos.Values)
                    {
                        foreach (GLMeshVao m in p.Values)
                            m.Dispose(); 
                        p.Clear();
                        //Materials are stored globally
                    }
                
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
#endregion

        
    }

    public class bufInfo
    {
        public int semantic;
        public VertexAttribPointerType type;
        public int count;
        public int stride;
        public string sem_text;
        public bool normalize;

        public bufInfo(int sem,VertexAttribPointerType typ, int c, int s, string t, bool n)
        {
            semantic = sem;
            type = typ;
            count = c;
            stride = s;
            sem_text = t;
            normalize = n;
        }
    }


    public class Sampler : TkMaterialSampler, IDisposable
    {
        public MyTextureUnit texUnit;
        public Texture tex;
        public textureManager texMgr; //For now it should be inherited from the scene. In the future I can use a delegate
        public bool isProcGen = false;

        //Override Properties
        public string PName
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }

        public string PMap
        {
            get
            {
                return Map;
            }
            set
            {
                Map = value;
            }
        }

        public Sampler()
        {

        }

        public Sampler(TkMaterialSampler ms)
        {
            //Pass everything here because there is no base copy constructor in the NMS template
            this.Name = "mpCustomPerMaterial." + ms.Name;
            this.Map = ms.Map;
            this.IsCube = ms.IsCube;
            this.IsSRGB = ms.IsSRGB;
            this.UseCompression = ms.UseCompression;
            this.UseMipMaps = ms.UseMipMaps;
        }

        public Sampler Clone()
        {
            Sampler newsampler = new Sampler();

            newsampler.PName = PName;
            newsampler.PMap = PMap;
            newsampler.texMgr = texMgr;
            newsampler.tex = tex;
            newsampler.texUnit = texUnit;
            newsampler.TextureAddressMode = TextureAddressMode;
            newsampler.TextureFilterMode = TextureFilterMode;

            return newsampler;
        }


        public void init(textureManager input_texMgr)
        {
            texMgr = input_texMgr;
            texUnit = new MyTextureUnit(Name);

            //Save texture to material
            switch (Name)
            {
                case "mpCustomPerMaterial.gDiffuseMap":
                case "mpCustomPerMaterial.gDiffuse2Map":
                case "mpCustomPerMaterial.gMasksMap":
                case "mpCustomPerMaterial.gNormalMap":
                    prepTextures();
                    break;
                default:
                    CallBacks.Log("Not sure how to handle Sampler " + Name);
                    break;
            }
        }


        public void prepTextures()
        {
            string[] split = Map.Split('.');

            string temp = "";
            if (Name == "mpCustomPerMaterial.gDiffuseMap")
            {
                //Check if the sampler describes a proc gen texture
                temp = split[0] + ".";
                //Construct main filename

                string texMbin = temp + "TEXTURE.MBIN";
                
                //Detect Procedural Texture
                if (Common.RenderState.activeResMgr.NMSFileToArchiveMap.Keys.Contains(texMbin))
                {
                    TextureMixer.combineTextures(Map, Palettes.paletteSel, ref texMgr);
                    //Override Map
                    Map = temp + "DDS";
                    isProcGen = true;
                }
            }

            //Load the texture to the sampler
            loadTexture();
        }


        private void loadTexture()
        {
            if (Map == "")
                return;

            //Try to load the texture
            if (texMgr.hasTexture(Map))
            {
                tex = texMgr.getTexture(Map);
            }
            else
            {
                tex = new Texture(Map);
                tex.palOpt = new PaletteOpt(false);
                tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                //At this point this should be a common texture. Store it to the master texture manager
                Common.RenderState.activeResMgr.texMgr.addTexture(tex);
            }
        }
        
        public static void dump_texture(string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.GetTexImage(TextureTarget.Texture2DArray, 0, PixelFormat.Rgba, PixelType.Byte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                        (int)pixels[4 * (width * i + j) + 0],
                        (int)pixels[4 * (width * i + j) + 1],
                        (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", ImageFormat.Png);
        }

        public static void dump_texture_fb(string name, int width, int height)
        {
            var pixels = new byte[4 * width * height];
            GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
            var bmp = new Bitmap(width, height);
            for (int i = 0; i < height; i++)
                for (int j = 0; j < width; j++)
                    bmp.SetPixel(j, i, Color.FromArgb(pixels[4 * (width * i + j) + 3],
                        (int)pixels[4 * (width * i + j) + 0],
                        (int)pixels[4 * (width * i + j) + 1],
                        (int)pixels[4 * (width * i + j) + 2]));
            bmp.Save("Temp//framebuffer_raw_" + name + ".png", ImageFormat.Png);
        }


        public static int generate2DTexture(PixelInternalFormat fmt, int w, int h, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex_id);
            GL.TexImage2D(TextureTarget.Texture2D, 0, fmt, w, h, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static int generateTexture2DArray(PixelInternalFormat fmt, int w, int h, int d, PixelFormat pix_fmt, PixelType pix_type, int mipmap_count)
        {
            int tex_id = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2DArray, tex_id);
            GL.TexImage3D(TextureTarget.Texture2DArray, 0, fmt, w, h, d, 0, pix_fmt, pix_type, IntPtr.Zero);
            return tex_id;
        }

        public static void generateTexture2DMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
        }

        public static void generateTexture2DArrayMipmaps(int texture)
        {
            GL.BindTexture(TextureTarget.Texture2DArray, texture);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2DArray);
        }

        public static void setupTextureParameters(TextureTarget texTarget, int texture, int wrapMode, int magFilter, int minFilter, float af_amount)
        {

            GL.BindTexture(texTarget, texture);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapS, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureWrapT, wrapMode);
            GL.TexParameter(texTarget, TextureParameterName.TextureMagFilter, magFilter);
            GL.TexParameter(texTarget, TextureParameterName.TextureMinFilter, minFilter);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);

            //Use anisotropic filtering
            af_amount = Math.Max(af_amount, GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));
            GL.TexParameter(texTarget, (TextureParameterName)0x84FE, af_amount);
        }



#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls



        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //Texture lists should have been disposed from the dictionary
                    //Free other resources here
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
#endregion


    }

    public class Material : TkMaterialData, IDisposable
    {
        private bool disposed = false;
        public bool proc = false;
        public float[] material_flags = new float[64];
        public string name_key = "";
        public textureManager texMgr;
        public int shaderHash = int.MaxValue;

        public static List<string> supported_flags = new List<string>() {
                "_F01_DIFFUSEMAP",
                "_F02_SKINNED",
                "_F03_NORMALMAP",
                "_F07_UNLIT",
                "_F09_TRANSPARENT",
                "_F11_ALPHACUTOUT",
                "_F14_UVSCROLL",
                "_F16_DIFFUSE2MAP",
                "_F17_MULTIPLYDIFFUSE2MAP",
                "_F21_VERTEXCOLOUR",
                "_F22_TRANSPARENT_SCALAR",
                "_F24_AOMAP",
                "_F34_GLOW",
                "_F35_GLOW_MASK",
                "_F39_METALLIC_MASK",
                "_F43_NORMAL_TILING",
                "_F51_DECAL_DIFFUSE",
                "_F52_DECAL_NORMAL",
                "_F55_MULTITEXTURE"};

    public string PName
        {
            get
            {
                return Name;
            }
            set
            {
                Name = value;
            }
        }

        public string PClass
        {
            get
            {
                return Class;
            }
        }

        public List<string> MaterialFlags
        {
            get
            {
                List<string> l = new List<string>();

                foreach (TkMaterialFlags f in Flags)
                {
                    l.Add(((TkMaterialFlags.UberFlagEnum) f.MaterialFlag).ToString());
                }

                return l;
            }
        }

        public string type;
        //public MatOpts opts;
        public Dictionary<string, Sampler> _PSamplers = new Dictionary<string, Sampler>();

        public Dictionary<string, Sampler> PSamplers {
            get
            {
                return _PSamplers;
            }
        }

        private Dictionary<string, Uniform> _CustomPerMaterialUniforms = new Dictionary<string, Uniform>();
        public Dictionary<string, Uniform> CustomPerMaterialUniforms {
            get
            {
                return _CustomPerMaterialUniforms;
            }
        }

        public Material()
        {
            Name = "NULL";
            Shader = "NULL";
            Link = "NULL";
            Class = "NULL";
            TransparencyLayerID = -1;
            CastShadow = false;
            DisableZTest = false;
            Flags = new List<TkMaterialFlags>();
            Samplers = new List<TkMaterialSampler>();
            Uniforms = new List<TkMaterialUniform>();

            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public Material(TkMaterialData md)
        {
            Name = md.Name;
            Shader = md.Shader;
            Link = md.Link;
            Class = md.Class;
            TransparencyLayerID = md.TransparencyLayerID;
            CastShadow = md.CastShadow;
            DisableZTest = md.DisableZTest;
            Flags = new List<TkMaterialFlags>();
            Samplers = new List<TkMaterialSampler>();
            Uniforms = new List<TkMaterialUniform>();

            for (int i = 0; i < md.Flags.Count; i++)
                Flags.Add(md.Flags[i]);
            for (int i = 0; i < md.Samplers.Count; i++)
                Samplers.Add(md.Samplers[i]);
            for (int i = 0; i < md.Uniforms.Count; i++)
                Uniforms.Add(md.Uniforms[i]);

            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public static Material Parse(string path, textureManager input_texMgr)
        {
            //Load template
            //Try to use libMBIN to load the Material files
            TkMaterialData template = NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr) as TkMaterialData;
#if DEBUG
            //Save NMSTemplate to exml
            template.WriteToExml("Temp\\" + template.Name + ".exml");
#endif
            
            //Make new material based on the template
            Material mat = new Material(template);

            mat.texMgr = input_texMgr;
            mat.init();
            return mat;
        }

        public void init()
        {
            //Get MaterialFlags
            foreach (TkMaterialFlags f in Flags)
                material_flags[(int) f.MaterialFlag] = 1.0f;
            
            //Get Uniforms
            foreach (TkMaterialUniform un in Uniforms)
            {
                Uniform my_un = new Uniform("mpCustomPerMaterial.", un);
                CustomPerMaterialUniforms[my_un.Name] = my_un;
            }

            //Get Samplers
            foreach (TkMaterialSampler sm in Samplers)
            {
                Sampler s = new Sampler(sm);
                s.init(texMgr);
                PSamplers[s.PName] = s;
            }


            //Workaround for Procedurally Generated Samplers
            //I need to check if the diffuse sampler is procgen and then force the maps
            //on the other samplers with the appropriate names

            foreach (Sampler s in PSamplers.Values)
            {
                //Check if the first sampler is procgen
                if (s.isProcGen)
                {
                    string name = s.Map;

                    //Properly assemble the mask and the normal map names

                    string[] split = name.Split('.');
                    string pre_ext_name = "";
                    for (int i = 0; i < split.Length-1; i++)
                        pre_ext_name += split[i] + '.';

                    if (PSamplers.ContainsKey("mpCustomPerMaterial.gMasksMap"))
                    {
                        string new_name = pre_ext_name + "MASKS.DDS";
                        PSamplers["mpCustomPerMaterial.gMasksMap"].PMap = new_name;
                        PSamplers["mpCustomPerMaterial.gMasksMap"].tex = PSamplers["mpCustomPerMaterial.gMasksMap"].texMgr.getTexture(new_name);
                    }

                    if (PSamplers.ContainsKey("mpCustomPerMaterial.gNormalMap"))
                    {
                        string new_name = pre_ext_name + "NORMAL.DDS";
                        PSamplers["mpCustomPerMaterial.gNormalMap"].PMap = new_name;
                        PSamplers["mpCustomPerMaterial.gNormalMap"].tex = PSamplers["mpCustomPerMaterial.gNormalMap"].texMgr.getTexture(new_name);
                    }
                    break;
                }
            }

            //Calculate material hash
            List<string> includes = new List<string>();
            for (int i = 0; i < MaterialFlags.Count; i++)
            {
                if (supported_flags.Contains(MaterialFlags[i]))
                    includes.Add(MaterialFlags[i]);
            }

            shaderHash = GLShaderHelper.calculateShaderHash(includes);
            
            if (!Common.RenderState.activeResMgr.shaderExistsForMaterial(this))
                compileMaterialShader();
            
        }

        //Wrapper to support uberflags
        public bool has_flag(TkMaterialFlags.UberFlagEnum flag)
        {
            return has_flag((TkMaterialFlags.MaterialFlagEnum) flag);
        }

        public bool has_flag(TkMaterialFlags.MaterialFlagEnum flag)
        {
            for (int i = 0; i < Flags.Count; i++)
            {
                if (Flags[i].MaterialFlag == flag)
                    return true;
            }
            return false;
        }

        public bool add_flag(TkMaterialFlags.UberFlagEnum flag)
        {
            //Check if material has flag
            foreach (TkMaterialFlags f in Flags)
            {
                if (f.MaterialFlag == (TkMaterialFlags.MaterialFlagEnum)flag)
                    return false;
            }
            
            TkMaterialFlags ff = new TkMaterialFlags();
            ff.MaterialFlag = (TkMaterialFlags.MaterialFlagEnum) flag;
            Flags.Add(ff);

            return true;
        }

        public GMDL.Material Clone()
        {
            GMDL.Material newmat = new GMDL.Material();
            //Remix textures
            return newmat;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //DISPOSE SAMPLERS HERE
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~Material()
        {
            Dispose(false);
        }

        public static int calculateShaderHash(List<TkMaterialFlags> flags)
        {
            string hash = "";

            for (int i = 0; i < flags.Count; i++)
            {
                string s_flag = ((TkMaterialFlags.UberFlagEnum) flags[i].MaterialFlag).ToString();
                if (supported_flags.Contains(s_flag))
                    hash += "_" + s_flag;
            }

            if (hash == "")
                hash = "DEFAULT";
            
            return hash.GetHashCode();
        }

        private void compileMaterialShader()
        {
            Dictionary<int, GLSLShaderConfig> shaderDict;
            Dictionary<int, List<GLMeshVao>>  meshList;

            List<string> includes = new List<string>();
            List<string> defines = new List<string>();

            //Save shader to resource Manager
            //Check for explicit materials
            if (Name == "collisionMat" || Name == "jointMat" || Name == "crossMat")
            {
                shaderDict = Common.RenderState.activeResMgr.GLDefaultShaderMap;
                meshList = Common.RenderState.activeResMgr.defaultMeshShaderMap;
                defines.Add("_D_DEFERRED_RENDERING");
            }
            else if (MaterialFlags.Contains("_F51_DECAL_DIFFUSE") ||
                MaterialFlags.Contains("_F52_DECAL_NORMAL"))
            {
                shaderDict = Common.RenderState.activeResMgr.GLDeferredShaderMapDecal;
                meshList = Common.RenderState.activeResMgr.decalMeshShaderMap;
                defines.Add("_D_DEFERRED_RENDERING");
            }
            else if (MaterialFlags.Contains("_F09_TRANSPARENT") ||
                     MaterialFlags.Contains("_F22_TRANSPARENT_SCALAR") ||
                     MaterialFlags.Contains("_F11_ALPHACUTOUT"))
            {
                shaderDict = Common.RenderState.activeResMgr.GLForwardShaderMapTransparent;
                meshList = Common.RenderState.activeResMgr.transparentMeshShaderMap;
            }
            
            else if (MaterialFlags.Contains("_F07_UNLIT"))
            {
                shaderDict = Common.RenderState.activeResMgr.GLDeferredUNLITShaderMap;
                meshList = Common.RenderState.activeResMgr.opaqueMeshShaderMap;
                defines.Add("_D_DEFERRED_RENDERING");
            }
            else
            {
                shaderDict = Common.RenderState.activeResMgr.GLDeferredLITShaderMap;
                meshList = Common.RenderState.activeResMgr.opaqueMeshShaderMap;
                defines.Add("_D_DEFERRED_RENDERING");
            }

            for (int i=0; i < MaterialFlags.Count; i++)
            {
                if (supported_flags.Contains(MaterialFlags[i]))
                    includes.Add(MaterialFlags[i]);
            }

            GLSLShaderConfig shader =  GLShaderHelper.compileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl", null, null, null,
                defines, includes, SHADER_TYPE.MATERIAL_SHADER, ref Common.RenderState.shaderCompilationLog);


            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);
            
            
            //Save shader to the resource Manager
            shaderDict[shader.shaderHash] = shader;
            meshList[shader.shaderHash] = new List<GLMeshVao>(); //Init list
        }

    }
    
    public class Uniform: TkMaterialUniform
    {
        public MVector4 vec;
        private string prefix;

        public Uniform()
        {
            prefix = "";
            vec = new MVector4(0.0f);
        }

        public Uniform(string name)
        {
            prefix = "";
            PName = name;
            vec = new MVector4(0.0f);
        }

        public Uniform(TkMaterialUniform un)
        {
            //Copy Attributes
            Name = un.Name;
            vec = new MVector4(un.Values.x, un.Values.y, un.Values.z, un.Values.t);
        }

        public Uniform(string pref, TkMaterialUniform un) : this(un)
        {
            prefix = pref;
            Name = prefix + un.Name;
        }

        public void setPrefix(string pref)
        {
            prefix = pref;
        }

        public string PName
        {
            get { return Name; }
            set { Name = value; }
        }

        public MVector4 Vec
        {
            get {
                return vec;
            }

            set
            {
                vec = value;
            }
        }

    }

    public class MVector4: INotifyPropertyChanged
    {
        public Vector4 vec4;

        public MVector4(Vector4 v)
        {
            vec4 = v;
        }

        public MVector4(float x , float y, float z, float w)
        {
            vec4 = new Vector4(x, y, z, w);
        }

        public MVector4(float x)
        {
            vec4 = new Vector4(x);
        }

        //Properties
        public Vector4 Vec
        {
            get { return vec4; }
            set { vec4 = value; RaisePropertyChanged("Vec"); }
        }
        public float X
        {
            get { return vec4.X; }
            set { vec4.X = value; RaisePropertyChanged("X"); }
        }
        public float Y
        {
            get { return vec4.Y; }
            set { vec4.Y = value; RaisePropertyChanged("Y"); }
        }

        public float Z
        {
            get { return vec4.Z; }
            set { vec4.Z = value; RaisePropertyChanged("Z"); }
        }

        public float W
        {
            get { return vec4.W; }
            set { vec4.W = value; RaisePropertyChanged("W"); }
        }

        //Property Change callbacks
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged(string prop)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(prop));
        }
        
    }

    public class MatOpts
    {
        public int transparency;
        public bool castshadow;
        public bool disableTestz;
        public string link;
        public string shadername;
    }


    public class MyTextureUnit
    {
        public OpenTK.Graphics.OpenGL4.TextureUnit texUnit;

        public static Dictionary<string, TextureUnit> MapTextureUnit = new Dictionary<string, TextureUnit> {
            { "mpCustomPerMaterial.gDiffuseMap" , TextureUnit.Texture0 },
            { "mpCustomPerMaterial.gMasksMap" ,   TextureUnit.Texture1 },
            { "mpCustomPerMaterial.gNormalMap" ,  TextureUnit.Texture2 },
            { "mpCustomPerMaterial.gDiffuse2Map" , TextureUnit.Texture3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", TextureUnit.Texture4},
            { "mpCustomPerMaterial.gDetailNormalMap", TextureUnit.Texture5}
        };

        public static Dictionary<string, int> MapTexUnitToSampler = new Dictionary<string, int> {
            { "mpCustomPerMaterial.gDiffuseMap" , 0 },
            { "mpCustomPerMaterial.gMasksMap" ,   1 },
            { "mpCustomPerMaterial.gNormalMap" ,  2 },
            { "mpCustomPerMaterial.gDiffuse2Map" , 3 },
            { "mpCustomPerMaterial.gDetailDiffuseMap", 4},
            { "mpCustomPerMaterial.gDetailNormalMap", 5}
        };

        public MyTextureUnit(string sampler_name)
        {
            texUnit = MapTextureUnit[sampler_name];
        }
    }


    public static class TextureMixer
    {
        //Local storage
        public static Dictionary<string, Dictionary<string, Vector4>> palette = new Dictionary<string, Dictionary<string, Vector4>>();
        public static List<PaletteOpt> palOpts = new List<PaletteOpt>();
        public static List<Texture> difftextures = new List<Texture>(8);
        public static List<Texture> masktextures = new List<Texture>(8);
        public static List<Texture> normaltextures = new List<Texture>(8);
        public static float[] baseLayersUsed = new float[8];
        public static float[] alphaLayersUsed = new float[8];
        public static List<float[]> reColourings = new List<float[]>(8);
        public static List<float[]> avgColourings = new List<float[]>(8);
        private static int[] old_vp_size = new int[4];


        public static void clear()
        {
            //Cleanup temp buffers
            difftextures.Clear();
            masktextures.Clear();
            normaltextures.Clear();
            reColourings.Clear();
            avgColourings.Clear();
            for (int i = 0; i < 8; i++)
            {
                difftextures.Add(null);
                masktextures.Add(null);
                normaltextures.Add(null);
                reColourings.Add(new float[] { 0.0f, 0.0f, 0.0f, 0.0f });
                avgColourings.Add(new float[] { 0.5f, 0.5f, 0.5f, 0.5f });
                palOpts.Add(null);
            }
        }

        public static void combineTextures(string path, Dictionary<string, Dictionary<string, Vector4>> pal_input, ref textureManager texMgr)
        {
            clear();
            palette = pal_input;

            //Contruct .mbin file from dds
            string[] split = path.Split('.');
            //Construct main filename
            string temp = split[0] + ".";
            
            string mbinPath = temp + "TEXTURE.MBIN";
            prepareTextures(texMgr, mbinPath);

            //Init framebuffer
            int tex_width = 0;
            int tex_height = 0;
            int fbo_tex = -1;
            int fbo = -1;
            
            bool fbo_status = setupFrameBuffer(ref fbo, ref fbo_tex, ref tex_width, ref tex_height);

            if (!fbo_status)
            {
                CallBacks.Log("Unable to mix textures, probably 0x0 textures...\n");
                return;
            }
                
            Texture diffTex = mixDiffuseTextures(tex_width, tex_height);
            diffTex.name = temp + "DDS";

            Texture maskTex = mixMaskTextures(tex_width, tex_height);
            maskTex.name = temp + "MASKS.DDS";

            Texture normalTex = mixNormalTextures(tex_width, tex_height);
            normalTex.name = temp + "NORMAL.DDS";

            revertFrameBuffer(fbo, fbo_tex);

            //Add the new procedural textures to the textureManager
            texMgr.addTexture(diffTex);
            texMgr.addTexture(maskTex);
            texMgr.addTexture(normalTex);
        }

        //Generate procedural textures
        private static void prepareTextures(textureManager texMgr, string path)
        {
            //At this point, at least one sampler exists, so for now I assume that the first sampler
            //is always the diffuse sampler and I can initiate the mixing process
            Console.WriteLine("Procedural Texture Detected: " + path);
            CallBacks.Log(string.Format("Parsing Procedural Texture"));

            TkProceduralTextureList template = NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr) as TkProceduralTextureList;
    
            List<TkProceduralTexture> texList = new List<TkProceduralTexture>(8);
            for (int i = 0; i < 8; i++) texList.Add(null);
            ModelProcGen.parse_procTexture(ref texList, template, ref Common.RenderState.activeResMgr);

            Common.CallBacks.Log("Proc Texture Selection");
            for (int i = 0; i < 8; i++)
            {
                if (texList[i] != null)
                {
                    string partNameDiff = texList[i].Diffuse;
                    Common.CallBacks.Log(partNameDiff);
                }
            }

            Common.CallBacks.Log("Procedural Material. Trying to generate procTextures...");

            for (int i = 0; i < 8; i++)
            {

                TkProceduralTexture ptex = texList[i];
                //Add defaults
                if (ptex == null)
                {
                    baseLayersUsed[i] = 0.0f;
                    alphaLayersUsed[i] = 0.0f;
                    continue;
                }

                string partNameDiff = ptex.Diffuse;
                string partNameMask = ptex.Mask;
                string partNameNormal = ptex.Normal;

                TkPaletteTexture paletteNode = ptex.Palette;
                string paletteName = paletteNode.Palette.ToString();
                string colorName = paletteNode.ColourAlt.ToString();
                Vector4 palColor = palette[paletteName][colorName];
                //Randomize palette Color every single time
                //Vector3 palColor = Model_Viewer.Palettes.get_color(paletteName, colorName);

                //Store pallete color to Recolouring List
                reColourings[i] = new float[] { palColor[0], palColor[1], palColor[2], palColor[3] };
                if (ptex.OverrideAverageColour)
                    avgColourings[i] = new float[] { ptex.AverageColour.R, ptex.AverageColour.G, ptex.AverageColour.B, ptex.AverageColour.A };
                    
                //Create Palette Option
                PaletteOpt palOpt = new PaletteOpt();
                palOpt.PaletteName = paletteName;
                palOpt.ColorName = colorName;
                palOpts[i] = palOpt;
                Console.WriteLine("Index {0} Palette Selection {1} {2} ", i, palOpt.PaletteName, palOpt.ColorName);
                Console.WriteLine("Index {0} Color {1} {2} {3} {4}", i, palColor[0], palColor[1], palColor[2], palColor[3]);

                //DIFFUSE
                if (partNameDiff == "")
                {
                    //Add White
                    baseLayersUsed[i] = 0.0f;
                }
                else if (!texMgr.hasTexture(partNameDiff))
                {
                    //Configure the Diffuse Texture
                    try
                    {
                        Texture tex = new Texture(partNameDiff);
                        tex.palOpt = palOpt;
                        tex.procColor = palColor;
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(tex);
                        
                        //Save Texture to material
                        difftextures[i] = tex;
                        baseLayersUsed[i] = 1.0f;
                        alphaLayersUsed[i] = 1.0f;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Texture Not Found Continue
                        Console.WriteLine("Diffuse Texture " + partNameDiff + " Not Found, Appending White Tex");
                        CallBacks.Log(string.Format("Diffuse Texture {0} Not Found", partNameDiff));
                        baseLayersUsed[i] = 0.0f;
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameDiff);
                    //Save Texture to material
                    difftextures[i] = tex;
                    baseLayersUsed[i] = 1.0f;
                }

                //MASK
                if (partNameMask == "")
                {
                    //Skip
                    alphaLayersUsed[i] = 0.0f;
                }
                else if (!texMgr.hasTexture(partNameMask))
                {
                    //Configure Mask
                    try
                    {
                        Texture texmask = new Texture(partNameMask);
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(texmask);
                        //Store Texture to material
                        masktextures[i] = texmask;
                        alphaLayersUsed[i] = 0.0f;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Mask Texture not found
                        Console.WriteLine("Mask Texture " + partNameMask + " Not Found");
                        CallBacks.Log(string.Format("Mask Texture {0} Not Found", partNameMask));
                        alphaLayersUsed[i] = 0.0f;
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameMask);
                    //Store Texture to material
                    masktextures[i] = tex;
                    alphaLayersUsed[i] = 1.0f;
                }


                //NORMALS
                if (partNameNormal == "")
                {
                    //Skip

                }
                else if (!texMgr.hasTexture(partNameNormal))
                {
                    try
                    {
                        Texture texnormal = new Texture(partNameNormal);
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(texnormal);
                        //Store Texture to material
                        normaltextures[i] = texnormal;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Normal Texture not found
                        CallBacks.Log(string.Format("Normal Texture {0} Not Found", partNameNormal));
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameNormal);
                    //Store Texture to material
                    normaltextures[i] = tex;
                }
            }
        }

        private static bool setupFrameBuffer(ref int fbo, ref int fbo_tex, ref int texWidth, ref int texHeight)
        {
            for (int i = 0; i < 8; i++)
            {
                if (difftextures[i] != null)
                {
                    texHeight = difftextures[i].height;
                    texWidth = difftextures[i].width;
                    break;
                }
            }

            if (texWidth == 0 || texHeight == 0)
            {
                //FUCKING HG HAS FUCKING EMPTY TEXTURES WTF AM I SUPPOSED TO MIX HERE
                return false;
            }


            //Diffuse Output
            fbo_tex = Sampler.generate2DTexture(PixelInternalFormat.Rgba, texWidth, texHeight, PixelFormat.Rgba, PixelType.UnsignedByte, 1);
            Sampler.setupTextureParameters(TextureTarget.Texture2D, fbo_tex, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);
            
            //Create New RenderBuffer for the diffuse
            fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            //Attach Textures to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fbo_tex, 0);
            
            //Check
            Debug.Assert(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete);
            
            //Bind the FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            //Set Viewport
            GL.GetInteger(GetPName.Viewport, old_vp_size);
            GL.Viewport(0, 0, texWidth, texHeight);

            return true;
        }

        private static void revertFrameBuffer(int fbo, int fbo_tex)
        {
            //Bring Back screen
            GL.Viewport(0, 0, old_vp_size[2], old_vp_size[3]);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo);

            //Delete Fraomebuffer Textures
            GL.DeleteTexture(fbo_tex);
        }

        public static Texture mixDiffuseTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    int active_id = i;
                    GL.Uniform1(loc + i, baseLayersUsed[active_id]);
                    if (baseLayersUsed[i] > 0.0f)
                        baseLayerIndex = i;
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);

            //Upload DiffuseTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 0.0f);

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 1.0f);

            //Upload Recolouring Information
            loc = GL.GetUniformLocation(pass_program, "lRecolours");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    GL.Uniform4(loc + i, (float)reColourings[i][0],
                                     (float)reColourings[i][1],
                                     (float)reColourings[i][2],
                                     (float)reColourings[i][3]);
                }
            }


            //Upload Average Colors Information
            loc = GL.GetUniformLocation(pass_program, "lAverageColors");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    GL.Uniform4(loc + i, 0.5f, 0.5f, 0.5f, 0.5f);
                }
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_diffuse = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_diffuse, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_diffuse);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_diffuse);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.width = texWidth;
            new_tex.height = texHeight;
            new_tex.texID = out_tex_2darray_diffuse;
            new_tex.target = TextureTarget.Texture2DArray;
            
#if (DUMP_TEXTURES)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("diffuse", texWidth, texHeight);
#endif
            return new_tex;
        }

        private static Texture mixMaskTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (masktextures[i] != null)
                    {
                        GL.Uniform1(loc + i, 1.0f);
                        baseLayerIndex = i;
                    } else
                        GL.Uniform1(loc + i, 0.0f);
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);


            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 1.0f);

            //Upload DiffuseTextures as alphaTextures
            loc = GL.GetUniformLocation(pass_program, "alphaTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Upload maskTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (masktextures[i] != null)
                        tex = masktextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, 8 + i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture8;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);

            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_mask);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_mask);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.texID = out_tex_2darray_mask;
            new_tex.target = TextureTarget.Texture2DArray;

#if (DUMP_TEXTURESNONO)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("mask", texWidth, texHeight);
#endif
            return new_tex;
        }

        private static Texture mixNormalTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (normaltextures[i] != null)
                    {
                        GL.Uniform1(loc + i, 1.0f);
                        baseLayerIndex = i;
                    }
                    else
                        GL.Uniform1(loc + i, 0.0f);
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);


            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 1.0f);

            //Upload DiffuseTextures as alphaTextures
            loc = GL.GetUniformLocation(pass_program, "alphaTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Upload maskTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (normaltextures[i] != null)
                        tex = normaltextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, 8 + i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture8;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);


            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_mask);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_mask);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.texID = out_tex_2darray_mask;
            new_tex.target = TextureTarget.Texture2DArray;

#if (DUMP_TEXTURES)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("normal", texWidth, texHeight);
#endif
            return new_tex;
        }
    }



    public class PaletteOpt
    {
        public string PaletteName;
        public string ColorName;

        //Default Empty Constructor
        public PaletteOpt() { }
        //Empty Palette Constructor
        public PaletteOpt(bool flag)
        {
            if (!flag)
            {
                PaletteName = "Fur";
                ColorName = "None";
            }
        }
    }

    public class Texture : IDisposable
    {
        private bool disposed = false;
        public int texID = -1;
        public int pboID = -1;
        public TextureTarget target;
        public string name;
        public int width;
        public int height;
        public InternalFormat pif;
        public PaletteOpt palOpt;
        public Vector4 procColor;
        public Vector3 avgColor;
        
        //Empty Initializer
        public Texture() {}
        //Path Initializer
        public Texture(string path, bool isCustom = false)
        {
            Stream fs;
            byte[] image_data;
            int data_length;
            try
            {
                if (!isCustom)
                    fs = NMSUtils.LoadNMSFileStream(path, ref Common.RenderState.activeResMgr);
                else
                    fs = new FileStream(path, FileMode.Open);
                

                if (fs == null)
                {
                    //throw new System.IO.FileNotFoundException();
                    Console.WriteLine("Texture {0} Missing. Using default.dds", path);

                    //Load default.dds from resources
                    image_data = File.ReadAllBytes("default.dds");
                    data_length = image_data.Length;
                }
                else
                {
                    data_length = (int)fs.Length;
                    image_data = new byte[data_length];
                }

                fs.Read(image_data, 0, data_length);

            } catch (FileNotFoundException e)
            {
                //Fallback to the default.dds
                image_data = WPFModelViewer.Properties.Resources._default;
            }
            
            textureInit(image_data, path);
        }

        public void textureInit(byte[] imageData, string _name)
        {
            DDSImage ddsImage;
            name = _name;
            
            ddsImage = new DDSImage(imageData);
            RenderStats.texturesNum += 1; //Accumulate settings

            Console.WriteLine("Sampler Name Path " + name + " Width {0} Height {1}", ddsImage.header.dwWidth, ddsImage.header.dwHeight);
            width = ddsImage.header.dwWidth;
            height = ddsImage.header.dwHeight;
            int blocksize = 16;
            switch (ddsImage.header.ddspf.dwFourCC)
            {
                //DXT1
                case (0x31545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt1Ext;
                    blocksize = 8;
                    break;
                case (0x35545844):
                    pif = InternalFormat.CompressedSrgbAlphaS3tcDxt5Ext;
                    break;
                case (0x32495441): //ATI2A2XY
                    pif = InternalFormat.CompressedRgRgtc2; //Normal maps are probably never srgb
                    break;
                //DXT10 HEADER
                case (0x30315844):
                    {
                        switch (ddsImage.header10.dxgiFormat)
                        {
                            case (DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM):
                                pif = InternalFormat.CompressedSrgbAlphaBptcUnorm;
                                break;
                            default:
                                throw new ApplicationException("Unimplemented DX10 Texture Pixel format");
                        }
                        break;
                    }
                default:
                    throw new ApplicationException("Unimplemented Pixel format");
            }
            
            //Temp Variables
            int w = width;
            int h = height;
            int mm_count = ddsImage.header.dwMipMapCount; 
            int depth_count = Math.Max(1, ddsImage.header.dwDepth); //Fix the counter to 1 to fit the texture in a 3D container
            int temp_size = ddsImage.header.dwPitchOrLinearSize;


            //Generate PBO
            pboID = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, pboID);
            GL.BufferData(BufferTarget.PixelUnpackBuffer, ddsImage.bdata.Length, ddsImage.bdata, BufferUsageHint.StaticDraw);
            //GL.BufferSubData(BufferTarget.PixelUnpackBuffer, IntPtr.Zero, ddsImage.bdata.Length, ddsImage.bdata);

            //Upload to GPU
            texID = GL.GenTexture();
            target = TextureTarget.Texture2DArray;

            GL.BindTexture(target, texID);
            
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mm_count - 1);

            int offset = 0;
            for (int i=0; i < mm_count; i++)
            {
                GL.CompressedTexImage3D(target, i, pif, w, h, depth_count, 0, temp_size * depth_count, IntPtr.Zero + offset);
                //GL.TexImage3D(target, i, PixelInternalFormat.Rgba8, w, h, depth_count, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                
                offset += temp_size * depth_count;

                w = Math.Max(w >> 1, 1);
                h = Math.Max(h >> 1, 1);

                temp_size = Math.Max(1, (w + 3) / 4) * Math.Max(1, (h + 3) / 4) * blocksize;
                //This works only for square textures
                //temp_size = Math.Max(temp_size/4, blocksize);
            }

            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(target, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Repeat);
            GL.TexParameter(target, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
            GL.TexParameter(target, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.LinearMipmapLinear);
            //Console.WriteLine(GL.GetError());

            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float) Math.Max(af_amount, 4.0f);
            //GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);
            int max_level = 0;
            GL.GetTexParameter(target, GetTextureParameter.TextureMaxLevel, out max_level);
            int base_level = 0;
            GL.GetTexParameter(target, GetTextureParameter.TextureBaseLevel, out base_level);

            int maxsize = Math.Max(height, width);
            int p = (int) Math.Floor(Math.Log(maxsize, 2)) + base_level;
            int q = Math.Min(p, max_level);

#if (DEBUGNONO)
            //Get all mipmaps
            temp_size = ddsImage.header.dwPitchOrLinearSize;
            for (int i = 0; i < q; i++)
            {
                //Get lowest calculated mipmap
                byte[] pixels = new byte[temp_size];
                
                //Save to disk
                GL.GetCompressedTexImage(TextureTarget.Texture2D, i, pixels);
                File.WriteAllBytes("Temp\\level" + i.ToString(), pixels);
                temp_size = Math.Max(temp_size / 4, 16);
            }
#endif

#if (DUMP_TEXTURESNONO)
            Sampler.dump_texture(name.Split('\\').Last().Split('/').Last(), width, height);
#endif
            //avgColor = getAvgColor(pixels);
            ddsImage = null;
            GL.BindBuffer(BufferTarget.PixelUnpackBuffer, 0); //Unbind texture PBO
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                if (texID != -1) GL.DeleteTexture(texID);
                GL.DeleteBuffer(pboID);
                
            }

            //Free unmanaged resources
            disposed = true;
        }

        private Vector3 getAvgColor(byte[] pixels)
        {
            //Assume that I have the 4x4 mipmap
            //I need to fetch the first 2 colors and calculate the Average

            MemoryStream ms = new MemoryStream(pixels);
            BinaryReader br = new BinaryReader(ms);

            int color0 = br.ReadUInt16();
            int color1 = br.ReadUInt16();

            br.Close();

            //int rmask = 0x1F << 11;
            //int gmask = 0x3F << 5;
            //int bmask = 0x1F;
            uint temp;

            temp = (uint) (color0 >> 11) * 255 + 16;
            char r0 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color0 & 0x07E0) >> 5) * 255 + 32;
            char g0 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color0 & 0x001F) * 255 + 16;
            char b0 = (char)((temp / 32 + temp) / 32);

            temp = (uint)(color1 >> 11) * 255 + 16;
            char r1 = (char)((temp / 32 + temp) / 32);
            temp = (uint)((color1 & 0x07E0) >> 5) * 255 + 32;
            char g1 = (char)((temp / 64 + temp) / 64);
            temp = (uint)(color1 & 0x001F) * 255 + 16;
            char b1 = (char)((temp / 32 + temp) / 32);

            char red = (char) (((int) ( r0 + r1)) / 2);
            char green = (char)(((int)(g0 + g1)) / 2);
            char blue = (char)(((int)(b0 + b1)) / 2);
            

            return new Vector3(red / 256.0f, green / 256.0f, blue / 256.0f);
            
        }

        private ulong PackRGBA( char r, char g, char b, char a)
        {
            return (ulong) ((r << 24) | (g << 16) | (b << 8) | a);
        }

        // void DecompressBlockDXT1(): Decompresses one block of a DXT1 texture and stores the resulting pixels at the appropriate offset in 'image'.
        //
        // unsigned long x:						x-coordinate of the first pixel in the block.
        // unsigned long y:						y-coordinate of the first pixel in the block.
        // unsigned long width: 				width of the texture being decompressed.
        // unsigned long height:				height of the texture being decompressed.
        // const unsigned char *blockStorage:	pointer to the block to decompress.
        // unsigned long *image:				pointer to image where the decompressed pixel data should be stored.

        private void DecompressBlockDXT1(ulong x, ulong y, ulong width, byte[] blockStorage, byte[] image)
        {

        }

    }

    public class Joint : model
    {
        public int jointIndex;
        public Vector3 color;

        //Add a bunch of shit for posing
        //public Vector3 _localPosePosition = new Vector3(0.0f);
        //public Matrix4 _localPoseRotation = Matrix4.Identity;
        //public Vector3 _localPoseScale = new Vector3(1.0f);
        public Matrix4 BindMat = Matrix4.Identity; //This is the local Bind Matrix related to the parent joint
        public Matrix4 invBMat = Matrix4.Identity; //This is the inverse of the local Bind Matrix related to the parent
        //DO NOT MIX WITH THE gobject.invBMat which is reverts the transformation to the global space
        
        //Props
        public Matrix4 localPoseMatrix
        {
            get { return _localPoseMatrix; }
            set { _localPoseMatrix = value; updated = true; }
        }

        public Joint()
        {
            type = TYPES.JOINT;   
        }

        protected Joint(Joint input) : base(input)
        {
            this.jointIndex = input.jointIndex;
            this.BindMat = input.BindMat;
            this.invBMat = input.invBMat;
            this.color = input.color;

            meshVao = new GLMeshVao();
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
            GLMeshBufferManager.setInstanceWorldMat(meshVao, instanceId, Matrix4.Identity);
            meshVao.type = TYPES.JOINT;
            meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs
            meshVao.vao = new Primitives.LineSegment(children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];
        }

        public override void updateMeshInfo()
        {
            //We do not apply frustum occlusion on joint objects
            if (renderable && (children.Count > 0))
            {
                //Update Vertex Buffer based on the new positions
                float[] verts = new float[2 * children.Count * 3];
                int arraysize = 2 * children.Count * 3 * sizeof(float);

                for (int i = 0; i < children.Count; i++)
                {
                    verts[i * 6 + 0] = worldPosition.X;
                    verts[i * 6 + 1] = worldPosition.Y;
                    verts[i * 6 + 2] = worldPosition.Z;
                    verts[i * 6 + 3] = children[i].worldPosition.X;
                    verts[i * 6 + 4] = children[i].worldPosition.Y;
                    verts[i * 6 + 5] = children[i].worldPosition.Z;
                }

                meshVao.metaData.batchcount = 2 * children.Count;

                GL.BindVertexArray(meshVao.vao.vao_id);
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                //Add verts data, color data should stay the same
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
                instanceId = GLMeshBufferManager.addInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity);
            }
            
            base.updateMeshInfo();
        }

        public override model Clone()
        {
            Joint j = new Joint();
            j.copyFrom(this);

            j.jointIndex = this.jointIndex;
            j.BindMat = this.BindMat;
            j.invBMat = this.invBMat;
            j.color = this.color;

            j.meshVao = new GLMeshVao();
            j.instanceId = GLMeshBufferManager.addInstance(ref j.meshVao, j);
            GLMeshBufferManager.setInstanceWorldMat(j.meshVao, j.instanceId, Matrix4.Identity);
            j.meshVao.type = TYPES.JOINT;
            j.meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs
            j.meshVao.vao = new Primitives.LineSegment(this.children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            j.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];

            //Clone children
            foreach (model child in children)
            {
                model new_child = child.Clone();
                new_child.parent = j;
                j.children.Add(new_child);
            }

            return j;
        }

        //DIsposal
        protected override void Dispose(bool disposing)
        {
            //Dispose GL Stuff
            meshVao?.Dispose();
            base.Dispose(disposing);
        }
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct GLLight
    {
        [FieldOffset(0)]
        public Vector4 position; //w is renderable
        [FieldOffset(16)]
        public Vector4 color; //w is intensity
        [FieldOffset(32)]
        public Vector4 direction; //w is fov
        [FieldOffset(48)]
        public int falloff;
        [FieldOffset(52)]
        public float type;
        
        public static readonly int SizeInBytes = 64;
    }

    public enum ATTENUATION_TYPE
    {
        QUADRATIC = 0x0,
        CONSTANT,
        LINEAR,
        COUNT
    }

    public enum LIGHT_TYPE
    {
        POINT = 0x0,
        SPOT,
        COUNT
    }

    public class Light : model
    {
        //I should expand the light properties here
        public MVector4 color = new MVector4(1.0f);
        //public GLMeshVao main_Vao;
        public float fov = 360.0f;
        public ATTENUATION_TYPE falloff;
        public LIGHT_TYPE light_type;
        
        public float intensity = 1.0f;
        public Vector3 direction = new Vector3();
        
        public bool update_changes = false; //Used to prevent unecessary uploads to the UBO

        //Light Projection + View Matrices
        public Matrix4[] lightSpaceMatrices;
        public Matrix4 lightProjectionMatrix;
        public GLLight strct;

        //Properties
        public MVector4 Color
        {
            get {
                return color;
            }

            set
            {
                catchPropertyChanged(color, new PropertyChangedEventArgs("Vec"));
            }
        }

        public float FOV
        {
            get
            {
                return fov;
            }

            set
            {
                fov = value;
                strct.direction.W = MathUtils.radians(fov);
                update_changes = true;
            }
        }

        public float Intensity
        {
            get
            {
                return intensity;
            }

            set
            {
                intensity = value;
                strct.color.W = value;
                update_changes = true;
            }
        }

        public string Attenuation
        {
            get
            {
                return falloff.ToString();
            }

            set
            {
                Enum.TryParse<ATTENUATION_TYPE>(value, out falloff);
                strct.falloff = (int) falloff;
                update_changes = true;
            }
        }

        public override bool IsRenderable
        {
            get
            {
                return renderable;
            }

            set
            {
                strct.position.W = value ? 1.0f : 0.0f;
                base.IsRenderable = value;
                update_changes = true;
            }
        }

        //Add event handler to catch changes to the Vector property

        private void catchPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            MVector4 t = sender as MVector4;
            
            //Update struct
            strct.color.X = t.X;
            strct.color.Y = t.Y;
            strct.color.Z = t.Z;
            update_changes = true;
        }


        public Light()
        {
            type = TYPES.LIGHT;
            fov = 360;
            intensity = 1.0f;
            falloff = ATTENUATION_TYPE.CONSTANT;


            //Initialize new MeshVao
            meshVao = new GLMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this); // Add instance

            //Init projection Matrix
            lightProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathUtils.radians(90), 1.0f, 1.0f, 300f);
            
            //Init lightSpace Matrices
            lightSpaceMatrices = new Matrix4[6];
            for (int i=0; i < 6; i++)
            {
                lightSpaceMatrices[i] = Matrix4.Identity * lightProjectionMatrix;
            }

            //Catch changes to MVector from the UI
            color = new MVector4(1.0f);
            color.PropertyChanged += catchPropertyChanged;
        }

        protected Light(Light input) : base(input)
        {
            Color = input.Color;
            intensity = input.intensity;
            falloff = input.falloff;
            fov = input.fov;
            strct = input.strct;
            
            //Initialize new MeshVao
            meshVao = new GLMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this); //Add instance
            

            //Copy Matrices
            lightProjectionMatrix = input.lightProjectionMatrix;
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
                lightSpaceMatrices[i] = input.lightSpaceMatrices[i];

            update_struct();
            RenderState.activeResMgr.GLlights.Add(this);
        }

        public override void updateMeshInfo()
        {
            if (RenderState.renderViewSettings.RenderLights && renderable)
            {
                //End Point
                Vector4 ep;
                //Lights with 360 FOV are points
                if (Math.Abs(FOV - 360.0f) <= 1e-4)
                {
                    ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                    light_type = LIGHT_TYPE.POINT;
                }
                else
                {
                    ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                    light_type = LIGHT_TYPE.SPOT;
                }

                ep = ep * _localRotation;
                direction = ep.Xyz; //Set spotlight direction
                update_struct();

                //Update Vertex Buffer based on the new data
                float[] verts = new float[6];
                int arraysize = 6 * sizeof(float);

                //Origin Point
                verts[0] = worldPosition.X;
                verts[1] = worldPosition.Y;
                verts[2] = worldPosition.Z;

                ep.X += worldPosition.X;
                ep.Y += worldPosition.Y;
                ep.Z += worldPosition.Z;

                verts[3] = ep.X;
                verts[4] = ep.Y;
                verts[5] = ep.Z;

                GL.BindVertexArray(meshVao.vao.vao_id);
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                //Add verts data, color data should stay the same
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);

                //Uplod worldMat to the meshVao
                instanceId = GLMeshBufferManager.addInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity); //Add instance
            }

            base.updateMeshInfo();
            updated = false; //All done
        }

        public override void update()
        {
            base.update();

            //End Point
            Vector4 ep;
            //Lights with 360 FOV are points
            if (Math.Abs(FOV - 360.0f) <= 1e-4)
            {
                ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                ep = ep * _localRotation;
                light_type = LIGHT_TYPE.POINT;
            }
            else
            {
                ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                ep = ep * _localRotation;
                light_type = LIGHT_TYPE.SPOT;
            }

            ep.Normalize();
            
            direction = ep.Xyz; //Set spotlight direction
            update_struct();

            //Assume that this is a point light for now
            //Right
            lightSpaceMatrices[0] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Left
            lightSpaceMatrices[1] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(-1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Up
            lightSpaceMatrices[2] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, -1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Down
            lightSpaceMatrices[3] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Near
            lightSpaceMatrices[4] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, 1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Far
            lightSpaceMatrices[5] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, -1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
        }

        public void update_struct()
        {
            Vector4 old_pos = strct.position;
            strct.position = new Vector4((new Vector4(worldPosition, 1.0f) * RenderState.rotMat).Xyz, renderable ? 1.0f : 0.0f);
            strct.color = new Vector4(Color.Vec.Xyz, intensity);
            strct.direction = new Vector4(direction, MathUtils.radians(fov));
            strct.falloff = (int) falloff;
            strct.type = (light_type == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f;
            
            if (old_pos != strct.position)
                update_changes = true;
        }

        public override model Clone()
        {
            return new Light(this);
        }

        //Disposal
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                base.Dispose(true);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }


    //Animation Classes

    //model Components
    //TODO Move them somewhere else
    public abstract class Component : IDisposable
    {
        public abstract Component Clone();
        
#region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            Dispose(true);
        }
#endregion
    };

    
    public class AnimComponent : Component
    {
        //animations list Contains all the animations bound to the locator through Tkanimationcomponent
        public List<AnimData> _animations = new List<AnimData>();
        public List<AnimData> Animations
        {
            get
            {
                return _animations;
            }
        }
        
        //Default Constructor
        public AnimComponent()
        {
            
        }

        public void assimpExport(ref Assimp.Scene scn)
        {
            foreach (AnimData ad in Animations)
            {
                Assimp.Animation anim = ad.assimpExport(ref scn);
                scn.Animations.Add(anim);
            }
        }

        public AnimComponent(TkAnimationComponentData data)
        {
            //Load Animations
            if (data.Idle.Anim != "")
                _animations.Add(new AnimData(data.Idle)); //Add Idle Animation
            
            for (int i = 0; i < data.Anims.Count; i++)
            {
                //Check if the animation is already loaded
                AnimData my_ad = new AnimData(data.Anims[i]);
                _animations.Add(my_ad);
            }

        }

        public void copyFrom(AnimComponent input)
        {
            //Base class is dummy
            //base.copyFrom(input); //Copy stuff from base class

            //TODO: Copy Animations
            
        }

        public override Component Clone()
        {
            AnimComponent ac = new AnimComponent();

            //Copy Animations
            foreach (AnimData ad in _animations)
                ac.Animations.Add(ad.Clone());
            
            return ac;
        }

        public void update()
        {
            
        }

        protected AnimComponent(AnimComponent input)
        {
            this.copyFrom(input);
        }

#region IDisposable Support
        private bool disposed = false; // To detect redundant calls
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
        
#endregion

    }

    public class LODModelComponent: Component
    {
        private List<LODModelResource> _resources;

        //Properties
        public List<LODModelResource> Resources => _resources;

        public LODModelComponent()
        {
            _resources = new List<LODModelResource>();
        }

        public override Component Clone()
        {
            LODModelComponent lmc = new LODModelComponent();
            return lmc;
        }


    }

    public class LODModelResource
    {
        private string _filename;
        private float _crossFadeTime;
        private float _crossFadeoverlap;

        //Properties
        public string Filename
        {
            get
            {
                return _filename;
            }
        }

        public LODModelResource(TkLODModelResource res)
        {
            _filename = res.LODModel.Filename;
            _crossFadeTime = res.CrossFadeTime;
            _crossFadeoverlap = res.CrossFadeOverlap;
        }
    }

    public class AnimPoseComponent: Component
    {
        public model ref_object = null;
        public TkAnimMetadata animMeta = null;
        //AnimationPoseData
        public List<AnimPoseData> _poseData = new List<AnimPoseData>();
        public TkAnimMetadata _poseFrameData = null; //Stores the actual poseFrameData
        public List<AnimPoseData> poseData
        {
            get
            {
                return _poseData;
            }
        }

        public ICommand ApplyPose
        {
            get { return new ApplyPoseCommand(); }
        }

        public ICommand ResetPose
        {
            get { return new ResetPoseCommand(); }
        }

        //Default Constructor
        public AnimPoseComponent()
        {

        }

        public AnimPoseComponent(TkAnimPoseComponentData apcd)
        {
            _poseFrameData = (TkAnimMetadata) NMSUtils.LoadNMSTemplate(apcd.Filename, 
                ref Common.RenderState.activeResMgr);

            //Load PoseAnims
            for (int i = 0; i < apcd.PoseAnims.Count; i++)
            {
                AnimPoseData my_apd = new AnimPoseData(apcd.PoseAnims[i]);
                poseData.Add(my_apd);
            }
        }

        public override Component Clone()
        {
            return new AnimPoseComponent();
        }

        //ICommands

        private class ApplyPoseCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                AnimPoseComponent apc = parameter as AnimPoseComponent;
                ((scene) apc.ref_object.parentScene).applyPoses(apc.ref_object.loadPose());
            }
        }

        private class ResetPoseCommand : ICommand
        {
            event EventHandler ICommand.CanExecuteChanged
            {
                add { }
                remove { }
            }

            bool ICommand.CanExecute(object parameter)
            {
                return true;
            }

            void ICommand.Execute(object parameter)
            {
                AnimPoseComponent apc = parameter as AnimPoseComponent;
                apc.ref_object.parentScene.resetPoses();
            }
        }

    }


    



    public class AnimNodeFrameData
    {
        public List<OpenTK.Quaternion> rotations = new List<OpenTK.Quaternion>();
        public List<Vector3> translations = new List<Vector3>();
        public List<Vector3> scales = new List<Vector3>();

        public void LoadRotations(FileStream fs,int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                OpenTK.Quaternion q = new OpenTK.Quaternion();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                q.W = br.ReadSingle();

                this.rotations.Add(q);
            }
        }

        public void LoadTranslations(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new Vector3();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                br.ReadSingle();
                this.translations.Add(q);
            }
        }

        public void LoadScales(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Vector3 q = new Vector3();
                q.X = br.ReadSingle();
                q.Y = br.ReadSingle();
                q.Z = br.ReadSingle();
                br.ReadSingle();
                this.scales.Add(q);
            }
        }

    }
    

    public class AnimPoseData: TkAnimPoseData
    {

        public AnimPoseData(TkAnimPoseData apd)
        {
            Anim = apd.Anim;
            FrameStart = apd.FrameStart;
            FrameEnd = apd.FrameEnd;
            PActivePoseFrame = (int) ((apd.FrameEnd - apd.FrameStart) / 2 + apd.FrameStart);
        }

        public string PAnim
        {
            get
            {
                return Anim;
            }
        }

        public int PFrameStart
        {
            get
            {
                return FrameStart;
            }
            set
            {
                FrameStart = value;
            }
        }

        public int PFrameEnd
        {
            get
            {
                return FrameEnd;
            }
            set
            {
                FrameEnd = value;
            }
        }

        public int PActivePoseFrame
        {
            get; set;
        }



    }

    
    public class AnimMetadata: TkAnimMetadata
    {
        public float duration;
        public Dictionary<string, OpenTK.Quaternion[]> anim_rotations;
        public Dictionary<string, Vector3[]> anim_positions;
        public Dictionary<string, Vector3[]> anim_scales;

        public AnimMetadata(TkAnimMetadata amd)
        {
            //Copy struct info
            FrameCount = amd.FrameCount;
            NodeCount = amd.NodeCount;
            NodeData = amd.NodeData;
            AnimFrameData = amd.AnimFrameData;
            StillFrameData = amd.StillFrameData;

            duration = FrameCount * 1000.0f;
        }

        public AnimMetadata()
        {
            duration = 0.0f;
        }

        public void load()
        {
            //Init dictionaries
            anim_rotations = new Dictionary<string, OpenTK.Quaternion[]>();
            anim_positions = new Dictionary<string, Vector3[]>();
            anim_scales = new Dictionary<string, Vector3[]>();

            loadData();
        }

        private void loadData()
        {
            for (int j = 0; j < NodeCount; j++)
            {
                TkAnimNodeData node = NodeData[j];
                //Init dictionary entries

                anim_rotations[node.Node] = new OpenTK.Quaternion[FrameCount];
                anim_positions[node.Node] = new Vector3[FrameCount];
                anim_scales[node.Node] = new Vector3[FrameCount];

                for (int i = 0; i < FrameCount; i++)
                {
                    NMSUtils.fetchRotQuaternion(node, this, i, ref anim_rotations[node.Node][i]); //use Ref
                    NMSUtils.fetchTransVector(node, this, i, ref anim_positions[node.Node][i]); //use Ref
                    NMSUtils.fetchScaleVector(node, this, i, ref anim_scales[node.Node][i]); //use Ref
                }
            }
        }
    }

    public class AnimData : TkAnimationData, INotifyPropertyChanged
    {
        public AnimMetadata animMeta;
        public float animationTime = 0.0f;
        public bool _animationToggle = false;
        private int prevFrameIndex = 0;
        private int nextFrameIndex = 0;
        private float LERP_coeff = 0.0f;
        public bool loaded = false;
        
        public event PropertyChangedEventHandler PropertyChanged;

        //Constructors
        public AnimData(TkAnimationData ad){
            Anim = ad.Anim; 
            Filename = ad.Filename;
            FrameStart = ad.FrameStart;
            FrameEnd = ad.FrameEnd;
            StartNode = ad.StartNode;
            AnimType = ad.AnimType;
            Speed = ad.Speed;
            Additive = ad.Additive;
        }

        public AnimData()
        {
            
        }

        public Assimp.Animation assimpExport(ref Assimp.Scene scn)
        {
            Assimp.Animation asAnim = new Assimp.Animation();
            asAnim.Name = Anim;

            
            
            
            //Make sure keyframe data is loaded from the files
            if (!loaded)
            {
                fetchAnimMetaData();
                loaded = true;
            }

            

            asAnim.TicksPerSecond = 60;
            asAnim.DurationInTicks = animMeta.FrameCount;
            float time_interval = 1.0f / (float) asAnim.TicksPerSecond;
            

            //Add Node-Bone Channels
            for (int i = 0; i < animMeta.NodeCount; i++)
            {
                string name = animMeta.NodeData[i].Node;
                Assimp.NodeAnimationChannel mChannel = new Assimp.NodeAnimationChannel();
                mChannel.NodeName = name;
                
                //mChannel.PostState = Assimp.AnimationBehaviour.Linear;
                //mChannel.PreState = Assimp.AnimationBehaviour.Linear;
                

                //Export Keyframe Data
                for (int j = 0; j < animMeta.FrameCount; j++)
                {
                    
                    //Position
                    Assimp.VectorKey vk = new Assimp.VectorKey(j * time_interval, MathUtils.convertVector(animMeta.anim_positions[name][j]));
                    mChannel.PositionKeys.Add(vk);
                    //Rotation
                    Assimp.QuaternionKey qk = new Assimp.QuaternionKey(j * time_interval, MathUtils.convertQuaternion(animMeta.anim_rotations[name][j]));
                    mChannel.RotationKeys.Add(qk);
                    //Scale
                    Assimp.VectorKey sk = new Assimp.VectorKey(j * time_interval, MathUtils.convertVector(animMeta.anim_scales[name][j]));
                    mChannel.ScalingKeys.Add(sk);
                    
                }
                
                asAnim.NodeAnimationChannels.Add(mChannel);
                
            }

            return asAnim;
            
        }

        public AnimData Clone()
        {
            AnimData ad = new AnimData();
            
            ad.Anim = Anim;
            ad.Filename = Filename;
            ad.FrameStart = FrameStart;
            ad.FrameEnd = FrameEnd;
            ad.StartNode = StartNode;
            ad.AnimType = AnimType;
            ad.Speed = Speed;
            ad.Additive = Additive;
            ad.animMeta = animMeta;

            return ad;
        }
        
        //Properties

        public string PName
        {
            get { return Anim; }
            set { Anim = value; }
        }

        public bool PActive
        {
            get { return Active; }
            set { Active = value; }
        }
        
        public bool AnimationToggle
        {
            get { return _animationToggle; }
            set { _animationToggle = value;
                NotifyPropertyChanged("AnimationToggle");
            }
        }

        public bool isValid
        {
            get { return Filename != "";}
        }

        public string PAnimType
        {
            get
            {
                return AnimType.ToString();
            }
        }

        public bool PAdditive
        {
            get { return Additive; }
            set { Additive = value; }
        }

        public float PSpeed
        {
            get { return Speed; }
            set { Speed = value; }
        }

        //UI update
        private void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        public void loadData()
        {
            if (Filename != "")
                fetchAnimMetaData();
        }


        private void fetchAnimMetaData()
        {
            if (Common.RenderState.activeResMgr.Animations.ContainsKey(Filename))
            {
                animMeta = Common.RenderState.activeResMgr.Animations[Filename];
            }
            else
            {
                TkAnimMetadata amd = NMSUtils.LoadNMSTemplate(Filename,
                    ref Common.RenderState.activeResMgr) as TkAnimMetadata;
                animMeta = new AnimMetadata(amd);
                animMeta.load(); //Load data as well
                Common.RenderState.activeResMgr.Animations[Filename] = animMeta;
            }
        }


        public void animate(float dt) //time in milliseconds
        {
            if (!loaded)
            {
                fetchAnimMetaData();
                loaded = true;
            }
                
            if (animMeta != null)
            {
                float activeAnimDuration = animMeta.duration / Speed;
                float activeAnimInterval = activeAnimDuration / (Common.RenderState.renderSettings.animFPS * animMeta.FrameCount);
                
                animationTime += dt; //Progress time

                if ((AnimType == AnimTypeEnum.OneShot) && animationTime > activeAnimDuration)
                {
                    animationTime = 0.0f;
                    _animationToggle = false;
                    AnimationToggle = false;
                    return;
                } 
                else
                    animationTime = animationTime % activeAnimDuration; //Clamp to correct time span

                
                //Find frames
                prevFrameIndex = (int) Math.Floor(animationTime / activeAnimInterval) % animMeta.FrameCount;
                nextFrameIndex = (prevFrameIndex + 1) % animMeta.FrameCount;

                float prevFrameTime = prevFrameIndex * activeAnimInterval;
                LERP_coeff = (animationTime - prevFrameTime) / activeAnimInterval;
            }
        }

        //TODO: Use this new definition for animation blending
        //public void applyNodeTransform(model m, string node, out Quaternion q, out Vector3 p)
        public void applyNodeTransform(model m, string node)
        {
            //Fetch prevFrame stuff
            OpenTK.Quaternion prev_q = animMeta.anim_rotations[node][prevFrameIndex];
            Vector3 prev_p = animMeta.anim_positions[node][prevFrameIndex];

            //Fetch nextFrame stuff
            OpenTK.Quaternion next_q = animMeta.anim_rotations[node][prevFrameIndex];
            Vector3 next_p = animMeta.anim_positions[node][prevFrameIndex];

            //Interpolate

            OpenTK.Quaternion q = OpenTK.Quaternion.Slerp(prev_q, next_q, LERP_coeff);
            Vector3 p = prev_p * LERP_coeff + next_p * (1.0f - LERP_coeff);

            //Convert transforms
            m.localRotation = Matrix4.CreateFromQuaternion(q);
            m.localPosition = p;
        }

    }




        
    public class JointBindingData
    {
        public Matrix4 invBindMatrix = Matrix4.Identity;
        public Matrix4 BindMatrix = Matrix4.Identity;

        public void Load(Stream fs)
        {
            //Binary Reader
            BinaryReader br = new BinaryReader(fs);
            //Lamest way to read a matrix
            invBindMatrix.M11 = br.ReadSingle();
            invBindMatrix.M12 = br.ReadSingle();
            invBindMatrix.M13 = br.ReadSingle();
            invBindMatrix.M14 = br.ReadSingle();
            invBindMatrix.M21 = br.ReadSingle();
            invBindMatrix.M22 = br.ReadSingle();
            invBindMatrix.M23 = br.ReadSingle();
            invBindMatrix.M24 = br.ReadSingle();
            invBindMatrix.M31 = br.ReadSingle();
            invBindMatrix.M32 = br.ReadSingle();
            invBindMatrix.M33 = br.ReadSingle();
            invBindMatrix.M34 = br.ReadSingle();
            invBindMatrix.M41 = br.ReadSingle();
            invBindMatrix.M42 = br.ReadSingle();
            invBindMatrix.M43 = br.ReadSingle();
            invBindMatrix.M44 = br.ReadSingle();

            //Calculate Binding Matrix
            Vector3 BindTranslate, BindScale;
            OpenTK.Quaternion BindRotation = new OpenTK.Quaternion();

            //Get Translate
            BindTranslate.X = br.ReadSingle();
            BindTranslate.Y = br.ReadSingle();
            BindTranslate.Z = br.ReadSingle();
            //Get Quaternion
            BindRotation.X = br.ReadSingle();
            BindRotation.Y = br.ReadSingle();
            BindRotation.Z = br.ReadSingle();
            BindRotation.W = br.ReadSingle();
            //Get Scale
            BindScale.X = br.ReadSingle();
            BindScale.Y = br.ReadSingle();
            BindScale.Z = br.ReadSingle();

            //Generate Matrix
            BindMatrix = Matrix4.CreateScale(BindScale) * Matrix4.CreateFromQuaternion(BindRotation) * Matrix4.CreateTranslation(BindTranslate);

            //Check Results [Except from joint 0, the determinant of the multiplication is always 1,
            // transforms should be good]
            //Console.WriteLine((BindMatrix * invBindMatrix).Determinant);
        }

        
        public float[] convertVec(Vector3 vec)
        {
            float[] fmat = new float[3];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;
            
            return fmat;
        }

        public float[] convertVec(Vector4 vec)
        {
            float[] fmat = new float[4];
            fmat[0] = vec.X;
            fmat[1] = vec.Y;
            fmat[2] = vec.Z;
            fmat[3] = vec.W;

            return fmat;
        }

        public float[] convertMat()
        {
            float[] fmat = new float[16];
            fmat[0] = this.invBindMatrix.M11;
            fmat[1] = this.invBindMatrix.M12;
            fmat[2] = this.invBindMatrix.M13;
            fmat[3] = this.invBindMatrix.M14;
            fmat[4] = this.invBindMatrix.M21;
            fmat[5] = this.invBindMatrix.M22;
            fmat[6] = this.invBindMatrix.M23;
            fmat[7] = this.invBindMatrix.M24;
            fmat[8] = this.invBindMatrix.M31;
            fmat[9] = this.invBindMatrix.M32;
            fmat[10] = this.invBindMatrix.M33;
            fmat[11] = this.invBindMatrix.M34;
            fmat[12] = this.invBindMatrix.M41;
            fmat[13] = this.invBindMatrix.M42;
            fmat[14] = this.invBindMatrix.M43;
            fmat[15] = this.invBindMatrix.M44;

            return fmat;
        }

    }
}