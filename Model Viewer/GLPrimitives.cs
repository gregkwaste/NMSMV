using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Model_Viewer
{
    class Sphere {
        private float radius;
        private float[] verts;
        private float[] normals;
        private int[] indices;


        //Constructor
        public Sphere(float radius)
        {
            int latBands = 10;
            int longBands = 10;

            //Init Arrays
            int arraysize = (latBands + 1) * (longBands + 1) * 3;
            int indarraysize = latBands * longBands * 3;
            verts = new float[arraysize];
            normals = new float[arraysize];
            indices = new int[2 * indarraysize];


            for (int lat = 0; lat <= latBands; lat++)
            {
                float theta = lat * (float)Math.PI / latBands;
                float sintheta = (float)Math.Sin(theta);
                float costheta = (float)Math.Cos(theta);

                for (int lng = 0; lng <= longBands; lng++)
                {
                    float phi = lng * 2 * (float) Math.PI / longBands;
                    float sinphi = (float) Math.Sin(phi);
                    float cosphi = (float) Math.Cos(phi);

                    float x = cosphi * sintheta;
                    float y = costheta;
                    float z = sinphi * sintheta;

                    verts[lat * latBands * 3 + 3 * lng + 0] = x;
                    verts[lat * latBands * 3 + 3 * lng + 1] = y;
                    verts[lat * latBands * 3 + 3 * lng + 2] = z;

                    normals[lat * latBands * 3 + 3 * lng + 0] = x;
                    normals[lat * latBands * 3 + 3 * lng + 1] = y;
                    normals[lat * latBands * 3 + 3 * lng + 2] = z;
                }

            }


            //Indices
            for (int lat = 0; lat < latBands; lat++)
            {
                for (int lng = 0; lng < longBands; lng++)
                {
                    int first = lat * (longBands + 1) + lng;
                    int second = first + longBands + 1;

                    indices[lat * latBands * 6 + 6 * lng + 0] = second;
                    indices[lat * latBands * 6 + 6 * lng + 1] = first;
                    indices[lat * latBands * 6 + 6 * lng + 2] = first + 1;

                    indices[lat * latBands * 6 + 6 * lng + 3] = second+1;
                    indices[lat * latBands * 6 + 6 * lng + 4] = second;
                    indices[lat * latBands * 6 + 6 * lng + 5] = first + 1;
                }
            }




        }

        public GMDL.customVBO getVBO()
        {
            GMDL.GeomObject geom = new GMDL.GeomObject();

            //Set main Geometry Info
            geom.vertCount = verts.Length / 3;
            geom.indicesCount = indices.Length;
            geom.indicesLength = 0x4;
            
            //Set Strides
            geom.vx_size = 3 * 4; //3 Floats * 4 Bytes each
            geom.small_mesh_descr = "";
            geom.small_vx_size = -1;

            //Set Buffer Offsets
            geom.offsets = new int[7];
            geom.bufInfo = new List<GMDL.bufInfo>();
            geom.small_offsets = new int[7];
            
            for (int i = 0; i < 7; i++)
            {
                geom.bufInfo.Add(null);
                geom.offsets[i] = -1;
                geom.small_offsets[i] = -1;
            }

            geom.mesh_descr = "vn";
            geom.offsets[0] = 0;
            geom.bufInfo[0] = new GMDL.bufInfo(0, OpenTK.Graphics.OpenGL.VertexAttribPointerType.Float, 3, 0, "vPosition");
            geom.bufInfo[2] = new GMDL.bufInfo(0, OpenTK.Graphics.OpenGL.VertexAttribPointerType.Float, 3, 0, "nPosition");
            geom.offsets[2] = 0;
            
            //Set Buffers
            geom.ibuffer = new byte[4* indices.Length];
            Buffer.BlockCopy(indices, 0, geom.ibuffer, 0, geom.ibuffer.Length);
            geom.vbuffer = new byte[4 * verts.Length];
            Buffer.BlockCopy(indices, 0, geom.vbuffer, 0, geom.vbuffer.Length);

            GMDL.customVBO vbo = new GMDL.customVBO(geom);
            
            return vbo;
        }

    }






}
