using System.IO;
using System.Diagnostics;
using System.Xml;
using System.Collections.Generic;
using System;
using OpenTK;
using Model_Viewer;

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
    UNKNOWN
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
        //Debug.WriteLine("Section Count: {0}",section_count);
        fs.Seek(0x4, SeekOrigin.Current);

        //Geometry File Name
        fs.Seek(geometry_file_offset+ 0x20, SeekOrigin.Begin);
        charbuffer = br.ReadChars(0x100);
        string geometry_file_name = new string(charbuffer);
        //Debug.WriteLine(geometry_file_name);
        //Debug.WriteLine(" ");

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
        _F53_,
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
            fs.Seek(0xC8 - 0xA0, SeekOrigin.Current);

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
        
        xml.Save("testmat.xml");
        return xml;
    }
    
    public static GMDL.Material ParseXml(XmlDocument xml)
    {
        //Make new material
        GMDL.Material mat = new GMDL.Material();

        //Find MaterialNode
        XmlNode node = xml.SelectSingleNode("/ROOT/MATERIAL");
        Debug.WriteLine(node);
        mat.name = node.SelectSingleNode(".//NAME").InnerText;
        mat.type = node.SelectSingleNode(".//CLASS").InnerText;

        XmlElement opt;
        GMDL.MatOpts opts = new GMDL.MatOpts();
        opt = (XmlElement) node.SelectSingleNode(".//PROPERTY[@NAME='TRANSPARENCY']");
        opts.transparency = int.Parse(opt.GetAttribute("VALUE"));
        opt = (XmlElement)node.SelectSingleNode(".//PROPERTY[@NAME='CASTSHADOW']");
        opts.castshadow = bool.Parse(opt.GetAttribute("VALUE"));
        opt = (XmlElement)node.SelectSingleNode(".//PROPERTY[@NAME='DISABLETESTZ']");
        opts.disableTestz = bool.Parse(opt.GetAttribute("VALUE"));
        opt = (XmlElement)node.SelectSingleNode(".//PROPERTY[@NAME='LINK']");
        opts.link = opt.GetAttribute("VALUE");
        opt = (XmlElement)node.SelectSingleNode(".//PROPERTY[@NAME='SHADER']");
        opts.shadername = opt.GetAttribute("VALUE");

        mat.opts = opts;

        //Get MaterialFlags
        opt = (XmlElement)node.SelectSingleNode(".//MATERIALFLAGS");
        foreach (XmlElement n in opt.ChildNodes)
            mat.materialflags.Add((int) MATERIALFLAGS.Parse(typeof(MATERIALFLAGS), n.InnerText));
        //Get Uniforms
        opt = (XmlElement)node.SelectSingleNode(".//UNIFORMS");
        foreach (XmlElement n in opt.ChildNodes)
        {
            GMDL.Uniform un = new GMDL.Uniform();
            un.name = n.GetAttribute("NAME");
            Vector4 vec = new Vector4();
            vec.X = float.Parse(n.GetAttribute("VALUE").Split(',')[0], System.Globalization.CultureInfo.InvariantCulture);
            vec.Y = float.Parse(n.GetAttribute("VALUE").Split(',')[1], System.Globalization.CultureInfo.InvariantCulture);
            vec.Z = float.Parse(n.GetAttribute("VALUE").Split(',')[2], System.Globalization.CultureInfo.InvariantCulture);
            vec.W = float.Parse(n.GetAttribute("VALUE").Split(',')[3], System.Globalization.CultureInfo.InvariantCulture);
            un.value = vec;
            mat.uniforms.Add(un);
        }
        //Get Samplers
        opt = (XmlElement)node.SelectSingleNode(".//SAMPLERS");
        foreach (XmlElement n in opt.ChildNodes)
        {
            XmlElement sn;
            GMDL.Sampler sampl = new GMDL.Sampler();
            sn = (XmlElement)n.SelectSingleNode(".//PROPERTY[@NAME='CLASS']");
            sampl.name = sn.GetAttribute("VALUE");
            sn = (XmlElement)n.SelectSingleNode(".//PROPERTY[@NAME='TEXPATH']");
            sampl.path = sn.GetAttribute("VALUE");

            mat.samplers.Add(sampl);
        }
        

        return mat;
    }

}


public static class GEOMMBIN{

    public static System.Windows.Forms.ToolStripStatusLabel strip;

    public static GMDL.GeomObject Parse(FileStream fs)
    {
        BinaryReader br = new BinaryReader(fs);
        Debug.WriteLine("Parsing MBIN");

        fs.Seek(0x60, SeekOrigin.Begin);

        var vert_num = br.ReadInt32();
        var indices_num = br.ReadInt32();
        var indices_flag = br.ReadInt32();

        Debug.WriteLine("Model Vertices: {0}", vert_num);
        Debug.WriteLine("Model Indices: {0}", indices_num);
        Debug.WriteLine("Indices Flag: {0}", indices_flag);

        //Joint Bindings
        fs.Seek(0x4, SeekOrigin.Current);
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


        fs.Seek(0x8, SeekOrigin.Current);
        var partcount = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);

        //Skip rest section type offsets
        fs.Seek(4 * 0x10, SeekOrigin.Current);

        var lod_count = br.ReadInt32();
        var vx_type = br.ReadInt32();
        Debug.WriteLine("Buffer Count: {0} VxType {1}", lod_count, vx_type);
        fs.Seek(0x8, SeekOrigin.Current);
        var mesh_descr_offset = fs.Position + br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        var buf_count = br.ReadInt32();
        fs.Seek(0x4, SeekOrigin.Current);
        fs.Seek(0x20, SeekOrigin.Current); //Second lod offsets

        //Get primary geom offsets
        var indices_offset = fs.Position + br.ReadInt32();
        fs.Seek(0xC, SeekOrigin.Current);
        var verts_offset = fs.Position + br.ReadInt32();
        fs.Seek(0xC, SeekOrigin.Current);

        fs.Seek(0x10, SeekOrigin.Current);
        /*
         * I need to get the vertex starts-ends
         * 
         * I need to get the bound starts-ends
         * 
         * 
         * */

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

        //Get Bone Remapping Information
        fs.Seek(skinmatoffset, SeekOrigin.Begin);
        geom.boneRemap = new int[bc];
        for (int i = 0; i < bc; i++)
           geom.boneRemap[i] = br.ReadInt32();
        
        //Store Joint Data
        fs.Seek(jointbindingOffset, SeekOrigin.Begin);
        for (int i = 0; i < jointCount; i++)
        {
            GMDL.JointBindingData jdata = new GMDL.JointBindingData();
            jdata.Load(fs);
            geom.jointData.Add(jdata);
        }

        //Get indices buffer
        fs.Seek(indices_offset, SeekOrigin.Begin);
        geom.ibuffer = new byte[indices_num * geom.indicesLength];
        fs.Read(geom.ibuffer, 0, indices_num * geom.indicesLength);

        //Get vx buffer
        fs.Seek(verts_offset, SeekOrigin.Begin);
        geom.vbuffer = new byte[vert_num * vx_type];
        fs.Read(geom.vbuffer, 0, vert_num * vx_type);


        //Get mesh description
        fs.Seek(mesh_descr_offset, SeekOrigin.Begin);
        var mesh_desc = "";
        //int[] mesh_offsets = new int[buf_count];
        //Set size excplicitly to 7
        int[] mesh_offsets = new int[7];
        //Set all offsets to -1
        for (int i = 0; i < 7; i++)
            mesh_offsets[i] = -1;
        
        
        for (int i = 0; i < buf_count; i++)
        {
            var buf_id = br.ReadInt32();
            fs.Seek(0x4, SeekOrigin.Current);
            var buf_type = br.ReadInt32();
            var buf_localoffset = br.ReadInt32();
            mesh_offsets[buf_id] = buf_localoffset;
            fs.Seek(0x10, SeekOrigin.Current);
            switch (buf_id) {
                case 0:
                    mesh_desc += "v";
                    break;
                case 1:
                    mesh_desc += "u";
                    break;
                case 2:
                    mesh_desc += "n";
                    break;
                case 3:
                    mesh_desc += "t";
                    break;
                case 4:
                    mesh_desc += "p";
                    break;
                case 5:
                    mesh_desc += "b";
                    break;
                case 6:
                    mesh_desc += "w";
                    break;
                default:
                    mesh_desc += "x";
                    break;
            }
        }
        Debug.WriteLine("Mesh Description: " + mesh_desc);

        //Store description
        geom.mesh_descr = mesh_desc;
        geom.offsets = mesh_offsets;

        //Set geom interleaved
        geom.interleaved = true;

        return geom;

    }

    public static GMDL.scene LoadObjects(XmlDocument xml)
    {
        Debug.WriteLine("Loading Objects from XML");
        //Get TkSceneNodeData
        XmlElement sceneNode = (XmlElement)xml.ChildNodes[1];
        XmlElement sceneName = (XmlElement) sceneNode.SelectSingleNode("Property[@name='Name']");

        string[] split = sceneName.GetAttribute("value").Split('\\');
        string scnName = split[split.Length - 1];
        Util.setStatus("Importing Scene: " + scnName, strip);
        //Debug.WriteLine("Importing Scene: " + scnName);
        //Get Geometry File
        XmlElement sceneNodeData = (XmlElement)sceneNode.SelectSingleNode("Property[@name='Attributes']");
        //Parse geometry once
        string geomfile;
        XmlElement geom = (XmlElement) sceneNodeData.ChildNodes[0].SelectSingleNode("Property[@name='Value']");
        geomfile = geom.GetAttribute("value");
        FileStream fs = new FileStream(Path.Combine(Util.dirpath, geomfile) + ".PC", FileMode.Open);
        GMDL.customVBO cvbo = new GMDL.customVBO(GEOMMBIN.Parse(fs));
        fs.Close();

        //Random Generetor for colors
        Random randgen = new Random();

        //Create Scene Root
        GMDL.scene root = new GMDL.scene();
        root.name = scnName;
        root.type = TYPES.SCENE;
        root.shader_program = ResourceMgmt.shader_programs[1];
        
        //Store sections node
        XmlElement children = (XmlElement)sceneNode.SelectSingleNode("Property[@name='Children']");
        foreach (XmlElement node in children)
        {
            GMDL.model part = parseNode(node, cvbo, root, root);
            //If joint save it also to the jointmodels of the scene
            if (part.type == TYPES.JOINT)
                root.jointModel.Add((GMDL.Joint) part);
            root.children.Add(part);
        }
        return root;
    }

       private static GMDL.model parseNode(XmlElement node, 
           GMDL.customVBO cvbo, GMDL.model parent, GMDL.scene scene)
        {
        XmlElement attribs,childs,transform;
        transform = (XmlElement)node.SelectSingleNode("Property[@name='Transform']");
        attribs = (XmlElement)node.SelectSingleNode("Property[@name='Attributes']");
        childs = (XmlElement)node.SelectSingleNode("Property[@name='Children']");

        //Load Transforms
        //Get Transformation
        string[] transforms = new string[] { ((XmlElement)transform.SelectSingleNode("Property[@name='TransX']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='TransY']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='TransZ']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='RotX']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='RotY']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='RotZ']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='ScaleX']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='ScaleY']")).GetAttribute("value"),
                                                 ((XmlElement)transform.SelectSingleNode("Property[@name='ScaleZ']")).GetAttribute("value") };
        //XmlElement info = (XmlElement)node.ChildNodes[0];
        //XmlElement opts = (XmlElement)node.ChildNodes[1];
        //XmlElement childs = (XmlElement)node.ChildNodes[2];

        string name = ((XmlElement)node.SelectSingleNode("Property[@name='Name']")).GetAttribute("value");
        //Fix double underscore names
        if (name.StartsWith("_"))
            name = "_" + name.TrimStart('_');

        TYPES typeEnum;
        string type = ((XmlElement)node.SelectSingleNode("Property[@name='Type']")).GetAttribute("value");
        Enum.TryParse<TYPES>(type, out typeEnum);

        if (typeEnum == TYPES.MESH)
        {
            Debug.WriteLine("Mesh Detected " + name);

            //Create model
            GMDL.sharedVBO so = new GMDL.sharedVBO();

            //Set cvbo
            so.vbo = cvbo;
            so.shader_program = ResourceMgmt.shader_programs[0];
            so.name = name;
            so.type = typeEnum;
            //Set Random Color
            so.color[0] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            so.color[1] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            so.color[2] = Model_Viewer.Util.randgen.Next(255) / 255.0f;

            Debug.WriteLine("Object Color {0}, {1}, {2}", so.color[0], so.color[1], so.color[2]);
            //Get Options
            so.batchstart = int.Parse(((XmlElement) attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            so.batchcount = int.Parse(((XmlElement)attribs.ChildNodes[1].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            so.vertrstart = int.Parse(((XmlElement)attribs.ChildNodes[2].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            so.vertrend = int.Parse(((XmlElement)attribs.ChildNodes[3].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            so.firstskinmat = int.Parse(((XmlElement)attribs.ChildNodes[4].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            so.lastskinmat = int.Parse(((XmlElement)attribs.ChildNodes[5].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            //opt = (XmlElement)attribs.SelectSingleNode(".//OPTION[@NAME='FIRSTSKINMAT']");
            //so.firstskinmat = int.Parse(opt.GetAttribute("VALUE"));
            //opt = (XmlElement)attribs.SelectSingleNode(".//OPTION[@NAME='LASTSKINMAT']");
            //so.lastskinmat = int.Parse(opt.GetAttribute("VALUE"));
            //opt = (XmlElement)attribs.SelectSingleNode(".//OPTION[@NAME='BATCHSTART']");
            //so.batchstart = int.Parse(opt.GetAttribute("VALUE"));
            //opt = (XmlElement)attribs.SelectSingleNode(".//OPTION[@NAME='BATCHCOUNT']");
            //so.batchcount = int.Parse(opt.GetAttribute("VALUE"));
            //opt = (XmlElement)attribs.SelectSingleNode(".//OPTION[@NAME='VERTRSTART']");
            //so.vertrstart = int.Parse(opt.GetAttribute("VALUE"));
            //opt = (XmlElement)attribs.SelectSingleNode(".//OPTION[@NAME='VERTREND']");
            //so.vertrend = int.Parse(opt.GetAttribute("VALUE"));
            Debug.WriteLine("Batch Start {0} Count {1} ", so.batchstart, so.batchcount);

            so.parent = parent;
            so.scene = scene;
            so.init(String.Join(",",transforms));

            //Get Material
            string matname = ((XmlElement)attribs.ChildNodes[6].SelectSingleNode("Property[@name='Value']")).GetAttribute("value");
            //Parse material file
            FileStream ms = new FileStream(Path.Combine(Model_Viewer.Util.dirpath, matname), FileMode.Open);
            GMDL.Material mat = MATERIALMBIN.ParseXml(MATERIALMBIN.Parse(ms));
            mat.prepTextures();
            ms.Close();
            so.material = mat;
            //Load default form palette on init
            so.palette = Model_Viewer.Palettes.paletteSel;

            //Decide if its a skinned mesh or not
            if (so.firstskinmat == so.lastskinmat)
                so.skinned = 0;
            //Configure boneRemap properly
            so.BoneRemap = new int[so.lastskinmat - so.firstskinmat];
            for (int i = 0; i < so.lastskinmat - so.firstskinmat; i++)
                so.BoneRemap[i] = so.vbo.boneRemap[so.firstskinmat + i];
            
            if (childs != null)
            {
                //Debug.WriteLine("Children Count {0}", childs.ChildNodes.Count);
                foreach (XmlElement childnode in childs.ChildNodes)
                {
                    GMDL.model part = parseNode(childnode, cvbo, so, scene);
                    if (part.type == TYPES.JOINT)
                        so.scene.jointModel.Add((GMDL.Joint) part);
                    so.children.Add(part);
                }
            }   
            

            return so;
        }
        else if (typeEnum == TYPES.LOCATOR)
        {
            Debug.WriteLine("Locator Detected");
            GMDL.locator so = new GMDL.locator();
            //Set Properties
            //Testingso.Name = name + "_LOC";
            so.name = name;
            so.type = typeEnum;
            //Set Shader Program
            so.shader_program = ResourceMgmt.shader_programs[1];

            //Get Transformation
            so.parent = parent;
            so.scene = scene;
            so.init(String.Join(",", transforms));
            
            //Locator Objects don't have options

            //take care of children
            if (childs != null)
            {
                //Debug.WriteLine("Children Count {0}", childs.ChildNodes.Count);
                foreach (XmlElement childnode in childs.ChildNodes)
                {
                    GMDL.model part = parseNode(childnode, cvbo, so, scene);
                    if (part.type == TYPES.JOINT)
                        so.scene.jointModel.Add((GMDL.Joint)part);
                    so.children.Add(part);
                }
            }

            return so;
        }
        else if (typeEnum == TYPES.JOINT)
        {
            Debug.WriteLine("Joint Detected");
            GMDL.Joint joint = new GMDL.Joint();
            //Set properties
            joint.name = name.ToUpper();
            joint.type = typeEnum;
            joint.shader_program = ResourceMgmt.shader_programs[2];
            //Get Transformation
            joint.parent = parent;
            joint.scene = scene;
            joint.init(String.Join(",", transforms));
            //Get JointIndex
            joint.jointIndex = int.Parse(((XmlElement)attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            //Set Random Color
            joint.color[0] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            joint.color[1] = Model_Viewer.Util.randgen.Next(255) / 255.0f;
            joint.color[2] = Model_Viewer.Util.randgen.Next(255) / 255.0f;

            //Handle Children
            if (childs != null)
            {
                //Debug.WriteLine("Children Count {0}", childs.ChildNodes.Count);
                foreach (XmlElement childnode in childs.ChildNodes)
                {
                    GMDL.model part = parseNode(childnode, cvbo, joint, scene);
                    joint.children.Add(part);
                }
            }
            
            return joint;
        }
        else if (typeEnum == TYPES.REFERENCE)
        {
            //Another Scene file referenced
            Debug.WriteLine("Reference Detected");
            //Getting Scene MBIN file
            XmlElement opt = ((XmlElement)attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']"));
            string path = Path.Combine(Util.dirpath, opt.GetAttribute("value"));

            //Debug.WriteLine("Loading Scene " + path);
            XmlDocument newXml = new XmlDocument(); 
            //Parse MBIN to xml
            if (!File.Exists(Util.getExmlPath(path)))
                Util.MbinToExml(path);
            
            newXml.Load(Util.getExmlPath(path));

            //Read new Scene
            GMDL.scene so = LoadObjects(newXml);
            so.parent = parent;
            so.scene = null;
            //Override Name
            so.name = name;
            //Override transforms
            so.init(String.Join(",", transforms));
            
            //Handle Children
            if (childs != null)
            {
                foreach (XmlElement childnode in childs.ChildNodes)
                {
                    GMDL.model part = parseNode(childnode, cvbo, so, scene);
                    //If joint save it also to the jointmodels of the scene
                    if (part.type == TYPES.JOINT)
                        so.jointModel.Add((GMDL.Joint)part);
                    so.children.Add(part);
                }
            }

            //Load Objects from new xml
            return so;
        }
        else if (typeEnum == TYPES.COLLISION)
        {
            Debug.WriteLine("Collision Detected " + name);

            //Create model
            GMDL.Collision so = new GMDL.Collision();

            //Set cvbo
            so.vbo = cvbo;
            so.shader_program = ResourceMgmt.shader_programs[0]; //Use Mesh program for collisions
            so.name = name + "_COLLISION";
            so.type = typeEnum;

            //Get Options
            //In collision objects first child is probably the type
            string collisionType = ((XmlElement)attribs.ChildNodes[0].SelectSingleNode("Property[@name='Value']")).GetAttribute("value");

            if (collisionType == "MESH")
            {
                so.batchstart = int.Parse(((XmlElement)attribs.ChildNodes[1].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
                so.batchcount = int.Parse(((XmlElement)attribs.ChildNodes[2].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
                so.vertrstart = int.Parse(((XmlElement)attribs.ChildNodes[3].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
                so.vertrend = int.Parse(((XmlElement)attribs.ChildNodes[4].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
                so.firstskinmat = int.Parse(((XmlElement)attribs.ChildNodes[5].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
                so.lastskinmat = int.Parse(((XmlElement)attribs.ChildNodes[6].SelectSingleNode("Property[@name='Value']")).GetAttribute("value"));
            } else if (collisionType == "Cylinder")
            {

            }
            else
            {

            }
            
            
            Debug.WriteLine("Batch Start {0} Count {1} ", so.batchstart, so.batchcount);

            so.parent = parent;
            so.init(String.Join(",", transforms));

            //Collision probably has no children biut I'm leaving that code here
            if (childs != null)
            {
                Debug.WriteLine("Children Count {0}", childs.ChildNodes.Count);
                foreach (XmlElement childnode in childs.ChildNodes)
                {
                    so.children.Add(parseNode(childnode, cvbo, so, scene));
                }
            }
            
            return so;

        }
        else
        {
            Debug.WriteLine("Unknown Type, {0}", type);
            GMDL.locator so = new GMDL.locator();
            //Set Properties
            so.name = name + "_UNKNOWN";
            so.type = TYPES.UNKNOWN;
            //Set Shader Program
            so.shader_program = ResourceMgmt.shader_programs[1];

            //Locator Objects don't have options

            //take care of children
            return so;
            //throw new ApplicationException("Unknown mesh type");
        }

        
    }
 

}


