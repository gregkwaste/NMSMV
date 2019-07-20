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
//using Matrix4 = MathNet.Numerics.LinearAlgebra.Matrix<float>;


namespace MVCore.GMDL
{
    public enum RENDERTYPE
    {
        MAIN = 0x0,
        BHULL,
        DEBUG,
        PICK,
        UNKNOWN
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
        public abstract bool render(RENDERTYPE pass);
        public bool renderable;
        public bool debuggable;
        public int selected;
        public GLSLHelper.GLSLShaderConfig[] shader_programs;
        public int ID;
        public TYPES type;
        public string name;
        public ObservableCollection<model> children = new ObservableCollection<model>();
        public Dictionary<string, Dictionary<string, Vector3>> palette;
        public bool procFlag; //This is used to define procgen usage
        public TkSceneNodeData nms_template;
        
        //Transformation Parameters
        public Vector3 worldPosition;
        public Matrix4 worldMat;
        public Matrix4 localMat;

        public Vector3 _localPosition;
        public Vector3 _localScale;
        public Vector3 _localRotationAngles;
        public Matrix4 _localRotation;
        public Matrix4 _localPoseMatrix;

        public model parent;
        public int cIndex = 0;
        public bool changed = true; //Making it public just for the joints

        //Components
        public List<Component> _components;
        public int animComponentID;
        public int animPoseComponentID;

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
            set { _localPosition = value; changed = true; }
        }

        public Matrix4 localRotation
        {
            get { return _localRotation; }
            set { _localRotation = value; changed = true; }
        }

        public Vector3 localScale
        {
            get { return _localScale; }
            set { _localScale = value; changed = true; }
        }

        public void updateRotationFromAngles(float x, float y, float z)
        {
            
        }

        public string Name
        {
            get { return name; }
        }
        public string Type
        {
            get { return type.ToString(); }
        }

        public bool IsRenderable
        {
            get
            {
                return renderable;
            }

            set
            {
                renderable = value;
                foreach (var child in Children)
                    child.IsRenderable = value;
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


        public abstract model Clone();

        public virtual void update()
        {

            if (changed)
            {
                //Create scaling matrix
                Matrix4 scale = Matrix4.Identity;
                scale[0, 0] = _localScale.X;
                scale[1, 1] = _localScale.Y;
                scale[2, 2] = _localScale.Z;

                localMat = _localPoseMatrix * scale * _localRotation * Matrix4.CreateTranslation(_localPosition);

                changed = false;
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
        }

        //Properties for Data Binding
        public ObservableCollection<model> Children{
            get
            {
                return children;
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
            selected = 0;
            ID = -1;
            name = "";
            procFlag = false;    //This is used to define procgen usage
        
            //Transformation Parameters
            worldPosition = new Vector3(0.0f, 0.0f, 0.0f);
            worldMat = Matrix4.Identity;
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
            this.shader_programs = input.shader_programs;
            this.type = input.type;
            this.name = input.name;
            this.ID = input.ID;
            this.cIndex = input.cIndex;
            //Clone transformation
            _localPosition = input._localPosition;
            _localRotationAngles = input._localRotationAngles;
            _localRotation = input._localRotation;
            _localScale = input._localScale;
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

        public void resetPose()
        {
            if (animComponentID < 0)
                return;

            AnimComponent ac = _components[animComponentID] as AnimComponent;

            foreach (Joint j in ac.jointDict.Values)
                j.localPoseMatrix = Matrix4.Identity;
        }

        //TODO Add button in the UI to toggle that shit
        private void resetAnimation()
        {
            if (animComponentID < 0)
                return;

            AnimComponent ac = _components[animComponentID] as AnimComponent;

            foreach (Joint j in ac.jointDict.Values)
            {
                j._localScale = j.BindMat.ExtractScale();
                j._localRotation = Matrix4.CreateFromQuaternion(j.BindMat.ExtractRotation());
                j._localPosition = j.BindMat.ExtractTranslation();
                j._localPoseMatrix = Matrix4.Identity;
            }
        }

        #endregion

        #region AnimPoseComponent
        //TODO: It would be nice if I didn't have to do make the method public, but it needs a lot of work on the 
        //AnimPoseComponent class to temporarily store the selected pose frames, while also in the model.update method

        //Locator Animation Stuff
        public void loadPose()
        {
            if (animPoseComponentID < 0)
                return;
            if (animComponentID < 0)
                return;

            AnimPoseComponent apc = _components[animPoseComponentID] as AnimPoseComponent;
            AnimComponent ac = _components[animComponentID] as AnimComponent;

            foreach (TkAnimNodeData node in apc._poseFrameData.NodeData)
            {
                if (!ac.jointDict.ContainsKey(node.Node))
                    continue;

                //We should interpolate frame shit over all the selected Pose Data
                List<Matrix4> framePoseMatrices = new List<Matrix4>();
                List<float> framePoseMatricesNorms = new List<float>();
                List<int> IDs = new List<int>();
                for (int i = 0; i < apc._poseData.Count; i++)
                {
                    //Get Pose Frame
                    int poseFrameIndex = apc._poseData[i].PActivePoseFrame;

                    Vector3 v_t, v_s;
                    Quaternion lq;
                    //Fetch Rotation Quaternion
                    lq = NMSUtils.fetchRotQuaternion(node, apc._poseFrameData, poseFrameIndex);
                    v_t = NMSUtils.fetchTransVector(node, apc._poseFrameData, poseFrameIndex);
                    v_s = NMSUtils.fetchScaleVector(node, apc._poseFrameData, poseFrameIndex);

                    //Generate Transformation Matrix
                    Matrix4 poseMat = Matrix4.CreateScale(v_s) * Matrix4.CreateFromQuaternion(lq) * Matrix4.CreateTranslation(v_t);

                    framePoseMatricesNorms.Add(MathUtils.Matrix4Norm(poseMat, ac.jointDict[node.Node].BindMat));
                    framePoseMatrices.Add(poseMat);
                    IDs.Add(i);
                }

                //Do some sorting
                IDs = IDs.OrderBy(d => framePoseMatricesNorms[d]).ToList();

                //Keep just the last matrix
                Matrix4 framePoseMatrix = framePoseMatrices[IDs[IDs.Count - 1]];


                if (framePoseMatrices.Count == 0)
                {
                    ac.jointDict[node.Node].localPoseMatrix = Matrix4.Identity;
                }
                else
                {
                    /* Weight multiple matrices
                    //Calculate frame weight
                    float w_m = 1.0f / framePoseMatrices.Count;

                    //Accumulate transforms
                    Matrix4 finalPoseMat = Matrix4.Zero;
                    for (int i = 0; i < framePoseMatrices.Count; i++)
                        finalPoseMat += framePoseMatrices[i] * w_m;

                    */
                    //Use just the selected matrix
                    Matrix4 finalPoseMat = framePoseMatrix;

                    ac.jointDict[node.Node].localPoseMatrix = finalPoseMat * ac.jointDict[node.Node].invBMat;
                }
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

    public class scene : locator
    {
        public GeomObject gobject; //Keep GeomObject reference
        public textureManager texMgr;
        
        public scene() : base(0.1f) {
            type = TYPES.MODEL;
            texMgr = new textureManager();
            //Set Shader Program
            shader_programs = new GLSLHelper.GLSLShaderConfig[]{Common.RenderState.activeResMgr.GLShaders["LOCATOR_SHADER"],
                                        Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                                        Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};
        }

        public scene(scene input) :base(input)
        {
            gobject = input.gobject;
        }      

        public void copyFrom(scene input)
        {
            base.copyFrom(input); //Copy base stuff
            this.gobject = input.gobject;
            
        }

        public override model Clone()
        {
            return new scene(this);
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

    public class locator: model
    {
        int vao_id;
        public float scale;
        
        //Animation Stuff
        
        //Default Constructor
        public locator(float s)
        {
            //Set type
            type = TYPES.LOCATOR;
            //Assemble geometry in the constructor
            //X
            scale = s;
            vao_id = MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_cross"].vao_id;
            //Add shaders
            shader_programs = new GLSLHelper.GLSLShaderConfig[] { MVCore.Common.RenderState.activeResMgr.GLShaders["LOCATOR_SHADER"],
                MVCore.Common.RenderState.activeResMgr.GLShaders["DEBUG_SHADER"],
                MVCore.Common.RenderState.activeResMgr.GLShaders["PICKING_SHADER"]};

        }

        public void copyFrom(locator input)
        {
            base.copyFrom(input); //Copy stuff from base class

            this.scale = input.scale;
            this.vao_id = input.vao_id;

        }

        protected locator(locator input) : base(input)
        {
            this.copyFrom(input);
        }

        public override GMDL.model Clone()
        {
            return new locator(this);
        }


        #region IDisposable Support
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    vao_id = -1; //VAO will be deleted from the resource manager since it is a common mesh
                    shader_programs = null;
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }

        
        #endregion

        private void renderMain(GLSLHelper.GLSLShaderConfig shader)
        {
            //Console.WriteLine("Rendering Locator {0}", this.name);
            //Console.WriteLine("Rendering VBO Object here");
            //VBO RENDERING
            GL.UseProgram(shader.program_id);

            //Upload scale

            GL.Uniform1(shader.uniformLocations["scale"], scale);

            GL.BindVertexArray(vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Lines, 6, DrawElementsType.UnsignedInt, (IntPtr) 0);
            //GL.PolygonMode(MaterialFace.FrontAndBack, MVCore.RenderOptions.RENDERMODE);
            GL.BindVertexArray(0);

        }


        public override bool render(RENDERTYPE pass)
        {

            GLSLHelper.GLSLShaderConfig shader = shader_programs[(int)pass];

            switch (pass)
            {
                case 0:
                    renderMain(shader);
                    break;
                default:
                    break;
            }
            

            return true;
        }

        
    }

    //Place holder struct for all rendered meshes
    public class mainVAO : IDisposable
    {
        public int rendermode;
        //VAO IDs
        public int vao_id;
        //VBO IDs
        public int vertex_buffer_object;
        public int element_buffer_object;

        public void mainVao() {
            //Generate Empty Vao
            vao_id = -1;
            vertex_buffer_object = -1;
            element_buffer_object = -1;
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
                    GL.DeleteVertexArray(vao_id);
                    GL.DeleteBuffer(vertex_buffer_object);
                    GL.DeleteBuffer(element_buffer_object);
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

    public class meshModel : model
    {
        public int vertrstart_physics = 0;
        public int vertrend_physics = 0;
        public int vertrstart_graphics = 0;
        public int vertrend_graphics = 0;
        public int batchstart_physics = 0;
        public int batchstart_graphics = 0;
        public int batchcount = 0;
        public int firstskinmat = 0;
        public int lastskinmat = 0;
        //New stuff Properties
        public int lod_level = 0;
        public int boundhullstart = 0;
        public int boundhullend = 0;
        public Vector3[] Bbox;
        public DrawElementsType indicesLength = DrawElementsType.UnsignedShort;

        public Material Material {get; set;}
        public Vector3 color = new Vector3();

        public int skinned = 0;
        public ulong hash = 0xFFFFFFFF;
        //Accurate boneRemap
        public int BoneRemapIndicesCount;
        public int[] BoneRemapIndices;
        public float[] BoneRemapMatrices = new float[16 * 128];
        public mainVAO main_Vao;
        public mainVAO debug_Vao;
        public mainVAO pick_Vao;
        public mainVAO bsh_Vao;
        public mainVAO bhull_Vao;
        public GeomObject gobject; //Ref to the geometry shit
        public model animScene; //Ref to connected animScene

        //Constructor
        public meshModel()
        {
            type = TYPES.MESH;
        }

        public meshModel(meshModel input) :base(input)
        {
            //Copy attributes
            this.vertrstart_graphics = input.vertrstart_graphics;
            this.vertrstart_physics = input.vertrstart_physics;
            this.vertrend_graphics = input.vertrend_graphics;
            this.vertrend_physics = input.vertrend_physics;
            //Render Tris
            this.batchcount = input.batchcount;
            this.batchstart_graphics = input.batchstart_graphics;
            this.batchstart_physics = input.batchstart_physics;

            //Bound Hulls
            this.boundhullstart = input.boundhullstart;
            this.boundhullend = input.boundhullend;
            this.Bbox = input.Bbox;
            
            //Skinning Stuff
            this.firstskinmat = input.firstskinmat;
            this.lastskinmat = input.lastskinmat;
            this.BoneRemapMatrices = input.BoneRemapMatrices;
            this.BoneRemapIndices = input.BoneRemapIndices;
            this.skinned = input.skinned;

            this.main_Vao = input.main_Vao;
            this.debug_Vao = input.debug_Vao;
            this.pick_Vao = input.pick_Vao;

            //Material Stuff
            this.color = input.color;
            if (input.Material.name_key != "")
            {
                this.Material = MVCore.Common.RenderState.activeResMgr.GLmaterials[input.Material.name_key]; // Keep reference
                //this.material = input.material.Clone(); //Clone material
            }
            else
                this.Material = new Material();
                
            this.palette = input.palette;
            this.gobject = input.gobject; //Leave geometry file intact, no need to copy anything here
        }

        public override model Clone()
        {
            return new meshModel(this);
        }


        //BSphere calculator
        public void setupBSphere()
        {
            //For now just setup the Bounding Sphere VBO
            Vector4 bsh_center = (new Vector4((Bbox[0] + Bbox[1])));
            bsh_center = 0.5f * bsh_center;
            bsh_center.W = 1.0f;

            float radius = (0.5f * (Bbox[1] - Bbox[0])).Length;

            //Create Sphere vbo
            bsh_Vao = new Primitives.Sphere(bsh_center.Xyz, radius).getVAO();
        }

        public void renderBbox(int pass)
        {
            GL.UseProgram(pass);

            Vector4[] tr_AABB = new Vector4[2];
            tr_AABB[0] = new Vector4(Bbox[0], 1.0f) * worldMat;
            tr_AABB[1] = new Vector4(Bbox[1], 1.0f) * worldMat;

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[0].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[0].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[0].Z,

                                           tr_AABB[0].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[0].Y, tr_AABB[1].Z,
                                           tr_AABB[0].X, tr_AABB[1].Y, tr_AABB[1].Z,
                                           tr_AABB[1].X, tr_AABB[1].Y, tr_AABB[1].Z };


            float [] verts = new float[] { Bbox[0].X, Bbox[0].Y, Bbox[0].Z,
                                           Bbox[1].X, Bbox[0].Y, Bbox[0].Z,
                                           Bbox[0].X, Bbox[1].Y, Bbox[0].Z,
                                           Bbox[1].X, Bbox[1].Y, Bbox[0].Z,

                                           Bbox[0].X, Bbox[0].Y, Bbox[1].Z,
                                           Bbox[1].X, Bbox[0].Y, Bbox[1].Z,
                                           Bbox[0].X, Bbox[1].Y, Bbox[1].Z,
                                           Bbox[1].X, Bbox[1].Y, Bbox[1].Z };


            float[] colors = new float[] { color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z,
                                                color.X,color.Y,color.Z};

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
            //Add vert color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, colors);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(Int32) * indices.Length), indices, BufferUsageHint.StaticDraw);


            //Render

            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //InverseBind Matrices
            int loc;
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Reset
            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            GL.Uniform3(loc, this.color);

            //Program changed so I ahve to reupload the model matrices
            loc = GL.GetUniformLocation(pass, "worldMat");
            Matrix4 wMat = Matrix4.Identity;
            GL.UniformMatrix4(loc, false, ref wMat);

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts.Length,
                indices.Length, DrawElementsType.UnsignedInt , IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

        }

        public void renderBSphere(int pass)
        {
            GL.UseProgram(pass);

            //Step 1 Upload uniform variables
            int loc;

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            GL.Uniform3(loc, 1.0f, 1.0f, 1.0f);

            //Program changed so I ahve to reupload the model matrices
            loc = GL.GetUniformLocation(pass, "worldMat");
            Matrix4 wMat = worldMat;
            GL.UniformMatrix4(loc, false, ref wMat);

            //Step 2 Bind & Render Vao
            //Render Bounding Sphere
            GL.BindVertexArray(bsh_Vao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Triangles, 600, DrawElementsType.UnsignedInt, (IntPtr)0);

            GL.BindVertexArray(0);
        }

        public virtual void renderMain(GLSLHelper.GLSLShaderConfig shader)
        {
            GL.UseProgram(shader.program_id);

            //Upload Material Information
            
            //Step 1 Upload uniform variables
            GL.Uniform1(shader.uniformLocations["mpCustomPerMaterial.matflags[0]"], 64, Material.material_flags); //Upload Material Flags
            
            //Upload Custom Per Material Uniforms
            foreach (Uniform un in Material.CustomPerMaterialUniforms.Values)
            {
                if (shader.uniformLocations.Keys.Contains(un.Name))
                    GL.Uniform4(shader.uniformLocations[un.Name], un.vec.Vec);
            }
            
            //BIND TEXTURES
            //Diffuse Texture

            /*
            if (Material.material_flags[(int) TkMaterialFlags.MaterialFlagEnum._F55_] > 0.0f)
            {
                //Upload depth : gUserVecData
                GL.Uniform4(216, new Vector4(0.0f));
            }
            */
            
            foreach (Sampler s in Material.PSamplers.Values)
            {
                if (shader.uniformLocations.ContainsKey(s.Name) && s.Map != "")
                {
                    GL.Uniform1(shader.uniformLocations[s.Name], MyTextureUnit.MapTexUnitToSampler[s.Name]);
                    GL.ActiveTexture(s.texUnit.texUnit);
                    GL.BindTexture(s.tex.target, s.tex.bufferID);
                }
            }
            
            //Step 2 Bind & Render Vao
            //Render Elements
            GL.BindVertexArray(main_Vao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, Common.RenderOptions.RENDERMODE);
            GL.DrawElements(PrimitiveType.Triangles, batchcount, indicesLength, IntPtr.Zero);
            GL.BindVertexArray(0);
        }

        private void renderBHull(GLSLHelper.GLSLShaderConfig shader) {
            GL.UseProgram(shader.program_id);


            GL.Uniform1(shader.uniformLocations["scale"], 1.0f);

            //I ASSUME THAT EVERYTHING I NEED IS ALREADY UPLODED FROM A PREVIOUS PASS
            GL.PointSize(10.0f);
            GL.BindVertexArray(bhull_Vao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.DrawElementsBaseVertex(PrimitiveType.Points, batchcount,
                        indicesLength, IntPtr.Zero, -vertrstart_physics);
            GL.DrawElementsBaseVertex(PrimitiveType.Triangles, batchcount,
                        indicesLength, IntPtr.Zero, -vertrstart_physics);
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

            for (int i = 0; i < Material.Flags.Count; i++)
                GL.Uniform1(loc + (int) Material.Flags[i].MaterialFlag, 1.0f);

            //Upload BoneRemap Information
            loc = GL.GetUniformLocation(pass, "boneRemap");
            GL.Uniform1(loc, BoneRemapMatrices.Length, BoneRemapMatrices);

            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel
            /*
            Util.mulMatArrays(ref skinMats, gobject.invBMats, scene.JMArray, 256);
            loc = GL.GetUniformLocation(pass, "skinMats");
            GL.UniformMatrix4(loc, 256, false, skinMats);
            */

            //Step 2: Render VAO
            GL.BindVertexArray(main_Vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles, batchcount, DrawElementsType.UnsignedShort, (IntPtr)0);
            GL.BindVertexArray(0);
        }

        public override bool render(RENDERTYPE pass)
        {
            GLSLHelper.GLSLShaderConfig shader = shader_programs[(int) pass];

            //Render Object
            switch (pass)
            {
                //Render Main
                case RENDERTYPE.MAIN:
                    renderMain(shader);
                    //renderBSphere(MVCore.Common.RenderState.activeResMgr.GLShaders["BBOX_SHADER"]);
                    //renderBbox(MVCore.Common.RenderState.activeResMgr.GLShaders["BBOX_SHADER"]);
                    break;
                //Render Bound Hull
                case RENDERTYPE.BHULL:
                    renderBHull(shader);
                    break;
                //Render Debug
                case RENDERTYPE.DEBUG:
                    renderDebug(shader.program_id);
                    break;
                //Render for Picking
                case RENDERTYPE.PICK:
                    renderDebug(shader.program_id);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }
            
            return true;
        }

        public override void update()
        {
            if (skinned > 0)
            {
                AnimComponent ac = animScene.Components[animScene.animComponentID] as AnimComponent;
                //Update the mesh remap matrices and continue with the transform updates
                for (int i = 0; i < BoneRemapIndicesCount; i++)
                {
                    Array.Copy(ac.skinMats, BoneRemapIndices[i] * 16, BoneRemapMatrices, i * 16, 16);
                }
            }
            
            base.update();
        }

        public void writeGeomToStream(StreamWriter s, ref uint index)
        {
            //For testing DO NOT EXPORT COLLISION OBJECTS
            if (this.type == TYPES.COLLISION) return;
            
            int vertcount = this.vertrend_graphics - this.vertrstart_graphics + 1;
            MemoryStream vms = new MemoryStream(gobject.meshDataDict[hash].vs_buffer);
            MemoryStream ims = new MemoryStream(gobject.meshDataDict[hash].is_buffer);
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
            
            vbr.BaseStream.Seek(Math.Max(gobject.offsets[1], 0) + gobject.vx_size * vertrstart_graphics, SeekOrigin.Begin);
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
            for (int i = 0; i < batchcount/3; i++)
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
            index += (uint) vertcount;
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
                    //vbo.Dispose(); I assume the the vbo's will be cleared with Resourcegmt cleanup
                    BoneRemapIndices = null;
                    BoneRemapMatrices = null;
                    //Dispose GL Stuff
                    main_Vao?.Dispose();
                    debug_Vao?.Dispose();
                    pick_Vao?.Dispose();
                    bsh_Vao?.Dispose();

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
        //[FieldOffset(356)] //16 bytes
        //public Vector4 gUserDataVec4; MOVE TO COMMON PER MESH

        public static readonly int SizeInBytes = 356;
    };

    public class Collision : meshModel
    {
        public COLLISIONTYPES collisionType;
        
        //Custom constructor
        public Collision()
        {
            this.skinned = 0; //Collision objects are not skinned (at least for now)
            this.color = new Vector3(1.0f, 1.0f, 0.0f); //Set Yellow Color for collision objects
        }

        public override model Clone()
        {
            return new Collision(this);
        }

        protected Collision(Collision input) : base(input)
        {
            collisionType = input.collisionType;
        }

        public override bool render(RENDERTYPE pass)
        {
            GLSLHelper.GLSLShaderConfig shader = shader_programs[(int) pass];

            if (this.main_Vao == null)
            {
                //Console.WriteLine("Not Renderable");
                return false;
            }

            int program;
            //Render Object
            switch (pass)
            {
                //Render Main
                case RENDERTYPE.MAIN:
                    renderMain(shader);
                    break;
                //Render Debug
                case RENDERTYPE.DEBUG:
                    renderDebug(shader.program_id);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        public override void renderMain(GLSLHelper.GLSLShaderConfig shader)
        {
            //Console.WriteLine(this.name + this);
            GL.UseProgram(shader.program_id);

            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            
            GL.Uniform1(11, 64, Material.material_flags); //Upload Material Flags
            //Upload Color Flag

            GL.Uniform3(209, color);

            //Step 2: Render Elements
            GL.PointSize(10.0f);
            GL.BindVertexArray(main_Vao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            switch (collisionType)
            {
                //Rendering based on the original mesh buffers
                case COLLISIONTYPES.MESH:
                    GL.DrawElementsBaseVertex(PrimitiveType.Points, batchcount,
                        indicesLength, IntPtr.Zero, -vertrstart_physics);
                    GL.DrawElementsBaseVertex(PrimitiveType.Triangles, batchcount,
                        indicesLength, IntPtr.Zero, -vertrstart_physics);
                    break;
                
                //Rendering custom geometry
                case COLLISIONTYPES.BOX:
                case COLLISIONTYPES.CYLINDER:
                case COLLISIONTYPES.CAPSULE:
                case COLLISIONTYPES.SPHERE:
                    GL.DrawElements(PrimitiveType.Triangles, batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero);
                    break;

            }
            
            GL.BindVertexArray(0);
        }

        public override void renderDebug(int pass)
        {
            GL.UseProgram(pass);

            //Render Elements
            GL.BindVertexArray(main_Vao.vao_id);
            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, batchcount,
               DrawElementsType.UnsignedShort, IntPtr.Zero);
            GL.BindVertexArray(0);
        }

        
    }

    public class Decal : meshModel
    {
        //Custom constructor
        public Decal() { }

        public Decal(meshModel input):base(input) { }
        public Decal(Decal input) : base(input) { }

        
        public override bool render(RENDERTYPE pass)
        {
            GLSLHelper.GLSLShaderConfig shader = shader_programs[(int)pass];
            if (this.main_Vao == null)
            {
                //Console.WriteLine("Not Renderable");
                return false;
            }

            return false;//Skip decal rendering for now

            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    renderMain(shader);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        public override void renderMain(GLSLHelper.GLSLShaderConfig shader)
        {
            //Console.WriteLine(this.name + this);
            GL.UseProgram(shader.program_id);

            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(shader.program_id, "matflags");

            if (loc > 0) 
            {
                for (int i = 0; i < 64; i++)
                    GL.Uniform1(loc + i, 0.0f);

                for (int i = 0; i < Material.Flags.Count; i++)
                    GL.Uniform1(loc + (int) Material.Flags[i].MaterialFlag, 1.0f);
            }
            

            //Upload decalTexture
            //BIND TEXTURES
            int tex0Id = (int)TextureUnit.Texture0;
            //Diffuse Texture
            string test = "decalTex";
            loc = GL.GetUniformLocation(shader.program_id, test);
            GL.Uniform1(loc, 0); // I need to upload the texture unit number

            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(Material.PSamplers["diffuseMap"].tex.target, Material.PSamplers["diffuseMap"].tex.bufferID);


            //TODO: Upload the gbuffer depth texture from outside when rendering decal meshes
            ////Depth Texture
            //test = "depthTex";
            //loc = GL.GetUniformLocation(pass, test);
            //GL.Uniform1(loc, 1); // I need to upload the texture unit number

            //GL.ActiveTexture((TextureUnit) (tex0Id + 1));
            //GL.BindTexture(TextureTarget.Texture2D, MVCore.Common.RenderState.gbuf.dump_pos);

            //Util.gbuf.dump();

            //Step 2: Render Elements
            GL.PointSize(5.0f);
            GL.BindVertexArray(main_Vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles, batchcount,
               DrawElementsType.UnsignedShort, IntPtr.Zero);
            GL.BindVertexArray(0);
        }

        public override void renderDebug(int pass)
        {
            GL.UseProgram(pass);
            //Step 1: Upload Uniforms

            //Step 2: Render Elements
            GL.PointSize(5.0f);
            GL.BindVertexArray(main_Vao.vao_id);
            GL.DrawElements(PrimitiveType.Triangles, batchcount,
               DrawElementsType.UnsignedShort, IntPtr.Zero);
            GL.BindVertexArray(0);

        }

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
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
        public Dictionary<ulong, meshMetaData> meshMetaDataDict = new Dictionary<ulong, meshMetaData>();
        public Dictionary<ulong, meshData> meshDataDict = new Dictionary<ulong, meshData>();
        
        //Joint info
        public List<JointBindingData> jointData = new List<JointBindingData>();
        public float[] invBMats = new float[256 * 16];
        
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



        //Fetch main VAO
        public mainVAO getMainVao(meshModel so)
        {
            
            
            //Make sure that the hash exists in the dictionary
            if (so.hash == 0xFFFFFFFF)
                throw new System.Collections.Generic.KeyNotFoundException("Invalid Mesh Hash");

            if (MVCore.Common.RenderState.activeResMgr.GLVaos.ContainsKey(so.hash))
                return MVCore.Common.RenderState.activeResMgr.GLVaos[so.hash];
            
            
            mainVAO vao = new mainVAO();

            //Generate VAO
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
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) meshMetaDataDict[so.hash].vs_size,
                meshDataDict[so.hash].vs_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != vx_size * (so.vertrend_graphics + 1))
                throw new ApplicationException(String.Format("Problem with vertex buffer"));

            MVCore.Common.RenderStats.vertNum += so.vertrend_graphics + 1; //Accumulate settings

            //Assign VertexAttribPointers
            for (int i = 0; i < 7; i++)
            {
                if (this.bufInfo[i] == null) continue;
                bufInfo buf = this.bufInfo[i];
                GL.VertexAttribPointer(i, buf.count, buf.type, false, this.vx_size, buf.stride);
                GL.EnableVertexAttribArray(i);
            }

            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, vao.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) meshMetaDataDict[so.hash].is_size, 
                meshDataDict[so.hash].is_buffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ElementArrayBuffer, BufferParameterName.BufferSize,
                out size);
            Console.WriteLine(GL.GetError());
            if (size != meshMetaDataDict[so.hash].is_size)
                throw new ApplicationException(String.Format("Problem with vertex buffer"));


            //Calculate indiceslength per index buffer
            int indicesLength = (int)meshMetaDataDict[so.hash].is_size / so.batchcount;

            switch (indicesLength)
            {
                case 1:
                    so.indicesLength = DrawElementsType.UnsignedByte;
                    break;
                case 2:
                    so.indicesLength = DrawElementsType.UnsignedShort;
                    break;
                case 4:
                    so.indicesLength = DrawElementsType.UnsignedInt;
                    break;
            }

            MVCore.Common.RenderStats.trisNum += (int) (so.batchcount / 3); //Accumulate settings

            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
        }

        public mainVAO getCollisionMeshVao(meshModel so)
        {
            //Collision Mesh isn't used anywhere else.
            //No need to check for hashes and shit

            float[] vx_buffer_float = new float[(so.boundhullend - so.boundhullstart) * 3];

            for (int i = 0; i < so.boundhullend - so.boundhullstart; i++)
            {
                Vector3 v = so.gobject.bhullverts[i + so.boundhullstart];
                vx_buffer_float[3 * i + 0] = v.X;
                vx_buffer_float[3 * i + 1] = v.Y;
                vx_buffer_float[3 * i + 2] = v.Z;
            }

            //Generate intermediate geom
            GMDL.GeomObject temp_geom = new GMDL.GeomObject();

            //Set main Geometry Info
            temp_geom.vertCount = vx_buffer_float.Length / 3;
            temp_geom.indicesCount = so.batchcount;
            temp_geom.indicesLength = so.gobject.indicesLength; 

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
            temp_geom.bufInfo[0] = new GMDL.bufInfo(0, VertexAttribPointerType.Float, 3, 0, "vPosition");
            temp_geom.bufInfo[2] = new GMDL.bufInfo(2, VertexAttribPointerType.Float, 3, 0, "nPosition");

            //Set Buffers
            temp_geom.ibuffer = new byte[temp_geom.indicesLength * so.batchcount];
            temp_geom.vbuffer = new byte[sizeof(float) * vx_buffer_float.Length];

            System.Buffer.BlockCopy(so.gobject.ibuffer, so.batchstart_physics * temp_geom.indicesLength, temp_geom.ibuffer, 0, temp_geom.ibuffer.Length);
            System.Buffer.BlockCopy(vx_buffer_float, 0, temp_geom.vbuffer, 0, temp_geom.vbuffer.Length);

            return temp_geom.getMainVao();
        }

        public mainVAO getMainVao()
        {
            //This method works with custom vbuffer and ibuffer
            //Used mostly from Primitive Object classes
            mainVAO vao = new mainVAO();

            //Generate VAO
            vao.vao_id = GL.GenVertexArray();
            GL.BindVertexArray(vao.vao_id);

            //Generate VBOs
            int[] vbo_buffers = new int[2];
            GL.GenBuffers(2, vbo_buffers);

            vao.vertex_buffer_object = vbo_buffers[0];
            vao.element_buffer_object = vbo_buffers[1];

            if (GL.GetError() != ErrorCode.NoError)
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
                    foreach (KeyValuePair<ulong, meshMetaData> pair in meshMetaDataDict)
                        meshDataDict[pair.Key] = null;

                    meshDataDict.Clear();
                    meshMetaDataDict.Clear();

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

        public bufInfo(int sem,VertexAttribPointerType typ, int c, int s, string t)
        {
            semantic = sem;
            type = typ;
            count = c;
            stride = s;
            sem_text = t;
        }
    }


    public class Sampler: TkMaterialSampler, IDisposable
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
                case "mpCustomPerMaterial.gMasksMap":
                case "mpCustomPerMaterial.gNormalMap":
                    prepTextures();
                    break;
                default:
                    MVCore.Common.CallBacks.Log("Not sure how to handle Sampler " + Name);
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
                texMbin = Path.GetFullPath(Path.Combine(FileUtils.dirpath, texMbin));

                //Detect Procedural Texture
                if (File.Exists(texMbin))
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
            Console.WriteLine("Trying to load Texture");

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

        public static void setupTextureParameters(int texture, int wrapMode, int magFilter, int minFilter, float af_amount)
        {
            GL.BindTexture(TextureTarget.Texture2D, texture);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, wrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, wrapMode);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, magFilter);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, minFilter);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);

            //Use anisotropic filtering
            af_amount = Math.Max(af_amount, GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy));
            GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)0x84FE, af_amount);
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
            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(path);
            mbinf.Load();
            TkMaterialData template = (TkMaterialData)mbinf.GetData();
            mbinf.Dispose();

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
            MVCore.Common.CallBacks.Log("Material Flags: ");
            
            foreach (TkMaterialFlags f in Flags)
            {
                material_flags[(int) f.MaterialFlag] = 1.0f;
                MVCore.Common.CallBacks.Log(((TkMaterialFlags.MaterialFlagEnum)f.MaterialFlag).ToString() + " ");
            }

            //Get Uniforms
            foreach (TkMaterialUniform un in Uniforms)
            {
                Uniform my_un = new Uniform(un);
                CustomPerMaterialUniforms[my_un.name] = my_un;
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
                
            MVCore.Common.CallBacks.Log("\n");
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

    }
    
    public class Uniform
    {
        public string name;
        public MVector4 vec;

        public Uniform()
        {
            name = "";
            vec = new MVector4(0.0f);
        }

        public Uniform(TkMaterialUniform un)
        {
            name = "mpCustomPerMaterial." + un.Name;
            vec = new MVector4(un.Values.x, un.Values.y, un.Values.z, un.Values.t);
        }

        public string Name
        {
            get { return name; }
            
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

    public class MVector4
    {
        private Vector4 vec4;

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
            set { vec4 = value; }
        }
        public float X
        {
            get { return vec4.X; }
            set { vec4.X = value; }
        }
        public float Y
        {
            get { return vec4.Y; }
            set { vec4.Y = value; }
        }

        public float Z
        {
            get { return vec4.Z; }
            set { vec4.Z = value; }
        }

        public float W
        {
            get { return vec4.W; }
            set { vec4.W = value; }
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
            mbinPath = Path.GetFullPath(Path.Combine(FileUtils.dirpath, mbinPath));

            prepareTextures(texMgr, mbinPath);

            //Init framebuffer
            int tex_width = 0;
            int tex_height = 0;
            int fbo_tex = -1;
            int fbo = -1;
            
            bool fbo_status = setupFrameBuffer(ref fbo, ref fbo_tex, ref tex_width, ref tex_height);

            if (!fbo_status)
            {
                MVCore.Common.CallBacks.Log("Unable to mix textures, probably 0x0 textures...\n");
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
            string texMbin = Path.GetFullPath(Path.Combine(FileUtils.dirpath, path));

            Console.WriteLine("Procedural Texture Detected: " + texMbin);
            MVCore.Common.CallBacks.Log(string.Format("Parsing Procedural Texture"));

            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(texMbin);
            mbinf.Load();
            TkProceduralTextureList template = (TkProceduralTextureList)mbinf.GetData();
            mbinf.Dispose();


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
                        MVCore.Common.CallBacks.Log(string.Format("Diffuse Texture {0} Not Found", partNameDiff));
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
                    string pathMask = Path.Combine(FileUtils.dirpath, partNameMask);
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
                        Console.WriteLine("Mask Texture " + pathMask + " Not Found");
                        MVCore.Common.CallBacks.Log(string.Format("Mask Texture {0} Not Found", pathMask));
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
                    string pathNormal = Path.Combine(FileUtils.dirpath, partNameNormal);

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
                        Console.WriteLine("Normal Texture " + pathNormal + " Not Found");
                        MVCore.Common.CallBacks.Log(string.Format("Normal Texture {0} Not Found", pathNormal));
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
            Console.WriteLine(GL.GetError());
            Sampler.setupTextureParameters(fbo_tex, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);
            Console.WriteLine(GL.GetError());

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
            int pass_program = Common.RenderState.activeResMgr.GLShaders["TEXTURE_MIXING_SHADER"].program_id;
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
                    GL.BindTexture(tex.target, tex.bufferID);
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

            GL.BindVertexArray(MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_diffuse = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(out_tex_2darray_diffuse, (int)TextureWrapMode.Repeat,
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
            new_tex.bufferID = out_tex_2darray_diffuse;
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
            int pass_program = Common.RenderState.activeResMgr.GLShaders["TEXTURE_MIXING_SHADER"].program_id;
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
                    GL.BindTexture(tex.target, tex.bufferID);
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
                    GL.BindTexture(tex.target, tex.bufferID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);

            
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
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
            new_tex.bufferID = out_tex_2darray_mask;
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
            int pass_program = Common.RenderState.activeResMgr.GLShaders["TEXTURE_MIXING_SHADER"].program_id;
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
                    GL.BindTexture(tex.target, tex.bufferID);
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
                    GL.BindTexture(tex.target, tex.bufferID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);


            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
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
            new_tex.bufferID = out_tex_2darray_mask;
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
        public int bufferID = -1;
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
        public Texture(string path)
        {
            DDSImage ddsImage;
            name = path;

            path = Path.Combine(FileUtils.dirpath, path);
            if (!File.Exists(path))
            {
                //throw new System.IO.FileNotFoundException();
                Console.WriteLine("Texture {0} Missing. Using default.dds", path);
                path = "default.dds";
            }
            
            ddsImage = new DDSImage(File.ReadAllBytes(path));


            MVCore.Common.RenderStats.texturesNum += 1; //Accumulate settings

            Console.WriteLine("Sampler Name Path " + path + " Width {0} Height {1}", ddsImage.header.dwWidth, ddsImage.header.dwHeight);
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
            int mm_count = Math.Min(7, ddsImage.header.dwMipMapCount); //Load only 7 or less mipmaps
            int depth_count = Math.Max(1, ddsImage.header.dwDepth); //Fix the counter to 1 to fit the texture in a 3D container
            int temp_size = ddsImage.header.dwPitchOrLinearSize;

            //Upload to GPU
            bufferID = GL.GenTexture();
            target = TextureTarget.Texture2DArray;
            
            GL.BindTexture(target, bufferID);
            
            //When manually loading mipmaps, levels should be loaded first
            GL.TexParameter(target, TextureParameterName.TextureBaseLevel, 0);
            GL.TexParameter(target, TextureParameterName.TextureMaxLevel, mm_count - 1);
            
            int offset = 0;
            for (int i=0; i < mm_count; i++)
            {
                byte[] tex_data = new byte[temp_size * depth_count];
                Array.Copy(ddsImage.bdata, offset, tex_data, 0, temp_size * depth_count);
                GL.CompressedTexImage3D(target, i, pif, w, h, depth_count, 0, temp_size * depth_count, tex_data);
                
                //GL.TexImage3D(target, i, PixelInternalFormat.Rgba8, w, h, depth_count, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
                //Console.WriteLine(GL.GetError());

                offset += temp_size * depth_count;
                temp_size = Math.Max(temp_size/4, blocksize);
                w = Math.Max(w >> 1, 4);
                h = Math.Max(h >> 1, 4);
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
                if (bufferID != -1) GL.DeleteTexture(bufferID);
                //Free other resources here
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

            int rmask = 0x1F << 11;
            int gmask = 0x3F << 5;
            int bmask = 0x1F;
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

            long temp;

        }

    }

    public class Joint : locator
    {
        public mainVAO main_Vao;
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
            set { _localPoseMatrix = value; changed = true; }
        }

        public Joint() :base(0.1f)
        {
            type = TYPES.JOINT;   
        }

        protected Joint(Joint input) : base(input)
        {
            this.main_Vao = input.main_Vao;
            this.jointIndex = input.jointIndex;
            this.color = input.color;
        }

        public override void update()
        {
            base.update(); 

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

            GL.BindVertexArray(main_Vao.vao_id);
            GL.BindBuffer(BufferTarget.ArrayBuffer, main_Vao.vertex_buffer_object);
            //Add verts data, color data should stay the same
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
        }

        public override model Clone()
        {
            return new Joint(this);
        }

        

        //Render should render Bones from joint to children
        private void renderMain(int pass)
        {
            GL.UseProgram(pass);

            GL.BindVertexArray(main_Vao.vao_id);
            GL.PointSize(5.0f);
            GL.DrawArrays(PrimitiveType.Lines, 0, 2 * children.Count);
            GL.DrawArrays(PrimitiveType.Points, 0, 1); //Draw only yourself
            GL.BindVertexArray(0);

        }

        public override bool render(RENDERTYPE pass)
        {
            
            if (this.children.Count == 0)
                return false;

            int program;
            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    program = shader_programs[(int) pass].program_id;
                    renderMain(program);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        //DIsposal
        protected override void Dispose(bool disposing)
        {
            //Dispose GL Stuff
            main_Vao?.Dispose();
            base.Dispose(disposing);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct GLLight
    {
        public Vector4 position;
        public Vector3 color;
        public Vector3 ambient;
        public float intensity;
        public float specular;
    }
    
    public class Light : model
    {
        //I should expand the light properties here
        public Vector3 color = new Vector3(1.0f);
        public Vector3 ambient = new Vector3(0.2f);
        public float intensity = 1.0f;
        public float specular = 0.5f;

        private int vertex_buffer_object;
        private int element_buffer_object;

        public Light()
        {
            type = TYPES.LIGHT;
            GL.GenBuffers(1, out vertex_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);
            if (GL.GetError() != ErrorCode.NoError)
                Console.WriteLine(GL.GetError());
        }

        protected Light(Light input) : base(input)
        {
            color = input.color;
            ambient = input.ambient;
            intensity = input.intensity;
            specular = input.specular;
            vertex_buffer_object = input.vertex_buffer_object;
            element_buffer_object = input.element_buffer_object;
        }

        public GLLight getStruct()
        {
            GLLight s = new GLLight();
            s.position = new Vector4(worldPosition, 1.0f); //For now we're switching to directional lights
            s.ambient = ambient;
            s.color = color;
            s.intensity = intensity;
            s.specular = specular;
            return s;
        }

        public override model Clone()
        {
            return new Light(this);
        }

        private void renderMain(int pass)
        {
            GL.UseProgram(pass);

            //Draw Single Points
            float[] vertsf = new float[3];
            int[] indices = new int[1];
            indices[0] = 0;
            
            vertsf[0] = this.worldPosition.X;
            vertsf[1] = this.worldPosition.Y;
            vertsf[2] = this.worldPosition.Z;

            int arraysize = 3 * sizeof(float);
            
            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, vertsf);
            
            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * indices.Length), indices, BufferUsageHint.StaticDraw);

            //Render Immediately
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.PointSize(10.0f);

            GL.DrawArrays(PrimitiveType.Points, 0, indices.Length);

            //Draw only Joint Point
            GL.DisableVertexAttribArray(0);
        }

        public override bool render(RENDERTYPE pass)
        {
            int program = shader_programs[(int) pass].program_id;

            switch (pass)
            {
                case 0:
                    renderMain(program);
                    break;
                default:
                    break;      
            }

            return true;
        }

        //Disposal
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                GL.DeleteBuffer(vertex_buffer_object);
                GL.DeleteBuffer(element_buffer_object);
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
    public class Component : IDisposable
    {

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
        //Joint Data
        public float[] JMArray;
        public float[] skinMats; //Final Matrices
        public float[] invBMats; //Joint Inverse Bound Matrices
        public Dictionary<string, Joint> jointDict;
        public Dictionary<string, model> modelDict; //This will probably replace the jointDict since it seems like animations contain data for all nodes

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
            //Init Animation Stuff
            JMArray = new float[256 * 16];
            skinMats = new float[256 * 16];
            invBMats = new float[256 * 16];
            modelDict = new Dictionary<string, model>();
            jointDict = new Dictionary<string, Joint>();
        }

        public AnimComponent(TkAnimationComponentData data)
        {
            //Init Animation Stuff
            JMArray = new float[256 * 16];
            skinMats = new float[256 * 16];
            invBMats = new float[256 * 16];
            modelDict = new Dictionary<string, model>();
            jointDict = new Dictionary<string, Joint>();

            //Load Animations
            _animations.Add(new AnimData(data.Idle)); //Add Idle Animation
            for (int i = 0; i < data.Anims.Count; i++)
            {
                AnimData my_ad = new AnimData(data.Anims[i]);
                _animations.Add(my_ad);
            }
        }

        public void copyFrom(AnimComponent input)
        {
            //Base class is dummy
            //base.copyFrom(input); //Copy stuff from base class

            //ANIMATION DATA
            modelDict = new Dictionary<string, model>();
            jointDict = new Dictionary<string, Joint>();
            JMArray = (float[])input.JMArray.Clone();

            //TODO: Copy Animations
            
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
                    skinMats = null;
                    invBMats = null;
                    JMArray = null;

                    modelDict.Clear();
                    jointDict.Clear();

                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
        
        #endregion

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
        public AnimPoseComponent(TkAnimPoseComponentData apcd)
        {
            _poseFrameData = (TkAnimMetadata) NMSUtils.LoadNMSFile(Path.GetFullPath(Path.Combine(FileUtils.dirpath, apcd.Filename)));

            //Load PoseAnims
            for (int i = 0; i < apcd.PoseAnims.Count; i++)
            {
                AnimPoseData my_apd = new AnimPoseData(apcd.PoseAnims[i]);
                poseData.Add(my_apd);
            }
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
                apc.ref_object.loadPose();
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
                apc.ref_object.resetPose();
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


    }

    public class AnimData : TkAnimationData
    {
        public TkAnimMetadata animMeta;
        public int frameCounter = 0;
        public bool _animationToggle = false;
        
        //Constructors
        public AnimData(TkAnimationData ad){
            Anim = ad.Anim; 
            Filename = ad.Filename;
            FrameStart = ad.FrameStart;
            FrameEnd = ad.FrameEnd;
            StartNode = ad.StartNode;
            AnimType = ad.AnimType;

            //Load Animation File
            if (Filename != "")
                loadAnimation();
        }
        
        //Properties

        public string PName
        {
            get { return Anim; }
            set { Anim = value; }
        }

        public bool AnimationToggle
        {
            get { return _animationToggle; }
            set { _animationToggle = value; animate(); }
        }

        private void loadAnimation()
        {
            animMeta = NMSUtils.LoadNMSFile(Path.GetFullPath(Path.Combine(FileUtils.dirpath, Filename))) as TkAnimMetadata;
        }


        //Methods
        //Animation frame data collection methods
        public Quaternion fetchRotQuaternion(TkAnimNodeData node)
        {
            return NMSUtils.fetchRotQuaternion(node, animMeta, frameCounter);
        }

        public Vector3 fetchTransVector(TkAnimNodeData node)
        {
            return NMSUtils.fetchTransVector(node, animMeta, frameCounter);
        }


        public Vector3 fetchScaleVector(TkAnimNodeData node)
        {
            return NMSUtils.fetchScaleVector(node, animMeta, frameCounter);
        }


        public void animate()
        {
            frameCounter += 1;
            if (frameCounter >= animMeta.FrameCount - 1)
                frameCounter = 0;
        }

    }




        
    public class JointBindingData
    {
        public Matrix4 invBindMatrix = Matrix4.Identity;
        public Matrix4 BindMatrix = Matrix4.Identity;

        public void Load(FileStream fs)
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
            Quaternion BindRotation = new Quaternion();

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