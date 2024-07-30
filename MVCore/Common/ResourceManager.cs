﻿using System;
using System.Collections.Generic;
using System.IO;
using GLSLHelper;
using libMBIN.NMS.Toolkit;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Text;
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
        public Dictionary<string, Font> FontMap = new Dictionary<string, Font>();
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
        public FontManager fontMgr = new FontManager();
        public TextManager txtMgr = new TextManager();

        public bool initialized = false;

        //Procedural Generation Options
        //TODO: This is 99% NOT correct
        //public Dictionary<string, int> procTextureLayerSelections = new Dictionary<string, int>();

        //public DebugForm DebugWin;

        public void Init()
        {
            initialized = false;

            //Add defaults
            addDefaultTextures();
            addDefaultMaterials();
            addDefaultPrimitives();
            addDefaultLights();
            addDefaultFonts();
            addDefaultTexts();
            addDefaultCameras();
            compileMainShaders();

            initialized = true;
        }

        public void compileMainShaders()
        {

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


            //GAUSSIAN HORIZONTAL BLUR SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText gaussian_blur_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gaussian_blur_shader_fs.addStringFromFile("Shaders/gaussian_horizontalBlur_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.GAUSSIAN_HORIZONTAL_BLUR_SHADER] = shader_conf;


            //GAUSSIAN VERTICAL BLUR SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gaussian_blur_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gaussian_blur_shader_fs.addStringFromFile("Shaders/gaussian_verticalBlur_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gaussian_blur_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.GAUSSIAN_VERTICAL_BLUR_SHADER] = shader_conf;


            //BRIGHTNESS EXTRACTION SHADER
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/brightness_extract_shader_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.BRIGHTNESS_EXTRACT_SHADER] = shader_conf;


            //ADDITIVE BLEND
            gbuffer_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            gbuffer_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            gbuffer_shader_vs.addStringFromFile("Shaders/Gbuffer_VS.glsl");
            gbuffer_shader_fs.addStringFromFile("Shaders/additive_blend_fs.glsl");
            shader_conf = GLShaderHelper.compileShader(gbuffer_shader_vs, gbuffer_shader_fs, null, null, null,
                            GLSLHelper.SHADER_TYPE.ADDITIVE_BLEND_SHADER, ref log);
            GLShaders[GLSLHelper.SHADER_TYPE.ADDITIVE_BLEND_SHADER] = shader_conf;

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


            //Text Shaders
            GLSLShaderText text_shader_vs = new GLSLShaderText(ShaderType.VertexShader);
            GLSLShaderText text_shader_fs = new GLSLShaderText(ShaderType.FragmentShader);
            text_shader_vs.addStringFromFile("Shaders/Text_VS.glsl");
            text_shader_fs.addStringFromFile("Shaders/Text_FS.glsl");
            shader_conf = GLShaderHelper.compileShader(text_shader_vs, text_shader_fs, null, null, null,
                            SHADER_TYPE.TEXT_SHADER, ref log);
            GLShaders[SHADER_TYPE.TEXT_SHADER] = shader_conf;

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
            tex.textureInit(WPFModelViewer.Properties.Resources._default, texpath); //Manually load data
            texMgr.addTexture(tex);

            //Transparent Mask
            texpath = "default_mask.dds";
            tex = new Texture();
            tex.textureInit(WPFModelViewer.Properties.Resources.default_mask, texpath);
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
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F21_VERTEXCOLOUR);
            TkMaterialUniform uf = new TkMaterialUniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 1.0f;
            uf.Values.y = 1.0f;
            uf.Values.z = 1.0f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["crossMat"] = mat;

            //Joint Material
            mat = new Material();
            mat.Name = "jointMat";
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);

            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 1.0f;
            uf.Values.y = 0.0f;
            uf.Values.z = 0.0f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["jointMat"] = mat;

            //Light Material
            mat = new Material();
            mat.Name = "lightMat";
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);

            uf = new TkMaterialUniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 1.0f;
            uf.Values.y = 1.0f;
            uf.Values.z = 0.0f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["lightMat"] = mat;

            //Collision Material
            mat = new Material();
            mat.Name = "collisionMat";
            mat.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);

            uf = new TkMaterialUniform();
            uf.Name = "gMaterialColourVec4";
            uf.Values = new libMBIN.NMS.Vector4f();
            uf.Values.x = 0.8f;
            uf.Values.y = 0.8f;
            uf.Values.z = 0.2f;
            uf.Values.t = 1.0f;
            mat.Uniforms.Add(uf);
            mat.init();
            GLmaterials["collisionMat"] = mat;

        }

        private void addDefaultFonts()
        {
            Font f;
            //Droid Sans
            f = new Font(WPFModelViewer.Properties.Resources.droid_fnt,
                        WPFModelViewer.Properties.Resources.droid_png, 1);
            fontMgr.addFont(f);

            //Segoe
            f = new Font(WPFModelViewer.Properties.Resources.segoe_fnt,
                        WPFModelViewer.Properties.Resources.segoe_png, 1);
            fontMgr.addFont(f);
        }

        //fontMgr.addFont("droid.fnt");
    

        private void addDefaultTexts()
        {
            Font f = fontMgr.getFont("Segoe UI");
            //Font f = fontMgr.getFont("Arial");
            //Font f = fontMgr.getFont("Droid Sans Mono");

            Vector3 default_color = new Vector3(1.0f, 1.0f, 0.5f);
            float lineHeight = 15.0f;
            int pos_x = 10;
            int pos_y = 90;
            Text.Text t = new Text.Text(f, new Vector2(pos_x, pos_y), lineHeight, default_color,
                string.Format("FPS: {0:000.0}", RenderStats.fpsCount));
            txtMgr.addText(t, TextManager.Semantic.FPS);
            
            t = new Text.Text(f, new Vector2(pos_x, pos_y - lineHeight), lineHeight, default_color,
                string.Format("OccludedNum: {0:0000}", RenderStats.occludedNum));
            txtMgr.addText(t, TextManager.Semantic.OCCLUDED_COUNT);

            t = new Text.Text(f, new Vector2(pos_x, pos_y - 2 * lineHeight), lineHeight, default_color,
                string.Format("Total Vertices: {0:D1}", RenderStats.vertNum));
            txtMgr.addText(t, TextManager.Semantic.VERT_COUNT);
            
            t = new Text.Text(f, new Vector2(pos_x, pos_y - 3 * lineHeight), lineHeight, default_color,
                string.Format("Total Triangles: {0:D1}", RenderStats.trisNum));
            txtMgr.addText(t, TextManager.Semantic.TRIS_COUNT);

            t = new Text.Text(f, new Vector2(pos_x, pos_y - 4 * lineHeight), lineHeight, default_color,
                string.Format("Textures: {0:D1}", RenderStats.texturesNum));
            txtMgr.addText(t, TextManager.Semantic.TEXTURE_COUNT);

            t = new Text.Text(f, new Vector2(pos_x, pos_y - 5 * lineHeight), lineHeight, default_color,
                string.Format("Controller: {0} ", RenderState.activeGamepad?.getName()));
            txtMgr.addText(t, TextManager.Semantic.CTRL_ID);
        
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
            GMDL.Primitives.Cross c = new GMDL.Primitives.Cross(0.1f, true);
            GLPrimitiveMeshVaos["default_cross"] = new GLMeshVao();
            GLPrimitiveVaos["default_cross"] = c.getVAO();
            GLPrimitiveMeshVaos["default_cross"].type = TYPES.GIZMO;
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

        public bool shaderExistsForMaterial(Material mat)
        {
            Dictionary<int, GLSLShaderConfig> shaderDict;
            
            //Save shader to resource Manager
            if (mat.Name == "collisionMat" || mat.Name == "crossMat" || mat.Name == "jointMat")
            {
                shaderDict = GLDefaultShaderMap;
            }
            else if (mat.MaterialFlags.Contains("_F51_DECAL_DIFFUSE") ||
                mat.MaterialFlags.Contains("_F52_DECAL_NORMAL"))
            {
                shaderDict = GLDeferredShaderMapDecal;
            }
            else if (mat.MaterialFlags.Contains("_F09_TRANSPARENT") ||
                     mat.MaterialFlags.Contains("_F22_TRANSPARENT_SCALAR") ||
                     mat.MaterialFlags.Contains("_F11_ALPHACUTOUT"))
            {
                shaderDict = GLForwardShaderMapTransparent;
            }
            else if (mat.MaterialFlags.Contains("_F07_UNLIT"))
            {
                shaderDict = GLDeferredUNLITShaderMap;
            }
            else
            {
                shaderDict = GLDeferredLITShaderMap;
            }

            return shaderDict.ContainsKey(mat.shaderHash);
        }

        public void Cleanup()
        {
            //Cleanup global texture manager
            texMgr.cleanup();
            fontMgr.cleanup();
            txtMgr.cleanup();
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

            //Cleanup Material Shaders
            opaqueMeshShaderMap.Clear();
            defaultMeshShaderMap.Clear();
            transparentMeshShaderMap.Clear();
            decalMeshShaderMap.Clear();

            activeGLDeferredLITShaders.Clear();
            activeGLDeferredUNLITShaders.Clear();
            activeGLDeferredDecalShaders.Clear();
            activeGLForwardTransparentShaders.Clear();

            GLDeferredLITShaderMap.Clear();
            GLDeferredUNLITShaderMap.Clear();
            GLForwardShaderMapTransparent.Clear();
            GLDeferredShaderMapDecal.Clear();
            GLDefaultShaderMap.Clear();

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
