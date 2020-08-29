using System;
using System.Collections.Generic;
using MVCore.GMDL;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace MVCore.Engine
{
    public class IVector3
    {
        public ulong x;
        public ulong y;
        public ulong z;
        
        public IVector3(ulong xx, ulong yy, ulong zz)
        {
            x = xx;
            y = yy;
            z = zz;
        }
    }


    public class Octree
    {
        public OctNode root;
        private readonly ulong max_width;

        enum EXPANSION_DIRECTION
        {
            LEFT = 0b1,
            RIGHT = 0b10,
            UP = 0b100,
            DOWN = 0b1000,
            FRONT = 0b10000,
            BACK = 0b100000,
            NONE = 0b0
        }

        public Octree(ulong width)
        {
            max_width = width;

            root = new OctNode(1, width);
            root.AABBMIN = new IVector3(0, 0, 0);
            root.AABBMAX = new IVector3(max_width, max_width, max_width);
        }

        ~Octree()
        {

        }


        //Methods
        public void insert(Model m)
        {
            if (m.type == TYPES.MODEL || m.type == TYPES.REFERENCE)
            {
                //Transform coordinates
                IVector3 mincoords = transform_coords(m.AABBMIN, max_width);
                IVector3 maxcoords = transform_coords(m.AABBMAX, max_width);

                if (checkIfFits(maxcoords) == 0 && checkIfFits(mincoords) == 0)
                {
                    root.insert(m, mincoords, maxcoords);
                }
                else
                {
                    //The octree should be expanded
                    Console.WriteLine("WARNING THE CURRENT OCTREE DOES NOT FIT THE OBJECT");
                }
            }
            
            //Look for other scenes
            foreach (Model child in m.children)
                insert(child);
        }

        public int checkIfFits(IVector3 coords)
        {
            //Coords should be transformed at this point to 0-max_width range

            int expansion_mode = 0;
            if (coords.x < 0)
                expansion_mode |= (int) EXPANSION_DIRECTION.LEFT;
            else if (coords.x > max_width)
                expansion_mode |= (int)EXPANSION_DIRECTION.RIGHT;

            if (coords.y < 0)
                expansion_mode |= (int)EXPANSION_DIRECTION.BACK;
            else if (coords.y > max_width)
                expansion_mode |= (int)EXPANSION_DIRECTION.FRONT;

            if (coords.z < 0)
                expansion_mode |= (int)EXPANSION_DIRECTION.DOWN;
            else if (coords.z > max_width)
                expansion_mode |= (int)EXPANSION_DIRECTION.UP;

            return expansion_mode;
        }

        public void report()
        {
            root.report();
        }

        public void clear()
        {
            root.clear();
        }

        public void render(int pass) {
            
            root.render(pass);
            
        }


        public static IVector3 transform_coords(Vector3 coords, ulong max_width)
        {
            ulong x, y, z;

            x = (ulong) (coords.X + max_width / 2);
            y = (ulong) (coords.Y + max_width / 2);
            z = (ulong) (coords.Z + max_width / 2);

            return new IVector3(x, y, z);
        }

        public static int calculate_index(IVector3 pos, ulong width)
        {
            int x = ((pos.x & width) > 0) ? 1 : 0;
            int y = ((pos.y & width) > 0) ? 1 : 0;
            int z = ((pos.z & width) > 0) ? 1 : 0;

            return z * 0b100 + y * 0b010 + x* 0b001;
        }


    }


    public class OctNode
    {
        public List<OctNode> children = new List<OctNode>(8);
        public List<Model> objects = new List<Model>();
        public static int maxObjects = 8;
        public static int maxDepth = 3;
        public static ulong maxWidth;
        public IVector3 AABBMIN;
        public IVector3 AABBMAX;
        public int depth;
        private readonly ulong width;
        private bool isLeaf = true;

        public OctNode(int l, ulong w){
            depth = l;
            maxWidth = w;
            width = maxWidth >> (l - 1);
        }

        ~OctNode()
        {

        }

        //Populate children Nodes
        void split()
        {
            isLeaf = false; //From now on the node works as a 
            for (int i = 0; i < 8; i++)
                children.Add(new OctNode(depth + 1, maxWidth));
            
            //Set proper bound limits to all the nodes

            ulong hAABBX = (AABBMIN.x + AABBMAX.x) / 2;
            ulong hAABBY = (AABBMIN.y + AABBMAX.y) / 2;
            ulong hAABBZ = (AABBMIN.z + AABBMAX.z) / 2;

            //000 (SHOULD BE OK)
            children[0].AABBMIN = AABBMIN;
            children[0].AABBMAX = new IVector3(hAABBX, hAABBY, hAABBZ);
            //001 (SHOULD BE OK)
            children[1].AABBMIN = new IVector3(hAABBX, AABBMIN.y, AABBMIN.z);
            children[1].AABBMAX = new IVector3(AABBMAX.x, hAABBY, hAABBZ);
            //010 (SHOULD BE OK)
            children[2].AABBMIN = new IVector3(AABBMIN.x, hAABBY, AABBMIN.z);
            children[2].AABBMAX = new IVector3(hAABBX, AABBMAX.y, hAABBZ);
            //011 (SHOULD BE OK)
            children[3].AABBMIN = new IVector3(hAABBX, hAABBY, AABBMIN.z);
            children[3].AABBMAX = new IVector3(AABBMAX.x, AABBMAX.y, hAABBZ);

            //100 (SHOULD BE OK)
            children[4].AABBMIN = new IVector3(AABBMIN.x, AABBMIN.y, hAABBZ);
            children[4].AABBMAX = new IVector3(hAABBX, hAABBY, AABBMAX.z);
            //101 (SHOULD BE OK)
            children[5].AABBMIN = new IVector3(hAABBX, AABBMIN.y, hAABBZ);
            children[5].AABBMAX = new IVector3(AABBMAX.x, hAABBY, AABBMAX.z);
            //110 (SHOULD BE OK)
            children[6].AABBMIN = new IVector3(AABBMIN.x, hAABBY, hAABBZ);
            children[6].AABBMAX = new IVector3(hAABBX, AABBMAX.y, AABBMAX.z);
            //111 (SHOULD BE OK)
            children[7].AABBMIN = new IVector3(hAABBX, hAABBY, hAABBZ);
            children[7].AABBMAX = AABBMAX;
        }

        //Insert object to the node
        public void insert(Model m, IVector3 t_coords_min, IVector3 t_coords_max)
        {
            if (depth == maxDepth)
            {
                objects.Add(m);
                return;
            }

            //Check if the object can fit to one of the children
            int i_min = Octree.calculate_index(t_coords_min, (ulong) width / 2);
            int i_max = Octree.calculate_index(t_coords_max, (ulong) width / 2);


            if ((i_min == i_max) && isLeaf)
            {
                //Otherwise the node needs splitting
                split();
                //Add the object to the calculated index
                children[i_min].insert(m, t_coords_min, t_coords_max);
                isLeaf = false;
            } else
            {
                //The object should stay here
                objects.Add(m);
            }
        }

        public void render(int pass)
        {
            GL.UseProgram(pass);

            Vector3 convAABBMIN = new Vector3(AABBMIN.x, AABBMIN.y, AABBMIN.z) - new Vector3(maxWidth / 2.0f);
            Vector3 convAABBMAX = new Vector3(AABBMAX.x, AABBMAX.y, AABBMAX.z) - new Vector3(maxWidth / 2.0f);

            //Generate all 8 points from the AABB
            float[] verts1 = new float[] {  convAABBMIN.X, convAABBMIN.Y, convAABBMIN.Z,
                                           convAABBMAX.X, convAABBMIN.Y, convAABBMIN.Z,
                                           convAABBMIN.X, convAABBMAX.Y, convAABBMIN.Z,
                                           convAABBMAX.X, convAABBMAX.Y, convAABBMIN.Z,

                                           convAABBMIN.X, convAABBMIN.Y, convAABBMAX.Z,
                                           convAABBMAX.X, convAABBMIN.Y, convAABBMAX.Z,
                                           convAABBMIN.X, convAABBMAX.Y, convAABBMAX.Z,
                                           convAABBMAX.X, convAABBMAX.Y, convAABBMAX.Z };


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

            //Render Elements
            GL.PointSize(5.0f);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, eb_bbox);

            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Line);

            GL.DrawRangeElements(PrimitiveType.Triangles, 0, verts1.Length,
                indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);

            GL.DisableVertexAttribArray(0);

            GL.DeleteBuffer(vb_bbox);
            GL.DeleteBuffer(eb_bbox);

            //Render all children recursively
            foreach (OctNode o in children)
                o.render(pass);
        }


        public void report()
        {
            Console.WriteLine("OCTNODE AABBMIN {0} {1} {2} - AABBMAX {3} {4} {5} - OBJECTS: {6} - CHILDREN NODES: {7}",
                AABBMIN.x, AABBMIN.y, AABBMIN.z,
                AABBMAX.x, AABBMAX.y, AABBMAX.z,
                objects.Count, children.Count);
        }

        public void clear()
        {
            objects.Clear();
            foreach (OctNode c in children)
                c.clear();
            children.Clear();
        }
    }
}
