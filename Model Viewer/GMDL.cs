using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;
using KUtility;

namespace GMDL
{
    public abstract class model
    {
        public abstract bool render();
        public abstract GMDL.model Clone();
        public bool renderable = true;
        public int shader_program = -1;
        public int index;
        public string type = "";
        public string name = "";
        public GMDL.Material material;
        public List<model> children = new List<model>();
        //Transformation Parameters
        //public Matrix4 localMat = Matrix4.Identity;
        //public Matrix4 worldMat = Matrix4.Identity;
        public Vector3 worldPosition {
            get
            {
                if (parent != null)
                {
                    //Original working
                    //return parent.worldPosition + Vector3.Transform(this.localPosition, parent.worldMat);
                    
                    //Add Translation as well
                    return Vector3.Transform(new Vector3(), this.worldMat);
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
                    return Matrix4.Mult(this.localMat, parent.worldMat);
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

                return rot * Matrix4.CreateTranslation(localPosition);
            }
        }
        public Vector3 localPosition = new Vector3(0.0f, 0.0f, 0.0f);
        public Vector3 localScale = new Vector3(1.0f, 1.0f, 1.0f);
        //public Vector3 localRotation = new Vector3(0.0f, 0.0f, 0.0f);
        public Matrix3 localRotation = Matrix3.Identity;

        public model parent;

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

        public void init(string trans)
        {
            //Get Local Position
            string[] split = trans.Split(',');
            Vector3 rotation;
            this.localPosition.X = float.Parse(split[0]);
            this.localPosition.Y = float.Parse(split[1]);
            this.localPosition.Z = float.Parse(split[2]);
            //Get Local Rotation
            //Quaternion qx = Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f),
            //    (float)Math.PI * float.Parse(split[3]) / 180.0f);
            //Quaternion qy = Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f),
            //    (float)Math.PI * float.Parse(split[4]) / 180.0f);
            //Quaternion qz = Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f),
            //    (float)Math.PI * float.Parse(split[5]) / 180.0f);

            Quaternion qx = Quaternion.FromAxisAngle(new Vector3(1.0f, 0.0f, 0.0f),
                float.Parse(split[3]));
            Quaternion qy = Quaternion.FromAxisAngle(new Vector3(0.0f, 1.0f, 0.0f),
                float.Parse(split[4]));
            Quaternion qz = Quaternion.FromAxisAngle(new Vector3(0.0f, 0.0f, 1.0f),
                float.Parse(split[5]));

            //this.localRotation = qz * qx * qy;
            rotation.X = float.Parse(split[3]);
            rotation.Y = float.Parse(split[4]);
            rotation.Z = float.Parse(split[5]);

            //Get Local Scale
            this.localScale.X = float.Parse(split[6]);
            this.localScale.Y = float.Parse(split[7]);
            this.localScale.Z = float.Parse(split[8]);

            //Now Calculate the joint matrix;

            Matrix3 rotx, roty, rotz;
            Matrix3.CreateRotationX((float)Math.PI * rotation.X / 180.0f, out rotx);
            Matrix3.CreateRotationY((float)Math.PI * rotation.Y / 180.0f, out roty);
            Matrix3.CreateRotationZ((float)Math.PI * rotation.Z / 180.0f, out rotz);
            //Matrix4.CreateTranslation(ref this.localPosition, out transM);
            //Calculate local matrix
            this.localRotation = rotz*rotx*roty;
            
            //this.localMat = rotz * rotx * roty * transM;
            


            //Calculation is done via properties
            ////Calculate world position
            //if (this.parent != null)
            //{
            //    //this.worldMat = Matrix4.Mult(parent.worldMat, this.localMat);
            //    this.worldMat = Matrix4.Mult(this.localMat, parent.worldMat);
            //    Vector3 transformed = Vector3.Transform(this.localPosition, parent.worldMat);
            //    this.worldPosition = parent.worldPosition + transformed;
            //}
            //else
            //{
            //    this.worldPosition = this.localPosition;
            //    this.worldMat = this.localMat;
            //}

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
    
    public class locator: model
    {
        //private Vector3[] verts;
        private float[] verts = new float[6*3];
        private float[] colors = new float[6 * 3];
        private Int32[] indices;
        //public bool renderable = true;
        int vertex_buffer_object;
        //int color_buffer_object;
        int element_buffer_object;
        //public int shader_program = -1;
        //this.type = "";
        //string name = "";
        //public int index;
        

        //Default Constructor
        public locator()
        {
            //Set type
            //this.type = "LOCATOR";
            //Assemble geometry in the constructor
            //X
            float vlen = 0.5f;
            verts = new float[6 * 3] { vlen, 0.0f, 0.0f,
                   -vlen, 0.0f, 0.0f,
                    0.0f, vlen, 0.0f,
                    0.0f, -vlen, 0.0f,
                    0.0f, 0.0f, vlen,
                    0.0f, 0.0f, -vlen};
            int b_size = verts.Length * sizeof(float) / 3;
            byte[] verts_b = new byte[b_size];
            
            Buffer.BlockCopy(verts, 0, verts_b, 0, b_size);
            //verts = new Vector3[6];
            //verts[0] = new Vector3(vlen, 0.0f, 0.0f);
            //verts[1] = new Vector3(-vlen, 0.0f, 0.0f);
            //verts[2] = new Vector3(0.0f, vlen, 0.0f);
            //verts[3] = new Vector3(0.0f, -vlen, 0.0f);
            //verts[4] = new Vector3(0.0f, 0.0f, vlen);
            //verts[5] = new Vector3(0.0f, 0.0f, -vlen);

            //Colors
            colors = new float[6 * 3] { 1.0f, 0.0f, 0.0f,
                    1.0f, 0.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    0.0f, 1.0f, 0.0f,
                    0.0f, 0.0f, 1.0f,
                    0.0f, 0.0f, 1.0f};

            //Indices
            indices = new Int32[2 * 3] {0, 1, 2, 3, 4, 5};
            
            //Generate OpenGL buffers
            int arraysize = sizeof(float) * 6 * 3;
            GL.GenBuffers(1, out vertex_buffer_object);
            //GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            //Allocate to NULL
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr)(2 * arraysize), (IntPtr)null, BufferUsageHint.StaticDraw);
            //Add verts data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
            //Add vert color data
            GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)arraysize, (IntPtr)arraysize, colors);

            ////Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr) (sizeof(int) * 6), indices, BufferUsageHint.StaticDraw);
        }

        public override bool render()
        {
            if (this.renderable == false)
            {
                //Debug.WriteLine("Not Renderable Locator");
                return false;
            }
            //Debug.WriteLine("Rendering Locator {0}", this.name);
            //Debug.WriteLine("Rendering VBO Object here");
            //VBO RENDERING
            {
                int vpos, cpos;
                int arraysize = sizeof(float) * 6 * 3;
                //Vertex attribute
                //Bind vertex buffer
                GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
                vpos = GL.GetAttribLocation(this.shader_program, "vPosition");
                GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.Float, false, 0, 0);
                GL.EnableVertexAttribArray(vpos);

                //Color Attribute
                cpos = GL.GetAttribLocation(this.shader_program, "vcolor");
                GL.VertexAttribPointer(cpos, 3, VertexAttribPointerType.Float, false, 0, (IntPtr) arraysize);
                GL.EnableVertexAttribArray(cpos);
                
                //Render Elements
                //Bind elem buffer
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
                GL.PointSize(10.0f);
                //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Point);
                
                //GL.DrawElements(PrimitiveType.Points, 6, DrawElementsType.UnsignedInt, this.indices);
                GL.DrawArrays(PrimitiveType.Lines, 0, 6);
                //Debug.WriteLine("Locator Object {2} vpos {0} cpos {1} prog {3}", vpos, cpos, this.name, this.shader_program);
                //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vertex_buffer_object,this.color_buffer_object);

                GL.DisableVertexAttribArray(vpos);
                GL.DisableVertexAttribArray(cpos);
            }

            return true;
        }

        public override GMDL.model Clone()
        {
            GMDL.locator copy = new GMDL.locator();
            copy.renderable = true; //Override Renderability
            copy.shader_program = this.shader_program;
            copy.type = this.type;
            copy.name = this.name;
            copy.index = this.index;

            return (GMDL.model) copy;
        }
    }

    public class sharedVBO : model
    {
        public int vertrstart = 0;
        public int vertrend = 0;
        public int batchstart = 0;
        public int batchcount = 0;
        public int firstskinmat = 0;
        public int lastskinmat = 0;
        //public string name = "";
        //public string type = "";
        public customVBO vbo;
        public Vector3 color = new Vector3();
        //public bool renderable = true;
        //public int shader_program = -1;
        //public int index;
        //Material
        //public GMDL.Material material;
        public float[] getBindRotMats
        {
            get
            {
                float[] jMats = new float[60 * 16];

                for (int i = 0; i < this.vbo.jointData.Count; i++)
                {
                    Matrix4 temp = Matrix4.CreateFromQuaternion(vbo.jointData[i].BindRotation);
                    jMats[i * 16] = temp.M11;
                    jMats[i * 16 + 1] = temp.M12;
                    jMats[i * 16 + 2] = temp.M13;
                    jMats[i * 16 + 3] = temp.M14;
                    jMats[i * 16 + 4] = temp.M21;
                    jMats[i * 16 + 5] = temp.M22;
                    jMats[i * 16 + 6] = temp.M23;
                    jMats[i * 16 + 7] = temp.M24;
                    jMats[i * 16 + 8] = temp.M31;
                    jMats[i * 16 + 9] = temp.M32;
                    jMats[i * 16 + 10] = temp.M33;
                    jMats[i * 16 + 11] = temp.M34;
                    //jMats[i * 16 + 12] = temp.M41;
                    //jMats[i * 16 + 13] = temp.M42;
                    //jMats[i * 16 + 14] = temp.M43;
                    //jMats[i * 16 + 15] = temp.M44;
                    jMats[i * 16 + 12] = 0.0f;
                    jMats[i * 16 + 13] = 0.0f;
                    jMats[i * 16 + 14] = 0.0f;
                    jMats[i * 16 + 15] = 1.0f;
                }

                return jMats;
            }
        }
        public float[] getBindTransMats
        {
            get
            {
                float[] trans = new float[60 * 3];

                for (int i = 0; i < this.vbo.jointData.Count; i++)
                {
                    Vector3 temp = vbo.jointData[i].BindTranslate;
                    trans[3 * i + 0] = temp.X;
                    trans[3 * i + 1] = temp.Y;
                    trans[3 * i + 2] = temp.Z;
                }

                return trans;
            }
        }
        public override bool render()
        {
            if (this.renderable == false)
            {
                //Debug.WriteLine("Not Renderable");
                return false;
            }
            //Debug.WriteLine(this.name + this);
            //GL.UseProgram(this.shader_program);

            //Bind vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vbo.vertex_buffer_object);

            int vpos,npos,uv0pos,bI,bW;
            //Vertex attribute
            vpos = GL.GetAttribLocation(this.shader_program, "vPosition");
            int vstride = vbo.vx_size * vertrstart;
            GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.HalfFloat, false, this.vbo.vx_size, vbo.vx_stride);
            GL.EnableVertexAttribArray(vpos);

            //Normal Attribute
            npos = GL.GetAttribLocation(this.shader_program, "nPosition");
            int nstride = vbo.vx_size * vertrstart + vbo.n_stride;
            GL.VertexAttribPointer(npos, 3, VertexAttribPointerType.HalfFloat, false, this.vbo.vx_size, vbo.n_stride);
            GL.EnableVertexAttribArray(npos);

            //UV0
            uv0pos = GL.GetAttribLocation(this.shader_program, "uvPosition0");
            GL.VertexAttribPointer(uv0pos, 2, VertexAttribPointerType.HalfFloat, false, this.vbo.vx_size, vbo.uv0_stride);
            GL.EnableVertexAttribArray(uv0pos);

            //If there are BlendIndices there are obviously blendWeights as well
            //Max Indices count found so far is 4. I'm hardcoding it unless i find something else in the files.
            bI = GL.GetAttribLocation(this.shader_program, "blendIndices");
            GL.VertexAttribPointer(bI, 4, VertexAttribPointerType.UnsignedByte , false, vbo.vx_size, vbo.blendI_stride);
            GL.EnableVertexAttribArray(bI);

            bW = GL.GetAttribLocation(this.shader_program, "blendWeights");
            GL.VertexAttribPointer(bW, 4, VertexAttribPointerType.HalfFloat, false, vbo.vx_size, vbo.blendW_stride);
            GL.EnableVertexAttribArray(bW);

            //Testing Upload full bIndices array
            //GL.BindBuffer(BufferTarget.ArrayBuffer, vbo.bIndices_buffer_object);
            //bI = GL.GetAttribLocation(this.shader_program, "blendIndices");
            //GL.VertexAttribPointer(bI, 4, VertexAttribPointerType.Int, false, 0, 0);
            //GL.EnableVertexAttribArray(bI);

            //InverseBind Matrices
            int loc;
            //loc = GL.GetUniformLocation(shader_program, "invBMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.vbo.invBMats);

            //Upload BoneRemap Information
            loc = GL.GetUniformLocation(shader_program, "boneRemap");
            GL.Uniform1(loc, 50, vbo.boneRemap);

            //Bind Matrices
            //loc = GL.GetUniformLocation(shader_program, "BMs");
            //GL.UniformMatrix4(loc, this.vbo.jointData.Count, false, this.getBindRotMats);
            
            //Bind Translations
            //loc = GL.GetUniformLocation(shader_program, "BTs");
            //GL.Uniform3(loc, this.vbo.jointData.Count, this.getBindTransMats);


            //BIND TEXTURES
            Texture tex;
            loc = GL.GetUniformLocation(shader_program, "diffuseFlag");
            if (this.material.textures.Count > 0)
            {
                // Diffuse Texture Exists
                GL.Uniform1(loc, 1.0f);
                tex = this.material.textures[0];
                // Bind Diffuse Texture
                GL.BindTexture(TextureTarget.Texture2D, tex.bufferID);
                // Send Image to Device
                //GL.TexImage2D(TextureTarget.Texture2D, 0, tex.pif, tex.width,
                //    tex.height, 0, tex.pf, PixelType.UnsignedByte, tex.ddsImage.bdata);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int) TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);

                GL.CompressedTexImage2D(TextureTarget.Texture2D, 0, tex.pif,
                    tex.width, tex.height, 0, tex.ddsImage.header.dwPitchOrLinearSize, tex.ddsImage.bdata);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
                //GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Nearest);
                loc = GL.GetUniformLocation(this.shader_program, "color");
                GL.Uniform3(loc, this.color);
            }
            else
            {
                GL.Uniform1(loc, 0.0f);
                loc = GL.GetUniformLocation(this.shader_program, "color");
                GL.Uniform3(loc, this.material.uniforms[0].value.Xyz);
            }

            //Uniform Color probably deprecated
            //Set Color

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            GL.PolygonMode(MaterialFace.Back, PolygonMode.Fill);
            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr) (batchstart*vbo.iLength));

            //Debug.WriteLine("Normal Object {2} vpos {0} cpos {1} prog {3}", vpos, npos, this.name, this.shader_program);
            //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vbo.vertex_buffer_object, this.vbo.color_buffer_object);
            
            GL.DisableVertexAttribArray(vpos);
            GL.DisableVertexAttribArray(npos);
            GL.DisableVertexAttribArray(uv0pos);
            GL.DisableVertexAttribArray(bI);
            GL.DisableVertexAttribArray(bW);

            return true;
        }

        public override GMDL.model Clone()
        {
            GMDL.sharedVBO copy = new GMDL.sharedVBO();
            copy.vertrend = this.vertrend;
            copy.vertrstart = this.vertrstart;
            copy.renderable = true; //Override Renderability
            copy.shader_program = this.shader_program;
            copy.type = this.type;
            copy.vbo = this.vbo;
            copy.name = this.name;
            copy.index = this.index;
            copy.firstskinmat = this.firstskinmat;
            copy.lastskinmat = this.lastskinmat;
            copy.batchcount = this.batchcount;
            copy.batchstart = this.batchstart;
            copy.color = this.color;
            copy.material = this.material;


            return (GMDL.model)copy;
        }



    }

    public class customVBO
    {
        public int vertex_buffer_object;
        public int normal_buffer_object;
        public int element_buffer_object;
        public int color_buffer_object;
        //Testing
        public int bIndices_buffer_object;

        public List<JointBindingData> jointData;
        public float[] invBMats;
        public int vx_size;
        public int vx_stride;
        public int n_stride;
        public int uv0_stride;
        public int blendI_stride;
        public int blendW_stride;
        public int trisCount;
        public int iCount;
        public int iLength;
        public int[] boneRemap = new int[40];
        public DrawElementsType iType;

        
        
        public customVBO()
        {
        }

        public customVBO(GeomObject geom)
        {
            this.LoadFromGeom(geom);
        }

        public void LoadFromGeom(GeomObject geom)
        {
            int size;
            //Set essential parameters
            this.vx_size = geom.vx_size;
            this.vx_stride = geom.offsets[0];
            this.uv0_stride = geom.offsets[1];
            this.n_stride = geom.offsets[2];
            this.blendI_stride = geom.offsets[5];
            this.blendW_stride = geom.offsets[6];
            this.iCount = (int) geom.indicesCount;
            this.trisCount = (int) geom.indicesCount / 3;
            this.iLength = (int)geom.indicesLength;
            this.boneRemap = geom.boneRemap;
            if (geom.indicesLength == 0x2)
                this.iType = DrawElementsType.UnsignedShort;
            else
                this.iType = DrawElementsType.UnsignedInt;
            //Set Joint Data
            this.jointData = geom.jointData;
            invBMats = new float[60 * 16];
            //Copy inverted Matrix to local variable
            for (int i = 0; i < jointData.Count; i++)
                Array.Copy(jointData[i].convertMat(), 0, invBMats, 16 * i, 16);
            
            GL.GenBuffers(1, out vertex_buffer_object);
            //Create normal buffer if normals exist
            if (geom.mesh_descr.Contains("n"))
                GL.GenBuffers(1, out normal_buffer_object);

            GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);
            GL.GenBuffers(1, out bIndices_buffer_object);

            //Upload vertex buffer
            GL.BindBuffer(BufferTarget.ArrayBuffer, vertex_buffer_object);
            GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (geom.vx_size * geom.vertCount),
                geom.vbuffer, BufferUsageHint.StaticDraw);
            GL.GetBufferParameter(BufferTarget.ArrayBuffer, BufferParameterName.BufferSize,
                out size);
            if (size != geom.vx_size * geom.vertCount)
                throw new ApplicationException(String.Format("Problem with vertex buffer"));
            
            //Upload index buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, element_buffer_object);
            GL.BufferData(BufferTarget.ElementArrayBuffer, (IntPtr)(geom.indicesLength * geom.indicesCount), geom.ibuffer, BufferUsageHint.StaticDraw);

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

            //    Debug.WriteLine("Indices {0} {1} {2} {3}", br.ReadByte(), br.ReadByte(), br.ReadByte(), br.ReadByte());
            //    ms.Position += geom.vx_size - 4;
            //}

            //GL.BindBuffer(BufferTarget.ArrayBuffer, bIndices_buffer_object);
            //GL.BufferData(BufferTarget.ArrayBuffer, (IntPtr) (sizeof(int) * 4 * geom.vertCount),
            //    bIndices, BufferUsageHint.StaticDraw);

        }
    }

    public class GeomObject
    {
        //public List<Vector3> verts = new List<Vector3>();
        //public List<Vector3> normals = new List<Vector3>();
        //public List<Vector3> tangents = new List<Vector3>();
        //public List<List<Vector2>> uvs = new List<List<Vector2>>();
        public string mesh_descr;
        public bool interleaved;
        public int vx_size;

        //Counters
        public int indicesCount=0;
        public int indicesLength = 0;
        public int vertCount = 0;

        //make sure there are enough buffers for non interleaved formats
        public byte[] ibuffer;
        public byte[] vbuffer;
        public byte[] cbuffer;
        public byte[] nbuffer;
        public byte[] ubuffer;
        public byte[] tbuffer;
        public List<int[]> bIndices;
        public List<float[]> bWeights;
        public int[] offsets; //List to save strides according to meshdescr
        public int[] boneRemap = new int[50];

        //Joint info
        public List<JointBindingData> jointData = new List<JointBindingData>();
        
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
            //Debug.WriteLine("half {0} {1} {2}", temp[0],temp[1],temp[2]);
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

    }
    
    public class Material
    {
        public string name;
        public string type;
        public MatOpts opts;
        public List<int> materialflags = new List<int>();
        public List<Uniform> uniforms = new List<Uniform>();
        public List<Sampler> samplers = new List<Sampler>();
        public List<Texture> textures = new List<Texture>();

        public void prepTextures()
        {
            int counter = 0;
            foreach (Sampler sam in samplers){
                Texture tex = new Texture();
                DDSImage dds;
                try { 
                    dds = new DDSImage(File.ReadAllBytes(Path.Combine(Model_Viewer.Util.dirpath, sam.path)));
                } catch (System.IO.FileNotFoundException e) {
                    Debug.WriteLine("Texture Not Found:" + Path.Combine(Model_Viewer.Util.dirpath, sam.path));
                    continue;
                }

                Debug.WriteLine("Sampler Name {2} Path "+sam.path+" Width {0} Height {1}", dds.header.dwWidth, dds.header.dwHeight,sam.name);
                tex.width = dds.header.dwWidth;
                tex.height = dds.header.dwHeight;
                switch (dds.header.ddspf.dwFourCC)
                {
                    //DXT1
                    case (0x31545844):
                        tex.pif = PixelInternalFormat.CompressedRgbaS3tcDxt1Ext;
                        break;
                    case (0x35545844):
                        tex.pif = PixelInternalFormat.CompressedRgbaS3tcDxt5Ext;
                        break;
                    default:
                        throw new ApplicationException("Unimplemented Pixel format");
                }
                //Force RGBA for now
                tex.pf = PixelFormat.Rgba;
                tex.bufferID = GL.GenTexture();
                tex.ddsImage = dds;
                counter++;
                this.textures.Add(tex);
            }
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
        public string path;
    }

    public class Texture
    {
        public int bufferID;
        public int width;
        public int height;
        public PixelInternalFormat pif;
        public PixelFormat pf;
        public DDSImage ddsImage;

    }

    public class Joint : model
    {
        private int vertex_buffer_object;
        private int element_buffer_object;
        public int jointIndex;
        public Vector3 color;


        public Joint()
        {
            //Create Buffers
            GL.GenBuffers(1, out vertex_buffer_object);
            //GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);
        }

        //Empty stuff
        public override model Clone()
        {
            throw new ApplicationException("Not Implemented yet");
        }
            
        //Render should render Bones from joint to children
        public override bool render()
        {
            if (this.renderable == false)
            {
                //Debug.WriteLine("Not Renderable");
                return false;
            }
            if (this.children.Count == 0)
                return false;

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
            int vpos, cpos;
            GL.BindBuffer(BufferTarget.ArrayBuffer, this.vertex_buffer_object);
            vpos = GL.GetAttribLocation(this.shader_program, "vPosition");
            GL.VertexAttribPointer(vpos, 3, VertexAttribPointerType.Float, false, 0, 0);
            GL.EnableVertexAttribArray(vpos);

            //Color Attribute
            cpos = GL.GetAttribLocation(this.shader_program, "vcolor");
            GL.VertexAttribPointer(cpos, 3, VertexAttribPointerType.Float, false, 0, (IntPtr)arraysize);
            GL.EnableVertexAttribArray(cpos);

            //Render Elements
            //Bind elem buffer
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.element_buffer_object);
            GL.PointSize(10.0f);
            //GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Point);


            GL.DrawArrays(PrimitiveType.Lines, 0, indices.Length);
            GL.DrawArrays(PrimitiveType.Points, 0, vertsf.Length);
            //Draw only Joint Point
            //GL.DrawArrays(PrimitiveType.Points, 0, 1);

            GL.DisableVertexAttribArray(vpos);
            GL.DisableVertexAttribArray(cpos);
            
            return true;
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

            charbuffer = br.ReadChars(0x10);
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

            Debug.WriteLine("Animation File");
            Debug.WriteLine("Frames {0} Nodes {1}", frameCount, nodeCount);
            Debug.WriteLine("Parsing Nodes NodeOffset {0}", nodeOffset);

            fs.Seek(nodeOffset, SeekOrigin.Begin);
            NodeData nodedata = new NodeData();
            nodedata.parseNodes(fs, nodeCount);
            nodeData = nodedata;

            Debug.WriteLine("Parsing Animation Frame Data Offset {0}", animeFrameDataOff);
            fs.Seek(animeFrameDataOff, SeekOrigin.Begin);
            AnimFrameData framedata = new AnimFrameData();
            framedata.Load(fs, frameCount);
            this.frameData = framedata;

        }

    }

    public class JointBindingData
    {
        public Matrix4 invBindMatrix = Matrix4.Identity;
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
            //transpose the matrix
            //invBindMatrix.Transpose();
            //invBindMatrix.Invert();
            
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



