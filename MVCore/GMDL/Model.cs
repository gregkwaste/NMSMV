using System;
using System.Collections.Generic;
using System.ComponentModel;
using OpenTK.Mathematics;
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
        //public List<Model> children = new List<Model>();
        public Dictionary<string, Dictionary<string, Vector3>> palette;
        public bool procFlag; //This is used to define procgen usage

        //True for nodes that belong to an exported scene file. Source attributes which the viewer does
        //not interpret are retained independently; the original TkSceneNodeData is not runtime state.
        public bool IsSceneNode;
        public Dictionary<string, string> SourceAttributes { get; } =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        //The entity file of the node (ATTACHMENT attribute)
        public TkAttachmentData AttachmentTemplate;
        public string AttachmentPath = "";
        //GcColouriseComponentData applies to the node and its descendants. The four entries correspond
        //to the RGBA channels of gColouriseMaskMap; null means there is no local override.
        public Vector4[] colouriseColours;
        //Path of the scene file on the root node of a loaded scene. Scenes that are files of their own
        //(references, LOD models) are not part of their parent's file
        public string SourceFile = "";
        public GLMeshVao meshVao;
        public int instanceId = -1;
            
        //Transformation Parameters
        public Vector3 worldPosition;
        public Matrix4 worldMat;

        //Inverse of worldMat, cached until worldMat changes (instancing asks for it every frame)
        private Matrix4 _worldMatInv = Matrix4.Identity;
        private Matrix4 _worldMatInvSource;
        private bool _worldMatInvValid = false;

        public Matrix4 getWorldMatInv()
        {
            if (!_worldMatInvValid || worldMat != _worldMatInvSource)
            {
                _worldMatInv = worldMat.Inverted();
                _worldMatInvSource = worldMat;
                _worldMatInvValid = true;
            }
            return _worldMatInv;
        }
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
        public List<IComponent> _components = new List<IComponent>();
        public int animComponentID;
        public int animPoseComponentID;
        public int actionComponentID;

        //LOD
        public float[] _LODDistances = new float[5];
        public int _LODNum = 1; //Default value of 1 LOD per model
        public int lodModelCount = 0; //LOD model scenes of a TkLODComponentData under this node, one is shown at a time
        public int lodModelIndex = -1; //This node's position among its parent's LOD models
        public int activeLODModel = 0;
        //Some scene files express whole LOD branches through sibling container names (FooLOD0,
        //FooLOD1, ...), rather than TkLODComponentData. These fields are rebuilt from the hierarchy
        //and allow every descendant type, including lights and VFX, to inherit that selection.
        public int namedLodLevel = -1;
        public uint namedLodAvailableMask = 0;
        public uint namedLodLightAvailableMask = 0;
        public Scene namedLodScene;
        public string namedLodGroupKey;

        //Bounding Volume
        public Vector3 AABBMIN = new Vector3();
        public Vector3 AABBMAX = new Vector3();

        //Offsets from the AnimPoseComponent, kept on top of the animated transforms
        public Vector3 posePosition = Vector3.Zero;
        public Vector3 poseScale = Vector3.One;
        public Quaternion poseRotation = Quaternion.Identity;

        public bool disposed = false;

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(String info)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(info));
        }

        //Properties
        public Vector3 localPosition
        {
            get { return _localPosition; }
            set { _localPosition = value; updated = true; Common.RenderState.instancesDirty = true; }
        }

        public Matrix4 localRotation
        {
            get { return _localRotation; }
            set { _localRotation = value; updated = true; Common.RenderState.instancesDirty = true; }
        }

        public Vector3 localScale
        {
            get { return _localScale; }
            set { _localScale = value; updated = true; Common.RenderState.instancesDirty = true; }
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

        public string ProcGenName { get; set; } = "";
        public int ProcGenId { get; set; } = -1;

        public virtual string Name
        {
            get { return name; }
            set
            {
                string newName = value ?? "";
                if (name == newName)
                    return;

                name = newName;
                nameHash = nameHashOf(name);
                NotifyPropertyChanged(nameof(Name));
                NotifyPropertyChanged(nameof(EditableName));
            }
        }

        //Raw node name for editors. Reference.Name adds load-state text for display, which must not be saved.
        public string EditableName
        {
            get { return name; }
            set { Name = value; }
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
                Common.RenderState.instancesDirty = true;
                foreach (var child in Children)
                    child.IsRenderable = value;
                NotifyPropertyChanged("IsRenderable"); //Make sure to update the UI because of the subsequent changes
            }
        }

        public List<IComponent> Components
        {
            get
            {
                return _components;
            }
        }

        //Only these children are serialized as part of this node's scene file. A loaded reference
        //scene has its own SourceFile and is exported separately.
        public IEnumerable<Model> EmbeddedSceneChildren
        {
            get
            {
                foreach (Model child in Children)
                {
                    if (child.SourceFile == "" && child.IsSceneNode)
                        yield return child;
                }
            }
        }


        //Methods
        public void recalculateAABB()
        {

            //Revert back to the original values
            AABBMIN = meshVao.metaData.AABBMIN;
            AABBMAX = meshVao.metaData.AABBMAX;

            //Generate all 8 points from the AABB (on the stack, this runs for every moved mesh)
            Span<Vector4> vecs = stackalloc Vector4[8]
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

        public abstract Model Clone();

        public virtual void updateLODDistances()
        {
            if (lodModelCount > 0)
                activeLODModel = selectLOD(worldPosition, _LODDistances, lodModelCount, true);

            foreach (Model s in Children)
                s.updateLODDistances();
        }

        //Picks the LOD level of a node: the level forced from the UI, LOD0 when LOD filtering is off,
        //otherwise by camera distance. distances[j] is where level j + 1 starts. Entity lists that begin
        //with 0 hold where each level starts, so they are read from their second entry
        public static int selectLOD(Vector3 position, float[] distances, int count, bool entityDistances)
        {
            count = Math.Max(count, 1);
            if (Common.RenderState.forcedLOD >= 0)
                return Math.Min(Common.RenderState.forcedLOD, count - 1);
            if (!Common.RenderState.renderSettings.LODFiltering || Common.RenderState.activeCam == null)
                return 0;

            //Meshes are drawn with worldMat * rotMat
            Vector3 pos = (new Vector4(position, 1.0f) * Common.RenderState.rotMat).Xyz;
            float distance = (pos - Common.RenderState.activeCam.Position).Length;

            int first = (entityDistances && distances.Length > 0 && distances[0] <= 0.0f) ? 1 : 0;
            for (int j = 0; j < count - 1; j++)
            {
                int k = j + first;
                if (k >= distances.Length || distances[k] <= 0.0f || distance < distances[k])
                    return j;
            }
            return count - 1;
        }

        private bool isNamedLodLevelActive(uint availableMask)
        {
            if (namedLodLevel < 0 || namedLodLevel >= 32 || availableMask == 0)
                return true;

            int requested = Common.RenderState.forcedLOD >= 0
                ? Math.Clamp(Common.RenderState.forcedLOD, 0, 31)
                : Math.Clamp(namedLodScene?.activeLOD ?? 0, 0, 31);
            int selected = requested;
            if ((availableMask & (1u << selected)) == 0)
            {
                selected = -1;
                for (int delta = 1; delta < 32; delta++)
                {
                    int detailed = requested - delta;
                    if (detailed >= 0 && (availableMask & (1u << detailed)) != 0)
                    {
                        selected = detailed;
                        break;
                    }

                    int coarser = requested + delta;
                    if (coarser < 32 && (availableMask & (1u << coarser)) != 0)
                    {
                        selected = coarser;
                        break;
                    }
                }
            }

            return selected < 0 || namedLodLevel == selected;
        }

        public bool isNamedLodBranchActive()
        {
            return isNamedLodLevelActive(namedLodAvailableMask);
        }

        public virtual void setupSkinMatrixArrays()
        {
            foreach (Model s in Children)
                s.setupSkinMatrixArrays();
        }

        //Updates only this node's render data. Derived renderable node types implement this; the recursive
        //method below remains the authoritative hierarchy path for full scene rebuilds.
        public virtual void updateOwnMeshInfo(bool lod_filter = false)
        {
        }

        //A hidden mesh hides its attached children as before. Other nodes retain only the inherited LOD state.
        protected virtual bool hideDescendants(bool lod_filter)
        {
            return lod_filter;
        }

        public void updateMeshInfo(bool lod_filter = false)
        {
            updateOwnMeshInfo(lod_filter);
            bool descendantsHidden = hideDescendants(lod_filter);
            foreach (Model child in Children)
            {
                //The LOD models that are not active are hidden with everything under them
                bool hidden = descendantsHidden ||
                              (child.lodModelIndex >= 0 && child.lodModelIndex != activeLODModel) ||
                              !child.isNamedLodBranchActive();
                child.updateMeshInfo(hidden);
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
                //worldPosition = (Vector4.Transform(new Vector4(0.0f, 0.0f, 0.0f, 1.0f), this.worldMat)).Xyz;
                worldPosition = (new Vector4(0.0f, 0.0f, 0.0f, 1.0f) * worldMat).Xyz;
            }
            else
                worldPosition = localPosition;

            //Trigger the position update of all children nodes
            foreach (Model child in Children)
            {
                child.update();
            }

            updated = true; //Transform changed, trigger mesh updates
        }


        //Finds the first node with this name in this node's own scene. Referenced scenes, LOD models and child
        //scenes are other instances that reuse the same names, so the search does not go into them
        public void findNode(string name, ref Model m)
        {
            m = findNodeInScene(name, true);
        }

        private Model findNodeInScene(string name, bool start)
        {
            if (this.name == name)
                return this;
            if (!start && (this is Scene || this is Reference))
                return null;

            foreach (Model child in Children)
            {
                Model found = child.findNodeInScene(name, false);
                if (found != null)
                    return found;
            }
            return null;
        }


        //Properties for Data Binding
        public ObservableCollection<Model> Children { get; set; } = new ObservableCollection<Model>();
        
            
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
                this.cIndex = this.parent.Children.Count;
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
            _components = new List<IComponent>();
            animComponentID = -1;
            animPoseComponentID = -1;
            actionComponentID = -1;
        }


        protected void copyFrom(Model input)
        {
            this.renderable = input.renderable; //Override Renderability
            this.debuggable = input.debuggable;
            this.selected = 0;
            this.type = input.type;
            this.name = input.name;
            this.nameHash = input.nameHash;
            this.ID = input.ID;

            //Copies are instances of the same file data
            this.IsSceneNode = input.IsSceneNode;
            foreach (KeyValuePair<string, string> attribute in input.SourceAttributes)
                this.SourceAttributes[attribute.Key] = attribute.Value;
            this.AttachmentTemplate = input.AttachmentTemplate;
            this.AttachmentPath = input.AttachmentPath;
            this.SourceFile = input.SourceFile;
            __localPosition = input.__localPosition;
            __localRotation = input.__localRotation;
            __localScale = input.__localScale;
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
            this.lodModelCount = input.lodModelCount;
            this.lodModelIndex = input.lodModelIndex;

            //The local transforms above include the pose offsets
            this.posePosition = input.posePosition;
            this.poseScale = input.poseScale;
            this.poseRotation = input.poseRotation;
            for (int i = 0; i < 5; i++)
                this._LODDistances[i] = input._LODDistances[i];

            //Component Stuff
            this.animComponentID = input.animComponentID;
            this.animPoseComponentID = input.animPoseComponentID;
            this.actionComponentID = input.actionComponentID;
            this.colouriseColours = input.colouriseColours == null
                ? null
                : (Vector4[])input.colouriseColours.Clone();

            //Clone components
            for (int i = 0; i < input.Components.Count; i++)
            {
                IComponent c = input.Components[i].Clone();
                if (c is AnimPoseComponent apc)
                    apc.ref_object = this;
                this.Components.Add(c);
            }
        }

        //Copy Constructor
        protected Model(Model input)
        {
            this.copyFrom(input);
            foreach (GMDL.Model child in input.Children)
            {
                GMDL.Model nChild = child.Clone();
                nChild.parent = this;
                this.Children.Add(nChild);
            }
        }

        //NMSTemplate Export

        //Sets the rest transform of the node (rotation in degrees, as in the scene file). The animation
        //system returns to it and the export writes it
        public void setUserTransform(Vector3 position, Vector3 rotationAngles, Vector3 scale)
        {
            Matrix4 rotx = Matrix4.CreateRotationX(MathUtils.radians(rotationAngles.X));
            Matrix4 roty = Matrix4.CreateRotationY(MathUtils.radians(rotationAngles.Y));
            Matrix4 rotz = Matrix4.CreateRotationZ(MathUtils.radians(rotationAngles.Z));

            __localPosition = position;
            __localRotation = rotz * rotx * roty;
            __localScale = scale;
            _localRotationAngles = rotationAngles;
            localPosition = __localPosition;
            localRotation = __localRotation;
            localScale = __localScale;
            NotifyPropertyChanged("Transform");
        }

        //Viewport tools work with rotation matrices to avoid accumulating Euler-angle error while dragging.
        //The scene format stores Z-X-Y Euler angles in degrees, so convert once at this persistence boundary.
        //invalidateInstances is false during a drag because CGLControl updates the affected stable instance
        //records directly; mouse-up requests one full culling/LOD refresh.
        public void SetUserTransformFromMatrix(Vector3 position, Matrix4 rotation, Vector3 scale,
                                               bool invalidateInstances = true)
        {
            __localPosition = position;
            __localRotation = Matrix4.CreateFromQuaternion(Quaternion.Normalize(rotation.ExtractRotation()));
            __localScale = scale;
            _localRotationAngles = RotationMatrixToSceneAngles(__localRotation);
            if (invalidateInstances)
            {
                localPosition = __localPosition;
                localRotation = __localRotation;
                localScale = __localScale;
            }
            else
            {
                _localPosition = __localPosition;
                _localRotation = __localRotation;
                _localScale = __localScale;
                updated = true;
            }
            NotifyPropertyChanged("Transform");
        }

        private static Vector3 RotationMatrixToSceneAngles(Matrix4 m)
        {
            //Inverse of the ZXY display conversion used by ModelTransform.
            float x = (float)Math.Asin(MathUtils.clamp(m.M32, -1.0f, 1.0f));
            float y, z;
            if (Math.Abs(m.M32) < 0.99999f)
            {
                y = (float)Math.Atan2(-m.M31, m.M33);
                z = (float)Math.Atan2(-m.M12, m.M22);
            }
            else
            {
                y = 0.0f;
                z = (float)Math.Atan2(m.M21, m.M11);
            }
            return new Vector3(-MathUtils.degrees(x), -MathUtils.degrees(y), -MathUtils.degrees(z));
        }

        //NameHash of scene nodes: Jenkins one-at-a-time over the upper case name
        public static uint nameHashOf(string nodeName)
        {
            uint h = 0;
            foreach (byte c in System.Text.Encoding.UTF8.GetBytes(nodeName.ToUpperInvariant()))
            {
                h += c;
                h += h << 10;
                h ^= h >> 6;
            }
            h += h << 3;
            h ^= h >> 11;
            h += h << 15;
            return h;
        }

        public bool CanExportScene => IsSceneNode;
        public bool HasEntity => AttachmentTemplate != null;
        public bool CanAddChildren => SceneEdit.canAdd(this);
        //Geometry import is currently offered on scene roots; imported nodes use the normal editable
        //scene-node classes and templates.
        public bool CanImportGeometry => this is Scene;
        public bool CanRemove => SceneEdit.canRemove(this, out _);

        //The entity file of the node with the viewer's edits
        public TkAttachmentData ExportAttachmentTemplate()
        {
            return AttachmentTemplate;
        }

        #region ComponentQueries
        public int hasComponent(Type ComponentType)
        {
            for (int i = 0; i < _components.Count; i++)
            {
                IComponent temp = _components[i];
                if (temp.GetType() == ComponentType)
                    return i;
            }

            return -1;
        }

        #endregion


        public void AddChild(Model child)
        {
            Children.Add(child);
            child.parent = this;
            child.inheritColourise(colouriseColours);
            NotifyPropertyChanged("Children");
        }

        private void inheritColourise(Vector4[] inherited)
        {
            Vector4[] active = colouriseColours ?? inherited;
            if (colouriseColours == null && inherited != null)
                colouriseColours = inherited;
            foreach (Model child in Children)
                child.inheritColourise(active);
        }


        #region AnimationComponent

        public virtual void setParentScene(Scene scene)
        {
            parentScene = scene;
            foreach (Model child in Children)
            {
                child.setParentScene(scene);
            }
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
                //Free other resources here
                if (Children != null)
                    foreach (Model c in Children) c.Dispose();
                //Do not clear the observable collection during disposal. Scene teardown now runs on
                //the GL thread; notifying a WPF CollectionView from that thread is illegal. Once the
                //disposed root is removed from the resource manager, the whole graph is collectible.

                //Free textureManager
            }

            //Free unmanaged resources

            disposed = true;
        }



    }

}
