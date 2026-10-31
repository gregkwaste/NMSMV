using Assimp;
using libMBIN.NMS.Toolkit;
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NumericsMatrix4x4 = System.Numerics.Matrix4x4;
using ViewerMaterial = MVCore.GMDL.Material;
using ViewerMesh = MVCore.GMDL.Mesh;

namespace WPFModelViewer
{
    //CPU-side result of an Assimp import. OpenGL resources are created later on the render thread.
    internal sealed class AssimpGeometryImportResult
    {
        public Model Root { get; init; }
        public int NodeCount { get; init; }
        public int MeshCount { get; init; }
        public int VertexCount { get; init; }
        public int TriangleCount { get; init; }
    }

    internal static class AssimpGeometryImporter
    {
        private const int VertexStrideFloats = 8; //position.xyz, uv.xy, normal.xyz

        public static string BuildFileFilter()
        {
            using AssimpContext context = new AssimpContext();
            string[] extensions = context.GetSupportedImportFormats()
                .Where(extension => !String.IsNullOrWhiteSpace(extension))
                .Select(extension => extension.Trim().TrimStart('*').TrimStart('.'))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(extension => extension, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            string pattern = String.Join(";", extensions.Select(extension => "*." + extension));
            return pattern.Length == 0
                ? "Geometry Files|*.*"
                : "Assimp Geometry Files|" + pattern + "|All Files|*.*";
        }

        public static AssimpGeometryImportResult Import(string filename)
        {
            using AssimpContext context = new AssimpContext();
            PostProcessSteps steps = PostProcessSteps.Triangulate |
                                     PostProcessSteps.JoinIdenticalVertices |
                                     PostProcessSteps.GenerateSmoothNormals |
                                     PostProcessSteps.CalculateTangentSpace |
                                     PostProcessSteps.FlipUVs |
                                     PostProcessSteps.ImproveCacheLocality |
                                     PostProcessSteps.SortByPrimitiveType;
            Assimp.Scene source = context.ImportFile(filename, steps);
            if (source?.RootNode == null)
                throw new InvalidDataException("Assimp did not return a scene hierarchy.");
            if (source.MeshCount == 0)
                throw new InvalidDataException("The imported file does not contain any meshes.");

            string sourceName = Path.GetFileNameWithoutExtension(filename);
            List<ViewerMaterial> materials = buildMaterials(source, filename, sourceName);
            int nodeCount = 0;
            int meshCount = 0;
            int vertexCount = 0;
            int triangleCount = 0;
            Model root = importNode(source.RootNode, source, materials, ref nodeCount, ref meshCount,
                ref vertexCount, ref triangleCount);
            if (String.IsNullOrWhiteSpace(root.Name))
                root.Name = sourceName;

            return new AssimpGeometryImportResult
            {
                Root = root,
                NodeCount = nodeCount,
                MeshCount = meshCount,
                VertexCount = vertexCount,
                TriangleCount = triangleCount
            };
        }

        private static Model importNode(Assimp.Node source, Assimp.Scene scene, List<ViewerMaterial> materials,
            ref int nodeCount, ref int meshCount, ref int vertexCount, ref int triangleCount)
        {
            string nodeName = String.IsNullOrWhiteSpace(source.Name) ? "Node" : source.Name;
            List<int> meshIndices = source.MeshIndices
                .Where(index => index >= 0 && index < scene.MeshCount)
                .ToList();

            Model result;
            if (meshIndices.Count == 1)
            {
                result = importMesh(scene.Meshes[meshIndices[0]], materials, meshIndices[0], 0, nodeName,
                    ref vertexCount, ref triangleCount);
                if (result == null)
                    result = createLocator(nodeName);
                else
                    meshCount++;
            }
            else
            {
                result = createLocator(nodeName);
                for (int slot = 0; slot < meshIndices.Count; slot++)
                {
                    int meshIndex = meshIndices[slot];
                    ViewerMesh mesh = importMesh(scene.Meshes[meshIndex], materials, meshIndex, slot, null,
                        ref vertexCount, ref triangleCount);
                    if (mesh == null)
                        continue;
                    addChild(result, mesh);
                    meshCount++;
                }
            }

            applyTransform(result, source.Transform);
            nodeCount++;

            foreach (Assimp.Node child in source.Children)
                addChild(result, importNode(child, scene, materials, ref nodeCount, ref meshCount,
                    ref vertexCount, ref triangleCount));
            return result;
        }

        private static ViewerMesh importMesh(Assimp.Mesh source, List<ViewerMaterial> materials, int meshIndex, int slot,
            string nodeName, ref int totalVertices, ref int totalTriangles)
        {
            List<uint> indices = new List<uint>();
            foreach (Face face in source.Faces)
            {
                if (face.IndexCount != 3)
                    continue;
                indices.Add((uint)face.Indices[0]);
                indices.Add((uint)face.Indices[1]);
                indices.Add((uint)face.Indices[2]);
            }
            if (source.VertexCount == 0 || indices.Count == 0)
                return null;

            float[] vertices = new float[source.VertexCount * VertexStrideFloats];
            Vector3 minimum = new Vector3(float.MaxValue);
            Vector3 maximum = new Vector3(float.MinValue);
            bool hasUvs = source.TextureCoordinateChannelCount > 0 &&
                           source.TextureCoordinateChannels[0]?.Count == source.VertexCount;
            bool hasNormals = source.HasNormals && source.Normals.Count == source.VertexCount;
            for (int i = 0; i < source.VertexCount; i++)
            {
                System.Numerics.Vector3 position = source.Vertices[i];
                System.Numerics.Vector3 normal = hasNormals ? source.Normals[i] : System.Numerics.Vector3.UnitY;
                System.Numerics.Vector3 uv = hasUvs
                    ? source.TextureCoordinateChannels[0][i]
                    : System.Numerics.Vector3.Zero;
                int offset = i * VertexStrideFloats;
                vertices[offset + 0] = position.X;
                vertices[offset + 1] = position.Y;
                vertices[offset + 2] = position.Z;
                vertices[offset + 3] = uv.X;
                vertices[offset + 4] = uv.Y;
                vertices[offset + 5] = normal.X;
                vertices[offset + 6] = normal.Y;
                vertices[offset + 7] = normal.Z;
                minimum = Vector3.ComponentMin(minimum, new Vector3(position.X, position.Y, position.Z));
                maximum = Vector3.ComponentMax(maximum, new Vector3(position.X, position.Y, position.Z));
            }

            GeomObject geometry = new GeomObject
            {
                vertCount = source.VertexCount,
                indicesCount = indices.Count,
                indicesLength = sizeof(uint),
                indicesLengthType = DrawElementsType.UnsignedInt,
                vx_size = VertexStrideFloats * sizeof(float),
                offsets = Enumerable.Repeat(-1, 7).ToArray(),
                vbuffer = new byte[vertices.Length * sizeof(float)],
                ibuffer = new byte[indices.Count * sizeof(uint)]
            };
            System.Buffer.BlockCopy(vertices, 0, geometry.vbuffer, 0, geometry.vbuffer.Length);
            System.Buffer.BlockCopy(indices.ToArray(), 0, geometry.ibuffer, 0, geometry.ibuffer.Length);
            geometry.bufInfo = Enumerable.Repeat<bufInfo>(null, 7).ToList();
            int stride = VertexStrideFloats * sizeof(float);
            geometry.bufInfo[0] = new bufInfo(0, VertexAttribPointerType.Float, 3, stride, 0, "vPosition", false);
            geometry.bufInfo[1] = new bufInfo(1, VertexAttribPointerType.Float, 2, stride, 3 * sizeof(float), "uvPosition", false);
            geometry.bufInfo[2] = new bufInfo(2, VertexAttribPointerType.Float, 3, stride, 5 * sizeof(float), "nPosition", false);
            geometry.offsets[0] = 0;
            geometry.offsets[1] = 3 * sizeof(float);
            geometry.offsets[2] = 5 * sizeof(float);

            MeshMetaData metadata = new MeshMetaData
            {
                batchstart_graphics = 0,
                batchcount = indices.Count,
                vertrstart_graphics = 0,
                vertrend_graphics = source.VertexCount - 1,
                indicesLength = DrawElementsType.UnsignedInt,
                AABBMIN = minimum,
                AABBMAX = maximum,
                Hash = 0
            };
            ViewerMaterial material = source.MaterialIndex >= 0 && source.MaterialIndex < materials.Count
                ? materials[source.MaterialIndex]
                : materials[0];
            GLMeshVao meshVao = new GLMeshVao(metadata)
            {
                type = TYPES.MESH,
                material = material,
                color = Vector3.One
            };
            string name = !String.IsNullOrWhiteSpace(nodeName)
                ? nodeName
                : (String.IsNullOrWhiteSpace(source.Name) ? "Mesh_" + meshIndex : source.Name);
            if (slot > 0)
                name += "_" + slot;
            metadata.Hash = MVCore.GMDL.Model.nameHashOf(name);
            ViewerMesh result = new ViewerMesh
            {
                Name = name,
                IsSceneNode = true,
                metaData = metadata,
                gobject = geometry,
                meshVao = meshVao,
                AABBMIN = minimum,
                AABBMAX = maximum,
                color = Vector3.One
            };
            totalVertices += source.VertexCount;
            totalTriangles += indices.Count / 3;
            return result;
        }

        private static Locator createLocator(string name)
        {
            Locator result = new Locator { Name = name, IsSceneNode = true };
            return result;
        }

        private static void addChild(Model parent, Model child)
        {
            parent.AddChild(child);
        }

        private static List<ViewerMaterial> buildMaterials(Assimp.Scene scene, string sourcePath, string sourceName)
        {
            List<ViewerMaterial> result = new List<ViewerMaterial>();
            if (scene.MaterialCount == 0)
                result.Add(createMaterial(scene, null, sourcePath, sourceName, 0));
            else
                for (int i = 0; i < scene.MaterialCount; i++)
                    result.Add(createMaterial(scene, scene.Materials[i], sourcePath, sourceName, i));
            return result;
        }

        private static ViewerMaterial createMaterial(Assimp.Scene scene, Assimp.Material source, string sourcePath,
            string sourceName, int index)
        {
            float red = 0.75f;
            float green = 0.75f;
            float blue = 0.75f;
            float alpha = 1.0f;
            if (source?.HasColorDiffuse == true)
            {
                red = source.ColorDiffuse.X;
                green = source.ColorDiffuse.Y;
                blue = source.ColorDiffuse.Z;
                alpha = source.ColorDiffuse.W;
            }

            ViewerMaterial material = new ViewerMaterial();
            string materialName = source == null || String.IsNullOrWhiteSpace(source.Name)
                ? "Material_" + index
                : source.Name;
            material.Name = "Imported_" + sourceName + "_" + materialName;
            material.name_key = material.Name;
            material.Template.CastShadow = true;
            material.Template.Class.MaterialClass = TkMaterialClass.MaterialClassEnum.Opaque;
            material.Template.Shader = "SHADERS/UBERSHADER.SHADER.BIN";
            material.Template.Link = "";

            //Translate texture slots to the sampler names consumed by the normal material shader. PBR
            //slots take precedence over legacy aliases because Assimp commonly exposes both for glTF.
            if (tryAddSampler(material, scene, source, sourcePath, sourceName, index, "gDiffuseMap",
                    TextureType.BaseColor, TextureType.Diffuse))
                material.add_flag(TkMaterialFlags.MaterialFlagEnum._F01_DIFFUSEMAP);
            if (tryAddSampler(material, scene, source, sourcePath, sourceName, index, "gNormalMap",
                    TextureType.NormalCamera, TextureType.Normals, TextureType.Height))
                material.add_flag(TkMaterialFlags.MaterialFlagEnum._F03_NORMALMAP);
            if (tryAddSampler(material, scene, source, sourcePath, sourceName, index, "gOcclusionMap",
                    TextureType.AmbientOcclusion, TextureType.Lightmap))
                material.add_flag(TkMaterialFlags.MaterialFlagEnum._F22_OCCLUSION_MAP);
            if (source?.ShadingMode == ShadingMode.NoShading)
                material.add_flag(TkMaterialFlags.MaterialFlagEnum._F07_UNLIT);

            string alphaMode = source?.GetAllProperties()
                .FirstOrDefault(property => property.Name == "$mat.gltf.alphaMode")
                ?.GetStringValue();
            if (String.Equals(alphaMode, "MASK", StringComparison.OrdinalIgnoreCase))
                material.Template.Class.MaterialClass = TkMaterialClass.MaterialClassEnum.Cutout;
            else if (String.Equals(alphaMode, "BLEND", StringComparison.OrdinalIgnoreCase) ||
                     source?.HasTextureOpacity == true ||
                     source?.BlendMode == BlendMode.Additive ||
                     (source?.HasOpacity == true && source.Opacity < 0.999f) || alpha < 0.999f)
                material.Template.Class.MaterialClass = TkMaterialClass.MaterialClassEnum.Translucent;
            if (source?.IsTwoSided == true)
            {
                material.add_flag(TkMaterialFlags.MaterialFlagEnum._F36_DOUBLESIDED);
                if (material.Template.Class.MaterialClass == TkMaterialClass.MaterialClassEnum.Opaque)
                    material.Template.Class.MaterialClass = TkMaterialClass.MaterialClassEnum.DoubleSided;
            }

            TkMaterialUniform_Float colour = new TkMaterialUniform_Float
            {
                Name = "gMaterialColourVec4",
                Values = new libMBIN.NMS.Vector4f()
            };
            colour.Values.X = red;
            colour.Values.Y = green;
            colour.Values.Z = blue;
            colour.Values.W = alpha;
            material.Uniforms_Float.Add(colour);

            TkMaterialUniform_Float parameters = new TkMaterialUniform_Float
            {
                Name = "gMaterialParamsVec4",
                Values = new libMBIN.NMS.Vector4f()
            };
            parameters.Values.X = 0.5f;
            parameters.Values.Y = 0.0f;
            parameters.Values.Z = 0.5f;
            parameters.Values.W = 1.0f;
            material.Uniforms_Float.Add(parameters);
            material.Uniforms_Float.Add(materialUniform("gMaterialParams2Vec4", 0.0f, 1.0f, 0.0f, 0.0f));
            material.Uniforms_Float.Add(materialUniform("gMaterialSFXVec4", 0.0f, 0.0f, 0.0f, 0.0f));
            material.Uniforms_Float.Add(materialUniform("gMaterialSFX2Vec4", 0.0f, 0.0f, 0.0f, 0.0f));
            material.Uniforms_Float.Add(materialUniform("gMaterialSFXColVec4", 0.0f, 0.0f, 0.0f, 0.0f));
            return material;
        }

        private static TkMaterialUniform_Float materialUniform(string name, float x, float y, float z, float w)
        {
            return new TkMaterialUniform_Float
            {
                Name = name,
                Values = new libMBIN.NMS.Vector4f { X = x, Y = y, Z = z, W = w }
            };
        }

        private static bool tryAddSampler(ViewerMaterial target, Assimp.Scene scene, Assimp.Material source,
            string sourcePath, string sourceName, int materialIndex, string samplerName, params TextureType[] types)
        {
            if (source == null)
                return false;
            foreach (TextureType type in types)
            {
                if (!source.GetMaterialTexture(type, 0, out TextureSlot slot))
                    continue;
                string texturePath = resolveTexturePath(scene, slot, sourcePath, sourceName, materialIndex,
                    samplerName);
                if (String.IsNullOrWhiteSpace(texturePath))
                    continue;
                target.Samplers.Add(new TkMaterialSampler
                {
                    Name = samplerName,
                    Map = texturePath,
                    UseCompression = true,
                    UseMipMaps = true,
                    IsSRGB = samplerName == "gDiffuseMap",
                    TextureFilterMode = TkMaterialSampler.TextureFilterModeEnum.Trilinear,
                    //libMBIN string wrapper fields must be present even when their value is empty.
                    //Vanilla materials always deserialize this field, while a newly constructed
                    //sampler otherwise leaves it null and cannot be serialized back to MXML/MBIN.
                    MaterialAlternativeId = ""
                });
                return true;
            }
            return false;
        }

        private static string resolveTexturePath(Assimp.Scene scene, TextureSlot slot, string sourcePath,
            string sourceName, int materialIndex, string samplerName)
        {
            string map = slot.FilePath?.Trim();
            if (String.IsNullOrWhiteSpace(map))
                return null;
            if (Path.IsPathFullyQualified(map))
                return map;

            EmbeddedTexture embedded = scene.GetEmbeddedTexture(map);
            if (embedded == null)
                return Path.GetFullPath(Path.Combine(Path.GetDirectoryName(sourcePath) ?? "", map));

            string outputRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
                sanitizePathPart(sourceName)));
            string relativeName = embedded.Filename?.Trim();
            if (String.IsNullOrWhiteSpace(relativeName) || Path.IsPathFullyQualified(relativeName))
            {
                int embeddedIndex = map.Length > 1 && map[0] == '*' &&
                    Int32.TryParse(map.Substring(1), out int parsedIndex) ? parsedIndex : materialIndex;
                string extension = embedded.IsCompressed && !String.IsNullOrWhiteSpace(embedded.CompressedFormatHint)
                    ? "." + embedded.CompressedFormatHint.Trim().TrimStart('.')
                    : ".png";
                relativeName = samplerName + "_" + embeddedIndex + extension;
            }

            relativeName = relativeName.Replace('/', Path.DirectorySeparatorChar)
                .Replace('\\', Path.DirectorySeparatorChar);
            string outputPath = Path.GetFullPath(Path.Combine(outputRoot, relativeName));
            string rootPrefix = outputRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!outputPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
                outputPath = Path.Combine(outputRoot, samplerName + "_" + materialIndex + ".png");

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
            if (embedded.IsCompressed && embedded.HasCompressedData)
                File.WriteAllBytes(outputPath, embedded.CompressedData);
            else if (embedded.HasNonCompressedData)
                writeEmbeddedBitmap(outputPath, embedded);
            else
                return null;

            CallBacks.Log("[Import] Extracted embedded texture {0} to {1}", map, outputPath);
            return outputPath;
        }

        private static string sanitizePathPart(string value)
        {
            char[] invalid = Path.GetInvalidFileNameChars();
            return new string((value ?? "ImportedModel").Select(c => invalid.Contains(c) ? '_' : c).ToArray());
        }

        private static void writeEmbeddedBitmap(string outputPath, EmbeddedTexture embedded)
        {
            using System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(embedded.Width, embedded.Height,
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            for (int y = 0; y < embedded.Height; y++)
            for (int x = 0; x < embedded.Width; x++)
            {
                Texel texel = embedded.NonCompressedData[y * embedded.Width + x];
                bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(texel.A, texel.R, texel.G, texel.B));
            }
            bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
        }

        private static void applyTransform(Model target, NumericsMatrix4x4 transform)
        {
            if (!NumericsMatrix4x4.Decompose(transform, out System.Numerics.Vector3 scale,
                    out System.Numerics.Quaternion rotation, out System.Numerics.Vector3 translation))
                throw new InvalidDataException("A node transform could not be decomposed: " + target.Name);
            Matrix4 rotationMatrix = Matrix4.CreateFromQuaternion(
                new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W));
            Vector3 rotationAngles = matrixToEulerZxy(rotationMatrix);
            rotationAngles.X = MathUtils.degrees(rotationAngles.X);
            rotationAngles.Y = MathUtils.degrees(rotationAngles.Y);
            rotationAngles.Z = MathUtils.degrees(rotationAngles.Z);
            target.setUserTransform(new Vector3(translation.X, translation.Y, translation.Z), rotationAngles,
                new Vector3(scale.X, scale.Y, scale.Z));
        }

        //Model.init composes rotations as Z * X * Y, so imported matrices must use the same inverse order.
        private static Vector3 matrixToEulerZxy(Matrix4 matrix)
        {
            double m11 = matrix[0, 0];
            double m12 = matrix[0, 1];
            double m21 = matrix[1, 0];
            double m22 = matrix[1, 1];
            double m31 = matrix[2, 0];
            double m32 = matrix[2, 1];
            double m33 = matrix[2, 2];
            Vector3 result = new Vector3
            {
                X = (float)Math.Asin(MathUtils.clamp(m32, -1.0, 1.0))
            };
            if (Math.Abs(m32) < 0.99999)
            {
                result.Y = (float)Math.Atan2(-m31, m33);
                result.Z = (float)Math.Atan2(-m12, m22);
            }
            else
            {
                result.Y = 0.0f;
                result.Z = (float)Math.Atan2(m21, m11);
            }
            return result;
        }
    }
}
