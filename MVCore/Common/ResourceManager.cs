using System;
using System.Collections.Generic;
using System.IO;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;


namespace MVCore
{
    public interface baseResourceManager
    {
        void cleanup();
    }


    //Class Which will store all the texture resources for better memory management
    public class ResourceManager
    {
        //public Dictionary<string, GMDL.Texture> GLtextures = new Dictionary<string, GMDL.Texture>();
        public Dictionary<string, Material> GLmaterials = new Dictionary<string, GMDL.Material>();
        public Dictionary<string, GeomObject> GLgeoms = new Dictionary<string, GMDL.GeomObject>();
        public Dictionary<string, Scene> GLScenes = new Dictionary<string, GMDL.Scene>();
        public Dictionary<string, Texture> GLTextures = new Dictionary<string, GMDL.Texture>();
        public Dictionary<string, AnimMetadata> Animations = new Dictionary<string, AnimMetadata>();

        public Dictionary<string, GLVao> GLPrimitiveVaos = new Dictionary<string, GLVao>();
        public Dictionary<string, GLVao> GLVaos = new Dictionary<string, GLVao>();
        public Dictionary<string, GLMeshVao> GLPrimitiveMeshVaos = new Dictionary<string, GLMeshVao>();

        public List<GMDL.Light> GLlights = new List<GMDL.Light>();
        public List<Camera> GLCameras = new List<Camera>();
        //public Dictionary<string, int> GLShaders = new Dictionary<string, int>();
        public Dictionary<GLSLHelper.SHADER_TYPE, GLSLHelper.GLSLShaderConfig> GLShaders = new Dictionary<GLSLHelper.SHADER_TYPE, GLSLHelper.GLSLShaderConfig>(); //Generic Shaders

        public Dictionary<int, GLSLShaderConfig> GLDeferredLITShaderMap = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLDeferredUNLITShaderMap = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLForwardShaderMapTransparent = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLDeferredShaderMapDecal = new Dictionary<int, GLSLShaderConfig>();
        public Dictionary<int, GLSLShaderConfig> GLDefaultShaderMap = new Dictionary<int, GLSLShaderConfig>();

        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredLITShaders = new List<GLSLHelper.GLSLShaderConfig>();
        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredUNLITShaders = new List<GLSLHelper.GLSLShaderConfig>();
        public List<GLSLHelper.GLSLShaderConfig> activeGLForwardTransparentShaders = new List<GLSLHelper.GLSLShaderConfig>();
        public List<GLSLHelper.GLSLShaderConfig> activeGLDeferredDecalShaders = new List<GLSLHelper.GLSLShaderConfig>();

        public Dictionary<int, List<GLMeshVao>> opaqueMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();
        public Dictionary<int, List<GLMeshVao>> defaultMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();
        public Dictionary<int, List<GLMeshVao>> transparentMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();
        public Dictionary<int, List<GLMeshVao>> decalMeshShaderMap = new Dictionary<int, List<GLMeshVao>>();

        //Global NMS File Archive handles
        public Dictionary<string, string> NMSFileToArchiveMap = new Dictionary<string, string>();
        
        //public int[] shader_programs;
        //Extra manager
        public textureManager texMgr = new textureManager();

        public bool initialized = false;

        //Procedural Generation Options
        //TODO: This is 99% NOT correct
        //public Dictionary<string, int> procTextureLayerSelections = new Dictionary<string, int>();

        //public DebugForm DebugWin;

        private bool sharedResourcesCreated = false;

        public void Init()
        {
            initialized = false;

            //Add defaults
            addDefaultTextures();
            addDefaultMaterials();
            addDefaultLights();
            addDefaultCameras();

            //Primitives and the main shaders don't depend on the scene, so they are created once and kept
            //across file opens instead of being rebuilt (and leaked) every time
            if (!sharedResourcesCreated)
            {
                addDefaultPrimitives();
                compileMainShaders();
                sharedResourcesCreated = true;
            }
            else
            {
                //The default materials were recreated above, point the primitives at the new ones
                foreach (GLMeshVao m in GLPrimitiveMeshVaos.Values)
                {
                    if (m.material != null && GLmaterials.TryGetValue(m.material.Name, out Material mat))
                        m.material = mat;
                }
            }

            initialized = true;
        }

        public void compileMainShaders()
        {
            //Recompiling (shader reload) replaces the programs, free the old ones first
            foreach (GLSLShaderConfig conf in GLShaders.Values)
            {
                if (conf != null && conf.program_id > 0)
                    GL.DeleteProgram(conf.program_id);
            }
            GLShaders.Clear();

#if (DEBUG)
            //Query GL Extensions
            Common.CallBacks.Log("OPENGL AVAILABLE EXTENSIONS:");
            string[] ext = GL.GetString(StringNameIndexed.Extensions, 0).Split(' ');
            foreach (string s in ext)
            {
                if (s.Contains("explicit"))
                    Common.CallBacks.Log(s);
                if (s.Contains("texture"))
                    Common.CallBacks.Log(s);
                if (s.Contains("16"))
                    Common.CallBacks.Log(s);
            }

            //Query maximum buffer sizes
            Common.CallBacks.Log("MaxUniformBlock Size {0}", GL.GetInteger(GetPName.MaxUniformBlockSize));
#endif

            //Populate shader list
            string log = "";
            GLSLHelper.GLSLShaderConfig shader_conf;

            //Geometry Shader
            //Compile Object Shaders
            GLSLShaderText geometry_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText geometry_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            GLSLShaderText geometry_shader_gs = new GLSLShaderText(ShaderType.GeometryShader);
            geometry_shader_vs.addStringFromFile("Shaders/Simple_VSEmpty.glsl");
            geometry_shader_fs.addStringFromFile("Shaders/Simple_FSEmpty.glsl");
            geometry_shader_gs.addStringFromFile("Shaders/Simple_GS.glsl");

            GLShaderHelper.compileShader(geometry_shader_vs, geometry_shader_fs, geometry_shader_gs, null, null,
                            SHADER_TYPE.DEBUG_MESH_SHADER, ref log);


            //Compile Object Shaders
            GLSLShaderText gizmo_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gizmo_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gizmo_shader_vs.addStringFromFile("Shaders/Gizmo_VS.glsl");
            gizmo_shader_fs.addStringFromFile("Shaders/Gizmo_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gizmo_shader_vs, gizmo_shader_fs, null, null, null,
                            SHADER_TYPE.GIZMO_SHADER, ref log);

            //Attach UBO binding Points
            GLShaderHelper.attachUBOToShaderBindingPoint(shader_conf, "_COMMON_PER_FRAME", 0);
            GLShaders[SHADER_TYPE.GIZMO_SHADER] = shader_conf;


#if DEBUG
            //Report UBOs
            GLShaderHelper.reportUBOs(shader_conf);
#endif

            //Picking Shader

            //Compile Default Shaders

            //BoundBox Shader
            GLSLShaderText bbox_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText bbox_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            bbox_shader_vs.addStringFromFile("Shaders/Bound_VS.glsl");
            bbox_shader_fs.addStringFromFile("Shaders/Bound_FS.glsl");
            GLShaderHelper.compileShader(bbox_shader_vs, bbox_shader_fs, null, null, null,
                GLSLHelper.SHADER_TYPE.BBOX_SHADER, ref log);

            //Texture Mixing Shader
            GLSLShaderText texture_mixing_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText texture_mixing_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            texture_mixing_shader_vs.addStringFromFile("Shaders/texture_mixer_VS.glsl");
            texture_mixing_shader_fs.addStringFromFile("Shaders/texture_mixer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(texture_mixing_shader_vs, texture_mixing_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER] = shader_conf;

            //GBuffer Shaders

            //UNLIT
            GLSLShaderText gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/Gbuffer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GBUFFER_UNLIT_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_UNLIT_SHADER] = shader_conf;

            //LIT
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addString("#define _D_LIGHTING");
            gbuffer_shader_fs.addStringFromFile("Shaders/Gbuffer_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GBUFFER_LIT_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.GBUFFER_LIT_SHADER] = shader_conf;


            //BLOOM DOWNSAMPLE (first pass also applies the bright threshold)
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/bloom_downsample_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.BLOOM_DOWNSAMPLE_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.BLOOM_DOWNSAMPLE_SHADER] = shader_conf;

            //BLOOM UPSAMPLE (also the final composite)
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/bloom_upsample_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.BLOOM_UPSAMPLE_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.BLOOM_UPSAMPLE_SHADER] = shader_conf;

            //FXAA
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/fxaa_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.FXAA_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.FXAA_SHADER] = shader_conf;

            //TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/tone_mapping_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.TONE_MAPPING, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.TONE_MAPPING] = shader_conf;

            //INV TONE MAPPING + GAMMA CORRECTION
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/inv_tone_mapping_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            SHADER_TYPE.INV_TONE_MAPPING, ref log);
            GLShaders[SHADER_TYPE.INV_TONE_MAPPING] = shader_conf;


            //BWOIT SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/bwoit_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            SHADER_TYPE.BWOIT_COMPOSITE_SHADER, ref log);
            GLShaders[SHADER_TYPE.BWOIT_COMPOSITE_SHADER] = shader_conf;


            //Camera Shaders
            //TODO: Add Camera Shaders if required
            GLShaders[GLSLHelper.SHADER_TYPE.CAMERA_SHADER] = null;

            //FILTERS - EFFECTS

            //Pass Shader
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText passthrough_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            passthrough_shader_fs.addStringFromFile("Shaders/PassThrough_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, passthrough_shader_fs, null, null, null,
                            SHADER_TYPE.PASSTHROUGH_SHADER, ref log);
            GLShaders[SHADER_TYPE.PASSTHROUGH_SHADER] = shader_conf;

        }

        private void addDefaultCameras()
        {
            Camera cam = new Camera(90, -1, 0, true);
            cam.isActive = false;
            GLCameras.Add(cam);

            if (RenderState.activeCam != null)
            {
                GLCameras[0].settings = RenderState.activeCam.settings;
                Camera.SetCameraPosition(ref cam, RenderState.activeCam.Position);
                //Camera.SetCameraDirection(ref cam, RenderState.activeCam.Direction);
            }

            //Set as active camera the first one by default
            RenderState.activeCam = GLCameras[0];
        }

        private void addDefaultTextures()
        {
            string execpath = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);

            //Add Default textures
            //White tex
            string texpath = "default.dds";
            Texture tex = new Texture();
            tex.textureInit((byte[]) CallBacks.getResource("_default"), texpath); //Manually load data
            texMgr.addTexture(tex);

            //Transparent Mask
            texpath = "default_mask.dds";
            tex = new Texture();
            tex.textureInit((byte[]) CallBacks.getResource("default_mask"), texpath);
            texMgr.addTexture(tex);
        }

        private void addDefaultLights()
        {
            //Add one and only light for now
            Light light = new Light
            {
                name = "Default Light",
                intensity = 200,
                localPosition = new Vector3(100.0f, 100.0f, 100.0f)
            };

            GLlights.Add(light);
        }

        private void addDefaultMaterials()
        {
            //Cross Material
            Material mat;

            mat = new Material();
            mat.Name = "crossMat";
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);
            mat.UseVertexColour = true; //Axis colours come from the vertices
            mat.ConstantScreenSize = true;
            var uf = new TkMaterialUniform_Float();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f(1.0f, 1.0f, 1.0f, 1.0f);
            mat.Uniforms_Float.Add(uf);
            mat.init();
            GLmaterials["crossMat"] = mat;

            //Joint Material
            mat = new Material();
            mat.Name = "jointMat";
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);

            uf = new TkMaterialUniform_Float();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f(1.0f, 0.0f, 0.0f, 1.0f);
            mat.Uniforms_Float.Add(uf);
            mat.init();
            GLmaterials["jointMat"] = mat;

            //Light Material
            mat = new Material();
            mat.Name = "lightMat";
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);

            uf = new TkMaterialUniform_Float();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f(1.0f, 1.0f, 0.0f, 1.0f);
            mat.Uniforms_Float.Add(uf);
            mat.init();
            GLmaterials["lightMat"] = mat;

            //Collision Material
            mat = new Material();
            mat.Name = "collisionMat";
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);

            uf = new TkMaterialUniform_Float();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f(0.8f, 0.8f, 0.2f, 1.0f);
            mat.Uniforms_Float.Add(uf);
            mat.init();
            GLmaterials["collisionMat"] = mat;

        }


        public void addMaterial(Material mat)
        {
            GLmaterials[mat.name_key] = mat;
            //Assign material to shaders
        }

        private void generateGizmoParts()
        {
            //Translation Gizmo
            GMDL.Primitives.Arrow translation_x_axis = new GMDL.Primitives.Arrow(0.015f, 0.25f, new Vector3(1.0f, 0.0f, 0.0f), false, 20);
            //Move arrowhead up in place
            Matrix4 t = Matrix4.CreateRotationZ(MathUtils.radians(90));
            translation_x_axis.applyTransform(t);

            GMDL.Primitives.Arrow translation_y_axis = new GMDL.Primitives.Arrow(0.015f, 0.25f, new Vector3(0.0f, 1.0f, 0.0f), false, 20);
            GMDL.Primitives.Arrow translation_z_axis = new GMDL.Primitives.Arrow(0.015f, 0.25f, new Vector3(0.0f, 0.0f, 1.0f), false, 20);
            t = Matrix4.CreateRotationX(MathUtils.radians(90));
            translation_z_axis.applyTransform(t);

            //Generate Geom objects
            translation_x_axis.geom = translation_x_axis.getGeom();
            translation_y_axis.geom = translation_y_axis.getGeom();
            translation_z_axis.geom = translation_z_axis.getGeom();


            GLPrimitiveVaos["default_translation_gizmo_x_axis"] = translation_x_axis.getVAO();
            GLPrimitiveVaos["default_translation_gizmo_y_axis"] = translation_y_axis.getVAO();
            GLPrimitiveVaos["default_translation_gizmo_z_axis"] = translation_z_axis.getVAO();


            //Generate PrimitiveMeshVaos
            for (int i = 0; i < 3; i++)
            {
                string name = "";
                GMDL.Primitives.Primitive arr = null;
                switch (i)
                {
                    case 0:
                        arr = translation_x_axis;
                        name = "default_translation_gizmo_x_axis";
                        break;
                    case 1:
                        arr = translation_y_axis;
                        name = "default_translation_gizmo_y_axis";
                        break;
                    case 2:
                        arr = translation_z_axis;
                        name = "default_translation_gizmo_z_axis";
                        break;
                }

                GLPrimitiveMeshVaos[name] = new GLMeshVao();
                GLPrimitiveMeshVaos[name].type = TYPES.GIZMOPART;
                GLPrimitiveMeshVaos[name].metaData = new MeshMetaData();
                GLPrimitiveMeshVaos[name].metaData.batchcount = arr.geom.indicesCount;
                GLPrimitiveMeshVaos[name].metaData.indicesLength = DrawElementsType.UnsignedInt;
                GLPrimitiveMeshVaos[name].vao = GLPrimitiveVaos[name];
                GLPrimitiveMeshVaos[name].material = GLmaterials["crossMat"];

            }

        }

        public void addDefaultPrimitives()
        {
            //Setup Primitive Vaos

            //Default quad
            GMDL.Primitives.Quad q = new GMDL.Primitives.Quad(1.0f, 1.0f);
            GLPrimitiveVaos["default_quad"] = q.getVAO();
            GLPrimitiveMeshVaos["default_quad"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_quad"].vao = GLPrimitiveVaos["default_quad"];
            
            //Default render quad
            q = new GMDL.Primitives.Quad();
            GLPrimitiveVaos["default_renderquad"] = q.getVAO();
            GLPrimitiveMeshVaos["default_renderquad"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_renderquad"].vao = GLPrimitiveVaos["default_renderquad"];

            //Default cross
            //Default cross (locators): lines, kept at a constant size on screen by the vertex shader
            GMDL.Primitives.Cross c = new GMDL.Primitives.Cross(0.1f, true, true);
            GLPrimitiveMeshVaos["default_cross"] = new GLMeshVao();
            GLPrimitiveVaos["default_cross"] = c.getVAO();
            GLPrimitiveMeshVaos["default_cross"].type = TYPES.LOCATOR; //Drawn as GL_LINES
            GLPrimitiveMeshVaos["default_cross"].metaData = new MeshMetaData();
            GLPrimitiveMeshVaos["default_cross"].metaData.batchcount = c.geom.indicesCount;
            GLPrimitiveMeshVaos["default_cross"].metaData.AABBMIN = new Vector3(-0.1f);
            GLPrimitiveMeshVaos["default_cross"].metaData.AABBMAX = new Vector3(0.1f);
            GLPrimitiveMeshVaos["default_cross"].metaData.indicesLength = DrawElementsType.UnsignedInt;
            GLPrimitiveMeshVaos["default_cross"].vao = GLPrimitiveVaos["default_cross"];
            GLPrimitiveMeshVaos["default_cross"].material = GLmaterials["crossMat"];


            //Default cube
            GMDL.Primitives.Box bx = new GMDL.Primitives.Box(1.0f, 1.0f, 1.0f, new Vector3(1.0f), true);
            GLPrimitiveVaos["default_box"] = bx.getVAO();
            GLPrimitiveMeshVaos["default_box"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_box"].vao = GLPrimitiveVaos["default_box"];


            //Default sphere
            GMDL.Primitives.Sphere sph = new GMDL.Primitives.Sphere(new Vector3(0.0f, 0.0f, 0.0f), 100.0f);
            GLPrimitiveVaos["default_sphere"] = sph.getVAO();
            GLPrimitiveMeshVaos["default_sphere"] = new GLMeshVao();
            GLPrimitiveMeshVaos["default_sphere"].vao = GLPrimitiveVaos["default_sphere"];

            generateGizmoParts();
        }

        //Picks the shader map and mesh map a material's shader belongs to. The existence check and the
        //compilation must agree, otherwise the shader is recompiled (and the old program leaked) every time
        public void getShaderMaps(Material mat, out Dictionary<int, GLSLShaderConfig> shaderDict,
                                  out Dictionary<int, List<GLMeshVao>> meshList, out bool deferred)
        {
            deferred = true;
            if (mat.Name == "collisionMat" || mat.Name == "crossMat" || mat.Name == "jointMat")
            {
                shaderDict = GLDefaultShaderMap;
                meshList = defaultMeshShaderMap;
            }
            else if (mat.ShaderFlags.Contains("_F51_DECAL_DIFFUSE") ||
                mat.ShaderFlags.Contains("_F52_DECAL_NORMAL"))
            {
                shaderDict = GLDeferredShaderMapDecal;
                meshList = decalMeshShaderMap;
            }
            else if (mat.ShaderFlags.Contains("_F09_TRANSPARENT"))
            {
                shaderDict = GLForwardShaderMapTransparent;
                meshList = transparentMeshShaderMap;
                deferred = false;
            }
            else if (mat.ShaderFlags.Contains("_F07_UNLIT"))
            {
                shaderDict = GLDeferredUNLITShaderMap;
                meshList = opaqueMeshShaderMap;
            }
            else
            {
                shaderDict = GLDeferredLITShaderMap;
                meshList = opaqueMeshShaderMap;
            }
        }

        public bool shaderExistsForMaterial(Material mat)
        {
            getShaderMaps(mat, out Dictionary<int, GLSLShaderConfig> shaderDict, out _, out _);
            return shaderDict.ContainsKey(mat.shaderHash);
        }

        public void Cleanup()
        {
            //Cleanup global texture manager
            texMgr.cleanup();
            //procTextureLayerSelections.Clear();

            foreach (Scene p in GLScenes.Values)
                p.Dispose();
            GLScenes.Clear();

            //Cleanup Geom Objects
            foreach (GeomObject p in GLgeoms.Values)
                p.Dispose();
            GLgeoms.Clear();

            //Cleanup GLVaos
            foreach (GLVao p in GLVaos.Values)
                p.Dispose();
            GLVaos.Clear();
            
            //Cleanup Animations
            Animations.Clear();

            //Cleanup Materials
            foreach (Material p in GLmaterials.Values)
                p.Dispose();
            GLmaterials.Clear();

            //Material shaders only depend on the material flags, so the compiled programs are kept for the
            //next file. Only the per-scene mesh lists are emptied (the keys stay, one per compiled shader)
            foreach (List<GLMeshVao> l in opaqueMeshShaderMap.Values)
                l.Clear();
            foreach (List<GLMeshVao> l in defaultMeshShaderMap.Values)
                l.Clear();
            foreach (List<GLMeshVao> l in transparentMeshShaderMap.Values)
                l.Clear();
            foreach (List<GLMeshVao> l in decalMeshShaderMap.Values)
                l.Clear();

            activeGLDeferredLITShaders.Clear();
            activeGLDeferredUNLITShaders.Clear();
            activeGLDeferredDecalShaders.Clear();
            activeGLForwardTransparentShaders.Clear();

            //Cleanup archives
            NMSUtils.DisposeArchives();

            //Cleanup Lights
            foreach (Light p in GLlights)
                p.Dispose();
            GLlights.Clear();

            //Cleanup Cameras
            //TODO: Make Camera Disposable
            //foreach (GMDL.Camera p in GLCameras)
            //    p.Dispose();
            GLCameras.Clear();
        
        }
    }

    


   
}
