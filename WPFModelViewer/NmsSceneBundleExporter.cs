using libMBIN;
using libMBIN.NMS;
using libMBIN.NMS.Toolkit;
using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using GmdlModel = MVCore.GMDL.Model;
using GmdlMaterial = MVCore.GMDL.Material;
using GmdlMesh = MVCore.GMDL.Mesh;
using GmdlScene = MVCore.GMDL.Scene;

namespace WPFModelViewer
{
    //Produces a new scene asset set from the live viewer graph.  No scene, material, entity,
    //particle or geometry template from the imported asset is used as the exported root object.
    internal sealed class NmsSceneBundleExporter
    {
        private readonly string exportRoot;
        private readonly bool writeMbin;
        private readonly HashSet<string> usedLogicalPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> writtenLogicalPaths = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<GmdlMaterial, string> materials = new();
        private readonly Dictionary<string, string> textures = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<GmdlModel, string> scenes = new();
        private readonly HashSet<GmdlModel> activeScenes = new();
        private int fileCount;

        private NmsSceneBundleExporter(string destination)
        {
            string fullDestination = Path.GetFullPath(destination);
            string marker = Path.DirectorySeparatorChar + "MODELS" + Path.DirectorySeparatorChar;
            int modelsIndex = fullDestination.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            exportRoot = modelsIndex >= 0
                ? fullDestination.Substring(0, modelsIndex)
                : Path.GetFullPath(Path.GetDirectoryName(fullDestination) ?? ".");
            writeMbin = destination.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase);
        }

        public static void Export(GmdlModel root, string destination)
        {
            NmsSceneBundleExporter exporter = new(destination);
            string fallback = ensureAssetKind(Path.GetFileName(destination), "SCENE");
            //The selected destination owns the recreated asset hierarchy. If it contains a MODELS
            //segment, retain everything from MODELS onward as the logical game path.
            string logicalScene = exporter.assetPath(destination, fallback, "SCENE");
            exporter.scenes[root] = logicalScene;
            exporter.exportScene(root, logicalScene, destination);
            CallBacks.Log($"Recreated scene bundle ({exporter.fileCount} files) at {exporter.exportRoot}");
        }

        private void exportScene(GmdlModel root, string logicalScene, string physicalScenePath)
        {
            if (!activeScenes.Add(root))
                throw new InvalidDataException("The scene contains a circular loaded reference at " + root.EditableName);

            try
            {
                GeometryBundle geometry = GeometryBundle.Build(root);
                string geometryFallback = replaceAssetKind(logicalScene, "SCENE", "GEOMETRY");
                string geometryLogical = assetPath("", geometryFallback, "GEOMETRY");
                string geometryPath = physicalPath(geometryLogical + ".PC", true);
                string dataLogical = replaceAssetKind(geometryLogical, "GEOMETRY", "GEOMETRY.DATA") + ".PC";
                string dataPath = physicalPath(dataLogical, true);
                createParentDirectory(geometryPath);
                createParentDirectory(dataPath);
                writeTemplate(geometry.CreateTemplate(), geometryPath);
                File.WriteAllBytes(dataPath, geometry.Stream);
                fileCount += 2;
                CallBacks.Log("Recreated geometry metadata " + geometryPath);
                CallBacks.Log("Recreated geometry stream " + dataPath);

                TkSceneNodeData scene = buildNode(root, logicalScene, geometry, true);
                setAttribute(scene, "GEOMETRY", geometryLogical);
                createParentDirectory(physicalScenePath);
                writeTemplate(scene, physicalScenePath);
                fileCount++;
                CallBacks.Log("Recreated scene " + physicalScenePath);
            }
            finally
            {
                activeScenes.Remove(root);
            }
        }

        private TkSceneNodeData buildNode(GmdlModel model, string logicalScene, GeometryBundle geometry,
            bool isRoot = false)
        {
            TkSceneNodeData result = new TkSceneNodeData
            {
                Name = model.EditableName ?? "",
                NameHash = GmdlModel.nameHashOf(model.EditableName ?? ""),
                Type = model.Type,
                PlatformExclusion = 0,
                Transform = new TkTransformData
                {
                    TransX = model.__localPosition.X,
                    TransY = model.__localPosition.Y,
                    TransZ = model.__localPosition.Z,
                    RotX = model._localRotationAngles.X,
                    RotY = model._localRotationAngles.Y,
                    RotZ = model._localRotationAngles.Z,
                    ScaleX = model.__localScale.X,
                    ScaleY = model.__localScale.Y,
                    ScaleZ = model.__localScale.Z
                },
                Attributes = attributesFrom(model.SourceAttributes),
                Children = new List<TkSceneNodeData>(),
                InstanceTransforms = new List<TkInstanceTransformData>()
            };

            if (model is MVCore.GMDL.Light light)
            {
                setAttribute(result, "COL_R", f(light.Color.X));
                setAttribute(result, "COL_G", f(light.Color.Y));
                setAttribute(result, "COL_B", f(light.Color.Z));
                setAttribute(result, "FOV", f(light.fov));
                setAttribute(result, "INTENSITY", f(light.intensity));
                setAttribute(result, "RADIUS", f(light.radius));
                setAttribute(result, "COOKIE_IDX", light.cookieIndex.ToString(CultureInfo.InvariantCulture));
                setAttribute(result, "VOLUMETRIC", f(light.volumetric));
                setAttribute(result, "LIGHTLAYERS", light.lightLayers.ToString(CultureInfo.InvariantCulture));
                setAttribute(result, "FALLOFF", light.falloff switch
                {
                    ATTENUATION_TYPE.CONSTANT => "0",
                    ATTENUATION_TYPE.LINEAR_SQRT => "0.5",
                    ATTENUATION_TYPE.LINEAR => "1",
                    _ => "2"
                });
            }

            if (model is GmdlScene scene)
            {
                setAttribute(result, "NUMLODS", scene._LODNum.ToString(CultureInfo.InvariantCulture));
                for (int i = 1; i < scene._LODNum && i <= scene._LODDistances.Length; i++)
                    setAttribute(result, "LODDIST" + i, f(scene._LODDistances[i - 1]));
            }

            if (model is Joint joint)
                setAttribute(result, "JOINTINDEX", joint.jointIndex.ToString(CultureInfo.InvariantCulture));

            if (model is GmdlMesh mesh && geometry.TryGetMesh(mesh, out GeometryMesh exportedMesh))
            {
                setMeshAttributes(result, mesh, exportedMesh);
                setAttribute(result, "MATERIAL", exportMaterial(mesh.material, logicalScene, mesh.EditableName));
            }

            if (model.AttachmentTemplate != null)
                setAttribute(result, "ATTACHMENT", exportAttachment(model, logicalScene));
            else
                removeAttribute(result, "ATTACHMENT");

            if (model is ParticleEmitter emitter)
            {
                string fallback = siblingAsset(logicalScene,
                    safePart(model.EditableName, "EMITTER") + ".PARTICLE.MBIN");
                string particleLogical = claimAssetPath(assetPath(emitter.DataPath, fallback, "PARTICLE"));
                writeSidecar(emitter.CreateParticleTemplate(), particleLogical);
                setAttribute(result, "DATA", particleLogical);
                if (emitter.ParticleMaterial != null)
                    setAttribute(result, "MATERIAL", exportMaterial(emitter.ParticleMaterial, logicalScene,
                        model.EditableName + ".PARTICLE"));
                else
                    removeAttribute(result, "MATERIAL");
            }

            if (model is Reference reference)
            {
                GmdlScene loaded = reference.Children.OfType<GmdlScene>().FirstOrDefault(child => child.SourceFile != "");
                if (loaded == null)
                    throw new InvalidDataException($"Reference '{model.EditableName}' is not loaded. Load it before exporting so its scene can be recreated.");

                string fallback = siblingAsset(logicalScene,
                    safePart(model.EditableName, "REFERENCE") + ".SCENE.MBIN");
                string logical = assetPath(reference.ref_scene_filepath, fallback, "SCENE");
                if (scenes.TryGetValue(loaded, out string existingLogical))
                    logical = existingLogical;
                else
                {
                    logical = claimAssetPath(logical);
                    scenes.Add(loaded, logical);
                    exportScene(loaded, logical, physicalPath(logical));
                }
                setAttribute(result, "SCENEGRAPH", logical);
            }

            foreach (GmdlModel child in model.EmbeddedSceneChildren)
                result.Children.Add(buildNode(child, logicalScene, geometry));

            return result;
        }

        private string exportMaterial(GmdlMaterial material, string logicalScene, string nodeName)
        {
            if (material == null || material.Template == null)
                throw new InvalidDataException($"Node '{nodeName}' has no material data to recreate.");
            if (materials.TryGetValue(material, out string logical))
                return logical;

            string fallback = siblingAsset(logicalScene,
                safePart(nodeName, "MATERIAL") + ".MATERIAL.MBIN");
            string sourceMaterialPath = material.fromFile ? material.name_key : "";
            logical = claimAssetPath(assetPath(sourceMaterialPath, fallback, "MATERIAL"));
            TkMaterialData exported = clone(material.ExportTemplate());
            foreach (TkMaterialSampler sampler in exported.Samplers ?? new List<TkMaterialSampler>())
            {
                string sourceTexture = sampler.Map?.Value ?? "";
                if (File.Exists(sourceTexture))
                {
                    sampler.Map = exportTexture(sourceTexture, logicalScene);
                    if (!sourceTexture.EndsWith(".DDS", StringComparison.OrdinalIgnoreCase))
                    {
                        sampler.UseCompression = true;
                        sampler.UseMipMaps = true;
                        sampler.IsSRGB = sampler.Name?.Value == "gDiffuseMap";
                        sampler.TextureFilterMode = TkMaterialSampler.TextureFilterModeEnum.Trilinear;
                    }
                }
            }
            writeSidecar(exported, logical);
            materials.Add(material, logical);
            return logical;
        }

        private string exportTexture(string sourcePath, string logicalScene)
        {
            string fullSource = Path.GetFullPath(sourcePath);
            if (textures.TryGetValue(fullSource, out string existing))
                return existing;

            string directory = textureDirectory(logicalScene);
            string baseName = safePart(Path.GetFileNameWithoutExtension(fullSource), "TEXTURE");
            string logical = directory + "\\" + baseName + ".DDS";
            int suffix = 2;
            while (usedLogicalPaths.Contains(logical))
                logical = directory + "\\" + baseName + "_" + suffix++ + ".DDS";
            claimAssetPath(logical);

            string destination = physicalPath(logical);
            createParentDirectory(destination);
            if (fullSource.EndsWith(".DDS", StringComparison.OrdinalIgnoreCase))
                File.Copy(fullSource, destination, true);
            else
            {
                bool transparent = DdsTextureConverter.HasTransparency(fullSource);
                DdsTextureConverter.Convert(fullSource, destination, transparent);
                CallBacks.Log($"Converted texture to {(transparent ? "BC5" : "BC1")} DDS {destination}");
            }
            textures.Add(fullSource, logical);
            fileCount++;
            return logical;
        }

        private static string textureDirectory(string logicalScene)
        {
            string normalized = normalizeAssetPath(logicalScene);
            string modelsPrefix = "MODELS\\";
            if (normalized.StartsWith(modelsPrefix, StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(modelsPrefix.Length);
            int separator = normalized.LastIndexOf('\\');
            string relativeDirectory = separator < 0 ? "" : normalized.Substring(0, separator);
            return relativeDirectory.Length == 0 ? "TEXTURES" : "TEXTURES\\" + relativeDirectory;
        }

        private string exportAttachment(GmdlModel model, string logicalScene)
        {
            string fallback = siblingAsset(logicalScene,
                safePart(model.EditableName, "ENTITY") + ".ENTITY.MBIN");
            string logical = claimAssetPath(assetPath(model.AttachmentPath, fallback, "ENTITY"));
            writeSidecar(clone(model.ExportAttachmentTemplate()), logical);
            return logical;
        }

        private void writeSidecar(NMSTemplate template, string logical)
        {
            if (!writtenLogicalPaths.Add(logical))
                return;
            string path = physicalPath(logical);
            createParentDirectory(path);
            writeTemplate(template, path);
            fileCount++;
            CallBacks.Log("Recreated " + template.GetType().Name + " " + path);
        }

        private static T clone<T>(T template) where T : NMSTemplate
        {
            return (T)MXmlFile.ReadTemplateFromString(MXmlFile.WriteTemplate(template));
        }

        private static List<TkSceneNodeAttributeData> attributesFrom(
            IReadOnlyDictionary<string, string> source)
        {
            return source.Select(attribute => new TkSceneNodeAttributeData
            {
                Name = attribute.Key,
                Value = attribute.Value ?? ""
            }).ToList();
        }

        private static void setMeshAttributes(TkSceneNodeData node, GmdlMesh source, GeometryMesh mesh)
        {
            setAttribute(node, "BATCHSTARTPHYSI", mesh.BatchStartPhysics.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "VERTRSTARTPHYSI", mesh.VertexStartPhysics.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "VERTRENDPHYSICS", mesh.VertexEndPhysics.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "BATCHSTARTGRAPH", mesh.BatchStartGraphics.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "BATCHCOUNT", mesh.IndexCount.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "VERTRSTARTGRAPH", mesh.VertexStartGraphics.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "VERTRENDGRAPHIC", mesh.VertexEndGraphics.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "FIRSTSKINMAT", mesh.FirstSkin.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "LASTSKINMAT", mesh.LastSkin.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "LODLEVEL", source.metaData.LODLevel.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "BOUNDHULLST", mesh.BoundHullStart.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "BOUNDHULLED", mesh.BoundHullEnd.ToString(CultureInfo.InvariantCulture));
            setAttribute(node, "AABBMINX", f(mesh.Min.X));
            setAttribute(node, "AABBMINY", f(mesh.Min.Y));
            setAttribute(node, "AABBMINZ", f(mesh.Min.Z));
            setAttribute(node, "AABBMAXX", f(mesh.Max.X));
            setAttribute(node, "AABBMAXY", f(mesh.Max.Y));
            setAttribute(node, "AABBMAXZ", f(mesh.Max.Z));
            setAttribute(node, "HASH", mesh.Hash.ToString(CultureInfo.InvariantCulture));
        }

        private static string f(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static void setAttribute(TkSceneNodeData node, string name, string value)
        {
            TkSceneNodeAttributeData attribute = node.Attributes.FirstOrDefault(a => a.Name?.Value == name);
            if (attribute == null)
            {
                attribute = new TkSceneNodeAttributeData { Name = name };
                node.Attributes.Add(attribute);
            }
            attribute.Value = value ?? "";
        }

        private static void removeAttribute(TkSceneNodeData node, string name)
        {
            node.Attributes.RemoveAll(a => a.Name?.Value == name);
        }

        private string claimAssetPath(string logical)
        {
            usedLogicalPaths.Add(logical);
            return logical;
        }

        private string assetPath(string source, string fallback, string kind)
        {
            string candidate = normalizeAssetPath(source);
            if (candidate == "")
                candidate = normalizeAssetPath(fallback);
            if (candidate == "")
                throw new InvalidDataException("Could not create a local asset path for " + kind);
            return ensureAssetKind(candidate, kind);
        }

        private static string normalizeAssetPath(string value)
        {
            if (String.IsNullOrWhiteSpace(value))
                return "";

            string path = value.Trim().Replace('/', '\\');
            //Resource-manager cache paths can be absolute. Recover the actual NMS asset path when
            //one of the standard roots occurs inside such a path.
            string[] roots = { "MODELS\\", "TEXTURES\\", "METADATA\\", "GLOBALS\\", "LANGUAGE\\" };
            int assetStart = -1;
            foreach (string root in roots)
            {
                int index = path.IndexOf(root, StringComparison.OrdinalIgnoreCase);
                if (index >= 0 && (assetStart < 0 || index < assetStart))
                    assetStart = index;
            }
            if (assetStart >= 0)
                path = path.Substring(assetStart);
            else if (Path.IsPathRooted(path) || path.Contains(':'))
                return "";

            string[] parts = path.Split('\\', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Any(part => part == "." || part == ".." ||
                part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
                return "";
            return String.Join("\\", parts);
        }

        private static string ensureAssetKind(string path, string kind)
        {
            if (path.EndsWith(".MXML", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 5) + ".MBIN";
            if (path.EndsWith(".PC", StringComparison.OrdinalIgnoreCase))
                path = path.Substring(0, path.Length - 3);

            string suffix = "." + kind + ".MBIN";
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return path;
            //Repair names produced by the former flattened exporter, e.g.
            //ANOMALY_MAT.MATERIAL.MBIN.MATERIAL.
            string duplicatedTail = ".MBIN." + kind;
            if (path.EndsWith(duplicatedTail, StringComparison.OrdinalIgnoreCase))
                return path.Substring(0, path.Length - (kind.Length + 1));
            if (path.EndsWith("." + kind, StringComparison.OrdinalIgnoreCase))
                return path + ".MBIN";
            return path + suffix;
        }

        private static string replaceAssetKind(string logical, string oldKind, string newKind)
        {
            string suffix = "." + oldKind + ".MBIN";
            if (!logical.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Expected a " + oldKind + " asset path: " + logical);
            return logical.Substring(0, logical.Length - suffix.Length) + "." + newKind + ".MBIN";
        }

        private static string siblingAsset(string logicalScene, string fileName)
        {
            int separator = logicalScene.LastIndexOf('\\');
            return separator < 0 ? fileName : logicalScene.Substring(0, separator + 1) + fileName;
        }

        private string physicalPath(string logical, bool forceMbin = false)
        {
            string relative = normalizeAssetPath(logical);
            if (relative == "")
                throw new InvalidDataException("Unsafe exported asset path: " + logical);
            if (!forceMbin && !writeMbin && relative.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase))
                relative = relative.Substring(0, relative.Length - 5) + ".MXML";

            string path = Path.GetFullPath(Path.Combine(exportRoot,
                relative.Replace('\\', Path.DirectorySeparatorChar)));
            string prefix = exportRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Export path escapes the selected folder: " + logical);
            return path;
        }

        private static void createParentDirectory(string path)
        {
            string parent = Path.GetDirectoryName(path);
            if (!String.IsNullOrEmpty(parent))
                Directory.CreateDirectory(parent);
        }

        private static string safePart(string value, string fallback)
        {
            string input = String.IsNullOrWhiteSpace(value) ? fallback : value;
            char[] invalid = Path.GetInvalidFileNameChars();
            string safe = new string(input.Select(c => invalid.Contains(c) || c == '\\' || c == '/' ? '_' : c).ToArray()).Trim(' ', '.');
            return safe.Length == 0 ? fallback : safe;
        }

        private static void writeTemplate(NMSTemplate template, string path)
        {
            if (path.EndsWith(".MBIN", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".MBIN.PC", StringComparison.OrdinalIgnoreCase))
                template.WriteToMbin(path);
            else
                template.WriteToMxml(path, false, true);
        }

        private sealed class GeometryMesh
        {
            public ulong Hash;
            public int VertexCount;
            public int IndexCount;
            public int FirstSkin;
            public int LastSkin;
            public int BatchStartPhysics;
            public int VertexStartPhysics;
            public int VertexEndPhysics;
            public int BatchStartGraphics;
            public int VertexStartGraphics;
            public int VertexEndGraphics;
            public int BoundHullStart;
            public int BoundHullEnd;
            public System.Numerics.Vector3 Min;
            public System.Numerics.Vector3 Max;
        }

        private sealed class GeometryBundle
        {
            public NativeGeometryData Data;
            public byte[] Stream;
            private readonly Dictionary<GmdlMesh, GeometryMesh> sourceMeshes = new();

            public bool TryGetMesh(GmdlMesh mesh, out GeometryMesh exported) => sourceMeshes.TryGetValue(mesh, out exported);
            public TkGeometryData CreateTemplate() => buildNativeTemplate(Data);

            public static GeometryBundle Build(GmdlModel root)
            {
                GeometryBundle result = new GeometryBundle();
                if (result.tryPreserveNative(root))
                    return result;

                AssimpGeometryExporter decoder = new AssimpGeometryExporter();
                Assimp.Scene decoded = decoder.Export(root, false, true, false);
                result.pack(root, decoded, decoder);
                return result;
            }

            private bool tryPreserveNative(GmdlModel root)
            {
                GeomObject native = (root as GmdlScene)?.gobject ?? root.parentScene?.gobject;
                List<GmdlMesh> meshes = descendants(root).OfType<GmdlMesh>().ToList();
                if (native?.sourceGeometry == null || native.sourceGeometryStream == null ||
                    meshes.Any(mesh => !ReferenceEquals(mesh.gobject, native) ||
                        !native.meshMetaDataDict.ContainsKey(mesh.metaData.Hash)))
                    return false;
                //One native stream record can be instanced by several scene nodes. It can retain one
                //hash only, so differently renamed instances require the general repacker.
                if (meshes.GroupBy(mesh => mesh.metaData.Hash).Any(group =>
                    group.Select(mesh => mesh.EditableName).Distinct(StringComparer.Ordinal).Count() > 1))
                    return false;

                NativeGeometryData geometry = native.sourceGeometry.Clone();
                Dictionary<ulong, NativeGeometryStreamMetadata> metadataByHash = geometry.Streams
                    .GroupBy(metadata => metadata.Hash).ToDictionary(group => group.Key, group => group.First());
                Dictionary<ulong, string> newHashOwners = new();

                foreach (GmdlMesh mesh in meshes)
                {
                    ulong newHash = GmdlModel.nameHashOf(mesh.EditableName ?? "");
                    if (newHashOwners.TryGetValue(newHash, out string owner) && owner != mesh.EditableName)
                        throw new InvalidDataException($"Mesh names '{owner}' and '{mesh.EditableName}' have the same NMS geometry hash {newHash}.");
                    newHashOwners[newHash] = mesh.EditableName;

                    if (!metadataByHash.TryGetValue(mesh.metaData.Hash, out NativeGeometryStreamMetadata metadata))
                        return false;
                    metadata.Hash = newHash;
                    metadata.Id = mesh.EditableName ?? "";
                    sourceMeshes[mesh] = fromNativeMesh(mesh, newHash);
                }

                Data = geometry;
                Stream = (byte[])native.sourceGeometryStream.Clone();
                CallBacks.Log($"Preserving native geometry layout and {Stream.Length} original stream bytes");
                return true;
            }

            private static TkGeometryData buildNativeTemplate(NativeGeometryData source)
            {
                TkGeometryData result = new TkGeometryData
                {
                    PositionVertexLayout = buildLayout(source.PositionLayout),
                    VertexLayout = buildLayout(source.MainLayout),
                    BoundHullVertEd = new List<int>(source.BoundHullEnds),
                    BoundHullVertSt = new List<int>(source.BoundHullStarts),
                    BoundHullVerts = source.BoundHullVertices.Select(toVector4f).ToList(),
                    IndexBuffer = new List<int>(source.IndexBuffer),
                    JointBindings = source.JointBindings.Select(binding => new TkJointBindingData
                    {
                        InvBindMatrix = copy(binding.InvBindMatrix),
                        BindRotate = copy(binding.BindRotate),
                        BindScale = copy(binding.BindScale),
                        BindTranslate = copy(binding.BindTranslate)
                    }).ToList(),
                    JointExtents = source.JointExtents.Select(extent => new TkJointExtentData
                    {
                        JointExtentCenter = copy(extent.Center),
                        JointExtentMax = copy(extent.Max),
                        JointExtentMin = copy(extent.Min),
                        JointExtentStdDev = copy(extent.StdDev)
                    }).ToList(),
                    JointMirrorAxes = source.JointMirrorAxes.Select(axis => new TkJointMirrorAxis
                    {
                        MirrorAxisMode = axis.Mode,
                        RotAdjustW = axis.RotAdjustW, RotAdjustX = axis.RotAdjustX,
                        RotAdjustY = axis.RotAdjustY, RotAdjustZ = axis.RotAdjustZ,
                        RotMirrorAxisX = axis.RotAxisX, RotMirrorAxisY = axis.RotAxisY,
                        RotMirrorAxisZ = axis.RotAxisZ, TransMirrorAxisX = axis.TransAxisX,
                        TransMirrorAxisY = axis.TransAxisY, TransMirrorAxisZ = axis.TransAxisZ
                    }).ToList(),
                    JointMirrorPairs = new List<int>(source.JointMirrorPairs),
                    MeshAABBMax = source.MeshAABBMax.Select(toVector4f).ToList(),
                    MeshAABBMin = source.MeshAABBMin.Select(toVector4f).ToList(),
                    MeshBaseSkinMat = new List<int>(source.MeshBaseSkinMat),
                    MeshVertREnd = new List<int>(source.MeshVertEnds),
                    MeshVertRStart = new List<int>(source.MeshVertStarts),
                    ProcGenNodeNames = source.ProcGenNodeNames.Select(value => new VariableSizeString(value)).ToList(),
                    ProcGenParentId = new List<int>(source.ProcGenParentIds),
                    SkinMatrixLayout = new List<int>(source.SkinMatrixLayout),
                    StreamMetaDataArray = source.Streams.Select(metadata => new TkMeshMetaData
                    {
                        IdString = metadata.Id,
                        Hash = metadata.Hash,
                        IndexDataOffset = metadata.IndexOffset,
                        IndexDataSize = metadata.IndexSize,
                        VertexDataOffset = metadata.VertexOffset,
                        VertexDataSize = metadata.VertexSize,
                        VertexPositionDataOffset = metadata.PositionOffset,
                        VertexPositionDataSize = metadata.PositionSize,
                        DoubleBufferGeometry = metadata.DoubleBuffered
                    }).ToList(),
                    CollisionIndexCount = source.CollisionIndexCount,
                    IndexCount = source.IndexCount,
                    Indices16Bit = source.Indices16Bit,
                    VertexCount = source.VertexCount
                };
                return result;
            }

            private static TkVertexLayout buildLayout(NativeGeometryVertexLayout source)
            {
                if (source == null)
                    return null;
                return new TkVertexLayout
                {
                    PlatformData = source.PlatformData,
                    ElementCount = source.ElementCount,
                    Stride = source.Stride,
                    VertexElements = source.Elements.Select(element => new TkVertexElement
                    {
                        Instancing = (TkVertexElement.InstancingEnum)element.Instancing,
                        Type = element.Type,
                        Normalise = element.Normalise,
                        Offset = element.Offset,
                        SemanticID = element.SemanticID,
                        Size = element.Size
                    }).ToList()
                };
            }

            private static Vector4f toVector4f(OpenTK.Mathematics.Vector4 value) =>
                new Vector4f { X = value.X, Y = value.Y, Z = value.Z, W = value.W };

            private static float[] copy(float[] values) =>
                values == null ? Array.Empty<float>() : (float[])values.Clone();

            private static GeometryMesh fromNativeMesh(GmdlMesh mesh, ulong hash)
            {
                MeshMetaData data = mesh.metaData;
                return new GeometryMesh
                {
                    Hash = hash,
                    VertexCount = data.vertrend_graphics - data.vertrstart_graphics + 1,
                    IndexCount = data.batchcount,
                    FirstSkin = data.firstskinmat,
                    LastSkin = data.lastskinmat,
                    BatchStartPhysics = data.batchstart_physics,
                    VertexStartPhysics = data.vertrstart_physics,
                    VertexEndPhysics = data.vertrend_physics,
                    BatchStartGraphics = data.batchstart_graphics,
                    VertexStartGraphics = data.vertrstart_graphics,
                    VertexEndGraphics = data.vertrend_graphics,
                    BoundHullStart = data.boundhullstart,
                    BoundHullEnd = data.boundhullend,
                    Min = new System.Numerics.Vector3(data.AABBMIN.X, data.AABBMIN.Y, data.AABBMIN.Z),
                    Max = new System.Numerics.Vector3(data.AABBMAX.X, data.AABBMAX.Y, data.AABBMAX.Z)
                };
            }

            private void pack(GmdlModel root, Assimp.Scene decoded, AssimpGeometryExporter decoder)
            {
                bool skinned = decoded.Meshes.Any(m => m.HasBones);
                bool indices16Bit = decoded.Meshes
                    .SelectMany(mesh => mesh.Faces)
                    .SelectMany(face => face.Indices)
                    .All(index => index >= 0 && index <= ushort.MaxValue);
                //Match the native NMS PC layouts: half4 position + half4 UV in the position
                //stream, then color/blend data and the packed normal/tangent frame in the main stream.
                int mainStride = skinned ? 20 : 8;
                NativeGeometryData geometry = createGeometry(mainStride, skinned, indices16Bit);
                MemoryStream stream = new MemoryStream();
                Dictionary<int, GeometryMesh> packed = new();
                Dictionary<ulong, string> hashOwners = new();
                GmdlScene owningScene = root.parentScene ?? root as GmdlScene;
                Dictionary<string, int> jointSlots = jointSlotsFor(owningScene);
                if (skinned)
                    populateJoints(owningScene, geometry);

                foreach (GmdlMesh source in descendants(root).OfType<GmdlMesh>())
                {
                    if (!decoder.TryGetMeshIndex(source, out int meshIndex))
                        continue;
                    if (!packed.TryGetValue(meshIndex, out GeometryMesh info))
                    {
                        info = packMesh(decoded.Meshes[meshIndex], geometry, stream, mainStride,
                            skinned, indices16Bit, jointSlots, hashOwners);
                        packed.Add(meshIndex, info);
                    }
                    sourceMeshes[source] = info;
                }

                copyCollisionData(root.parentScene?.gobject ?? (root as Scene)?.gobject, geometry);
                geometry.VertexCount = packed.Values.Sum(m => m.VertexCount);
                geometry.IndexCount = packed.Values.Sum(m => m.IndexCount);
                geometry.CollisionIndexCount = geometry.IndexBuffer.Count;
                Data = geometry;
                Stream = buildGeometryStreamMbin(geometry, stream.ToArray());
            }

            private static byte[] buildGeometryStreamMbin(NativeGeometryData geometry, byte[] packedStream)
            {
                TkGeometryStreamData streamTemplate = new TkGeometryStreamData
                {
                    StreamDataArray = new List<TkMeshData>()
                };
                List<byte[]> meshStreams = new List<byte[]>();
                List<byte[]> positionStreams = new List<byte[]>();

                foreach (NativeGeometryStreamMetadata metadata in geometry.Streams)
                {
                    byte[] meshStream = new byte[checked(metadata.VertexSize + metadata.IndexSize)];
                    byte[] positionStream = new byte[metadata.PositionSize];
                    copyStreamSection(packedStream, metadata.VertexOffset, meshStream, 0,
                        metadata.VertexSize, metadata.Id, "vertex");
                    copyStreamSection(packedStream, checked(metadata.VertexOffset + metadata.IndexOffset),
                        meshStream, metadata.VertexSize, metadata.IndexSize, metadata.Id, "index");
                    copyStreamSection(packedStream, metadata.PositionOffset, positionStream, 0,
                        metadata.PositionSize, metadata.Id, "position");

                    streamTemplate.StreamDataArray.Add(new TkMeshData
                    {
                        IdString = metadata.Id,
                        Hash = metadata.Hash,
                        MeshDataStream = meshStream,
                        MeshPositionDataStream = positionStream,
                        IndexDataSize = metadata.IndexSize,
                        VertexDataSize = metadata.VertexSize,
                        VertexPositionDataSize = metadata.PositionSize
                    });
                    meshStreams.Add(meshStream);
                    positionStreams.Add(positionStream);
                }

                byte[] serialized = serializeMbin(streamTemplate);
                correctPositionStreamLengths(serialized, meshStreams, positionStreams);
                int searchOffset = 32; //Skip the libMBIN header before locating serialized buffers.
                for (int i = 0; i < geometry.Streams.Count; i++)
                {
                    NativeGeometryStreamMetadata metadata = geometry.Streams[i];
                    metadata.VertexOffset = findStream(serialized, meshStreams[i], searchOffset,
                        metadata.Id, "vertex/index");
                    metadata.IndexOffset = metadata.VertexSize;
                    searchOffset = metadata.VertexOffset + meshStreams[i].Length;
                    metadata.PositionOffset = findStream(serialized, positionStreams[i], searchOffset,
                        metadata.Id, "position");
                    searchOffset = metadata.PositionOffset + positionStreams[i].Length;
                }
                return serialized;
            }

            private static void correctPositionStreamLengths(byte[] serialized,
                IReadOnlyList<byte[]> meshStreams, IReadOnlyList<byte[]> positionStreams)
            {
                // libMBIN currently writes MeshDataStream.Length into the variable-size descriptor
                // for MeshPositionDataStream on multi-entry TkGeometryStreamData templates. The
                // pointer and payload are correct, but the oversized descriptor makes each position
                // stream overlap subsequent records. TkMeshData's fixed serialized size is 72 bytes;
                // its position-stream descriptor starts at +32 and stores its length at +40.
                const int mbinHeaderSize = 32;
                const int meshDataSize = 72;
                const int positionLengthOffset = 40;

                if (serialized.Length < mbinHeaderSize + 16)
                    throw new InvalidDataException("Serialized geometry stream is missing its root descriptor.");

                long relativeArrayOffset = BitConverter.ToInt64(serialized, mbinHeaderSize);
                int entryCount = BitConverter.ToInt32(serialized, mbinHeaderSize + 8);
                long arrayOffset64 = mbinHeaderSize + relativeArrayOffset;
                if (entryCount != meshStreams.Count || entryCount != positionStreams.Count ||
                    arrayOffset64 < 0 || arrayOffset64 > serialized.Length)
                    throw new InvalidDataException("Serialized geometry stream descriptor does not match its mesh entries.");

                int arrayOffset = checked((int)arrayOffset64);
                for (int i = 0; i < entryCount; i++)
                {
                    int lengthOffset = checked(arrayOffset + i * meshDataSize + positionLengthOffset);
                    if (lengthOffset < 0 || lengthOffset > serialized.Length - sizeof(int))
                        throw new InvalidDataException("Serialized geometry position descriptor is out of range.");

                    int serializedLength = BitConverter.ToInt32(serialized, lengthOffset);
                    if (serializedLength != meshStreams[i].Length &&
                        serializedLength != positionStreams[i].Length)
                        throw new InvalidDataException("Unexpected serialized geometry position-stream length.");

                    byte[] corrected = BitConverter.GetBytes(positionStreams[i].Length);
                    System.Buffer.BlockCopy(corrected, 0, serialized, lengthOffset, corrected.Length);
                }
            }

            private static void copyStreamSection(byte[] source, int sourceOffset, byte[] destination,
                int destinationOffset, int count, string mesh, string section)
            {
                if (sourceOffset < 0 || count < 0 || sourceOffset > source.Length - count)
                    throw new InvalidDataException($"Invalid {section} stream range for mesh '{mesh}'.");
                System.Buffer.BlockCopy(source, sourceOffset, destination, destinationOffset, count);
            }

            private static byte[] serializeMbin(NMSTemplate template)
            {
                string path = Path.Combine(Path.GetTempPath(), "NMSMV-" + Guid.NewGuid().ToString("N") + ".MBIN.PC");
                try
                {
                    template.WriteToMbin(path);
                    return File.ReadAllBytes(path);
                }
                finally
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }

            private static int findStream(byte[] serialized, byte[] stream, int start,
                string mesh, string section)
            {
                if (stream.Length == 0)
                    return 0;
                int relative = serialized.AsSpan(start).IndexOf(stream);
                if (relative < 0)
                    throw new InvalidDataException($"Could not locate serialized {section} stream for mesh '{mesh}'.");
                return checked(start + relative);
            }

            private static GeometryMesh packMesh(Assimp.Mesh mesh, NativeGeometryData geometry,
                MemoryStream stream, int mainStride, bool skinned, bool indices16Bit,
                Dictionary<string, int> jointSlots, Dictionary<ulong, string> hashOwners)
            {
                string meshName = String.IsNullOrWhiteSpace(mesh.Name) ? "MESH" : mesh.Name;
                //NMS geometry stream metadata is keyed by the same 32-bit Jenkins hash stored as
                //the scene node NameHash. It is a hash of the upper-case UTF-8 mesh name, not of the
                //vertex/index bytes. The value is held in a UInt64 field but only the low 32 bits are used.
                ulong hash = GmdlModel.nameHashOf(meshName);
                if (hashOwners.TryGetValue(hash, out string owner) && owner != meshName)
                    throw new InvalidDataException($"Mesh names '{owner}' and '{meshName}' have the same NMS geometry hash {hash}.");
                hashOwners[hash] = meshName;
                int positionOffset = checked((int)stream.Position);
                using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        var p = mesh.Vertices[i];
                        var uv = mesh.HasTextureCoords(0) ? mesh.TextureCoordinateChannels[0][i] : default;
                        writeHalf(writer, p.X); writeHalf(writer, p.Y); writeHalf(writer, p.Z); writeHalf(writer, 1.0f);
                        writeHalf(writer, uv.X); writeHalf(writer, uv.Y); writeHalf(writer, uv.Z); writeHalf(writer, 0.0f);
                    }
                }

                int vertexOffset = checked((int)stream.Position);
                uint[,] boneIndices = new uint[mesh.VertexCount, 4];
                float[,] boneWeights = new float[mesh.VertexCount, 4];
                if (skinned)
                    collectWeights(mesh, jointSlots, boneIndices, boneWeights);

                System.Numerics.Vector3 min = new(float.MaxValue), max = new(float.MinValue);
                using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                {
                    for (int i = 0; i < mesh.VertexCount; i++)
                    {
                        var p = mesh.Vertices[i];
                        min = System.Numerics.Vector3.Min(min, p);
                        max = System.Numerics.Vector3.Max(max, p);
                        var n = mesh.HasNormals ? mesh.Normals[i] : new System.Numerics.Vector3(0, 1, 0);
                        var t = mesh.HasTangentBasis ? mesh.Tangents[i] : tangentFor(n);
                        float tangentSign = 1.0f;
                        if (mesh.HasTangentBasis && mesh.BiTangents.Count > i)
                        {
                            System.Numerics.Vector3 reconstructed = System.Numerics.Vector3.Cross(n, t);
                            if (System.Numerics.Vector3.Dot(reconstructed, mesh.BiTangents[i]) < 0.0f)
                                tangentSign = -1.0f;
                        }
                        writer.Write(uint.MaxValue); //semantic 4: default white vertex colour
                        if (skinned)
                        {
                            for (int j = 0; j < 4; j++)
                            {
                                if (boneIndices[i, j] > byte.MaxValue)
                                    throw new InvalidDataException("A skin joint index exceeds the native byte layout.");
                                writer.Write((byte)boneIndices[i, j]);
                            }
                            for (int j = 0; j < 4; j++) writeHalf(writer, boneWeights[i, j]);
                        }
                        writer.Write(packFrame(n, t, tangentSign));
                    }
                }

                int indexOffset = checked((int)stream.Position);
                int indexCount = 0;
                using (BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
                    foreach (Assimp.Face face in mesh.Faces)
                        foreach (int index in face.Indices)
                        {
                            if (indices16Bit)
                                writer.Write((ushort)index);
                            else
                                writer.Write((uint)index);
                            indexCount++;
                        }

                NativeGeometryStreamMetadata metadata = new NativeGeometryStreamMetadata
                {
                    Id = meshName,
                    Hash = hash,
                    PositionOffset = positionOffset,
                    PositionSize = mesh.VertexCount * 16,
                    VertexOffset = vertexOffset,
                    VertexSize = mesh.VertexCount * mainStride,
                    IndexOffset = indexOffset - vertexOffset,
                    IndexSize = indexCount * (indices16Bit ? sizeof(ushort) : sizeof(uint)),
                    DoubleBuffered = false
                };
                geometry.Streams.Add(metadata);
                geometry.MeshVertStarts.Add(0);
                geometry.MeshVertEnds.Add(Math.Max(0, mesh.VertexCount - 1));
                geometry.MeshAABBMin.Add(vec4(min));
                geometry.MeshAABBMax.Add(vec4(max));
                if (skinned)
                    geometry.MeshBaseSkinMat.Add(0);

                return new GeometryMesh
                {
                    Hash = hash, VertexCount = mesh.VertexCount, IndexCount = indexCount,
                    FirstSkin = 0, LastSkin = skinned ? jointSlots.Count : 0,
                    VertexStartGraphics = 0, VertexEndGraphics = mesh.VertexCount - 1,
                    Min = min, Max = max
                };
            }

            private static NativeGeometryData createGeometry(int mainStride, bool skinned,
                bool indices16Bit)
            {
                NativeGeometryData result = new NativeGeometryData
                {
                    PositionLayout = layout(16,
                        element(0, 0, 4, VertexAttribPointerType.HalfFloat),
                        element(1, 8, 4, VertexAttribPointerType.HalfFloat)),
                    MainLayout = skinned
                        ? layout(mainStride,
                            element(4, 0, 4, VertexAttribPointerType.UnsignedByte),
                            element(5, 4, 4, VertexAttribPointerType.UnsignedByte),
                            element(6, 8, 4, VertexAttribPointerType.HalfFloat),
                            element(11, 16, 1, VertexAttribPointerType.UnsignedInt))
                        : layout(mainStride,
                            element(4, 0, 4, VertexAttribPointerType.UnsignedByte),
                            element(11, 4, 1, VertexAttribPointerType.UnsignedInt)),
                    Indices16Bit = indices16Bit ? 1 : 0
                };
                return result;
            }

            private static void writeHalf(BinaryWriter writer, float value)
            {
                if (!float.IsFinite(value) || Math.Abs(value) > 65504.0f)
                    throw new InvalidDataException($"Vertex value {value} cannot be represented as a native half float.");
                writer.Write(BitConverter.HalfToUInt16Bits((System.Half)value));
            }

            private static uint packFrame(System.Numerics.Vector3 normal,
                System.Numerics.Vector3 tangent, float bitangentSign)
            {
                normal = safeNormal(normal, System.Numerics.Vector3.UnitY);
                tangent -= System.Numerics.Vector3.Dot(tangent, normal) * normal;
                tangent = safeNormal(tangent, tangentFor(normal));

                float denominator = Math.Abs(normal.X) + Math.Abs(normal.Y) + Math.Abs(normal.Z);
                System.Numerics.Vector2 oct = new(normal.X / denominator, normal.Y / denominator);
                if (normal.Z < 0.0f)
                {
                    float oldX = oct.X;
                    oct.X = (1.0f - Math.Abs(oct.Y)) * (oldX >= 0.0f ? 1.0f : -1.0f);
                    oct.Y = (1.0f - Math.Abs(oldX)) * (oct.Y >= 0.0f ? 1.0f : -1.0f);
                }
                uint packedX = (uint)Math.Clamp((int)Math.Round((oct.X * 0.5f + 0.5f) * 1023.0f), 0, 1023);
                uint packedY = (uint)Math.Clamp((int)Math.Round((oct.Y * 0.5f + 0.5f) * 1023.0f), 0, 1023);

                System.Numerics.Vector3 first = Math.Abs(normal.Z) > Math.Abs(normal.Y)
                    ? safeNormal(new System.Numerics.Vector3(normal.Z, 0.0f, -normal.X), System.Numerics.Vector3.UnitX)
                    : safeNormal(new System.Numerics.Vector3(normal.Y, -normal.X, 0.0f), System.Numerics.Vector3.UnitX);
                System.Numerics.Vector3 second = System.Numerics.Vector3.Cross(normal, first);
                float u = System.Numerics.Vector3.Dot(tangent, first);
                float v = System.Numerics.Vector3.Dot(tangent, second);
                bool uMajor = Math.Abs(u) >= Math.Abs(v);
                float angle = uMajor ? (float)Math.Atan2(Math.Abs(v), Math.Abs(u))
                    : (float)Math.Atan2(Math.Abs(u), Math.Abs(v));
                uint packedAngle = (uint)Math.Clamp((int)Math.Round(angle / ((float)Math.PI / 4.0f) * 256.0f), 0, 255);

                uint result = packedX | (packedY << 10) | (packedAngle << 20);
                if (v < 0.0f) result |= 1u << 28;
                if (u >= 0.0f) result |= 1u << 29;
                if (uMajor) result |= 1u << 30;
                if (bitangentSign < 0.0f) result |= 1u << 31;
                return result;
            }

            private static System.Numerics.Vector3 safeNormal(System.Numerics.Vector3 value,
                System.Numerics.Vector3 fallback)
            {
                float lengthSquared = value.LengthSquared();
                return float.IsFinite(lengthSquared) && lengthSquared > 1e-12f
                    ? value / (float)Math.Sqrt(lengthSquared) : fallback;
            }

            private static NativeGeometryVertexLayout layout(int stride, params NativeGeometryVertexElement[] elements) => new NativeGeometryVertexLayout
            {
                Elements = elements.ToList(), ElementCount = elements.Length, Stride = stride, PlatformData = 0
            };

            private static NativeGeometryVertexElement element(byte semantic, byte offset, byte size, VertexAttribPointerType type) =>
                new NativeGeometryVertexElement
                {
                    SemanticID = semantic, Offset = offset, Size = size, Type = (int)type,
                    Normalise = 0, Instancing = (int)TkVertexElement.InstancingEnum.PerVertex
                };

            private static void collectWeights(Assimp.Mesh mesh, Dictionary<string, int> slots,
                uint[,] indices, float[,] weights)
            {
                foreach (Assimp.Bone bone in mesh.Bones)
                {
                    if (!slots.TryGetValue(bone.Name, out int slot))
                        continue;
                    foreach (Assimp.VertexWeight weight in bone.VertexWeights)
                    {
                        int vertex = weight.VertexID;
                        int target = 0;
                        while (target < 4 && weights[vertex, target] > 0) target++;
                        if (target == 4) continue;
                        indices[vertex, target] = (uint)slot;
                        weights[vertex, target] = weight.Weight;
                    }
                }
            }

            private static Dictionary<string, int> jointSlotsFor(GmdlScene scene)
            {
                Dictionary<string, int> result = new(StringComparer.Ordinal);
                if (scene == null) return result;
                foreach (Joint joint in scene.jointDict.Values.OrderBy(j => j.jointIndex))
                    if (!result.ContainsKey(joint.Name)) result.Add(joint.Name, result.Count);
                return result;
            }

            private static void populateJoints(GmdlScene scene, NativeGeometryData geometry)
            {
                if (scene == null)
                    return;

                foreach (Joint joint in scene.jointDict.Values.OrderBy(j => j.jointIndex))
                {
                    geometry.SkinMatrixLayout.Add(joint.jointIndex);
                    float[] inverse = new float[16];
                    MVCore.Utils.MathUtils.insertMatToArray16(inverse, 0, joint.invBMat);
                    var rotation = joint.BindMat.ExtractRotation();
                    var scale = joint.BindMat.ExtractScale();
                    var translation = joint.BindMat.ExtractTranslation();
                    geometry.JointBindings.Add(new NativeGeometryJointBinding
                    {
                        InvBindMatrix = inverse,
                        BindRotate = new[] { rotation.X, rotation.Y, rotation.Z, rotation.W },
                        BindScale = new[] { scale.X, scale.Y, scale.Z },
                        BindTranslate = new[] { translation.X, translation.Y, translation.Z }
                    });
                }
            }

            private static void copyCollisionData(GeomObject source, NativeGeometryData target)
            {
                if (source == null) return;
                target.BoundHullStarts.AddRange(source.bhullstarts);
                target.BoundHullEnds.AddRange(source.bhullends);
                target.BoundHullVertices.AddRange(source.bhullverts.Select(v => new OpenTK.Mathematics.Vector4(v.X, v.Y, v.Z, 1)));
                if (source.ibuffer == null) return;
                int size = source.indicesLength > 0 ? source.indicesLength : 4;
                for (int offset = 0; offset + size <= source.ibuffer.Length; offset += size)
                    target.IndexBuffer.Add(size == 2 ? BitConverter.ToUInt16(source.ibuffer, offset) :
                        checked((int)BitConverter.ToUInt32(source.ibuffer, offset)));
            }

            private static IEnumerable<GmdlModel> descendants(GmdlModel root)
            {
                yield return root;
                foreach (GmdlModel child in root.EmbeddedSceneChildren)
                    foreach (GmdlModel nested in descendants(child))
                        yield return nested;
            }

            private static OpenTK.Mathematics.Vector4 vec4(System.Numerics.Vector3 value) =>
                new OpenTK.Mathematics.Vector4(value.X, value.Y, value.Z, 1);

            private static System.Numerics.Vector3 tangentFor(System.Numerics.Vector3 normal)
            {
                System.Numerics.Vector3 axis = Math.Abs(normal.Y) < 0.9f
                    ? System.Numerics.Vector3.UnitY : System.Numerics.Vector3.UnitX;
                return System.Numerics.Vector3.Normalize(System.Numerics.Vector3.Cross(axis, normal));
            }
        }
    }
}
