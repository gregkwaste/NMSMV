using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using GLSLHelper;
using libHGPAK;
using libMBIN.NMS.GameComponents;

namespace MVCore.GMDL
{
    public class Material : IDisposable
    {
        //The material file as libMBIN loaded it. The viewer reads the material data from here and
        //ExportTemplate writes it back
        public TkMaterialData Template;
        //Only materials that were read from a file are written back. Fallback and viewer materials are not
        public bool fromFile = false;

        private bool disposed = false;
        public bool proc = false;
        public float[] material_flags = new float[64];
        public string name_key = "";
        public textureManager texMgr;
        public int shaderHash = int.MaxValue;
        public bool DoubleSided = false; //Back faces are drawn (no culling). Set in init
        //Viewer helpers (locator crosses, ...) carry float colours in vertex attribute 4. Game meshes use that
        //slot for _F21_VERTEXCUSTOM data, which is not a colour, so this is a separate viewer-only switch
        public bool UseVertexColour = false;
        //Viewer helpers: scaled by the camera distance in the vertex shader so they keep their size on screen
        public bool ConstantScreenSize = false;
        
        public static List<string> supported_flags = new List<string>() {
                "_F01_DIFFUSEMAP",
                "_F02_SKINNED",
                "_F03_NORMALMAP",
                "_F07_UNLIT",
                "_F09_TRANSPARENT",
                "_F11_ALPHACUTOUT",
                "_F13_UV_EFFECT", //UV scrolling (gUVScrollStepVec4). Formerly _F14_UVSCROLL
                "_F16_DIFFUSE2MAP",
                "_F20_PARALLAX",
                "_F21_VERTEXCUSTOM",
                "_F22_OCCLUSION_MAP",
                "_F25_MASKS_MAP",
                "_F36_DOUBLESIDED",
                "_F42_DETAIL_NORMAL",
                "_F53_COLOURISABLE",
                "_F55_MULTITEXTURE"};

        //Template data
        public string Name
        {
            get { return Template.Name; }
            set { Template.Name = value; }
        }
        public TkMaterialClass Class => Template.Class;
        public List<TkMaterialFlags> Flags => Template.Flags;
        public List<TkMaterialSampler> Samplers => Template.Samplers;
        public List<TkMaterialUniform_Float> Uniforms_Float => Template.Uniforms_Float;
        public List<TkMaterialUniform_UInt> Uniforms_UInt => Template.Uniforms_UInt;
        public bool CastShadow => Template.CastShadow;
        public bool DisableZTest => Template.DisableZTest;
        public int TransparencyLayerID => Template.TransparencyLayerID;

        //Shadow proxy: the game draws these meshes only into its shadow maps, never into the visible frame
        public bool ShadowOnly => Class?.MaterialClass == TkMaterialClass.MaterialClassEnum.ShadowOnly;

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

        public string PClass
        {
            get
            {
                return Class.MaterialClass.ToString(); //TODO: Check this
            }
        }

        //Shader features of the material: the supported material flags, plus the features current game
        //versions express through the material class (their _F09/_F11/_F34/_F51 flags are unnamed and unused)
        public List<string> ShaderFlags
        {
            get
            {
                List<string> l = new List<string>();
                foreach (string f in MaterialFlags)
                {
                    if (supported_flags.Contains(f) && !l.Contains(f))
                        l.Add(f);
                }

                void add(string f) { if (!l.Contains(f)) l.Add(f); }

                if (UseVertexColour)
                    add("_D_VERTEX_COLOUR");
                if (ConstantScreenSize)
                    add("_D_SCREEN_SIZE");

                switch (Class.MaterialClass)
                {
                    case TkMaterialClass.MaterialClassEnum.Translucent:
                    case TkMaterialClass.MaterialClassEnum.TranslucentPostScene:
                    case TkMaterialClass.MaterialClassEnum.MapTrans:
                    case TkMaterialClass.MaterialClassEnum.HighlightTrans:
                    case TkMaterialClass.MaterialClassEnum.HighlightTransDoubleSided:
                    case TkMaterialClass.MaterialClassEnum.HighlightTransOccluded:
                    //Additive blending is approximated with the regular transparent pass
                    case TkMaterialClass.MaterialClassEnum.Additive:
                    case TkMaterialClass.MaterialClassEnum.AdditiveHighQuality:
                    case TkMaterialClass.MaterialClassEnum.DoublesidedAdditive:
                    case TkMaterialClass.MaterialClassEnum.GunAdditive:
                    case TkMaterialClass.MaterialClassEnum.HighlightAdditive:
                        add("_F09_TRANSPARENT");
                        break;
                    case TkMaterialClass.MaterialClassEnum.GlowTranslucent:
                        add("_F09_TRANSPARENT");
                        add("_D_CLASS_GLOW");
                        break;
                    case TkMaterialClass.MaterialClassEnum.Glow:
                    case TkMaterialClass.MaterialClassEnum.GunGlow:
                        add("_D_CLASS_GLOW");
                        break;
                    case TkMaterialClass.MaterialClassEnum.Cutout:
                        add("_F11_ALPHACUTOUT");
                        break;
                    case TkMaterialClass.MaterialClassEnum.Decal:
                    case TkMaterialClass.MaterialClassEnum.DecalTerrain:
                    case TkMaterialClass.MaterialClassEnum.GunDecal:
                        add("_F51_DECAL_DIFFUSE");
                        if (l.Contains("_F03_NORMALMAP"))
                            add("_F52_DECAL_NORMAL");
                        break;
                }

                switch (Class.MaterialClass)
                {
                    case TkMaterialClass.MaterialClassEnum.DoubleSided:
                    case TkMaterialClass.MaterialClassEnum.DoublesidedAdditive:
                    case TkMaterialClass.MaterialClassEnum.HighlightDoubleSided:
                    case TkMaterialClass.MaterialClassEnum.HighlightOverlayDoubleSided:
                    case TkMaterialClass.MaterialClassEnum.HighlightTransDoubleSided:
                        add("_F36_DOUBLESIDED");
                        break;
                }

                return l;
            }
        }

        public List<string> MaterialFlags
        {
            get
            {
                List<string> l = new List<string>();

                foreach (TkMaterialFlags f in Flags)
                {
                    l.Add(f.MaterialFlag.ToString());
                }

                return l;
            }
        }

        public string type;
        //public MatOpts opts;
        public Dictionary<string, Sampler> _PSamplers = new Dictionary<string, Sampler>();

        public Dictionary<string, Sampler> PSamplers
        {
            get
            {
                return _PSamplers;
            }
        }

        private Dictionary<string, Uniform> _CustomPerMaterialUniforms = new Dictionary<string, Uniform>();
        public Dictionary<string, Uniform> CustomPerMaterialUniforms
        {
            get
            {
                return _CustomPerMaterialUniforms;
            }
        }

        public Material()
        {
            Template = new TkMaterialData();
            Template.Name = "NULL";
            Template.Shader = "NULL";
            Template.Link = "NULL";
            Template.Class = new TkMaterialClass();
            Template.TransparencyLayerID = -1;
            Template.CastShadow = false;
            Template.DisableZTest = false;
            Template.Flags = new List<TkMaterialFlags>();
            Template.Samplers = new List<TkMaterialSampler>();
            Template.Uniforms_Float = new List<TkMaterialUniform_Float>();
            Template.Uniforms_UInt = new List<TkMaterialUniform_UInt>();
            normalizeTemplate();
            
            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        public Material(TkMaterialData md)
        {
            Template = md;
            normalizeTemplate();
            
            //Clear material flags
            for (int i = 0; i < 64; i++)
                material_flags[i] = 0.0f;
        }

        //The material file. All the material data lives in the template, so there is nothing to sync
        public TkMaterialData ExportTemplate()
        {
            normalizeTemplate();
            return Template;
        }

        //New viewer/Assimp materials do not pass through libMBIN deserialization, which normally
        //instantiates every string wrapper and collection. Keep the in-memory template complete so
        //it can be serialized just like a material loaded from the game.
        private void normalizeTemplate()
        {
            Template ??= new TkMaterialData();
            Template.Name ??= "";
            Template.Shader ??= "";
            Template.Link ??= "";
            Template.Metamaterial ??= "";
            Template.Class ??= new TkMaterialClass();
            Template.Flags ??= new List<TkMaterialFlags>();
            Template.FxFlags ??= new List<TkMaterialFxFlags>();
            Template.Samplers ??= new List<TkMaterialSampler>();
            Template.Uniforms_Float ??= new List<TkMaterialUniform_Float>();
            Template.Uniforms_UInt ??= new List<TkMaterialUniform_UInt>();

            foreach (TkMaterialSampler sampler in Template.Samplers)
            {
                sampler.Name ??= "";
                sampler.Map ??= "";
                sampler.MaterialAlternativeId ??= "";
            }
            foreach (TkMaterialUniform_Float uniform in Template.Uniforms_Float)
            {
                uniform.Name ??= "";
                uniform.Values ??= new libMBIN.NMS.Vector4f();
                uniform.ExtendedValues ??= new List<libMBIN.NMS.Vector4f>();
            }
            foreach (TkMaterialUniform_UInt uniform in Template.Uniforms_UInt)
            {
                uniform.Name ??= "";
                uniform.Values ??= new libMBIN.NMS.Vector4i();
                uniform.ExtendedValues ??= new List<libMBIN.NMS.Vector4i>();
            }
        }

        public static Material Parse(string path, textureManager input_texMgr)
        {
            //Load template
            //Try to use libMBIN to load the Material files
            TkMaterialData template = NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr) as TkMaterialData;
            if (template == null)
                return null; //Missing or unreadable material, the caller uses a fallback
#if DEBUG
            //Save NMSTemplate to mxml
            template.WriteToMxml("Temp\\" + template.Name + ".mxml", false, true);
#endif

            //Make new material based on the template
            Material mat = new Material(template);
            mat.fromFile = true;

            mat.texMgr = input_texMgr;
            mat.init();
            return mat;
        }

        //Mix this material's procedural textures again (new layer picks and palette colours) and point the
        //samplers at the results. Several materials can share one procedural texture, mixed holds what is done
        public void regenProcTextures(HashSet<string> mixed)
        {
            Sampler diffuse = null;
            foreach (Sampler s in PSamplers.Values)
            {
                if (s.isProcGen)
                {
                    diffuse = s;
                    break;
                }
            }
            if (diffuse == null || diffuse.texMgr == null)
                return;

            string baseName = diffuse.Map; //X.MIXED.DDS
            string prefix = baseName.Substring(0, baseName.Length - 3); //X.MIXED.
            string[] names = { prefix + "DDS", prefix + "MASKS.DDS", prefix + "NORMAL.DDS" };
            textureManager tm = diffuse.texMgr;

            if (mixed.Add(tm.GetHashCode() + ":" + baseName))
            {
                //The mixer replaces the entries, free the old textures only once the new ones exist
                List<Texture> old = new List<Texture>();
                foreach (string n in names)
                    if (tm.GLtextures.TryGetValue(n, out Texture t))
                        old.Add(t);

                TextureMixer.combineTextures(baseName, Model_Viewer.Palettes.paletteSel, ref tm);

                foreach (Texture t in old)
                {
                    bool replaced = true;
                    foreach (string n in names)
                        if (tm.GLtextures.TryGetValue(n, out Texture cur) && cur == t)
                            replaced = false;
                    if (replaced)
                        t.Dispose();
                }
            }

            foreach (Sampler s in PSamplers.Values)
            {
                string map = s.Map;
                if (Array.IndexOf(names, map) >= 0 && s.texMgr != null && s.texMgr.hasTexture(map))
                    s.tex = s.texMgr.getTexture(map);
            }
        }

        public void init()
        {
            //Get MaterialFlags
            foreach (TkMaterialFlags f in Flags)
                material_flags[(int)f.MaterialFlag] = 1.0f;

            
            //Get Uniforms
            foreach (TkMaterialUniform_Float un in Uniforms_Float)
            {
                Uniform my_un = new Uniform("mpCustomPerMaterial.", un);
                CustomPerMaterialUniforms[my_un.Name] = my_un;
            }

            foreach (TkMaterialUniform_UInt un in Uniforms_UInt)
            {
                Uniform my_un = new Uniform("mpCustomPerMaterial.", un);
                CustomPerMaterialUniforms[my_un.Name] = my_un;
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
                    for (int i = 0; i < split.Length - 1; i++)
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

            //Calculate material hash
            List<string> shaderFlags = ShaderFlags;
            shaderHash = GLShaderHelper.calculateShaderHash(shaderFlags);
            DoubleSided = shaderFlags.Contains("_F36_DOUBLESIDED");

            if (!Common.RenderState.activeResMgr.shaderExistsForMaterial(this))
            {
                try
                {
                    compileMaterialShader();
                } catch (Exception e)
                {
                    Common.CallBacks.Log("Error during material shader compilation: " + e.Message);
                }
            }
                

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

        public bool add_flag(TkMaterialFlags.MaterialFlagEnum flag)
        {
            //Check if material has flag
            foreach (TkMaterialFlags f in Flags)
            {
                if (f.MaterialFlag == flag)
                    return false;
            }

            TkMaterialFlags ff = new TkMaterialFlags();
            ff.MaterialFlag = flag;
            Flags.Add(ff);

            return true;
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

        public static int calculateShaderHash(List<TkMaterialFlags> flags)
        {
            string hash = "";

            for (int i = 0; i < flags.Count; i++)
            {
                string s_flag = flags[i].MaterialFlag.ToString();
                if (supported_flags.Contains(s_flag))
                    hash += "_" + s_flag;
            }

            if (hash == "")
                hash = "DEFAULT";

            return hash.GetHashCode();
        }

        private void compileMaterialShader()
        {
            List<string> includes = new List<string>();
            List<string> defines = new List<string>();

            //Save shader to resource Manager
            Common.RenderState.activeResMgr.getShaderMaps(this, out Dictionary<int, GLSLShaderConfig> shaderDict,
                out Dictionary<int, List<GLMeshVao>> meshList, out bool deferred);
            if (deferred)
                defines.Add("_D_DEFERRED_RENDERING");

            includes.AddRange(ShaderFlags);

            GLSLShaderConfig shader = GLShaderHelper.compileShader("Shaders/Simple_VS.glsl", "Shaders/Simple_FS.glsl", null, null, null,
                defines, includes, SHADER_TYPE.MATERIAL_SHADER, ref Common.RenderState.shaderCompilationLog);


            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader, "_COMMON_PER_FRAME", 0);
            GLShaderHelper.attachSSBOToShaderBindingPoint(shader, "_COMMON_PER_MESH", 1);


            //Save shader to the resource Manager
            shaderDict[shader.shaderHash] = shader;
            meshList[shader.shaderHash] = new List<GLMeshVao>(); //Init list
        }

    }

}
