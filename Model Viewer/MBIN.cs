using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Collections.Generic;
using System;
using OpenTK;
using Model_Viewer;
using libMBIN;
using libMBIN.Models;
using libMBIN.Models.Structs;
using System.Linq;

public static class ANIMMBIN
{
    public static XmlDocument Parse(FileStream fs)
    {
        //Readers
        BinaryReader br = new BinaryReader(fs);

        //Create new xml document
        XmlDocument xml = new XmlDocument();
        char[] charbuffer = new char[0x100];

        return xml;
    }
}

public enum TYPES
{
    MESH=0x0,
    LOCATOR,
    JOINT,
    LIGHT,
    EMITTER,
    COLLISION,
    SCENE,
    REFERENCE,
    DECAL,
    UNKNOWN
}

public enum COLLISIONTYPES
{
    MESH = 0x0,
    SPHERE,
    CYLINDER,
    BOX,
    CAPSULE    
}

public enum PALLETENAMES
{

}

public class meshMetaData
{
    public string name;
    public ulong hash;
    public uint vs_size;
    public uint vs_abs_offset;
    public uint is_size;
    public uint is_abs_offset;
}

public class meshData
{
    public ulong hash;
    public byte[] vs_buffer;
    public byte[] is_buffer;
}

public static class SCENEMBIN
{
    public static XmlDocument Parse(FileStream fs)
    {
        //Readers
        BinaryReader br = new BinaryReader(fs);
        
        //Create new xml document
        XmlDocument xml = new XmlDocument();
        char[] charbuffer = new char[0x100];

        fs.Seek(0x18, SeekOrigin.Begin);
        //Read Data Type
        charbuffer = br.ReadChars(0x48);
        //Read Directory Type
        charbuffer = br.ReadChars(0x80);
        string scene_name = new string(charbuffer);
        //Read What the file is about
        charbuffer = br.ReadChars(0x10);
        fs.Seek(0x28, SeekOrigin.Current);
        uint geometry_file_offset = (uint) fs.Position + br.ReadUInt32();
        fs.Seek(0xC, SeekOrigin.Current);
        uint section_offset = (uint)fs.Position + br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        uint section_count = br.ReadUInt32();
        //Console.WriteLine("Section Count: {0}",section_count);
        fs.Seek(0x4, SeekOrigin.Current);

        //Geometry File Name
        fs.Seek(geometry_file_offset+ 0x20, SeekOrigin.Begin);
        charbuffer = br.ReadChars(0x100);
        string geometry_file_name = new string(charbuffer);
        //Console.WriteLine(geometry_file_name);
        //Console.WriteLine(" ");

        XmlElement el, root;
        root = xml.CreateElement("ROOT");
        xml.AppendChild(root);

        el = xml.CreateElement("SCENE");
        el.InnerText = scene_name.Split('\0')[0];
        root.AppendChild(el);

        el = xml.CreateElement("GEOMETRY");
        el.InnerText = geometry_file_name.Split('\0')[0];
        root.AppendChild(el);

        el = xml.CreateElement("SECTIONS");
        //Parse Sections
        fs.Seek(section_offset, SeekOrigin.Begin);

        for (int i = 0; i < section_count; i++)
            ParseSections(fs, xml, el);

        root.AppendChild(el);

        xml.Save("test.xml");
        return xml;
    }
    
    private static bool ParseSections(FileStream fs, XmlDocument xml, XmlElement parent){
        BinaryReader br = new BinaryReader(fs);
        char[] charbuffer = new char[0x100];
        //Create Element       
        XmlElement el;
        el = xml.CreateElement("SECTION");

        XmlElement info = xml.CreateElement("INFO");
        //Name
        XmlElement part_name = xml.CreateElement("NAME");
        charbuffer = br.ReadChars(0x80);
        part_name.InnerText = (new string(charbuffer)).Trim('\0');
        info.AppendChild(part_name);
        //Type
        XmlElement part_type = xml.CreateElement("TYPE");
        charbuffer = br.ReadChars(0x10);
        part_type.InnerText = (new string(charbuffer)).Trim('\0');
        info.AppendChild(part_type);

        //Transmat
        XmlElement part_trans = xml.CreateElement("TRANSMAT");
        //Get data
        string[] trans = new string[9];
        trans[0] = br.ReadSingle().ToString();
        trans[1] = br.ReadSingle().ToString();
        trans[2] = br.ReadSingle().ToString();
        trans[3] = br.ReadSingle().ToString();
        trans[4] = br.ReadSingle().ToString();
        trans[5] = br.ReadSingle().ToString();
        trans[6] = br.ReadSingle().ToString();
        trans[7] = br.ReadSingle().ToString();
        trans[8] = br.ReadSingle().ToString();

        part_trans.InnerText = string.Join(",", trans);
        info.AppendChild(part_trans);

        //Append Info
        el.AppendChild(info);

        fs.Seek(0x4, SeekOrigin.Current);
        uint options_offset = (uint) fs.Position + br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        int options_count = (int) br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current); //Skip 0x01AAAAAA
        uint children_offset = (uint)fs.Position + br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        int children_count = (int)br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current); //Skip 0x01AAAAAA

        //Parse Options
        uint back = (uint) fs.Position;
        if (options_count > 0)
        {
            fs.Seek(options_offset, SeekOrigin.Begin);
            XmlElement opts = xml.CreateElement("OPTIONS");
            for (int i = 0; i < options_count; i++)
            {
                //OptionsName
                XmlElement opt = xml.CreateElement("OPTION");
                charbuffer = br.ReadChars(0x20);
                opt.SetAttribute("NAME", (new string(charbuffer)).Trim('\0'));
                charbuffer = br.ReadChars(0x100);
                opt.SetAttribute("VALUE", (new string(charbuffer)).Trim('\0'));
                opts.AppendChild(opt);
            }
            el.AppendChild(opts);
        }
        fs.Seek(back,SeekOrigin.Begin);

        //Parse Children
        back = (uint) fs.Position;

        if (children_count > 0)
        {
            fs.Seek(children_offset, SeekOrigin.Begin);
            XmlElement children = xml.CreateElement("CHILDREN");
            for (int i = 0; i < children_count; i++)
            {
                ParseSections(fs, xml, children);
            }
            el.AppendChild(children);
        }
        fs.Seek(back, SeekOrigin.Begin);

        parent.AppendChild(el);
        
        return true;
    }
    
}

public static class MATERIALMBIN
{
    public enum MATERIALFLAGS
    {
        _F01_DIFFUSEMAP=0,
        _F02_SKINNED,
        _F03_NORMALMAP,
        _F04_,
        _F05_,
        _F06_,
        _F07_UNLIT,
        _F08_,
        _F09_TRANSPARENT,
        _F10_NORECEIVESHADOW,
        _F11_ALPHACUTOUT,
        _F12_BATCHED_BILLBOARD,
        _F13_UVANIMATION,
        _F14_UVSCROLL,
        _F15_WIND,
        _F16_DIFFUSE2MAP,
        _F17_MULTIPLYDIFFUSE2MAP,
        _F18_UVTILES,
        _F19_BILLBOARD,
        _F20_PARALLAXMAP,
        _F21_VERTEXCOLOUR,
        _F22_TRANSPARENT_SCALAR,
        _F23_CAMERA_RELATIVE,
        _F24_AOMAP,
        _F25_ROUGHNESS_MASK,
        _F26_STRETCHY_PARTICLE,
        _F27_VBTANGENT,
        _F28_VBSKINNED,
        _F29_VBCOLOUR,
        _F30_REFRACTION_MAP,
        _F31_DISPLACEMENT,
        _F32_LEAF,
        _F33_GRASS,
        _F34_GLOW,
        _F35_GLOW_MASK,
        _F36_DOUBLESIDED,
        _F37_RECOLOUR,
        _F38_NO_DEFORM,
        _F39_METALLIC_MASK,
        _F40_SUBSURFACE_MASK,
        _F41_DETAIL_DIFFUSE,
        _F42_DETAIL_NORMAL,
        _F43_NORMAL_TILING,
        _F44_IMPOSTER,
        _F45_SCANABLE,
        _F46_BILLBOARD_AT,
        _F47_WRITE_LOG_Z,
        _F48_WARPED_DIFFUSE_LIGHTING,
        _F49_DISABLE_AMBIENT,
        _F50_DISABLE_POSTPROCESS,
        _F51_DECAL_DIFFUSE,
        _F52_DECAL_NORMAL,
        _F53_COLOURISABLE,
        _F54_,
        _F55_,
        _F56_,
        _F57_,
        _F58_,
        _F59_,
        _F60_,
        _F61_,
        _F62_,
        _F63_,
        _F64_ 
    }

    public static XmlDocument Parse(FileStream fs)
    {
        XmlDocument xml = new XmlDocument();

        //Readers
        BinaryReader br = new BinaryReader(fs);
        char[] charbuffer = new char[0x100];

        fs.Seek(0x18, SeekOrigin.Begin);
        //Read Data Type
        charbuffer = br.ReadChars(0x48);
        //Read Directory Type
        br.BaseStream.Seek(0x60, SeekOrigin.Begin);
        charbuffer = br.ReadChars(0x80);
        string matname = new string(charbuffer);
        
        //Read What the file is about
        charbuffer = br.ReadChars(0x20);
        string classname = new string(charbuffer);
        
        XmlElement el, root;

        root = xml.CreateElement("ROOT");
        
        XmlElement mat = xml.CreateElement("MATERIAL");
        el = xml.CreateElement("NAME");
        el.InnerText = matname.Trim('\0');
        mat.AppendChild(el);
        el = xml.CreateElement("CLASS");
        el.InnerText = classname.Trim('\0');
        mat.AppendChild(el);

        int transparency = br.ReadInt32();
        bool castshadow = Convert.ToBoolean(br.ReadByte());
        bool disabletestZ = Convert.ToBoolean(br.ReadByte());

        el = xml.CreateElement("PROPERTY");
        el.SetAttribute("NAME", "TRANSPARENCY");
        el.SetAttribute("VALUE",transparency.ToString());
        mat.AppendChild(el);
        el = xml.CreateElement("PROPERTY");
        el.SetAttribute("NAME", "CASTSHADOW");
        el.SetAttribute("VALUE", castshadow.ToString());
        mat.AppendChild(el);
        el = xml.CreateElement("PROPERTY");
        el.SetAttribute("NAME", "DISABLETESTZ");
        el.SetAttribute("VALUE", disabletestZ.ToString());
        mat.AppendChild(el);
        //Read Link
        charbuffer = br.ReadChars(0x80);
        el = xml.CreateElement("PROPERTY");
        el.SetAttribute("NAME", "LINK");
        el.SetAttribute("VALUE", (new string(charbuffer)).Trim('\0'));
        mat.AppendChild(el);
        //Read Shader Name
        charbuffer = br.ReadChars(0x80);
        el = xml.CreateElement("PROPERTY");
        el.SetAttribute("NAME", "SHADER");
        el.SetAttribute("VALUE", (new string(charbuffer)).Trim('\0'));
        mat.AppendChild(el);

        //Force address for now, seems to work with all material files
        fs.Seek(0x2, SeekOrigin.Current);

        //Materialflags
        uint matflagsOffset = (uint) fs.Position + br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        int matflagsCount = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        //Uniforms
        uint uniOffset = (uint)fs.Position + br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        int uniCount = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        //Samplers
        uint samplerOffset = (uint)fs.Position + br.ReadUInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        int samplerCount = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);


        //Get MaterialFlags
        fs.Seek(matflagsOffset, SeekOrigin.Begin);
        el = xml.CreateElement("MATERIALFLAGS");
        for (int i = 0; i < matflagsCount; i++)
        {
            MATERIALFLAGS opt = (MATERIALFLAGS)br.ReadInt32();
            XmlElement mt = xml.CreateElement("FLAG");
            mt.InnerText = opt.ToString();
            el.AppendChild(mt);
        }
        mat.AppendChild(el);

        //Get Uniforms
        fs.Seek(uniOffset, SeekOrigin.Begin);
        el = xml.CreateElement("UNIFORMS");
        for (int i = 0; i < uniCount; i++)
        {
            XmlElement opt = xml.CreateElement("UNIFORM");
            charbuffer = br.ReadChars(0x20);
            string[] trans = new string[4];
            trans[0] = br.ReadSingle().ToString();
            trans[1] = br.ReadSingle().ToString();
            trans[2] = br.ReadSingle().ToString();
            trans[3] = br.ReadSingle().ToString();

            fs.Seek(0x10, SeekOrigin.Current);
            opt.SetAttribute("NAME", (new string(charbuffer)).Trim('\0'));
            opt.SetAttribute("VALUE", string.Join(",", trans));
            el.AppendChild(opt);
        }
        mat.AppendChild(el);

        //Get Samplers
        fs.Seek(samplerOffset, SeekOrigin.Begin);
        el = xml.CreateElement("SAMPLERS");
        for (int i = 0; i < samplerCount; i++)
        {
            XmlElement sampl = xml.CreateElement("SAMPLER");
            charbuffer = br.ReadChars(0x20);
            string samplName = (new string(charbuffer)).Trim('\0');
            charbuffer = br.ReadChars(0x80);
            string samplfile = (new string(charbuffer)).Trim('\0');
            fs.Seek(0xD8 - 0xA0, SeekOrigin.Current);

            XmlElement prop = xml.CreateElement("PROPERTY");
            prop.SetAttribute("NAME", "CLASS");
            prop.SetAttribute("VALUE", samplName);
            sampl.AppendChild(prop);

            prop = xml.CreateElement("PROPERTY");
            prop.SetAttribute("NAME", "TEXPATH");
            prop.SetAttribute("VALUE", samplfile);
            sampl.AppendChild(prop);

            el.AppendChild(sampl);
        }

        mat.AppendChild(el);


        root.AppendChild(mat);
        xml.AppendChild(root);

#if DEBUG
        xml.Save("testmat.xml");
#endif
        return xml;
    }
    
    public static GMDL.Material Parse(XmlDocument xml)
    {
        //Make new material
        GMDL.Material mat = new GMDL.Material();


        XmlElement rootNode = (XmlElement) xml.ChildNodes[2];
        mat.name = ((XmlElement) rootNode.SelectSingleNode("Property[@name='Name']")).GetAttribute("value");
        String matClass = ((XmlElement) rootNode.SelectSingleNode("Property[@name='Class']")).GetAttribute("value");
        //Load Options

        GMDL.MatOpts opts = new GMDL.MatOpts();
        opts.transparency = int.Parse(((XmlElement) rootNode.SelectSingleNode("Property[@name='TransparencyLayerID']")).GetAttribute("value"));
        opts.castshadow = bool.Parse(((XmlElement) rootNode.SelectSingleNode("Property[@name='CastShadow']")).GetAttribute("value"));
        opts.disableTestz = bool.Parse(((XmlElement) rootNode.SelectSingleNode("Property[@name='DisableZTest']")).GetAttribute("value"));
        opts.link = ((XmlElement)rootNode.SelectSingleNode("Property[@name='Link']")).GetAttribute("value");
        opts.shadername = ((XmlElement)rootNode.SelectSingleNode("Property[@name='Shader']")).GetAttribute("value");
        mat.opts = opts;

        //Get MaterialFlags
        XmlElement matflagsNode = ((XmlElement)rootNode.SelectSingleNode("Property[@name='Flags']"));
        foreach (XmlElement n in matflagsNode.ChildNodes)
        {
            string flag = ((XmlElement) n.SelectSingleNode("Property[@name='MaterialFlag']")).GetAttribute("value");
            mat.materialflags.Add((int) MATERIALFLAGS.Parse(typeof(MATERIALFLAGS), flag));
        }

        //Get Uniforms
        XmlElement matuniformsNode = ((XmlElement)rootNode.SelectSingleNode("Property[@name='Uniforms']"));
        foreach (XmlElement n in matuniformsNode.ChildNodes)
        {
            GMDL.Uniform un = new GMDL.Uniform();
            un.name = ((XmlElement) n.SelectSingleNode("Property[@name='Name']")).GetAttribute("value");
            XmlElement valuesNode = (XmlElement) n.SelectSingleNode("Property[@name='Values']");
            Vector4 vec = new Vector4();
            vec.X = float.Parse(((XmlElement)valuesNode.SelectSingleNode("Property[@name='x']")).GetAttribute("value"),
                System.Globalization.CultureInfo.InvariantCulture);
            vec.Y = float.Parse(((XmlElement)valuesNode.SelectSingleNode("Property[@name='y']")).GetAttribute("value"),
                System.Globalization.CultureInfo.InvariantCulture);
            vec.Z = float.Parse(((XmlElement)valuesNode.SelectSingleNode("Property[@name='z']")).GetAttribute("value"),
                System.Globalization.CultureInfo.InvariantCulture);
            vec.W = float.Parse(((XmlElement)valuesNode.SelectSingleNode("Property[@name='t']")).GetAttribute("value"),
                System.Globalization.CultureInfo.InvariantCulture);
            un.value = vec;
            mat.uniforms.Add(un);
        }

        //Get Samplers
        XmlElement matsamplersNode = ((XmlElement)rootNode.SelectSingleNode("Property[@name='Samplers']"));
        foreach (XmlElement n in matsamplersNode.ChildNodes)
        {
            GMDL.Sampler sampl = new GMDL.Sampler();
            
            sampl.name = ((XmlElement) n.SelectSingleNode("Property[@name='Name']")).GetAttribute("value");
            sampl.map = ((XmlElement)n.SelectSingleNode("Property[@name='Map']")).GetAttribute("value");
            mat.samplers.Add(sampl);
        }


        /* oLD WAY
        foreach (XmlElement n in opt.ChildNodes)
        {
            XmlElement sn;
            sn = (XmlElement)n.SelectSingleNode(".//PROPERTY[@NAME='CLASS']");
            string name = sn.GetAttribute("VALUE");
            sn = (XmlElement)n.SelectSingleNode(".//PROPERTY[@NAME='TEXPATH']");
            string path = sn.GetAttribute("VALUE");
            switch (name)
            {
                case "gDiffuseMap":
                    sampl = new GMDL.Sampler();
                    sampl.name = name;
                    sampl.pathDiff = path;
                    break;
                case "gMasksMap":
                    if (sampl != null) sampl.pathMask = path;
                    break;
                case "gNormalMap":
                    if (sampl != null) sampl.pathNormal = path;
                    break;
            }
            
        }
        

        if (sampl !=null) mat.samplers.Add(sampl);
        */

        return mat;
    }

    public static GMDL.Material Parse(string path)
    {
        //Try to use libMBIN to load the Material files
        libMBIN.MBINFile mbinf = new libMBIN.MBINFile(path);
        mbinf.Load();
        TkMaterialData template = (TkMaterialData)mbinf.GetData();
        mbinf.Dispose();

        //Make new material
        GMDL.Material mat = new GMDL.Material();

        mat.name = template.Name;
        String matClass = template.Class;
        //Load Options

        GMDL.MatOpts opts = new GMDL.MatOpts();
        opts.transparency = template.TransparencyLayerID;
        opts.castshadow = template.CastShadow;
        opts.disableTestz = template.DisableZTest;
        opts.link = template.Link;
        opts.shadername = template.Shader;
        mat.opts = opts;

        //Get MaterialFlags
        foreach (TkMaterialFlags f in template.Flags)
            mat.materialflags.Add(f.MaterialFlag);
        
        //Get Uniforms
        foreach (TkMaterialUniform n in template.Uniforms)
        {
            GMDL.Uniform un = new GMDL.Uniform();
            un.name = n.Name;
            Vector4 vec = new Vector4();
            vec.X = n.Values.x;
            vec.Y = n.Values.y;
            vec.Z = n.Values.z;
            vec.W = n.Values.t;
            un.value = vec;
            mat.uniforms.Add(un);
        }

        //Get Samplers
        foreach (TkMaterialSampler n in template.Samplers)
        {
            GMDL.Sampler sampl = new GMDL.Sampler();
            sampl.name = n.Name;
            sampl.map = n.Map;
            mat.samplers.Add(sampl);
        }

        return mat;
    }

}


public static class GEOMMBIN {

    public static System.Windows.Forms.ToolStripStatusLabel strip;

    public static GMDL.GeomObject Parse(FileStream fs)
    {
        BinaryReader br = new BinaryReader(fs);
        Console.WriteLine("Parsing MBIN");

        fs.Seek(0x60, SeekOrigin.Begin);

        var vert_num = br.ReadInt32();
        var indices_num = br.ReadInt32();
        var indices_flag = br.ReadInt32();
        var collision_index_count = br.ReadInt32();

        Console.WriteLine("Model Vertices: {0}", vert_num);
        Console.WriteLine("Model Indices: {0}", indices_num);
        Console.WriteLine("Indices Flag: {0}", indices_flag);
        Console.WriteLine("Collision Index Count: {0}", collision_index_count);

        //Joint Bindings
        var jointbindingOffset = fs.Position + br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        var jointCount = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        //Skip Unknown yet offset sections
        //Joint Extensions
        //Joint Mirror Pairs
        fs.Seek(3 * 0x10, SeekOrigin.Current);

        //Usefull Bone Remapping information

        var skinmatoffset = fs.Position + br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        var bc = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);

        //Vertstarts
        var vsoffset = fs.Position + br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        var partcount = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        //VertEnds
        fs.Seek(0x10, SeekOrigin.Current);

        //Bound Hull Vert start
        fs.Seek(0x10, SeekOrigin.Current);
        
        //Bound Hull Vert end
        fs.Seek(0x10, SeekOrigin.Current);

        //MatrixLayouts
        fs.Seek(0x10, SeekOrigin.Current);

        //BoundBoxes
        var bboxminoffset = fs.Position + br.ReadInt32();
        fs.Seek(0xC, SeekOrigin.Current);
        var bboxmaxoffset = fs.Position + br.ReadInt32();
        fs.Seek(0xC, SeekOrigin.Current);

        //Bound Hull Verts
        var bhulloffset = fs.Position + br.ReadInt64();
        fs.Seek(0x8, SeekOrigin.Current);


        var lod_count = br.ReadInt32();
        var vx_type = br.ReadInt32();
        Console.WriteLine("Buffer Count: {0} VxType {1}", lod_count, vx_type);
        fs.Seek(0x8, SeekOrigin.Current);
        var mesh_descr_offset = fs.Position + br.ReadInt64();
        var buf_count = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

        //Parse Small Vertex Layout Info
        var small_bufcount = br.ReadInt32();
        var small_vx_type = br.ReadInt32();
        Console.WriteLine("Small Buffer Count: {0} VxType {1}", small_bufcount, small_vx_type);
        fs.Seek(0x8, SeekOrigin.Current);
        var small_mesh_descr_offset = fs.Position + br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        br.ReadInt32(); //Skip second buf count
        fs.Seek(0x4, SeekOrigin.Current); //Skipping a 1

        //fs.Seek(0x20, SeekOrigin.Current); //Second lod offsets

        //Get primary geom offsets
        var indices_offset = fs.Position + br.ReadInt64();
        fs.Seek(0x8, SeekOrigin.Current); //Skip Section Sizes and a 1

        var meshMetaData_offset = fs.Position + br.ReadInt64();
        var meshMetaData_counter = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current); //Skip Section Sizes and a 1

        //fs.Seek(0x10, SeekOrigin.Current);

        //Initialize geometry object
        var geom = new GMDL.GeomObject();

        //Store Counts
        geom.indicesCount = indices_num;
        if (indices_flag == 0x1)
            geom.indicesLength = 0x2;
        else
            geom.indicesLength = 0x4;

        geom.vertCount = vert_num;
        geom.vx_size = vx_type;
        geom.small_vx_size = small_vx_type;

        //Get Bone Remapping Information
        //I'm 99% sure that boneRemap is not a case in NEXT models
        //it is still there though...
        fs.Seek(skinmatoffset, SeekOrigin.Begin);
        geom.boneRemap = new short[bc];
        for (int i = 0; i < bc; i++)
            geom.boneRemap[i] = (short) br.ReadInt32();

        //Store Joint Data
        fs.Seek(jointbindingOffset, SeekOrigin.Begin);
        for (int i = 0; i < jointCount; i++)
        {
            GMDL.JointBindingData jdata = new GMDL.JointBindingData();
            jdata.Load(fs);
            //Copy Matrix
            Array.Copy(jdata.convertMat(), 0, geom.invBMats, 16 * i, 16);
            //Store the struct
            geom.jointData.Add(jdata);
        }

        //Get Vertex Starts
        //I'm fetching that just for getting the object id within the geometry file
        fs.Seek(vsoffset, SeekOrigin.Begin);
        for (int i = 0; i < partcount; i++)
            geom.vstarts.Add(br.ReadInt32());
        
        //Get BBoxes
        //Init first
        for (int i = 0; i < partcount; i++)
        {
            Vector3[] bb = new Vector3[2];
            geom.bboxes.Add(bb);
        }

        fs.Seek(bboxminoffset, SeekOrigin.Begin);
        for (int i = 0; i < partcount; i++) {
            geom.bboxes[i][0] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            br.ReadBytes(4);
        }

        fs.Seek(bboxmaxoffset, SeekOrigin.Begin);
        for (int i = 0; i < partcount; i++)
        {
            geom.bboxes[i][1] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
            br.ReadBytes(4);
        }

        //Get BoundHullStarts
        //TODO : Recheck and fix that shit
        //fs.Seek(bboxminoffset, SeekOrigin.Begin);
        //for (int i = 0; i < partcount; i++)
        //{
        //    geom.bhullverts[i] = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle());
        //    br.ReadBytes(4);
        //}

        //Get indices buffer
        fs.Seek(indices_offset, SeekOrigin.Begin);
        geom.ibuffer = new byte[indices_num * geom.indicesLength];
        fs.Read(geom.ibuffer, 0, indices_num * geom.indicesLength);

        //Get MeshMetaData
        fs.Seek(meshMetaData_offset, SeekOrigin.Begin);
        for (int i = 0; i < meshMetaData_counter; i++)
        {
            meshMetaData mmd = new meshMetaData();
            mmd.name = Util.read_string(br, 0x80);
            mmd.hash = br.ReadUInt64();
            mmd.vs_size = br.ReadUInt32();
            mmd.vs_abs_offset = br.ReadUInt32();
            mmd.is_size = br.ReadUInt32();
            mmd.is_abs_offset = br.ReadUInt32();
            geom.meshMetaDataDict[mmd.hash] = mmd;
            Console.WriteLine(mmd.name);
        }
        
        //Get main mesh description
        fs.Seek(mesh_descr_offset, SeekOrigin.Begin);
        var mesh_desc = "";
        //int[] mesh_offsets = new int[buf_count];
        //Set size excplicitly to 7
        int[] mesh_offsets = new int[7];
        geom.bufInfo = new List<GMDL.bufInfo>();
        //Set all offsets to -1
        for (int i = 0; i < 7; i++)
        {
            mesh_offsets[i] = -1;
            geom.bufInfo.Add(null);
        }

        
        for (int i = 0; i < buf_count; i++)
        {
            var buf_id = br.ReadInt32();
            var buf_elem_count = br.ReadInt32();
            var buf_type = br.ReadInt32();
            var buf_localoffset = br.ReadInt32();
            geom.bufInfo[buf_id]= get_bufInfo_item(buf_id, buf_localoffset, buf_elem_count, buf_type);
            mesh_offsets[buf_id] = buf_localoffset;
            fs.Seek(0x10, SeekOrigin.Current);
        }
        //Get Descr
        mesh_desc = getDescr(ref mesh_offsets, buf_count);

        Console.WriteLine("Mesh Description: " + mesh_desc);

        //Store description
        geom.mesh_descr = mesh_desc;
        geom.offsets = mesh_offsets;
        //Get small description
        fs.Seek(small_mesh_descr_offset, SeekOrigin.Begin);
        var small_mesh_desc = "";
        //int[] mesh_offsets = new int[buf_count];
        //Set size excplicitly to 7
        int[] small_mesh_offsets = new int[7];
        //Set all offsets to -1
        for (int i = 0; i < 7; i++)
            small_mesh_offsets[i] = -1;


        for (int i = 0; i < small_bufcount; i++)
        {
            var buf_id = br.ReadInt32();
            var buf_elem_count = br.ReadInt32();
            var buf_type = br.ReadInt32();
            var buf_localoffset = br.ReadInt32();
            small_mesh_offsets[buf_id] = buf_localoffset;
            fs.Seek(0x10, SeekOrigin.Current);
        }

        //Get Small Descr
        small_mesh_desc = getDescr(ref small_mesh_offsets, small_bufcount);
        Console.WriteLine("Small Mesh Description: " + small_mesh_desc);

        //Store description
        geom.small_mesh_descr = small_mesh_desc;
        geom.small_offsets = small_mesh_offsets;
        //Set geom interleaved
        geom.interleaved = true;


        //Try to fetch the geometry.data.mbin file in order to fetch the streams
        string gstream_path = "";
        string[] split = fs.Name.Split('.');
        for (int i = 0; i < split.Length - 2; i++)
            gstream_path += split[i] + ".";
        gstream_path += "DATA.MBIN.PC";

        //Check if file exists
        if (!File.Exists(gstream_path))
            throw new IOException("Geometry Stream File Missing. Check your fucking files...");

        //Fetch streams from the gstream file based on the metadatafile
        FileStream gs_fs = new FileStream(gstream_path, FileMode.Open);
        
        foreach (KeyValuePair<ulong, meshMetaData> pair in geom.meshMetaDataDict)
        {
            meshMetaData mmd = pair.Value;
            meshData md = new meshData();
            md.vs_buffer = new byte[mmd.vs_size];
            md.is_buffer = new byte[mmd.is_size];
            
            //Fetch Buffers
            gs_fs.Seek((int) mmd.vs_abs_offset, SeekOrigin.Begin);
            gs_fs.Read(md.vs_buffer, 0, (int) mmd.vs_size);
            
            gs_fs.Seek((int) mmd.is_abs_offset, SeekOrigin.Begin);
            gs_fs.Read(md.is_buffer, 0, (int) mmd.is_size);
            
            geom.meshDataDict[mmd.hash] = md;
        }

        gs_fs.Close();

        return geom;

    }


    private static GMDL.bufInfo get_bufInfo_item(int buf_id, int buf_localoffset, int count, int buf_type)
    {
        int sem = buf_id;
        int off = buf_localoffset;
        OpenTK.Graphics.OpenGL.VertexAttribPointerType typ = get_type(buf_type);
        string text = get_shader_sem(buf_id);
        return new GMDL.bufInfo(sem, typ, count, off, text);
    }


    private static string get_shader_sem(int buf_id)
    {
        switch (buf_id)
        {
            case 0:
                return "vPosition"; //Verts
            case 1:
                return "uvPosition0"; //Verts
            case 2:
                return "nPosition"; //Verts
            case 3:
                return "tPosition"; //Verts
            case 4:
                return "bPosition"; //Verts
            case 5:
                return "blendIndices"; //Verts
            case 6:
                return "blendWeights"; //Verts
            default:
                return "shit"; //Default
        }

        
    }

    private static OpenTK.Graphics.OpenGL.VertexAttribPointerType get_type(int val){

        switch (val)
        {
            case (0x140B):
                return OpenTK.Graphics.OpenGL.VertexAttribPointerType.HalfFloat;
            case (0x1401):
                return OpenTK.Graphics.OpenGL.VertexAttribPointerType.UnsignedByte;
            case (0x8D9F):
                return OpenTK.Graphics.OpenGL.VertexAttribPointerType.Int2101010Rev;
            default:
                Console.WriteLine("Unknown VERTEX SECTION TYPE-----------------------------------");
                throw new ApplicationException("NEW VERTEX SECTION TYPE. FIX IT ASSHOLE...");
                //return OpenTK.Graphics.OpenGL4.VertexAttribPointerType.UnsignedByte;
        }
    }

    private static int get_type_count(int val)
    {

        switch (val)
        {
            case (0x140B):
                return 4;
            case (0x1401):
                return 1;
            default:
                Console.WriteLine("Unknown VERTEX SECTION TYPE-----------------------------------");
                return 1;
        }
    }

    private static string getDescr(ref int[] offsets, int count)
    {
        string mesh_desc = "";


        for (int i = 0; i < count; i++)
        {
            if (offsets[i] != -1)
            {
                switch (i)
                {
                    case 0:
                        mesh_desc += "v"; //Verts
                        break;
                    case 1:
                        mesh_desc += "u"; //UVs
                        break;
                    case 2:
                        mesh_desc += "n"; //Normals
                        break;
                    case 3:
                        mesh_desc += "t"; //Tangents
                        break;
                    case 4:
                        mesh_desc += "p"; //Vertex Color
                        break;
                    case 5:
                        mesh_desc += "b"; //BlendIndices
                        break;
                    case 6:
                        mesh_desc += "w"; //BlendWeights
                        break;
                    default:
                        mesh_desc += "x"; //Default
                        break;
                }
            }
        }



        return mesh_desc;
    }

    public static GMDL.scene LoadObjects(string filepath)
    {
        libMBIN.MBINFile mbinf = new libMBIN.MBINFile(filepath);
        mbinf.Load();
        TkSceneNodeData scene = (TkSceneNodeData) mbinf.GetData();
        mbinf.Dispose();
        Console.WriteLine("Loading Objects from MBINFile");
        //Get TkSceneNodeData
        string sceneName = scene.Name;

        string[] split = sceneName.Split('\\');
        string scnName = split[split.Length - 1];
        Util.setStatus("Importing Scene: " + scnName, strip);
        Console.WriteLine("Importing Scene: " + scnName);
        //Get Geometry File
        //Parse geometry once
        string geomfile;
        geomfile = scene.Attributes[0].Value;
        FileStream fs = new FileStream(Path.Combine(Util.dirpath, geomfile) + ".PC", FileMode.Open);
        GMDL.GeomObject gobject;
        
        if (!Util.activeResMgmt.GLgeoms.ContainsKey(geomfile))
        {
            gobject = GEOMMBIN.Parse(fs);
            Util.activeResMgmt.GLgeoms[geomfile] = gobject;
        }
        else
        {
            //Load from dict
            gobject = Util.activeResMgmt.GLgeoms[geomfile];
        }
        
        fs.Close();

        //Random Generetor for colors
        Random randgen = new Random();

        //Create Scene Root
        GMDL.scene root = new GMDL.scene();
        root.name = scnName;
        root.type = TYPES.SCENE;
        root.shader_programs = new int[] {Util.activeResMgmt.shader_programs[1],
                                          Util.activeResMgmt.shader_programs[5],
                                          Util.activeResMgmt.shader_programs[6]};

        //Store sections node
        foreach (TkSceneNodeData node in scene.Children)
        {
            GMDL.model part = parseNode(node, gobject, root, root);
            //If joint save it also to the jointmodels of the scene
            if (part.type == TYPES.JOINT)
            {
                GMDL.Joint j = (GMDL.Joint)part;
                root.jointDict[j.name] = j;
            }
            root.children.Add(part);
        }

        //Set root scene for rootObject
        gobject.rootObject = root;
        return root;
    }

    private static GMDL.model parseNode(TkSceneNodeData node, 
        GMDL.GeomObject gobject, GMDL.model parent, GMDL.scene scene)
    {
        TkTransformData transform = node.Transform;
        List<TkSceneNodeAttributeData> attribs = node.Attributes;
        List<TkSceneNodeData> children = node.Children;
        
        //Load Transforms
        //Get Transformation
        float[] transforms = new float[] { transform.TransX,
                                           transform.TransY,
                                           transform.TransZ,
                                           transform.RotX,
                                           transform.RotY,
                                           transform.RotZ,
                                           transform.ScaleX,
                                           transform.ScaleY,
                                           transform.ScaleZ};

        //XmlElement info = (XmlElement)node.ChildNodes[0];
        //XmlElement opts = (XmlElement)node.ChildNodes[1];
        //XmlElement childs = (XmlElement)node.ChildNodes[2];

        string name = node.Name;
        string type = node.Type;
        //Fix double underscore names
        if (name.StartsWith("_"))
            name = "_" + name.TrimStart('_');
        
        //Notify
        Util.setStatus("Importing Scene: " + scene.name + " Part: " + name, strip);
        Console.WriteLine("Importing Scene: " + scene.name + " Part: " + name);
        TYPES typeEnum;
        Enum.TryParse<TYPES>(node.Type, out typeEnum);

        //if (typeEnum == TYPES.MESH && !name.Contains("GekNeck_XK"))
        //    typeEnum = (TYPES) 25;

        if (typeEnum == TYPES.MESH)
        {
            Console.WriteLine("Mesh Detected " + name);

            //Create model
            GMDL.meshModel so = new GMDL.meshModel();

            so.name = name;
            so.type = typeEnum;
            so.debuggable = true;
            //Set Random Color
            so.color[0] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            so.color[1] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            so.color[2] = Model_Viewer.Util.randgen.Next(255) / 255.0f;

            Console.WriteLine("Object Color {0}, {1}, {2}", so.color[0], so.color[1], so.color[2]);
            //Get Options
            so.batchstart_physics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name== "BATCHSTARTPHYSI").Value);
            so.vertrstart_physics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "VERTRSTARTPHYSI").Value);
            so.vertrend_physics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "VERTRENDPHYSICS").Value);
            so.batchstart_graphics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "BATCHSTARTGRAPH").Value);
            so.batchcount = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "BATCHCOUNT").Value);
            so.vertrstart_graphics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "VERTRSTARTGRAPH").Value);
            so.vertrend_graphics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "VERTRENDGRAPHIC").Value);
            so.firstskinmat = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "FIRSTSKINMAT").Value);
            so.lastskinmat = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "LASTSKINMAT").Value);
            so.lod_level = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "LODLEVEL").Value);
            so.boundhullstart = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "BOUNDHULLST").Value);
            so.boundhullend = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "BOUNDHULLED").Value);

            //Get Hash
            so.hash = ulong.Parse(node.Attributes.FirstOrDefault(item => item.Name == "HASH").Value);
            Console.WriteLine("Batch Physics Start {0} Count {1} Vertex Physics {2} - {3}", 
                so.batchstart_physics, so.batchcount, so.vertrstart_physics, so.vertrend_physics);

            //Find id within the vbo
            int iid = -1;
            for (int i = 0; i < gobject.vstarts.Count; i++)
                if (gobject.vstarts[i] == so.vertrstart_physics)
                {
                    iid = i;
                    break;
                }

            so.shader_programs = new int[] { Util.activeResMgmt.shader_programs[0],
                                             Util.activeResMgmt.shader_programs[5],
                                             Util.activeResMgmt.shader_programs[6]};
            
            so.Bbox = gobject.bboxes[iid];
            so.setupBSphere();
            so.parent = parent;
            so.scene = scene;
            so.gobject = gobject; //Store the gobject for easier access of uniforms
            so.init(String.Join(",",transforms));

            //Get Material
            string matname = node.Attributes.FirstOrDefault(item => item.Name == "MATERIAL").Value;
            string[] split = matname.Split('\\');
            string matkey = split[split.Length - 1];
            //Check if material already in Resources
            if (Util.activeResMgmt.GLmaterials.ContainsKey(matkey))
                so.material = Util.activeResMgmt.GLmaterials[matkey];
            else
            {
                //Parse material file
                Console.WriteLine("Parsing Material File");
                string mat_path = Path.GetFullPath(Path.Combine(Util.dirpath, matname));
                string mat_exmlPath = Path.GetFullPath(FileUtils.getExmlPath(mat_path));
                //Console.WriteLine("Loading Scene " + path);
                
                //Check if path exists
                if (File.Exists(mat_path))
                {
                    //if (!File.Exists(mat_exmlPath))
                    //    Util.MbinToExml(mat_path, mat_exmlPath);

                    //GMDL.Material mat = MATERIALMBIN.Parse(newXml);
                    GMDL.Material mat = MATERIALMBIN.Parse(mat_path);
                    //Load default form palette on init
                    mat.palette = Model_Viewer.Palettes.paletteSel;

                    mat.prepTextures();
                    mat.mixTextures();
                    so.material = mat;
                    //Store the material to the Resources
                    Util.activeResMgmt.GLmaterials[matkey] = mat;
                } else
                {
                    //Generate empty material
                    GMDL.Material mat = new GMDL.Material();
                    so.material = mat;
                }
            }

            //Decide if its a skinned mesh or not
            so.skinned = 0;
            if (so.material.materialflags.Contains(1))
                so.skinned = 1;
            
            //Generate Vao's
            so.main_Vao = gobject.getMainVao(so);
            
            //Configure boneRemap properly
            so.BoneRemap = new int[so.lastskinmat - so.firstskinmat];
            for (int i = 0; i < so.lastskinmat - so.firstskinmat; i++)
                so.BoneRemap[i] = gobject.boneRemap[so.firstskinmat + i];

            Console.WriteLine("Object {0}, Number of skinmatrices required: {1}", so.name, so.lastskinmat - so.firstskinmat);

            if (children != null)
            {
                //Console.WriteLine("Children Count {0}", childs.ChildNodes.Count);
                foreach (TkSceneNodeData child in children)
                {
                    GMDL.model part = parseNode(child, gobject, so, scene);
                    if (part.type == TYPES.JOINT)
                    {
                        GMDL.Joint j = (GMDL.Joint) part;
                        scene.jointDict[j.name] = j;
                    }
                    so.children.Add(part);
                }
            }

            //Check if it is a decal object
            if (so.material.materialflags.Contains(50) || so.material.materialflags.Contains(51))
            {
                GMDL.Decal newso = new GMDL.Decal(so);
                //Change object type
                newso.type = TYPES.DECAL;
                newso.shader_programs[0] = Util.activeResMgmt.shader_programs[10];
                
                Util.activeResMgmt.GLDecals.Add(newso);
                return newso;
            }

            return so;
        }
        else if (typeEnum == TYPES.LOCATOR)
        {
            Console.WriteLine("Locator Detected");
            GMDL.locator so = new GMDL.locator();
            //Set Properties
            //Testingso.Name = name + "_LOC";
            so.name = name;
            so.type = typeEnum;
            //Set Shader Program
            so.shader_programs = new int[]{     Util.activeResMgmt.shader_programs[1],
                                                Util.activeResMgmt.shader_programs[5],
                                                Util.activeResMgmt.shader_programs[6]};

            //Get Transformation
            so.parent = parent;
            so.scene = scene;
            so.init(String.Join(",", transforms));

            //Locator Objects don't have options

            //Handle Children
            if (children != null)
            {
                foreach (TkSceneNodeData child in children)
                {
                    GMDL.model part = parseNode(child, gobject, so, scene);
                    //If joint save it also to the jointmodels of the scene
                    if (part.type == TYPES.JOINT)
                    {
                        GMDL.Joint j = (GMDL.Joint)part;
                        scene.jointDict[j.name] = j;
                    }
                    so.children.Add(part);
                }
            }

            return so;
        }
        else if (typeEnum == TYPES.JOINT)
        {
            Console.WriteLine("Joint Detected");
            GMDL.Joint joint = new GMDL.Joint();
            //Set properties
            joint.name = name;
            joint.type = typeEnum;
            joint.shader_programs = new int[]{ Util.activeResMgmt.shader_programs[2],
                                               Util.activeResMgmt.shader_programs[5],
                                               Util.activeResMgmt.shader_programs[6]};
            //Get Transformation
            joint.parent = parent;
            joint.scene = scene;
            joint.init(String.Join(",", transforms));
            //Get JointIndex
            joint.jointIndex = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "JOINTINDEX").Value);
            //Set Random Color
            joint.color[0] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            joint.color[1] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            joint.color[2] = Model_Viewer.Util.randgen.Next(255) / 255.0f;

            //Handle Children
            if (children != null)
            {
                foreach (TkSceneNodeData child in children)
                {
                    GMDL.model part = parseNode(child, gobject, joint, scene);
                    //If joint save it also to the jointmodels of the root scene
                    if (part.type == TYPES.JOINT)
                    {
                        GMDL.Joint j = (GMDL.Joint) part;
                        scene.jointDict[j.name] = j;
                    }
                    joint.children.Add(part);
                }
            }

            return joint;
        }
        else if (typeEnum == TYPES.REFERENCE)
        {
            //Another Scene file referenced
            Console.WriteLine("Reference Detected");
            //Getting Scene MBIN file
            string path = Path.GetFullPath(Path.Combine(Util.dirpath, node.Attributes.FirstOrDefault(item => item.Name == "SCENEGRAPH").Value));
            //string exmlPath = Path.GetFullPath(Util.getFullExmlPath(path));
            //Console.WriteLine("Loading Scene " + path);
            //Parse MBIN to xml

            //Check if path exists
            if (File.Exists(path)) {

                //if (!File.Exists(exmlPath))
                //    Util.MbinToExml(path, exmlPath);

                //Read new Scene
                GMDL.scene so = LoadObjects(path);
                so.parent = parent;
                so.scene = null;
                //Override Name
                so.name = name;
                //Override transforms
                so.init(String.Join(",", transforms));

                //Handle Children
                if (children != null)
                {
                    foreach (TkSceneNodeData child in children)
                    {
                        GMDL.model part = parseNode(child, gobject, so, scene);
                        //If joint save it also to the jointmodels of the new scene
                        if (part.type == TYPES.JOINT)
                        {
                            GMDL.Joint j = (GMDL.Joint) part;
                            so.jointDict[j.name] = j;
                        }
                        so.children.Add(part);
                    }
                }

                //Load Objects from new xml
                return so;


            } else {
                Console.WriteLine("Reference Missing");
                GMDL.locator so = new GMDL.locator();
                //Set Properties
                so.name = name + "_UNKNOWN";
                so.type = TYPES.UNKNOWN;
                //Set Shader Program
                so.shader_programs = new int[] { Util.activeResMgmt.shader_programs[1],
                                                 Util.activeResMgmt.shader_programs[5],
                                                 Util.activeResMgmt.shader_programs[6]};

                //Locator Objects don't have options

                //take care of children
                return so;
            }
            
        }
        else if (typeEnum == TYPES.COLLISION)
        {
            //Create model
            GMDL.Collision so = new GMDL.Collision();

            //Remove that after implemented all the different collision types
            so.shader_programs = new int[] { Util.activeResMgmt.shader_programs[0],
                                              Util.activeResMgmt.shader_programs[5],
                                              Util.activeResMgmt.shader_programs[6]}; //Use Mesh program for collisions
            so.debuggable = true;
            so.name = name + "_COLLISION";
            so.type = typeEnum;

            //Get Options
            //In collision objects first child is probably the type
            //string collisionType = ((XmlElement)attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']")).GetAttribute("value").ToUpper();
            string collisionType = node.Attributes.FirstOrDefault(item => item.Name == "TYPE").Value.ToUpper();

            Console.WriteLine("Collision Detected " + name + "TYPE: " + collisionType);
            if (collisionType == "MESH")
            {
                so.collisionType = (int) COLLISIONTYPES.MESH;
                so.batchstart_graphics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "BATCHSTART").Value);
                so.batchcount = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "BATCHCOUNT").Value);
                so.vertrstart_graphics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "VERTRSTART").Value);
                so.vertrend_graphics = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "VERTREND").Value);
                so.firstskinmat = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "FIRSTSKINMAT").Value);
                so.lastskinmat = int.Parse(node.Attributes.FirstOrDefault(item => item.Name == "LASTSKINMAT").Value);

                //Find id within the vbo
                int iid = -1;
                for (int i = 0; i < gobject.vstarts.Count; i++)
                    if (gobject.vstarts[i] == so.vertrstart_physics)
                    {
                        iid = i;
                        break;
                    }

                //Set cvbo
                try
                {
                    so.main_Vao = gobject.getMainVao(so);
                } catch (System.Collections.Generic.KeyNotFoundException e)
                {
                    Console.WriteLine("Missing Collision Mesh\n");
                    so.main_Vao = null;
                }
                
            
            } else if (collisionType == "CYLINDER")
            {
                //Console.WriteLine("CYLINDER NODE PARSING NOT IMPLEMENTED");
                //Set cvbo

                float radius = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "RADIUS").Value);
                float height = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "HEIGHT").Value);
                so.main_Vao = (new Cylinder(radius,height)).getVAO();
                so.collisionType = (int)COLLISIONTYPES.CYLINDER;

            }
            else if (collisionType == "BOX")
            {
                //Console.WriteLine("BOX NODE PARSING NOT IMPLEMENTED");
                //Set cvbo
                float width = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "WIDTH").Value);
                float height = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "HEIGHT").Value);
                float depth = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "DEPTH").Value);
                so.main_Vao = (new Box(width, height, depth)).getVAO();
                so.collisionType = (int)COLLISIONTYPES.BOX;
                //Set general vbo properties
                so.batchstart_graphics = 0;
                so.batchcount = 36;
                so.vertrstart_graphics = 0;
                so.vertrend_graphics = 8-1;

            }
            else if (collisionType == "CAPSULE")
            {
                //Set cvbo
                float radius = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "RADIUS").Value);
                float height = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "HEIGHT").Value);
                so.main_Vao = (new Capsule(new Vector3(), height, radius)).getVAO();
                so.collisionType = (int) COLLISIONTYPES.CAPSULE;
                so.batchstart_graphics = 0;
                so.batchcount = 726;
                so.vertrstart_graphics = 0;
                so.vertrend_graphics = 144 - 1;
            }
            else if (collisionType == "SPHERE")
            {
                //Set cvbo
                float radius = float.Parse(node.Attributes.FirstOrDefault(item => item.Name == "RADIUS").Value);
                so.main_Vao = (new Sphere(new Vector3(), radius)).getVAO();
                so.collisionType = (int)COLLISIONTYPES.SPHERE;
                so.batchstart_graphics = 0;
                so.batchcount = 600;
                so.vertrstart_graphics = 0;
                so.vertrend_graphics = 121 - 1;
            }
            else
            {
                Console.WriteLine("NEW COLLISION TYPE: " + collisionType);
            }


            Console.WriteLine("Batch Start {0} Count {1} ", 
                so.batchstart_physics, so.batchcount);

            so.parent = parent;
            so.init(String.Join(",", transforms));

            //Collision probably has no children biut I'm leaving that code here
            if (children != null)
            {
                Console.WriteLine("Children Count {0}", children.Count);
                foreach (TkSceneNodeData child in children)
                    so.children.Add(parseNode(child, gobject, so, scene));
            }

            return so;

        }
        else
        {
            Console.WriteLine("Unknown Type, {0}", type);
            GMDL.locator so = new GMDL.locator();
            //Set Properties
            so.name = name + "_UNKNOWN";
            so.type = TYPES.UNKNOWN;
            //Set Shader Program
            so.shader_programs = new int[] { Util.activeResMgmt.shader_programs[1],
                                             Util.activeResMgmt.shader_programs[5],
                                             Util.activeResMgmt.shader_programs[6]};

            //Locator Objects don't have options

            //take care of children
            return so;
            //throw new ApplicationException("Unknown mesh type");
        }

        
    }
 

}


