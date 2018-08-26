using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using OpenTK.Graphics.OpenGL;
using OpenTK;
using KUtility;
using Model_Viewer;
using System.Linq;
using System.Net.Mime;
using System.Xml;
using libMBIN.Models.Structs;
using System.Reflection;
using MVCore;
using ExtTextureFilterAnisotropic = OpenTK.Graphics.ES30.ExtTextureFilterAnisotropic;
using PixelFormat = OpenTK.Graphics.OpenGL.PixelFormat;

namespace MVCore.GMDL
{
    public abstract class model: IDisposable
    {
        public abstract bool render(int pass);
        public abstract model Clone(scene scn);
        public scene scene;
        public TkSceneNodeData mbin_scene;
        public GeomObject gobject;
        public GLControl pcontrol;
        public bool renderable = true;
        public bool debuggable = false;
        public int selected = 0;
        public int[] shader_programs;
        public int ID;
        public TYPES type;
        public string name = "";
        public Material material;
        public List<model> children = new List<model>();
        public Dictionary<string, Dictionary<string, Vector3>> palette;
        public bool procFlag = false; //This is used to define procgen usage

        //Transformation Parameters
        public Vector3 worldPosition {
            get
            {
                if (parent != null)
                {
                    //Original working
                    //return parent.worldPosition + Vector3.Transform(this.localPosition, parent.worldMat);
                    
                    //Add Translation as well
                    return (Vector4.Transform(new Vector4(0.0f,0.0f,0.0f,1.0f), this.worldMat)).Xyz;
                }
                    
                else
                    return this.localPosition;
            }
        }
        public Matrix4 worldMat
        {
            get
            {
                if (parent != null)
                {
                    //Original working
                    return this.localMat * parent.worldMat;
                    //return this.localMat;
                }

                else
                    return this.localMat;
            }
        }
        public Matrix4 localMat
        {
            get
            {
                //Combine localRotation and Position to return the localMatrix
                Matrix4 rot = Matrix4.Identity;
                rot.M11 = localRotation.M11;
                rot.M12 = localRotation.M12;
                rot.M13 = localRotation.M13;
                rot.M21 = localRotation.M21;
                rot.M22 = localRotation.M22;
                rot.M23 = localRotation.M23;
                rot.M31 = localRotation.M31;
                rot.M32 = localRotation.M32;
                rot.M33 = localRotation.M33;
                //Create scaling matrix
                Matrix4 scale = Matrix4.Identity;
                scale[0, 0] = localScale[0];
                scale[1, 1] = localScale[1];
                scale[2, 2] = localScale[2];

                return scale * rot * Matrix4.CreateTranslation(localPosition);
            }
        }
        public Vector3 localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 localScale = new Vector3(1.0f, 1.0f, 1.0f);
        //public Vector3 localRotation = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 localRotationAngles = new Vector3(0.0f, 0.0f, 0.0f);
        public Matrix3 localRotation = Matrix3.Identity;
        public Vector3[] Bbox = new Vector3[2];

        public model parent;
        public int cIndex = 0;
        //Disposable Stuff
        public bool disposed = false;
        public Microsoft.Win32.SafeHandles.SafeFileHandle handle = new Microsoft.Win32.SafeHandles.SafeFileHandle(IntPtr.Zero, true);

        public static void vectofloatArray(float[] flist, List<Vector3> veclist)
        {
            int count = veclist.Count;
            for (int i = 0; i < count; i++)
            {
                flist[3 * i] = veclist[i].X;
                flist[3 * i+1] = veclist[i].Y;
                flist[3 * i+2] = veclist[i].Z;
            }   
        }

        public void init(float[] trans)
        {
            //Get Local Position
            Vector3 rotation;
            this.localPosition.X = trans[0];
            this.localPosition.Y = trans[1];
            this.localPosition.Z = trans[2];


            //using (System.IO.StreamWriter file =
            //new System.IO.StreamWriter(@"readtransformsGMDL.txt", true))
            //{
            //    file.WriteLine(String.Join(" ", new string[] { this.localPosition.X.ToString(),this.localPosition.Y.ToString(),this.localPosition.Z.ToString(),"INPUTS:",split[0],split[1],split[2]}));
            //}

            //Get Local Rotation
            //Quaternion qx = Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f),
            //    (float)Math.PI * float.Parse(split[3]) / 180.0f);
            //Quaternion qy = Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f),
            //    (float)Math.PI * float.Parse(split[4]) / 180.0f);
            //Quaternion qz = Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f),
            //    (float)Math.PI * float.Parse(split[5]) / 180.0f);

            Quaternion qx = Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f), trans[3]);
            Quaternion qy = Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f), trans[4]);
            Quaternion qz = Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f), trans[5]);

            //this.localRotation = qz * qx * qy;
            rotation.X = trans[3];
            rotation.Y = trans[4];
            rotation.Z = trans[5];

            localRotationAngles = rotation;

            //Get Local Scale
            this.localScale.X = trans[6];
            this.localScale.Y = trans[7];
            this.localScale.Z = trans[8];

            //Now Calculate the joint matrix;

            Matrix3 rotx, roty, rotz;
            Matrix3.CreateRotationX(MathUtils.radians(rotation.X), out rotx);
            Matrix3.CreateRotationY(MathUtils.radians(rotation.Y), out roty);
            Matrix3.CreateRotationZ(MathUtils.radians(rotation.Z), out rotz);
            //Matrix4.CreateTranslation(ref this.localPosition, out transM);
            //Calculate local matrix
            this.localRotation = rotz * rotx * roty;

            //this.localMat = rotz * rotx * roty * transM;

            //Set paths
            if (parent!=null)
                this.cIndex = this.parent.children.Count;
        }

        public void TakeValuesFrom(GMDL.model input)
        {
            this.renderable = true; //Override Renderability
            this.shader_programs = input.shader_programs;
            this.type = input.type;
            this.name = input.name;
            this.ID = input.ID;
            this.cIndex = input.cIndex;
            //Clone transformation
            this.localPosition = input.localPosition;
            this.localRotation = input.localRotation;
            this.localScale = input.localScale;
            
            foreach (GMDL.model child in input.children)
            {
                GMDL.model nChild = child.Clone(this.scene);
                nChild.parent = this;
                this.children.Add(nChild);
            }
        }

        public List<int> hpath()
        {
            List<int> list = new List<int>();

            list.Insert(0,cIndex); //Add current index
            GMDL.model recparent = parent;

            while (recparent != null)
            {
                list.Insert(0, recparent.cIndex);
                recparent = recparent.parent;
            }
                
            return list;
        }


        

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

        public void delete()
        {
            if (parent != null)
                parent.children.Remove(this);
        }

    }

    //public interface model{
    //    bool render();
    //    GMDL.model Clone();
    //    bool Renderable { get; set; }
    //    int ShaderProgram { set; get; }
    //    int Index { set; get; }
    //    string Type { set; get; }
    //    string Name { set; get; }
    //    GMDL.Material material { set; get; }
    //    List<model> Children { set; get; }
    //};
    
    public class scene : locator
    {
        public scene() : base(0.1f) { }
        public override model Clone(scene scn)
        {
            GMDL.scene copy = new GMDL.scene();
            copy.TakeValuesFrom(this);
            copy.scene = scn; //Explicitly assign scene

            //ANIMATION DATA
            copy.jointDict = new Dictionary<string, model>();
            copy.JMArray = (float[]) this.JMArray.Clone();
            foreach (GMDL.Joint j in this.jointDict.Values)
                copy.jointDict[j.name] = (GMDL.Joint) j.Clone(copy);

            //When cloning scene objects the scene has the scn arguments as its parent scene
            //BUT children will have this copy as their scene
            //Clone Children as well

            return copy;
        }


        //Animation Stuff
        public float[] JMArray = new float[256 * 16];

        //public List<GMDL.Joint> jointModel = new List<GMDL.Joint>(); The dict should be more than enough
        public Dictionary<string, model> jointDict = new Dictionary<string, model>();
        public TkAnimMetadata animMeta = null;
        public int frameCounter = 0;

        public void animate()
        {
            //Console.WriteLine("Setting Frame Index {0}", frameIndex);
            TkAnimNodeFrameData frame = animMeta.AnimFrameData[frameCounter];
            TkAnimNodeFrameData stillframe = animMeta.StillFrameData;

            foreach (TkAnimNodeData node in animMeta.NodeData)
            {
                if (jointDict.ContainsKey(node.Node))
                {
                    //Console.WriteLine("Frame {0} Node {1} RotationIndex {2}", frameCounter, node.Node, node.RotIndex);

                    //Load Rotations
                    UInt16 c_x, c_y, c_z;
                    UInt16 i_x, i_y, i_z;

                    //Check if there is a rotation for that node
                    if (node.RotIndex < frame.Rotations.Count / 3)
                    {
                        int rotindex = node.RotIndex;
                        c_x = (UInt16) frame.Rotations[3 * rotindex + 0];
                        c_y = (UInt16) frame.Rotations[3 * rotindex + 1];
                        c_z = (UInt16) frame.Rotations[3 * rotindex + 2];
                        
                    } else //Load stillframedata
                    {
                        int rotindex = node.RotIndex - frame.Rotations.Count / 3;
                        c_x = (UInt16)stillframe.Rotations[3 * rotindex + 0];
                        c_y = (UInt16)stillframe.Rotations[3 * rotindex + 1];
                        c_z = (UInt16)stillframe.Rotations[3 * rotindex + 2];
                    }

                    i_x = (UInt16)(c_x >> 15);
                    i_y = (UInt16)(c_y >> 15);
                    i_z = (UInt16)(c_z >> 15);
                    
                    ushort axisflag = (ushort) (i_x << 0 | i_y << 1 | i_z << 2);

                    //Mask Values
                    c_x = (UInt16) (c_x & 0x7FFF);
                    c_y = (UInt16) (c_y & 0x7FFF);
                    c_z = (UInt16) (c_z & 0x7FFF);

                    float norm = 1.0f / 0x3FFF;
                    float scale = 1.0f / (float) Math.Sqrt(2.0f);

                    float[] values = new float[4];
                    values[0] = ((float)(c_x - 0x3FFF)) * norm * scale;
                    values[1] = ((float)(c_y - 0x3FFF)) * norm * scale;
                    values[2] = ((float)(c_z - 0x3FFF)) * norm * scale;
                    //I assume that W is positive by default
                    values[3] = (float)Math.Sqrt(Math.Max(1.0f - values[0] * values[0] - values[1] * values[1] - values[2] * values[2], 0.0));

                    //Quaternion oq = Quaternion.FromMatrix(jointDict[node.Node].localRotation);
                    Quaternion q = new Quaternion();

                    switch (axisflag)
                    {
                        case 3:
                            q = new Quaternion(values[3], values[0], values[1], values[2]);
                            break;
                        case 2:
                            q = new Quaternion(values[0], values[1], values[3], values[2]);
                            break;
                        case 1:
                            q = new Quaternion(values[0], values[3], values[1], values[2]);
                            break;
                        case 0:
                            q = new Quaternion(values[0], values[1], values[2], values[3]);
                            break;
                        default:
                            break;
                    }

                    /*
                    if (axisflag == 1)
                    {
                        //Console.WriteLine("New Values   masked {0:X} {1:X} {2:X}", c_x, c_y, c_z);
                        Console.WriteLine("Old Quaternion {0} {1} {2} {3}", oq.X, oq.Y, oq.Z, oq.W);
                        Console.WriteLine("New Quaternion {0} {1} {2} {3}", q.X, q.Y, q.Z, q.W);
                        Console.WriteLine("Break");
                    }
                    */
                    
                    Matrix3 nMat = Matrix3.CreateFromQuaternion(q);
                    jointDict[node.Node].localRotation = nMat;
                    
                    //Load Translations
                    if (node.TransIndex < frame.Translations.Count)
                    {
                        jointDict[node.Node].localPosition.X = frame.Translations[node.TransIndex].x;
                        jointDict[node.Node].localPosition.Y = frame.Translations[node.TransIndex].y;
                        jointDict[node.Node].localPosition.Z = frame.Translations[node.TransIndex].z;
                    }
                    else //Load stillframedata
                    {
                        int transindex = node.TransIndex - frame.Translations.Count;
                        jointDict[node.Node].localPosition.X = stillframe.Translations[transindex].x;
                        jointDict[node.Node].localPosition.Y = stillframe.Translations[transindex].y;
                        jointDict[node.Node].localPosition.Z = stillframe.Translations[transindex].z;
                    }

                    //Load Scaling - TODO
                    //Load Translations
                    if (node.ScaleIndex < frame.Scales.Count)
                    {
                        jointDict[node.Node].localScale.X = frame.Scales[node.ScaleIndex].x;
                        jointDict[node.Node].localScale.Y = frame.Scales[node.ScaleIndex].y;
                        jointDict[node.Node].localScale.Z = frame.Scales[node.ScaleIndex].z;
                    }
                    else //Load stillframedata
                    {
                        int scaleindex = node.ScaleIndex - frame.Scales.Count;
                        jointDict[node.Node].localScale.X = stillframe.Scales[scaleindex].x;
                        jointDict[node.Node].localScale.Y = stillframe.Scales[scaleindex].y;
                        jointDict[node.Node].localScale.Z = stillframe.Scales[scaleindex].z;
                    }

                }
                //Console.WriteLine("Node " + node.name+ " {0} {1} {2}",node.rotIndex,node.transIndex,node.scaleIndex);
            }

            frameCounter += 1;
            if (frameCounter >= animMeta.FrameCount - 1)
                frameCounter = 0;
        }

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                JMArray = null;
                jointDict = null;
                animMeta = null;
                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }

    }

    public class locator: model
    {
        //public bool renderable = true;
        int vao_id;
        public float scale;
        
        //Default Constructor
        public locator(float s)
        {
            //Set type
            //this.type = "LOCATOR";
            //Assemble geometry in the constructor
            //X
            scale = s;
            vao_id = MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_cross"].vao_id;
        }

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            vao_id = -1;
            base.Dispose(disposing);
        }
        
        private void renderMain(int pass)
        {
            //Console.WriteLine("Rendering Locator {0}", this.name);
            //Console.WriteLine("Rendering VBO Object here");
            //VBO RENDERING
            GL.UseProgram(pass);

            //Upload scale

            int loc = GL.GetUniformLocation(pass, "scale");
            GL.Uniform1(loc, scale);

            GL.BindVertexArray(vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);
            GL.DrawElements(PrimitiveType.Lines, 6, DrawElementsType.UnsignedInt, (IntPtr) 0);
            GL.PolygonMode(MaterialFace.FrontAndBack, MVCore.RenderOptions.RENDERMODE);
            GL.BindVertexArray(0);

        }


        public override bool render(int pass)
        {

            int program = this.shader_programs[pass];

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

        public override GMDL.model Clone(GMDL.scene scn)
        {
            GMDL.locator copy = new GMDL.locator(0.01f);
            copy.TakeValuesFrom(this);
            copy.scene = scn;
            return (GMDL.model) copy;
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
        public int small_vertex_buffer_object;
        public int element_buffer_object;

        public void mainVao() { }

        public void Dispose()
        {
            GL.DeleteVertexArray(vao_id);
            GL.DeleteBuffer(vertex_buffer_object);
            GL.DeleteBuffer(small_vertex_buffer_object);
            GL.DeleteBuffer(element_buffer_object);
        }

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
        
        public int skinned = 1;
        public ulong hash = 0xFFFFFFFF;
        //Accurate boneRemap
        public int[] BoneRemap;
        public mainVAO main_Vao;
        public mainVAO debug_Vao;
        public mainVAO pick_Vao;
        public mainVAO bsh_Vao;

        public Vector3 color = new Vector3();
        //public bool renderable = true;
        //public int shader_program = -1;
        //public int index;
        
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
            int arraysize = sizeof(float) * verts.Length;
            int vb_bbox, eb_bbox;
            GL.GenBuffers(1, out vb_bbox);
            GL.GenBuffers(1, out eb_bbox);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vb_bbox);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2*arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
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
            for (int i = 0; i < 64; i++)
            {
                loc = GL.GetUniformLocation(pass, "matflags[" + i.ToString() + "]");
                GL.Uniform1(loc, 0.0f);
            }

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            GL.Uniform3(loc, this.color);
            

            //Upload Light Flag
            loc = GL.GetUniformLocation(pass, "use_lighting");
            GL.Uniform1(loc, 0.0f);

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

        public virtual void renderMain(int pass)
        {
            GL.UseProgram(pass);

            //Step 1 Upload uniform variables
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");
            //loc = 11;

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            for (int i = 0; i < material.materialflags.Count; i++)
                GL.Uniform1(loc + (int) material.materialflags[i], 1.0f);

            //Upload Material Uniforms
            for (int i = 0; i < material.uniforms.Count; i++)
            {
                Uniform un = material.uniforms[i];
                loc = GL.GetUniformLocation(pass, un.name);
                GL.Uniform4(loc, un.value.X, un.value.Y, un.value.Z, un.value.W);
            }
            
            //Upload joint transform data
            //Multiply matrices before sending them
            //Check if scene has the jointModel

            if (skinned > 0.0f)
            {
                float[] remapped = new float[128 * 16];
                for (int i=0; i<BoneRemap.Length; i++)
                {
                    Array.Copy(gobject.skinMats, BoneRemap[i] * 16, remapped, i*16, 16);
                }
                
                GL.UniformMatrix4(78, 128, false, remapped);
            }
            
            //Upload Light Flag
            //loc = GL.GetUniformLocation(pass, "useLighting");
            //GL.Uniform1(loc, 1.0f);

            //BIND TEXTURES
            int tex0Id = (int)TextureUnit.Texture0;
            //Diffuse Texture
            GL.Uniform1(75, 0); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit)(tex0Id + 0));
            GL.BindTexture(TextureTarget.Texture2D, material.fDiffuseMap.bufferID);

            //Mask Texture
            GL.Uniform1(76, 1); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit)(tex0Id + 1));
            GL.BindTexture(TextureTarget.Texture2D, material.fMaskMap.bufferID);

            //Normal Texture
            GL.Uniform1(77, 2); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit)(tex0Id + 2));
            GL.BindTexture(TextureTarget.Texture2D, material.fNormalMap.bufferID);


            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            //NEW WAY OF TEXTURE BINDING
            //If there are samples defined, there are diffuse textures for sure

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            GL.Uniform3(loc, this.color);


            //Step 2 Bind & Render Vao
            //Render Elements
            GL.BindVertexArray(main_Vao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, RenderOptions.RENDERMODE);
            GL.DrawElements(PrimitiveType.Triangles, batchcount, DrawElementsType.UnsignedShort, (IntPtr) 0);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
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

            for (int i = 0; i < material.materialflags.Count; i++)
                GL.Uniform1(loc + (int) material.materialflags[i], 1.0f);

            //Upload BoneRemap Information
            loc = GL.GetUniformLocation(pass, "boneRemap");
            GL.Uniform1(loc, BoneRemap.Length, BoneRemap);

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

        public override bool render(int pass)
        {
            int program = this.shader_programs[pass];

            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    //renderBsphere(program);
                    //renderBbox(program);
                    renderMain(program);
                    break;
                //Render Debug
                case 1:
                    renderDebug(program);
                    break;
                //Render for Picking
                case 2:
                    renderDebug(program);
                    break;
                default:
                    //Do nothing in any other case
                    break;
            }
            
            return true;
        }

        public override GMDL.model Clone(GMDL.scene scn)
        {
            GMDL.meshModel copy = new GMDL.meshModel();
            copy.TakeValuesFrom(this);
            copy.vertrstart_graphics = this.vertrstart_graphics;
            copy.vertrstart_physics = this.vertrstart_physics;
            copy.vertrend_graphics = this.vertrend_graphics;
            copy.vertrend_physics = this.vertrend_physics;
            //Skinning Stuff
            copy.firstskinmat = this.firstskinmat;
            copy.lastskinmat = this.lastskinmat;
            copy.BoneRemap = this.BoneRemap;
            copy.skinned = this.skinned;

            copy.main_Vao = main_Vao;
            copy.debug_Vao = debug_Vao;
            copy.pick_Vao = pick_Vao;
            
            //Render Tris
            copy.batchcount = this.batchcount;
            copy.batchstart_graphics = this.batchstart_graphics;
            copy.batchstart_physics = this.batchstart_physics;
            //Bound Hulls
            copy.boundhullstart = this.boundhullstart;
            copy.boundhullend = this.boundhullend;
            copy.color = this.color;
            if (this.material != null)
                copy.material = this.material.Clone();
            copy.palette = this.palette;
            
            //In sharedVBO objects, both this and all the children have the same scene
            copy.scene = scn;
            copy.gobject = gobject; //Leave geometry file intact, no need to copy anything here
            
            return (GMDL.model)copy;
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
                        uint v1, v2, v3, v4;
                        v1 = vbr.ReadUInt16();
                        v2 = vbr.ReadUInt16();
                        v3 = vbr.ReadUInt16();
                        vN = new Vector4(Half.decompress(v1), Half.decompress(v2), Half.decompress(v3), 1.0f);
                        n_section_bytes = 6;
                        break;
                    case (VertexAttribPointerType.Int2101010Rev):
                        int i1, i2, i3, i4;
                        uint value;
                        byte[] a32 = new byte[4];
                        a32 = vbr.ReadBytes(4);

                        //Big Endian
                        //Array.Reverse(a32);
                        value =  BitConverter.ToUInt32(a32, 0);
                        //Convert Values
                        //i1 = Convert.ToInt32((value >> 00) & 0x3FF);
                        i1 = _2sComplement.toInt((value >> 00) & 0x3FF, 10);
                        //i2 = Convert.ToInt32((value >> 10) & 0x3FF);
                        i2 = _2sComplement.toInt((value >> 10) & 0x3FF, 10);
                        //i3 = Convert.ToInt32((value >> 20) & 0x3FF);
                        i3 = _2sComplement.toInt((value >> 20) & 0x3FF, 10);
                        //i4 = Convert.ToInt32((value >> 30) & 0x3FF);
                        i4 = _2sComplement.toInt((value >> 30) & 0x3FF, 10);

                        //Convert Values
                        //i4 = _2sComplement.toInt((value >> 00) & 0x003, 02);
                        //i3 = _2sComplement.toInt((value >> 02) & 0x3FF, 10);
                        //i2 = _2sComplement.toInt((value >> 12) & 0x3FF, 10);
                        //i1 = _2sComplement.toInt((value >> 22) & 0x3FF, 10);
                        //Debug.WriteLine("{0}, {1}, {2}", i1, i2, i3);

                        vN = new Vector4(Convert.ToSingle(i1) / 512.0f,
                                         Convert.ToSingle(i2) / 512.0f,
                                         Convert.ToSingle(i3) / 512.0f,
                                         1.0f);
                        //(Convert.ToSingle(v4) - 1.5f) / 1.5f);
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
                float uv1, uv2, uv3;
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

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            //if (material != null) material.Dispose();
            //NOTE: No need to dispose material, because the materials reside in the resource manager
            //vbo.Dispose(); I assume the the vbo's will be cleared with Resourcegmt cleanup
            BoneRemap = null;
            //Dispose GL Stuff
            main_Vao?.Dispose();
            base.Dispose(disposing);
        }

    }

    public class Collision : meshModel
    {
        public COLLISIONTYPES collisionType;

        //Custom constructor
        public Collision()
        {
            this.skinned = 0; //Collision objects are not skinned (at least for now)
            this.color = new Vector3(1.0f, 1.0f, 0.0f); //Set Yellow Color for collision objects
        }

        public override bool render(int pass)
        {
            if (this.main_Vao == null || RenderOptions.RenderCollisions == false)
            {
                //Console.WriteLine("Not Renderable");
                return false;
            }

            int program;
            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    program = this.shader_programs[pass];
                    renderMain(program);
                    break;
                //Render Debug
                case 1:
                    program = this.shader_programs[pass];
                    renderDebug(program);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        public override void renderMain(int pass)
        {
            //Console.WriteLine(this.name + this);
            GL.UseProgram(pass);

            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            for (int i = 0; i < 64; i++)
                GL.Uniform1(loc + i, 0.0f);

            //Upload Default Color
            loc = GL.GetUniformLocation(pass, "color");
            //GL.Uniform3(loc, this.color);
            GL.Uniform3(loc, this.color);

            //Upload Light Flag
            loc = GL.GetUniformLocation(pass, "useLighting");
            GL.Uniform1(loc, 0.0f);

            //Step 2: Render Elements
            GL.PointSize(5.0f);
            GL.BindVertexArray(main_Vao.vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            switch (collisionType)
            {
                case COLLISIONTYPES.MESH:
                    GL.DrawElements(PrimitiveType.Triangles, batchcount,
                        DrawElementsType.UnsignedShort, IntPtr.Zero);
                    break;
                case COLLISIONTYPES.BOX:
                case COLLISIONTYPES.CYLINDER:
                case COLLISIONTYPES.CAPSULE:
                case COLLISIONTYPES.SPHERE:
                    GL.DrawElements(PrimitiveType.Triangles, batchcount,
                        DrawElementsType.UnsignedInt, IntPtr.Zero);
                    break;

            }
            
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);

            //Console.WriteLine("Normal Object {2} vpos {0} cpos {1} prog {3}", vpos, npos, this.name, this.shader_program);
            //Console.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vbo.vertex_buffer_object, this.vbo.color_buffer_object);

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

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            //Call Dispose of the meshModel
            base.Dispose(disposing);
        }

    }

    public class Decal : meshModel
    {
        //Custom constructor
        public Decal() { }

        public Decal(GMDL.meshModel root) {
            //Copy attributes from root object
            this.vertrend_graphics = root.vertrend_graphics;
            this.vertrend_physics = root.vertrend_physics;
            this.vertrstart_graphics = root.vertrstart_graphics;
            this.vertrstart_physics = root.vertrstart_physics;
            this.renderable = true; //Override Renderability
            this.shader_programs = root.shader_programs;
            this.type = TYPES.DECAL;
            this.main_Vao = root.main_Vao;
            this.name = root.name;
            this.ID = root.ID;
            //Clone transformation
            this.localPosition = root.localPosition;
            this.localRotation = root.localRotation;
            this.localScale = root.localScale;
            //Skinning Stuff
            this.firstskinmat = root.firstskinmat;
            this.lastskinmat = root.lastskinmat;
            this.batchcount = root.batchcount;
            this.batchstart_graphics = root.batchstart_graphics;
            this.batchstart_physics = root.batchstart_physics;
            this.color = root.color;
            this.material = root.material;
            this.BoneRemap = root.BoneRemap;
            this.skinned = root.skinned;
            this.palette = root.palette;
            this.cIndex = root.cIndex;
            
            //In sharedVBO objects, both root and all the children have the same scene
            this.scene = root.scene;
            this.children = root.children; //Just assign them by ref

        }

        public override bool render(int pass)
        {
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
                case 0:
                    program = this.shader_programs[pass];
                    renderMain(program);
                    break;
                default:
                    //Do nothing otherwise
                    break;
            }

            return true;
        }

        public override void renderMain(int pass)
        {
            //Console.WriteLine(this.name + this);
            GL.UseProgram(pass);

            //Step 1: Upload Uniforms
            int loc;
            //Upload Material Flags here
            //Reset
            loc = GL.GetUniformLocation(pass, "matflags");

            if (loc > 0) 
            {
                for (int i = 0; i < 64; i++)
                    GL.Uniform1(loc + i, 0.0f);

                for (int i = 0; i < material.materialflags.Count; i++)
                    GL.Uniform1(loc + (int) material.materialflags[i], 1.0f);
            }
            

            //Upload decalTexture

            //BIND TEXTURES
            int tex0Id = (int)TextureUnit.Texture0;
            //Diffuse Texture
            string test = "decalTex";
            loc = GL.GetUniformLocation(pass, test);
            GL.Uniform1(loc, 0); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit) (tex0Id + 0));
            GL.BindTexture(TextureTarget.Texture2D, material.fDiffuseMap.bufferID);

            //Depth Texture
            test = "depthTex";
            loc = GL.GetUniformLocation(pass, test);
            GL.Uniform1(loc, 1); // I need to upload the texture unit number

            GL.ActiveTexture((TextureUnit) (tex0Id + 1));
            GL.BindTexture(TextureTarget.Texture2D, MVCore.Common.RenderState.gbuf.dump_pos);

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

    public class customVBO: IDisposable
    {
        private bool disposed = false;
        public int vertex_buffer_object;
        public int small_vertex_buffer_object;
        public int element_buffer_object;

        public List<JointBindingData> jointData;
        public List<GMDL.bufInfo> bufInfo;
        public float[] invBMats;
        public int vx_size;
        public int vx_stride;
        public int n_stride;
        public int t_stride;
        public int b_stride;
        public int uv0_stride;
        public int blendI_stride;
        public int blendW_stride;

        //Small Stuff
        public int small_vx_size;
        public int small_vx_stride;
        public int small_blendI_stride;
        public int small_blendW_stride;

        public int trisCount;
        public int iCount;
        public int vCount;
        public int iLength;
        public short[] boneRemap = new short[512];
        public DrawElementsType iType;
        public byte[] geomVbuf;
        public byte[] geomIbuf;

        public customVBO()
        {
        }

        public customVBO(GeomObject geom, int streamID)
        {
            this.LoadFromGeom(geom, streamID);
        }

        public void LoadFromGeom(GeomObject geom, int streamID)
        {
            //Set essential parameters
            this.vx_size = geom.vx_size;
            this.small_vx_size = geom.small_vx_size;
            this.vx_stride = geom.offsets[0];
            this.bufInfo = geom.bufInfo;
            this.small_vx_stride = geom.small_offsets[0];
            this.uv0_stride = geom.offsets[1];
            this.n_stride = geom.offsets[2];
            this.t_stride = geom.offsets[3];
            this.b_stride = geom.offsets[4];
            this.blendI_stride = geom.offsets[5];
            this.small_blendI_stride = geom.small_offsets[5];
            this.blendW_stride = geom.offsets[6];
            this.small_blendW_stride = geom.small_offsets[6];
            this.vCount = (int) geom.vertCount;
            this.iCount = (int) geom.indicesCount;
            this.trisCount = (int) geom.indicesCount / 3;
            this.iLength = (int) geom.indicesLength;
            this.boneRemap = geom.boneRemap;
            this.geomVbuf = geom.vbuffer;
            this.geomIbuf = geom.ibuffer;
            
            if (geom.indicesLength == 0x2)
                this.iType = DrawElementsType.UnsignedShort;
            else
                this.iType = DrawElementsType.UnsignedInt;
            //Set Joint Data
            this.jointData = geom.jointData;
            
            ////Explicitly Parse BlendIndices
            //int offset = blendI_stride;
            //int[] bIndices = new int[geom.vertCount * 4];
            //float[] bWeights = new float[geom.vertCount * 4];

            ////Binary Reader
            //MemoryStream ms = new MemoryStream();
            //ms.Write(geom.vbuffer, 0, geom.vbuffer.Length);
            //BinaryReader br = new BinaryReader(ms);
            //ms.Position = blendI_stride;
            //for (int i = 0; i < geom.vertCount; i++)
            //{
            //    //bIndices[4 * i] = br.ReadByte();
            //    //bIndices[4 * i + 1] = br.ReadByte();
            //    //bIndices[4 * i + 2] = br.ReadByte();
            //    //bIndices[4 * i + 3] = br.ReadByte();

            //    Console.WriteLine("Indices {0} {1} {2} {3}", br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
            //    ms.Position += geom.vx_size - 4;
            //}

            //GL.BindBuffer(BufferTarget.ArrayBuffer, bIndices_buffer_object);
            //GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (sizeof(int) * 4 * geom.vertCount),
            //    bIndices, BufferUsageHint.StaticDraw);

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
                bufInfo.Clear();
                jointData.Clear();
                //Clear gl arrays
                GL.DeleteBuffer(vertex_buffer_object);
                GL.DeleteBuffer(small_vertex_buffer_object);
                GL.DeleteBuffer(element_buffer_object);
                //Free other resources here
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~customVBO()
        {
            Dispose(false);
        }

        
    }

    public class GeomObject : IDisposable
    {
        private bool disposed = false;
        public string mesh_descr;
        public string small_mesh_descr;

        public bool interleaved;
        public int vx_size;
        public int small_vx_size;

        //Counters
        public int indicesCount=0;
        public int indicesLength = 0;
        public int vertCount = 0;

        //make sure there are enough buffers for non interleaved formats
        public byte[] ibuffer;
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
        public List<int> vstarts = new List<int>();
        public Dictionary<ulong, meshMetaData> meshMetaDataDict = new Dictionary<ulong, meshMetaData>();
        public Dictionary<ulong, meshData> meshDataDict = new Dictionary<ulong, meshData>();
        
        //Joint info
        public List<JointBindingData> jointData = new List<JointBindingData>();
        public float[] invBMats = new float[256 * 16];
        public float[] skinMats = new float[256 * 16]; //Final Matrices

        public GMDL.scene rootObject = null;

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


            //Unbind
            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
            for (int i = 0; i < 7; i++)
                GL.DisableVertexAttribArray(i);

            return vao;
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
                bIndices.Clear();
                bWeights.Clear();
                bufInfo.Clear();
                bboxes.Clear();
                vstarts.Clear();

                //Clear buffers
                foreach (KeyValuePair<ulong, meshMetaData> pair in meshMetaDataDict)
                    meshDataDict[pair.Key] = null;

                GC.Collect();
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~GeomObject()
        {
            Dispose(false);
        }

        
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

    public class Material: IDisposable
    {
        private bool disposed = false;
        public string name;
        public string type;
        public MatOpts opts;
        public List<MATERIALMBIN.MATERIALFLAGS> materialflags = new List<MATERIALMBIN.MATERIALFLAGS>();
        public Dictionary<string, Dictionary<string, Vector4>> palette = new Dictionary<string, Dictionary<string, Vector4>>();
        public List<Uniform> uniforms = new List<Uniform>();
        public List<Sampler> samplers = new List<Sampler>();
        public List<PaletteOpt> palOpts = new List<PaletteOpt>();
        public List<Texture> difftextures = new List<Texture>(8);
        public List<Texture> masktextures = new List<Texture>(8);
        public List<Texture> normaltextures = new List<Texture>(8);
        public float[] baseLayersUsed = new float[8];
        public float[] alphaLayersUsed = new float[8];
        public List<float[]> reColourings = new List<float[]>(8);
        public Texture fDiffuseMap = new Texture();
        public Texture fMaskMap = new Texture();
        public Texture fNormalMap = new Texture();

        public Material()
        {
            //Init texture buffers
            for (int i = 0; i < 8; i++)
            {
                difftextures.Add(null);
                masktextures.Add(null);
                normaltextures.Add(null);
                reColourings.Add(new float[] { 1.0f, 1.0f, 1.0f, 0.0f });
                palOpts.Add(null);
            }

        }

        public GMDL.Material Clone()
        {
            GMDL.Material newmat = new GMDL.Material();

            //Clone Samplers
            for (int i = 0; i < samplers.Count; i++)
                newmat.samplers.Add(samplers[i].Clone());

            //Copy materialflags
            for (int i=0;i<materialflags.Count;i++)
                newmat.materialflags.Add(materialflags[i]);

            //Copy arrays
            for (int i = 0; i < 8; i++)
            {
                //newmat.alphaLayersUsed = this.alphaLayersUsed;
                //newmat.baseLayersUsed = this.baseLayersUsed;
                //newmat.difftextures[i] = this.difftextures[i];
                //newmat.masktextures[i] = this.masktextures[i];
                //newmat.normaltextures[i] = this.normaltextures[i];
                //newmat.reColourings[i] = this.reColourings[i];
                
                //Create palOpts
                if (this.palOpts[i] != null)
                {
                    PaletteOpt palOpt = new PaletteOpt();
                    palOpt.ColorName = this.palOpts[i].ColorName;
                    palOpt.PaletteName = this.palOpts[i].PaletteName;
                    newmat.palOpts[i] = palOpt;
                }
            }

            //Remix textures

            return newmat;
        }

        public void prepTextures()
        {
            //Testing
            if (this.name.Contains("Boots1"))
            {
                Console.WriteLine("Test");
            }
            foreach (Sampler sam in samplers){
                if (sam.name != "gDiffuseMap") continue;

                string[] split = sam.map.Split('.');
                //Construct main filename
                string temp = "";
                for (int i = 0; i < split.Length - 1; i++)
                    temp += split[i] + ".";
                string texMbin = temp + "TEXTURE.MBIN";
                string texMbinexml = temp + "TEXTURE.exml";
                texMbin = Path.GetFullPath(Path.Combine(FileUtils.dirpath, texMbin));
                //texMbinexml = Path.Combine(FileUtils.dirpath, texMbinexml);
                texMbinexml = FileUtils.getExmlPath(texMbin);
                
                //Force procgen if there is a sub procgen texture defined in the sampler
                if (Common.RenderState.forceProcGen)
                {
                    texMbin = split[0] + ".TEXTURE.MBIN";
                    texMbin = Path.GetFullPath(Path.Combine(FileUtils.dirpath, texMbin));
                    texMbinexml = FileUtils.getExmlPath(texMbin);
                }
                 
                //Detect Procedural Texture
                if (File.Exists(texMbin))
                {
                    Console.WriteLine("Procedural Texture Detected: " + texMbin);
                    MVCore.Common.CallBacks.Log(string.Format("Parsing Procedural Texture"));
                    sam.proc = true;

                    libMBIN.MBINFile mbinf = new libMBIN.MBINFile(texMbin);
                    mbinf.Load();
                    TkProceduralTextureList template = (TkProceduralTextureList) mbinf.GetData();
                    mbinf.Dispose();
                    
                    //Convert to exml - just for testing
                    //if (!File.Exists(texMbinexml))
                    //    Util.MbinToExml(texMbin, texMbinexml);
                    
                    //Parse exml now
                    List<TkProceduralTexture> texList = new List<TkProceduralTexture>(8);
                    for (int i = 0; i < 8; i++) texList.Add(null);
                    ModelProcGen.parse_procTexture(ref texList, template);

#if DEBUG           
                    Console.WriteLine("Proc Texture Selection");
                    for (int i = 0; i < 8; i++) {
                        if (texList[i] != null)
                        {
                            string partNameDiff = texList[i].Diffuse;
                            Console.WriteLine(partNameDiff);
                        }
                    }
                        
#endif
                    Console.WriteLine("Proc Textures");

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
                        string paletteName = Palettes.palette_IDToName[paletteNode.Palette];
                        string colorName = Palettes.colourAlt_IDToName[paletteNode.ColourAlt];
                        Vector4 palColor = palette[paletteName][colorName];
                        //Randomize palette Color every single time
                        //Vector3 palColor = Model_Viewer.Palettes.get_color(paletteName, colorName);
                        
                        //Store pallete color to Recolouring List
                        reColourings[i] = new float[] { palColor[0], palColor[1], palColor[2], palColor[3] };
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
                        } else if (!Common.RenderState.activeResMgr.GLtextures.ContainsKey(partNameDiff))
                        {
                            //Construct Texture paths
                            string pathDiff = Path.Combine(FileUtils.dirpath, partNameDiff);

                            //Configure the Diffuse Texture
                            try
                            {
                                Texture tex = new Texture(pathDiff);
                                tex.palOpt = palOpt;
                                tex.procColor = palColor;
                                //store to global dict
                                Common.RenderState.activeResMgr.GLtextures[partNameDiff] = tex;

                                //Save Texture to material
                                this.difftextures[i] = tex;
                                baseLayersUsed[i] = 1.0f;
                                alphaLayersUsed[i] = 1.0f;
                            }
                            catch (System.IO.FileNotFoundException)
                            {
                                //Texture Not Found Continue
                                Console.WriteLine("Diffuse Texture " + pathDiff + " Not Found, Appending White Tex");
                                MVCore.Common.CallBacks.Log(string.Format("Diffuse Texture {0} Not Found", pathDiff));
                                baseLayersUsed[i] = 0.0f;
                            }
                        } else
                        //Load texture from dict
                        {
                            Texture tex = Common.RenderState.activeResMgr.GLtextures[partNameDiff];
                            //Save Texture to material
                            this.difftextures[i] = tex;
                            baseLayersUsed[i] = 1.0f;
                        }


                        //MASK
                        if (partNameMask == "")
                        {
                            //Skip
                            alphaLayersUsed[i] = 0.0f;
                        } else if (!Common.RenderState.activeResMgr.GLtextures.ContainsKey(partNameMask))
                        {
                            string pathMask = Path.Combine(FileUtils.dirpath, partNameMask);
                            //Configure Mask
                            try
                            {
                                Texture texmask = new Texture(pathMask);
                                //store to global dict
                                Common.RenderState.activeResMgr.GLtextures[partNameMask] = texmask;
                                //Store Texture to material
                                this.masktextures[i] = texmask;
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
                            Texture tex = Common.RenderState.activeResMgr.GLtextures[partNameMask];
                            //Store Texture to material
                            this.masktextures[i] = tex;
                            alphaLayersUsed[i] = 1.0f;
                        }


                        //NORMALS
                        if (partNameNormal == "")
                        {
                            //Skip

                        } else if (!Common.RenderState.activeResMgr.GLtextures.ContainsKey(partNameNormal))
                        {
                            string pathNormal = Path.Combine(FileUtils.dirpath, partNameNormal);
                            
                            try
                            {
                                Texture texnormal = new Texture(pathNormal);
                                //store to global dict
                                Common.RenderState.activeResMgr.GLtextures[partNameNormal] = texnormal;
                                //Store Texture to material
                                this.normaltextures[i] = texnormal;
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
                            Texture tex = Common.RenderState.activeResMgr.GLtextures[partNameNormal];
                            //Store Texture to material
                            this.normaltextures[i] = tex;
                        }

                    }

                    //Mix Textures now
                    this.mixTextures();
                }
                //Store Non Proc Texture
                else
                {
                    int active_id = 0;
                    Console.WriteLine("Proper Texture ");
                    //Handle Diffuse
                    if (sam.map != "")
                        if (Common.RenderState.activeResMgr.GLtextures.ContainsKey(sam.map))
                        {
                            Texture tex = Common.RenderState.activeResMgr.GLtextures[sam.map];
                            fDiffuseMap = tex;
                        }
                        else
                        {
                            Texture tex = new Texture(Path.Combine(FileUtils.dirpath, sam.map));
                            tex.palOpt = new PaletteOpt(false);
                            tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                            fDiffuseMap = tex;
                            //Store to resource
                            Common.RenderState.activeResMgr.GLtextures[sam.map] = tex;
                        }


                    //Handle Mask
                    if (sam.map != "" && sam.map != null)
                        if (Common.RenderState.activeResMgr.GLtextures.ContainsKey(sam.map))
                        {
                            Texture tex = Common.RenderState.activeResMgr.GLtextures[sam.map];
                            fMaskMap = tex;
                        }
                        //else if (!File.Exists(Path.Combine(FileUtils.dirpath, sam.pathMask)))
                        //{
                        //    Texture tex = Util.resMgr.GLtextures["default_mask.dds"];
                        //    masktextures[active_id] = tex;
                        //    alphaLayersUsed[active_id] = 1.0f;
                        //}
                        else
                        {
                            Texture tex = new Texture(Path.Combine(FileUtils.dirpath, sam.map));
                            tex.palOpt = new PaletteOpt(false);
                            tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                            fMaskMap = tex;
                            //Store to resource
                            Common.RenderState.activeResMgr.GLtextures[sam.map] = tex;
                        }

                    //Handle Normal
                    if (sam.map != "" && sam.map != null)
                        if (Common.RenderState.activeResMgr.GLtextures.ContainsKey(sam.map))
                        {
                            Texture tex = Common.RenderState.activeResMgr.GLtextures[sam.map];
                            fNormalMap = tex;
                        }
                        else
                        {
                            try
                            {
                                Texture tex = new Texture(Path.Combine(FileUtils.dirpath, sam.map));
                                tex.palOpt = new PaletteOpt(false);
                                tex.procColor = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);
                                fNormalMap = tex;
                                //Store to resource
                                Common.RenderState.activeResMgr.GLtextures[sam.map] = tex;
                            }
                            catch (System.IO.FileNotFoundException)
                            { 
                                //File doesn't exist, to nothing
                            }
                        }
                }
                    
            }

            //Reverse Lists

            //Console.WriteLine("PrepTextures, Last GL Error: " + GL.GetError());

        }

        public void mixTextures() {
            //Testing
            if (this.name.Contains("Boots1"))
            {
                Console.WriteLine("Test");
            }

            //Find texture sizes
            int texWidth = 0;
            int texHeight = 0;

            for (int i = 0; i < 8; i++)
            {
                if (difftextures[i] != null)
                {
                    texHeight = difftextures[i].height;
                    texWidth = difftextures[i].width;
                    break;
                }
            }
            
            //Diffuse Output
            int out_tex_diffuse = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, out_tex_diffuse);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D); //Generate Mipmaps from the base level
            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float)Math.Max(af_amount, 4.0f);
            GL.TexParameter(TextureTarget.Texture2D, (TextureParameterName)0x84FE, af_amount);

            //NULL means reserve texture memory, but texels are undefined
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texWidth, texHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            //GL.Ext.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            //Create New RenderBuffer for the diffuse
            int fb_diffuse = GL.GenFramebuffer();

            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb_diffuse);
            //Attach Texture to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, out_tex_diffuse, 0);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER");

            //Mask Output
            int out_tex_mask = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, out_tex_mask);
            //NULL means reserve texture memory, but texels are undefined
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texWidth, texHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);

            //GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
            
            //Create New RenderBuffer for the diffuse
            //Attach Texture to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2D, out_tex_mask, 0);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER");

            //Normal Output
            int out_tex_normal = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, out_tex_normal);
            //NULL means reserve texture memory, but texels are undefined
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4.0f);
            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, texWidth, texHeight, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D); //Generate Mipmaps from the base level
            
            
            //Create New RenderBuffer for the diffuse
            //Attach Texture to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2D, out_tex_normal, 0);

            //Check
            if (GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) != FramebufferErrorCode.FramebufferComplete)
                Console.WriteLine("MALAKIES STO FRAMEBUFFER");


            //Upload Textures
            
            //BIND TEXTURES
            Texture tex;
            int loc;

            //Console.WriteLine("Rendering Textures of : " + name);
            //If there are samples defined, there are diffuse textures for sure

            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            //NEW WAY OF TEXTURE BINDING

            //DIFFUSE TEXTURES
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.ConstantAlpha, BlendingFactorDest.OneMinusConstantAlpha);

            Texture dMask = Common.RenderState.activeResMgr.GLtextures["default_mask.dds"];
            Texture dDiff = Common.RenderState.activeResMgr.GLtextures["default.dds"];

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.shader_programs[3];
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            for (int i = 0; i < 8; i++)
            {
                int active_id = i;
                loc = GL.GetUniformLocation(pass_program, "d_lbaseLayersUsed[" + active_id.ToString() + "]");
                GL.Uniform1(loc, baseLayersUsed[active_id]);
                if (baseLayersUsed[i] > 0.0f)
                    baseLayerIndex = i;
            }

            for (int i = 0; i < 8; i++)
            {
                int active_id = i;

                if (difftextures[active_id] != null)
                    tex = difftextures[active_id];
                else
                    tex = dMask;

                //Upload diffuse Texture
                string sem = "diffuseTex[" + active_id.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(pass_program, sem);
                GL.Uniform1(loc, active_id); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                GL.ActiveTexture((TextureUnit) (tex0Id + active_id));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);

            }

            //Seems like alphaChannel variable is set from the _F24_AOMAP flag
            //^^^ AO Map flag probably does not fix the alpha situation.
            //Decals don't have the flag but their textures contain alpha. For now I'm adding the Transparent flag on the check as well
            loc = GL.GetUniformLocation(pass_program, "hasAlphaChannel");
            if (materialflags.Contains(MATERIALMBIN.MATERIALFLAGS._F24_AOMAP) || 
                materialflags.Contains(MATERIALMBIN.MATERIALFLAGS._F09_TRANSPARENT))
                GL.Uniform1(loc, 1.0f);
            else
                GL.Uniform1(loc, 0.0f);

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);

            //MASKS
            //Upload alpha Layers Used
            //for (int i = 0; i < 8; i++)
            //{
            //    int active_id = i;
            //    loc = GL.GetUniformLocation(pass_program, "lalphaLayersUsed[" + active_id.ToString() + "]");
            //    GL.Uniform1(loc, alphaLayersUsed[active_id]);
            //}

            //Upload Mask Textures -- Alpha Masks???
            loc = GL.GetUniformLocation(pass_program, "m_lbaseLayersUsed");
            for (int i = 0; i < 8; i++)
            {
                if (masktextures[i] != null) GL.Uniform1(loc + i, 1.0f);
                else GL.Uniform1(loc + i, 0.0f);
            }

            for (int i = 0; i < 8; i++)
            {
                int active_id = i;

                if (masktextures[active_id] != null)
                    tex = masktextures[active_id];
                else
                    tex = dDiff;


                //Upload mask Texture
                string sem = "maskTex[" + active_id.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(pass_program, sem);
                GL.Uniform1(loc, 8 + active_id); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                //Upload PaletteColor
                //loc = GL.GetUniformLocation(pass_program, "palColors[" + i.ToString() + "]");
                //Use Texture paletteOpt and object palette to load the palette color
                //GL.Uniform3(loc, palette[tex.palOpt.PaletteName][tex.palOpt.ColorName]);

                GL.ActiveTexture((OpenTK.Graphics.OpenGL.TextureUnit)(tex0Id + 8 + active_id));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);

            }

            //Upload Normal Textures
            loc = GL.GetUniformLocation(pass_program, "n_lbaseLayersUsed");
            for (int i = 0; i < 8; i++)
            {
                if (normaltextures[i] != null) GL.Uniform1(loc + i, 1.0f);
                else GL.Uniform1(loc + i, 0.0f);
            }

            for (int i = 0; i < 8; i++)
            {
                int active_id = i;

                if (normaltextures[active_id] != null)
                    tex = normaltextures[active_id];
                else
                    tex = dMask;

                //Upload diffuse Texture
                string sem = "normalTex[" + active_id.ToString() + "]";
                //Get Texture location
                loc = GL.GetUniformLocation(pass_program, sem);
                GL.Uniform1(loc, 16 + active_id); // I need to upload the texture unit number

                int tex0Id = (int)TextureUnit.Texture0;

                GL.ActiveTexture((TextureUnit)(tex0Id + 16 + active_id));
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);
            }

            //Upload Recolouring Information
            for (int i = 0; i < 8; i++)
            {
                int active_id = i;
                loc = GL.GetUniformLocation(pass_program, "lRecolours[" + active_id.ToString() + "]");
                //GL.Uniform4(loc, reColourings[active_id][0], reColourings[active_id][1], reColourings[active_id][2], reColourings[active_id][3]);
                GL.Uniform4(loc, (float) reColourings[active_id][0],
                                 (float) reColourings[active_id][1],
                                 (float) reColourings[active_id][2],
                                 (float) reColourings[active_id][3]);
                //GL.Uniform4(loc, 1.0f, 1.0f, 1.0f, 1.0f);
            }

            //RENDERING PHASE
            //Render to the FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fb_diffuse);
            GL.DrawBuffers(3, new DrawBuffersEnum[] { DrawBuffersEnum.ColorAttachment0, DrawBuffersEnum.ColorAttachment1, DrawBuffersEnum.ColorAttachment2 });

            //Set Viewport
            GL.Viewport(0, 0, texWidth, texHeight);
            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            //GL.Disable(EnableCap.DepthTest);
            //GL.Enable(EnableCap.Blend);
            //GL.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);

            GL.BindVertexArray(MVCore.Common.RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(BeginMode.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);
            //GL.DrawArrays(PrimitiveType.Triangles, 0, 6);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());

            //Store Diffuse Texture to material
            fDiffuseMap.bufferID = out_tex_diffuse;
            //Store Mask Texture to material
            fMaskMap.bufferID = out_tex_mask;
            //Store Normal Texture to material
            fNormalMap.bufferID = out_tex_normal;

#if DEBUG
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            dump_texture("diffuse", texWidth, texHeight);

            GL.ReadBuffer(ReadBufferMode.ColorAttachment1);
            dump_texture("mask", texWidth, texHeight);

            GL.ReadBuffer(ReadBufferMode.ColorAttachment2);
            dump_texture("normal", texWidth, texHeight);
#endif

            //Bring Back screen
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fb_diffuse);
        }

        private void dump_texture(string name, int width, int height)
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
            bmp.Save("Temp//framebuffer_raw_"+ name + "_" + this.name + ".png", ImageFormat.Png);
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
                materialflags.Clear();
                palette.Clear();
                uniforms.Clear();
                samplers.Clear();
                reColourings.Clear();
                //Texture lists should have been disposed from the dictionary
                cleanupOriginals();

                if (fDiffuseMap != null) fDiffuseMap.Dispose();
                if (fMaskMap != null) fMaskMap.Dispose();
                if (fNormalMap != null) fNormalMap.Dispose();
                //Free other resources here
            }

            //Free unmanaged resources
            disposed = true;
        }

        ~Material()
        {
            Dispose(false);
        }

        public void cleanupOriginals()
        {
            foreach (Texture t in difftextures)
                if (t != null) t.Dispose();
            difftextures.Clear();
            foreach (Texture t in masktextures)
                if (t != null) t.Dispose();
            masktextures.Clear();
            foreach (Texture t in normaltextures)
                if (t != null) t.Dispose();
            normaltextures.Clear();

        }
        
    }
    
    public class Uniform
    {
        public string name;
        public Vector4 value;
    }

    public class MatOpts
    {
        public int transparency;
        public bool castshadow;
        public bool disableTestz;
        public string link;
        public string shadername;
    }

    public class Sampler
    {
        public string name;
        public string map;
        //public string pathDiff;
        //public string pathMask = null;
        //public string pathNormal = null;
        public bool proc = false;
        //public List<Texture> procTextures = new List<Texture>();

        public Sampler Clone()
        {
            Sampler newsampler = new Sampler();
            newsampler.name = name;
            newsampler.map = map;
            //newsampler.pathDiff = pathDiff;
            //newsampler.pathMask = pathMask;
            //newsampler.pathNormal = pathNormal;
            newsampler.proc = proc;
            return newsampler;

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
        public string name;
        public int width;
        public int height;
        public InternalFormat pif;
        public bool containsAlphaMap;
        public PaletteOpt palOpt;
        public Vector4 procColor;
        public Vector3 avgColor;
        public PixelFormat pf;
        //public DDSImage ddsImage;
        //Attach mask and normal textures to the diffuse
        public Texture mask;
        public Texture normal;

        //Empty Initializer
        public Texture() {}
        //Path Initializer
        public Texture(string path)
        {
            DDSImage ddsImage;
            if (!File.Exists(path))
            {
                //throw new System.IO.FileNotFoundException();
                Console.WriteLine("Texture {0} Missing. Using default.dds", path);
                path = "default.dds";
            }
            else
                path = Path.Combine(FileUtils.dirpath, path);

            ddsImage = new DDSImage(File.ReadAllBytes(path));

            name = path;
            Console.WriteLine("Sampler Name Path " + path + " Width {0} Height {1}", ddsImage.header.dwWidth, ddsImage.header.dwHeight);
            width = ddsImage.header.dwWidth;
            height = ddsImage.header.dwHeight;
            switch (ddsImage.header.ddspf.dwFourCC)
            {
                //DXT1
                case (0x31545844):
                    pif = InternalFormat.CompressedRgbaS3tcDxt1Ext;
                    containsAlphaMap = false;
                    break;
                case (0x35545844):
                    pif = InternalFormat.CompressedRgbaS3tcDxt5Ext;
                    containsAlphaMap = true;
                    break;
                case (0x32495441): //ATI2A2XY
                    pif = InternalFormat.CompressedRgRgtc2;
                    containsAlphaMap = true;
                    break;
                //DXT10 HEADER
                case (0x30315844):
                    {
                        switch (ddsImage.header10.dxgiFormat)
                        {
                            case (DXGI_FORMAT.DXGI_FORMAT_BC7_UNORM):
                                pif = InternalFormat.CompressedRgbaBptcUnorm;
                                containsAlphaMap = true;
                                break;
                            default:
                                throw new ApplicationException("Unimplemented DX10 Texture Pixel format");
                        }
                        
                        break;
                    }
                default:
                    throw new ApplicationException("Unimplemented Pixel format");
            }
            //Force RGBA for now
            pf = PixelFormat.Rgba;
            //Upload to GPU
            bufferID = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, bufferID);
            //GL.CompreTextureStorage2D(bufferID, ddsImage.header.dwMipMapCount, SizedInternalFormat.rgba, width, height);
            //Console.WriteLine(GL.GetError());
            GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, this.pif,
                this.width, this.height, 0, ddsImage.header.dwPitchOrLinearSize, ddsImage.bdata);
            //Console.WriteLine(GL.GetError());
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureLodBias, -0.2f);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int) TextureWrapMode.Repeat);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D); //Generate Mipmaps from the base level
            //Console.WriteLine(GL.GetError());

            //Use anisotropic filtering
            float af_amount = GL.GetFloat((GetPName)All.MaxTextureMaxAnisotropy);
            af_amount = (float) Math.Max(af_amount, 4.0f);
            GL.TexParameter(TextureTarget.Texture2D,  (TextureParameterName) 0x84FE, af_amount);

            int max_level = 0;
            GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureMaxLevel, out max_level);
            int base_level = 0;
            GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureBaseLevel, out base_level);

            int maxsize = Math.Max(height, width);
            int p = (int) Math.Floor(Math.Log(maxsize, 2)) + base_level;
            int q = Math.Min(p, max_level);

            //Get lowest calculated mipmap
            byte[] pixels = new byte[8];
            /*
            byte[] pixels00 = new byte[1024 * 1024 / 2];
            byte[] pixels01 = new byte[512 * 512 / 2];
            byte[] pixels02 = new byte[256 * 256 / 2];
            byte[] pixels03 = new byte[128 * 128 / 2];
            byte[] pixels04 = new byte[64 * 64 / 2];
            byte[] pixels05 = new byte[32 * 32 / 2];
            byte[] pixels06 = new byte[16 * 16 / 2];
            byte[] pixels07 = new byte[8 * 8 / 2];
            byte[] pixels08 = new byte[4 * 4 / 2];
            byte[] pixels10 = new byte[8];
            byte[] pixels11 = new byte[8];
            */
            
            //Save to disk
            GL.GetCompressedTexImage(TextureTarget.Texture2D, q-1, pixels);
            //File.WriteAllBytes("Temp\\level" + (q-1).ToString(), pixels);
            
            
            /*
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 0, pixels00);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 1, pixels01);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 2, pixels02);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 3, pixels03);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 4, pixels04);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 5, pixels05);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 6, pixels06);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 7, pixels07);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 8, pixels08);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 9, pixels09);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 10, pixels10);
            GL.GetCompressedTexImage(TextureTarget.Texture2D, 11, pixels11);
            
            File.WriteAllBytes("level0", pixels00);
            File.WriteAllBytes("level1", pixels01);
            File.WriteAllBytes("level2", pixels02);
            File.WriteAllBytes("level3", pixels03);
            File.WriteAllBytes("level4", pixels04);
            File.WriteAllBytes("level5", pixels05);
            File.WriteAllBytes("level6", pixels06);
            File.WriteAllBytes("level7", pixels07);
            File.WriteAllBytes("level8", pixels08);
            File.WriteAllBytes("level9", pixels09);
            File.WriteAllBytes("level10", pixels10);
            File.WriteAllBytes("level11", pixels11);
             */


            avgColor = getAvgColor(pixels);

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
                if (mask != null) mask.Dispose();
                if (normal != null) normal.Dispose();
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


        ~Texture()
        {
            Dispose(false);
        }

    }

    public class Joint : locator
    {
        private int vertex_buffer_object;
        private int element_buffer_object;
        public int jointIndex;
        public Vector3 color;

        public Joint() :base(0.1f)
        {
            //Create Buffers
            GL.GenBuffers(1, out vertex_buffer_object);
            //GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);
        }

        
        
        //Empty stuff
        public override model Clone(scene scn)
        {
            Joint copy = new Joint();
            copy.renderable = true; //Override Renderability
            copy.shader_programs = this.shader_programs;
            copy.type = this.type;
            copy.name = this.name;
            copy.ID = this.ID;
            copy.vertex_buffer_object = this.vertex_buffer_object;
            copy.element_buffer_object = this.element_buffer_object;
            copy.jointIndex = this.jointIndex;
            copy.color = this.color;
            
            //Copy Transformations
            copy.localPosition = this.localPosition;
            copy.localScale = this.localScale;
            copy.localRotation = this.localRotation;
            copy.scene = scn;

            //Clone Children as well
            foreach (model child in this.children)
            {
                model nChild = child.Clone(scn);
                nChild.parent = copy;
                copy.children.Add(nChild);
            }

            return copy;
        }

        //Render should render Bones from joint to children
        private void renderMain(int pass)
        {
            GL.UseProgram(pass);
            
            //Draw Lines to children joints
            List<Vector3> verts = new List<Vector3>();
            //List<int> indices = new List<int>();
            List<Vector3> colors = new List<Vector3>();
            int arraysize = this.children.Count * 2 * 3 * sizeof(float);
            int[] indices = new int[this.children.Count * 2];
            for (int i = 0; i < this.children.Count; i++)
            {
                verts.Add(this.worldPosition);
                verts.Add(children[i].worldPosition);
                ////Choosing red color for the skeleton
                colors.Add(new Vector3(1.0f, 0.0f, 0.0f));
                colors.Add(new Vector3(1.0f, 0.0f, 0.0f));
                //Use Random Color for Testing
                //colors.Add(color);
                //colors.Add(color);

                //Add line indices
                indices[2 * i] = 2 * i;
                indices[2 * i + 1] = 2 * i + 1;
            }

            float[] vertsf = new float[verts.Count * 3];
            float[] colorf = new float[colors.Count * 3];
            vectofloatArray(vertsf, verts);
            vectofloatArray(colorf, colors);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, vertsf);
            //Add vert color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, colorf);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(sizeof(int) * indices.Length), indices, BufferUsageHint.StaticDraw);

            //Render Immediately
            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(0);

            //Color Attribute
            GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
            GL.EnableVertexAttribArray(1);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.PointSize(5.0f);

            GL.DrawArrays(PrimitiveType.Lines, 0, indices.Length);
            GL.DrawArrays(PrimitiveType.Points, 0, indices.Length);
            
            //Draw only Joint Point
            GL.DisableVertexAttribArray(0);
            GL.DisableVertexAttribArray(1);
        }

        public override bool render(int pass)
        {
            
            if (this.children.Count == 0)
                return false;

            int program;
            //Render Object
            switch (pass)
            {
                //Render Main
                case 0:
                    program = this.shader_programs[pass];
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

    public class Light : model
    {
        //I should expand the light properties here
        public float intensity = 1.0f;
        public float distance = 1.0f;
        
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

        public override bool render(int pass)
        {
            int program = this.shader_programs[pass];

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

        public override model Clone(scene scene)
        {
            throw new NotImplementedException();
        }

        public void updatePosition(Vector3 newPosition)
        {
            this.localPosition = newPosition;
        }

        //DIsposal
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
    public class AnimNodeFrameData
    {
        public List<Quaternion> rotations = new List<Quaternion>();
        public List<Vector3> translations = new List<Vector3>();
        public List<Vector3> scales = new List<Vector3>();

        public void LoadRotations(FileStream fs,int count)
        {
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < count; i++)
            {
                Quaternion q = new Quaternion();
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


    public class AnimFrameData
    {
        public List<AnimNodeFrameData> frames = new List<AnimNodeFrameData>();
        public int frameCount;

        public void Load(FileStream fs, int count)
        {
            BinaryReader br = new BinaryReader(fs);
            this.frameCount = count;
            for (int i = 0; i < count; i++)
            {
                uint rotOff = (uint)fs.Position + br.ReadUInt32();
                fs.Seek(0x4, SeekOrigin.Current);
                int rotCount = br.ReadInt32();
                fs.Seek(0x4, SeekOrigin.Current);

                uint transOff = (uint)fs.Position + br.ReadUInt32();
                fs.Seek(0x4, SeekOrigin.Current);
                int transCount = br.ReadInt32();
                fs.Seek(0x4, SeekOrigin.Current);

                uint scaleOff = (uint)fs.Position + br.ReadUInt32();
                fs.Seek(0x4, SeekOrigin.Current);
                int scaleCount = br.ReadInt32();
                fs.Seek(0x4, SeekOrigin.Current);

                long back = fs.Position;

                AnimNodeFrameData frame = new AnimNodeFrameData();
                fs.Seek(rotOff, SeekOrigin.Begin);
                frame.LoadRotations(fs, rotCount);
                fs.Seek(transOff, SeekOrigin.Begin);
                frame.LoadTranslations(fs, transCount);
                fs.Seek(scaleOff, SeekOrigin.Begin);
                frame.LoadScales(fs, scaleCount);

                fs.Seek(back, SeekOrigin.Begin);

                this.frames.Add(frame);

            }
        }
    }
    public class AnimeNode
    {
        public int index;
        public string name = "";
        public bool canCompress = false;
        public int rotIndex = 0;
        public int transIndex = 0;
        public int scaleIndex = 0;


        public AnimeNode(int fIndex)
        {
            this.index = fIndex;
        }
        public void Load(FileStream fs)
        {
            //Binary reader
            BinaryReader br = new BinaryReader(fs);
            char[] charbuffer = new char[0x100];

            charbuffer = br.ReadChars(0x40);
            name = (new string(charbuffer)).Trim('\0');
            canCompress = (br.ReadInt32()==0) ? false : true;
            rotIndex = br.ReadInt32();
            transIndex = br.ReadInt32();
            scaleIndex = br.ReadInt32();
        }
    }
    public class NodeData
    {
        public List<AnimeNode> nodeList = new List<AnimeNode>();
        public int nodeCount = 0;

        public void parseNodes(FileStream fs, int count)
        {
            nodeCount = count;

            for (int i = 0; i < count; i++)
            {
                AnimeNode node = new AnimeNode(i);
                node.Load(fs);
                nodeList.Add(node);
            }
        }

    }

    public class AnimeMetaData
    {
        public int nodeCount = 0;
        public int frameCount = 0;
        public NodeData nodeData = new NodeData();
        public AnimFrameData frameData = new AnimFrameData();

        public void Load(FileStream fs)
        {
            //Binary Reader
            BinaryReader br = new BinaryReader(fs);
            fs.Seek(0x60, SeekOrigin.Begin);
            frameCount = br.ReadInt32();
            nodeCount = br.ReadInt32();

            //Get Offsets
            uint nodeOffset = (uint)fs.Position + br.ReadUInt32();
            fs.Seek(0xC, SeekOrigin.Current);
            uint animeFrameDataOff = (uint)fs.Position + br.ReadUInt32();
            fs.Seek(0xC, SeekOrigin.Current);
            uint staticFrameOff = (uint)fs.Position;

            Console.WriteLine("Animation File");
            Console.WriteLine("Frames {0} Nodes {1}", frameCount, nodeCount);
            Console.WriteLine("Parsing Nodes NodeOffset {0}", nodeOffset);

            fs.Seek(nodeOffset, SeekOrigin.Begin);
            NodeData nodedata = new NodeData();
            nodedata.parseNodes(fs, nodeCount);
            nodeData = nodedata;

            Console.WriteLine("Parsing Animation Frame Data Offset {0}", animeFrameDataOff);
            fs.Seek(animeFrameDataOff, SeekOrigin.Begin);
            AnimFrameData framedata = new AnimFrameData();
            framedata.Load(fs, frameCount);
            this.frameData = framedata;

        }

    }

    public class JointBindingData
    {
        public Matrix4 invBindMatrix = Matrix4.Identity;
        public Vector4 iBM_Row1;
        public Vector4 iBM_Row2;
        public Vector4 iBM_Row3;
        public Vector3 BindTranslate;
        public Quaternion BindRotation;
        public Vector3 Bindscale;

        
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

            //Load the Rows transposed in order to get rid ot (0, 0, 0, 1)
            iBM_Row1 = new Vector4(invBindMatrix.M11, invBindMatrix.M21, invBindMatrix.M31, invBindMatrix.M41);
            iBM_Row2 = new Vector4(invBindMatrix.M12, invBindMatrix.M22, invBindMatrix.M32, invBindMatrix.M42);
            iBM_Row3 = new Vector4(invBindMatrix.M13, invBindMatrix.M23, invBindMatrix.M33, invBindMatrix.M43);

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
            Bindscale.X = br.ReadSingle();
            Bindscale.Y = br.ReadSingle();
            Bindscale.Z = br.ReadSingle();

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




