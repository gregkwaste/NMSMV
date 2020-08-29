using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using MVCore.Common;
using MVCore.Utils;
using libMBIN.NMS.Toolkit;
using System.Diagnostics;

namespace MVCore.GMDL
{
    public static class TextureMixer
    {
        //Local storage
        public static Dictionary<string, Dictionary<string, Vector4>> palette = new Dictionary<string, Dictionary<string, Vector4>>();
        public static List<PaletteOpt> palOpts = new List<PaletteOpt>();
        public static List<Texture> difftextures = new List<Texture>(8);
        public static List<Texture> masktextures = new List<Texture>(8);
        public static List<Texture> normaltextures = new List<Texture>(8);
        public static float[] baseLayersUsed = new float[8];
        public static float[] alphaLayersUsed = new float[8];
        public static List<float[]> reColourings = new List<float[]>(8);
        public static List<float[]> avgColourings = new List<float[]>(8);
        private static int[] old_vp_size = new int[4];


        public static void clear()
        {
            //Cleanup temp buffers
            difftextures.Clear();
            masktextures.Clear();
            normaltextures.Clear();
            reColourings.Clear();
            avgColourings.Clear();
            for (int i = 0; i < 8; i++)
            {
                difftextures.Add(null);
                masktextures.Add(null);
                normaltextures.Add(null);
                reColourings.Add(new float[] { 0.0f, 0.0f, 0.0f, 0.0f });
                avgColourings.Add(new float[] { 0.5f, 0.5f, 0.5f, 0.5f });
                palOpts.Add(null);
            }
        }

        public static void combineTextures(string path, Dictionary<string, Dictionary<string, Vector4>> pal_input, ref textureManager texMgr)
        {
            clear();
            palette = pal_input;

            //Contruct .mbin file from dds
            string[] split = path.Split('.');
            //Construct main filename
            string temp = split[0] + ".";

            string mbinPath = temp + "TEXTURE.MBIN";
            prepareTextures(texMgr, mbinPath);

            //Init framebuffer
            int tex_width = 0;
            int tex_height = 0;
            int fbo_tex = -1;
            int fbo = -1;

            bool fbo_status = setupFrameBuffer(ref fbo, ref fbo_tex, ref tex_width, ref tex_height);

            if (!fbo_status)
            {
                CallBacks.Log("Unable to mix textures, probably 0x0 textures...\n");
                return;
            }

            Texture diffTex = mixDiffuseTextures(tex_width, tex_height);
            diffTex.name = temp + "DDS";

            Texture maskTex = mixMaskTextures(tex_width, tex_height);
            maskTex.name = temp + "MASKS.DDS";

            Texture normalTex = mixNormalTextures(tex_width, tex_height);
            normalTex.name = temp + "NORMAL.DDS";

            revertFrameBuffer(fbo, fbo_tex);

            //Add the new procedural textures to the textureManager
            texMgr.addTexture(diffTex);
            texMgr.addTexture(maskTex);
            texMgr.addTexture(normalTex);
        }

        //Generate procedural textures
        private static void prepareTextures(textureManager texMgr, string path)
        {
            //At this point, at least one sampler exists, so for now I assume that the first sampler
            //is always the diffuse sampler and I can initiate the mixing process
            Console.WriteLine("Procedural Texture Detected: " + path);
            CallBacks.Log(string.Format("Parsing Procedural Texture"));

            TkProceduralTextureList template = NMSUtils.LoadNMSTemplate(path, ref Common.RenderState.activeResMgr) as TkProceduralTextureList;

            List<TkProceduralTexture> texList = new List<TkProceduralTexture>(8);
            for (int i = 0; i < 8; i++) texList.Add(null);
            ModelProcGen.parse_procTexture(ref texList, template, ref Common.RenderState.activeResMgr);

            Common.CallBacks.Log("Proc Texture Selection");
            for (int i = 0; i < 8; i++)
            {
                if (texList[i] != null)
                {
                    string partNameDiff = texList[i].Diffuse;
                    Common.CallBacks.Log(partNameDiff);
                }
            }

            Common.CallBacks.Log("Procedural Material. Trying to generate procTextures...");

            for (int i = 0; i < 8; i++)
            {

                TkProceduralTexture ptex = texList[i];
                //Add defaults
                if (ptex == null)
                {
                    baseLayersUsed[i] = 0.0f;
                    alphaLayersUsed[i] = 0.0f;
                    continue;
                }

                string partNameDiff = ptex.Diffuse;
                string partNameMask = ptex.Mask;
                string partNameNormal = ptex.Normal;

                TkPaletteTexture paletteNode = ptex.Palette;
                string paletteName = paletteNode.Palette.ToString();
                string colorName = paletteNode.ColourAlt.ToString();
                Vector4 palColor = palette[paletteName][colorName];
                //Randomize palette Color every single time
                //Vector3 palColor = Model_Viewer.Palettes.get_color(paletteName, colorName);

                //Store pallete color to Recolouring List
                reColourings[i] = new float[] { palColor[0], palColor[1], palColor[2], palColor[3] };
                if (ptex.OverrideAverageColour)
                    avgColourings[i] = new float[] { ptex.AverageColour.R, ptex.AverageColour.G, ptex.AverageColour.B, ptex.AverageColour.A };

                //Create Palette Option
                PaletteOpt palOpt = new PaletteOpt();
                palOpt.PaletteName = paletteName;
                palOpt.ColorName = colorName;
                palOpts[i] = palOpt;
                Console.WriteLine("Index {0} Palette Selection {1} {2} ", i, palOpt.PaletteName, palOpt.ColorName);
                Console.WriteLine("Index {0} Color {1} {2} {3} {4}", i, palColor[0], palColor[1], palColor[2], palColor[3]);

                //DIFFUSE
                if (partNameDiff == "")
                {
                    //Add White
                    baseLayersUsed[i] = 0.0f;
                }
                else if (!texMgr.hasTexture(partNameDiff))
                {
                    //Configure the Diffuse Texture
                    try
                    {
                        Texture tex = new Texture(partNameDiff);
                        tex.palOpt = palOpt;
                        tex.procColor = palColor;
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(tex);

                        //Save Texture to material
                        difftextures[i] = tex;
                        baseLayersUsed[i] = 1.0f;
                        alphaLayersUsed[i] = 1.0f;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Texture Not Found Continue
                        Console.WriteLine("Diffuse Texture " + partNameDiff + " Not Found, Appending White Tex");
                        CallBacks.Log(string.Format("Diffuse Texture {0} Not Found", partNameDiff));
                        baseLayersUsed[i] = 0.0f;
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameDiff);
                    //Save Texture to material
                    difftextures[i] = tex;
                    baseLayersUsed[i] = 1.0f;
                }

                //MASK
                if (partNameMask == "")
                {
                    //Skip
                    alphaLayersUsed[i] = 0.0f;
                }
                else if (!texMgr.hasTexture(partNameMask))
                {
                    //Configure Mask
                    try
                    {
                        Texture texmask = new Texture(partNameMask);
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(texmask);
                        //Store Texture to material
                        masktextures[i] = texmask;
                        alphaLayersUsed[i] = 0.0f;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Mask Texture not found
                        Console.WriteLine("Mask Texture " + partNameMask + " Not Found");
                        CallBacks.Log(string.Format("Mask Texture {0} Not Found", partNameMask));
                        alphaLayersUsed[i] = 0.0f;
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameMask);
                    //Store Texture to material
                    masktextures[i] = tex;
                    alphaLayersUsed[i] = 1.0f;
                }


                //NORMALS
                if (partNameNormal == "")
                {
                    //Skip

                }
                else if (!texMgr.hasTexture(partNameNormal))
                {
                    try
                    {
                        Texture texnormal = new Texture(partNameNormal);
                        //Store to master texture manager
                        Common.RenderState.activeResMgr.texMgr.addTexture(texnormal);
                        //Store Texture to material
                        normaltextures[i] = texnormal;
                    }
                    catch (System.IO.FileNotFoundException)
                    {
                        //Normal Texture not found
                        CallBacks.Log(string.Format("Normal Texture {0} Not Found", partNameNormal));
                    }
                }
                else
                //Load texture from dict
                {
                    Texture tex = texMgr.getTexture(partNameNormal);
                    //Store Texture to material
                    normaltextures[i] = tex;
                }
            }
        }

        private static bool setupFrameBuffer(ref int fbo, ref int fbo_tex, ref int texWidth, ref int texHeight)
        {
            for (int i = 0; i < 8; i++)
            {
                if (difftextures[i] != null)
                {
                    texHeight = difftextures[i].height;
                    texWidth = difftextures[i].width;
                    break;
                }
            }

            if (texWidth == 0 || texHeight == 0)
            {
                //FUCKING HG HAS FUCKING EMPTY TEXTURES WTF AM I SUPPOSED TO MIX HERE
                return false;
            }


            //Diffuse Output
            fbo_tex = Sampler.generate2DTexture(PixelInternalFormat.Rgba, texWidth, texHeight, PixelFormat.Rgba, PixelType.UnsignedByte, 1);
            Sampler.setupTextureParameters(TextureTarget.Texture2D, fbo_tex, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Create New RenderBuffer for the diffuse
            fbo = GL.GenFramebuffer();
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            //Attach Textures to this FBO
            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, fbo_tex, 0);

            //Check
            Debug.Assert(GL.CheckFramebufferStatus(FramebufferTarget.Framebuffer) == FramebufferErrorCode.FramebufferComplete);

            //Bind the FBO
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);

            //Set Viewport
            GL.GetInteger(GetPName.Viewport, old_vp_size);
            GL.Viewport(0, 0, texWidth, texHeight);

            return true;
        }

        private static void revertFrameBuffer(int fbo, int fbo_tex)
        {
            //Bring Back screen
            GL.Viewport(0, 0, old_vp_size[2], old_vp_size[3]);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.DeleteFramebuffer(fbo);

            //Delete Fraomebuffer Textures
            GL.DeleteTexture(fbo_tex);
        }

        public static Texture mixDiffuseTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    int active_id = i;
                    GL.Uniform1(loc + i, baseLayersUsed[active_id]);
                    if (baseLayersUsed[i] > 0.0f)
                        baseLayerIndex = i;
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);

            //Upload DiffuseTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 0.0f);

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 1.0f);

            //Upload Recolouring Information
            loc = GL.GetUniformLocation(pass_program, "lRecolours");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    GL.Uniform4(loc + i, (float)reColourings[i][0],
                                     (float)reColourings[i][1],
                                     (float)reColourings[i][2],
                                     (float)reColourings[i][3]);
                }
            }


            //Upload Average Colors Information
            loc = GL.GetUniformLocation(pass_program, "lAverageColors");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    GL.Uniform4(loc + i, 0.5f, 0.5f, 0.5f, 0.5f);
                }
            }

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_diffuse = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_diffuse, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_diffuse);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_diffuse);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.width = texWidth;
            new_tex.height = texHeight;
            new_tex.texID = out_tex_2darray_diffuse;
            new_tex.target = TextureTarget.Texture2DArray;

#if (DUMP_TEXTURES)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("diffuse", texWidth, texHeight);
#endif
            return new_tex;
        }

        private static Texture mixMaskTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (masktextures[i] != null)
                    {
                        GL.Uniform1(loc + i, 1.0f);
                        baseLayerIndex = i;
                    }
                    else
                        GL.Uniform1(loc + i, 0.0f);
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);


            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 1.0f);

            //Upload DiffuseTextures as alphaTextures
            loc = GL.GetUniformLocation(pass_program, "alphaTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Upload maskTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (masktextures[i] != null)
                        tex = masktextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, 8 + i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture8;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);


            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_mask);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_mask);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.texID = out_tex_2darray_mask;
            new_tex.target = TextureTarget.Texture2DArray;

#if (DUMP_TEXTURESNONO)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("mask", texWidth, texHeight);
#endif
            return new_tex;
        }

        private static Texture mixNormalTextures(int texWidth, int texHeight)
        {
            //Upload Textures

            //BIND TEXTURES
            Texture tex;
            int loc;

            Texture dMask = Common.RenderState.activeResMgr.texMgr.getTexture("default_mask.dds");
            Texture dDiff = Common.RenderState.activeResMgr.texMgr.getTexture("default.dds");

            //USE PROGRAM
            int pass_program = Common.RenderState.activeResMgr.GLShaders[GLSLHelper.SHADER_TYPE.TEXTURE_MIX_SHADER].program_id;
            GL.UseProgram(pass_program);

            //Upload base Layers Used
            int baseLayerIndex = 0;
            loc = GL.GetUniformLocation(pass_program, "lbaseLayersUsed");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (normaltextures[i] != null)
                    {
                        GL.Uniform1(loc + i, 1.0f);
                        baseLayerIndex = i;
                    }
                    else
                        GL.Uniform1(loc + i, 0.0f);
                }
            }

            loc = GL.GetUniformLocation(pass_program, "baseLayerIndex");
            GL.Uniform1(loc, baseLayerIndex);


            //No need for extra alpha tetuxres
            loc = GL.GetUniformLocation(pass_program, "use_alpha_textures");
            GL.Uniform1(loc, 1.0f);

            //Upload DiffuseTextures as alphaTextures
            loc = GL.GetUniformLocation(pass_program, "alphaTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (difftextures[i] != null)
                        tex = difftextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture0;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Upload maskTextures
            loc = GL.GetUniformLocation(pass_program, "mainTex");
            if (loc >= 0)
            {
                for (int i = 0; i < 8; i++)
                {
                    if (normaltextures[i] != null)
                        tex = normaltextures[i];
                    else
                        tex = dMask;

                    //Upload diffuse Texture
                    GL.Uniform1(loc + i, 8 + i); // I need to upload the texture unit number

                    int tex0Id = (int)TextureUnit.Texture8;

                    GL.ActiveTexture((TextureUnit)(tex0Id + i));
                    GL.BindTexture(tex.target, tex.texID);
                }
            }

            //Activate Recoloring
            loc = GL.GetUniformLocation(pass_program, "recolor_flag");
            GL.Uniform1(loc, 0.0f);


            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            GL.BindVertexArray(RenderState.activeResMgr.GLPrimitiveVaos["default_renderquad"].vao_id);
            GL.PolygonMode(MaterialFace.FrontAndBack, PolygonMode.Fill);
            GL.DrawElements(PrimitiveType.Triangles, 6, DrawElementsType.UnsignedInt, IntPtr.Zero);

            //Console.WriteLine("MixTextures5, Last GL Error: " + GL.GetError());
            int out_tex_2darray_mask = Sampler.generateTexture2DArray(PixelInternalFormat.Rgba8, texWidth, texHeight, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 11);
            Sampler.setupTextureParameters(TextureTarget.Texture2DArray, out_tex_2darray_mask, (int)TextureWrapMode.Repeat,
                (int)TextureMagFilter.Linear, (int)TextureMinFilter.LinearMipmapLinear, 4.0f);

            //Copy the read buffers to the 

            GL.BindTexture(TextureTarget.Texture2DArray, out_tex_2darray_mask);
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            GL.CopyTexSubImage3D(TextureTarget.Texture2DArray, 0, 0, 0, 0, 0, 0, texWidth, texHeight);

            //Generate Mipmaps to the new textures from the base level
            Sampler.generateTexture2DArrayMipmaps(out_tex_2darray_mask);

            //Find name for textures

            //Store Diffuse Texture to material
            Texture new_tex = new Texture();
            new_tex.texID = out_tex_2darray_mask;
            new_tex.target = TextureTarget.Texture2DArray;

#if (DUMP_TEXTURES)
            GL.ReadBuffer(ReadBufferMode.ColorAttachment0);
            Sampler.dump_texture("normal", texWidth, texHeight);
#endif
            return new_tex;
        }
    }

}
