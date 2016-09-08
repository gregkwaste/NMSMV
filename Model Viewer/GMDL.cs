using System;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics;
using OpenTK;

namespace GMDL
{
    public interface model{
        bool render();
        GMDL.model Clone();
        bool Renderable { get; set; }
        int ShaderProgram { set; get; }
        int Index { set; get; }
        string Type { set; get; }
        string Name { set; get; }
        List<model> Children { set; get; }
    };
    
    public class locator: model
    {
        //private Vector3[] verts;
        private float[] verts = new float[6*3];
        private float[] colors = new float[6 * 3];
        private Int32[] indices;
        public bool renderable = true;
        int vertex_buffer_object;
        int color_buffer_object;
        int element_buffer_object;
        public int shader_program = -1;
        string type = "";
        string name = "";
        public int index;

        public int Index
        {
            get
            {
                return this.index;
            }
            set
            {
                this.index = value;
            }
        }
        public bool Renderable
        {
            get
            {
                return this.renderable;
            }
            set
            {
                this.renderable = value;
            }
        }
        public int ShaderProgram
        {
            set
            {
                this.shader_program = value;
            }
            get
            {
                return this.shader_program;
            }
        }
        public string Name
        {
            set
            {
                this.name = value;
            }

            get
            {
                return this.name;
            }

        }
        public string Type
        {
            set
            {
                this.type = value;
            }
            get
            {
                return this.type;
            }
        }
        public List<model> children = new List<model>();
        public List<model> Children
        {
            set
            {
                this.children = value;
            }

            get
            {
                return this.children;
            }
        }

        //Default Constructor
        public locator()
        {
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
            int size;
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

        public bool render()
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

        public GMDL.model Clone()
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
        public string name = "";
        public string type = "";
        public customVBO vbo;
        public Vector3 color = new Vector3();
        public bool renderable = true;
        public int shader_program = -1;
        public int index;

        //Interface properties
        public int Index
        {
            get
            {
                return this.index;
            }
            set
            {
                this.index = value;
            }
        }
        public bool Renderable
        {
            get { return this.renderable; }
            set { this.renderable = value; }
        }
        public int ShaderProgram
        {
            set
            {
                this.shader_program = value;
            }
            get
            {
                return this.shader_program;
            }
        }
        public string Name
        {
            set
            {
                this.name = value;
            }

            get
            {
                return this.name;
            }
        }
        public string Type
        {
            set
            {
                this.type = value;
            }
            get
            {
                return this.type;
            }
        }
        public List<model> children = new List<model>();
        public List<model> Children
        {
            set
            {
                this.children = value;
            }

            get
            {
                return this.children;
            }
        }

        public bool render()
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

            int vpos, npos;
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

            //Set Color
            int loc;
            loc = GL.GetUniformLocation(this.shader_program, "color");
            GL.Uniform3(loc, this.color);

            //Render Elements
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, this.vbo.element_buffer_object);

            GL.PolygonMode(MaterialFace.Front, PolygonMode.Fill);
            GL.PolygonMode(MaterialFace.Back, PolygonMode.Fill);
            GL.DrawRangeElements(PrimitiveType.Triangles, vertrstart, vertrend,
                batchcount, vbo.iType, (IntPtr) (batchstart*vbo.iLength));

            //Debug.WriteLine("Normal Object {2} vpos {0} cpos {1} prog {3}", vpos, npos, this.name, this.shader_program);
            //Debug.WriteLine("Buffer IDs vpos {0} vcol {1}", this.vbo.vertex_buffer_object, this.vbo.color_buffer_object);
            
            GL.DisableVertexAttribArray(vpos);
            GL.DisableVertexAttribArray(npos);

            return true;
        }

        public GMDL.model Clone()
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
            

            return (GMDL.model)copy;
        }
    }

    public class customVBO
    {
        public int vertex_buffer_object;
        public int normal_buffer_object;
        public int element_buffer_object;
        public int color_buffer_object;

        public int vx_size;
        public int vx_stride;
        public int n_stride;
        public int trisCount;
        public int iCount;
        public int iLength;
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
            this.n_stride = geom.offsets[2];
            this.iCount = (int) geom.indicesCount;
            this.trisCount = (int) geom.indicesCount / 3;
            this.iLength = (int)geom.indicesLength;
            if (geom.indicesLength == 0x2)
                this.iType = DrawElementsType.UnsignedShort;
            else
                this.iType = DrawElementsType.UnsignedInt;
            

            GL.GenBuffers(1, out vertex_buffer_object);
            //Create normal buffer if normals exist
            if (geom.mesh_descr.Contains("n"))
                GL.GenBuffers(1, out normal_buffer_object);

            GL.GenBuffers(1, out color_buffer_object);
            GL.GenBuffers(1, out element_buffer_object);

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
        public UInt32 indicesCount=0;
        public int indicesLength = 0;
        public UInt32 vertCount = 0;

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
}
