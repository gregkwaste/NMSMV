using System;
using System.Collections.Generic;
using System.Text;

using MVCore.GMDL;
using OpenTK;


namespace MVCore
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
        private ulong max_width;

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
        public void insert(model m)
        {
            //Transform coordinates
            IVector3 mincoords = transform_coords(m.AABBMIN, max_width);
            IVector3 maxcoords = transform_coords(m.AABBMAX, max_width);

            if (checkIfFits(maxcoords) == 0 && checkIfFits(mincoords) == 0)
            {
                root.insert(m, mincoords, maxcoords);
            } else
            {
                //The octree should be expanded
                Console.WriteLine("WARNING THE CURRENT OCTREE DOES NOT FIT THE OBJECT");
            }

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
        public List<model> objects = new List<model>();
        public static int maxObjects = 8;
        public static int maxDepth = 3;
        public static ulong maxWidth;
        public IVector3 AABBMIN;
        public IVector3 AABBMAX;
        public int depth;
        private ulong width;
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
        public void insert(model m, IVector3 t_coords_min, IVector3 t_coords_max)
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
            } else
            {
                //The object should stay here
                objects.Add(m);
            }


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
