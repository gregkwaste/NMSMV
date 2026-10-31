using MVCore;
using MVCore.Common;
using MVCore.GMDL;
using MVCore.Utils;
using OpenTK.Graphics.OpenGL4;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace WPFModelViewer
{
    //Builds an Assimp scene from the CPU-side geometry buffers, including split position/main streams.
    internal sealed class AssimpGeometryExporter
    {
        private readonly Assimp.Scene scene = new Assimp.Scene();
        private readonly Dictionary<(GeomObject Geometry, ulong Hash, Material Material,
            float Red, float Green, float Blue), int> meshes =
            new Dictionary<(GeomObject, ulong, Material, float, float, float), int>();
        private readonly HashSet<string> visibleNodeNames = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<AnimData> exportedAnimations = new HashSet<AnimData>();
        private readonly HashSet<string> exportedAnimationNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<Mesh, int> sourceMeshIndices = new Dictionary<Mesh, int>();
        private bool currentFileOnly;
        private bool onlyRenderable;

        public Assimp.Scene Export(Model root, bool includeAnimations, bool currentFileOnly = false,
            bool onlyRenderable = true)
        {
            this.currentFileOnly = currentFileOnly;
            this.onlyRenderable = onlyRenderable;
            collectVisibleNodeNames(root);
            scene.RootNode = exportNode(root);
            if (includeAnimations)
            {
                //Animation components are commonly attached to the owning entity/scene rather than the
                //selected geometry node. Search that owner, then retain only channels for this subtree.
                exportAnimations(root.parentScene ?? root);
                if (scene.AnimationCount == 0)
                    CallBacks.Log("Animation export was requested, but no animation channels drive checked nodes " +
                        "in the selected subtree");
            }
            return scene;
        }

        private void collectVisibleNodeNames(Model model)
        {
            if (onlyRenderable && !model.IsRenderable)
                return;
            visibleNodeNames.Add(model.Name);
            foreach (Model child in model.Children)
                if (includeChild(child))
                    collectVisibleNodeNames(child);
        }

        private bool includeChild(Model child)
        {
            return (!currentFileOnly || child.SourceFile == "") &&
                   (!onlyRenderable || child.IsRenderable);
        }

        private Assimp.Node exportNode(Model model)
        {
            Assimp.Node node = new Assimp.Node(model.Name)
            {
                Transform = MathUtils.convertMatrix(model.localMat)
            };

            if (model is Mesh mesh && tryExportMesh(mesh, out int meshIndex))
                node.MeshIndices.Add(meshIndex);

            foreach (Model child in model.Children)
                if (includeChild(child))
                    node.Children.Add(exportNode(child));
            return node;
        }

        public bool TryGetMeshIndex(Mesh source, out int meshIndex)
        {
            return sourceMeshIndices.TryGetValue(source, out meshIndex);
        }

        private bool tryExportMesh(Mesh source, out int meshIndex)
        {
            meshIndex = -1;
            if (source.gobject == null || source.meshVao == null ||
                !tryGetMeshData(source, out geomMeshData data))
                return false;

            var key = (source.gobject, source.metaData.Hash, source.material,
                source.color.X, source.color.Y, source.color.Z);
            if (meshes.TryGetValue(key, out meshIndex))
            {
                sourceMeshIndices[source] = meshIndex;
                return true;
            }

            int vertexCount = source.metaData.vertrend_graphics - source.metaData.vertrstart_graphics + 1;
            if (vertexCount <= 0 || source.gobject.bufInfo.Count == 0 || source.gobject.bufInfo[0] == null)
                return false;

            Assimp.Mesh result = new Assimp.Mesh(source.Name, Assimp.PrimitiveType.Triangle);
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                System.Numerics.Vector3 position = readVector3(source.gobject, data, source.gobject.bufInfo[0], vertex);
                if (!isFinite(position))
                    throw new InvalidDataException($"Mesh {source.Name} contains a non-finite vertex position.");
                result.Vertices.Add(position);

                bool packedFrame = source.gobject.packedFrameOffset >= 0;
                System.Numerics.Vector3 packedNormal = default;
                System.Numerics.Vector3 packedTangent = default;
                float packedSign = 1.0f;
                if (packedFrame)
                    readPackedFrame(source.gobject, data, vertex, out packedNormal, out packedTangent, out packedSign);

                if (source.gobject.bufInfo.Count > 2 && source.gobject.bufInfo[2] != null)
                    result.Normals.Add(readVector3(source.gobject, data, source.gobject.bufInfo[2], vertex));
                else if (packedFrame)
                    result.Normals.Add(packedNormal);
                else if (data.n_buffer != null && data.n_buffer.Length >= (vertex + 1) * 3 * sizeof(float))
                    result.Normals.Add(readGeneratedNormal(data.n_buffer, vertex));

                if (source.gobject.bufInfo.Count > 1 && source.gobject.bufInfo[1] != null)
                {
                    if (result.TextureCoordinateChannelCount == 0)
                    {
                        result.TextureCoordinateChannels[0] = new List<System.Numerics.Vector3>();
                        result.UVComponentCount[0] = 2;
                    }
                    result.TextureCoordinateChannels[0].Add(
                        readVector3(source.gobject, data, source.gobject.bufInfo[1], vertex, 2));
                }

                if (source.gobject.bufInfo.Count > 3 && source.gobject.bufInfo[3] != null)
                {
                    bufInfo tangentInfo = source.gobject.bufInfo[3];
                    System.Numerics.Vector3 tangent = readVector3(source.gobject, data, tangentInfo, vertex);
                    result.Tangents.Add(tangent);
                    System.Numerics.Vector3 normal = result.Normals.Count > vertex
                        ? result.Normals[vertex] : new System.Numerics.Vector3(0, 0, 1);
                    float sign = readTangentSign(source.gobject, data, tangentInfo, vertex);
                    result.BiTangents.Add(cross(normal, tangent) * (sign == 0 ? 1.0f : sign));
                }
                else if (packedFrame)
                {
                    result.Tangents.Add(packedTangent);
                    result.BiTangents.Add(cross(packedNormal, packedTangent) * packedSign);
                }
            }

            addFaces(source, data.is_buffer, result, vertexCount);
            if (result.FaceCount == 0)
                return false;

            //Assimp requires optional per-vertex channels to be either absent or complete.
            if (result.Normals.Count != 0 && result.Normals.Count != vertexCount)
                result.Normals.Clear();
            if (result.Normals.Count != vertexCount || result.Tangents.Count != vertexCount ||
                result.BiTangents.Count != vertexCount)
            {
                result.Tangents.Clear();
                result.BiTangents.Clear();
            }
            addBones(source, data, result, vertexCount);

            meshIndex = scene.MeshCount;
            scene.Meshes.Add(result);
            meshes.Add(key, meshIndex);
            sourceMeshIndices[source] = meshIndex;
            return true;
        }

        //Assimp imports use one interleaved CPU buffer and have not yet been packed into the split
        //NMS geometry streams. Present that buffer through the same reader used for native meshes.
        private static bool tryGetMeshData(Mesh source, out geomMeshData data)
        {
            if (source.gobject.meshDataDict.TryGetValue(source.metaData.Hash, out data) &&
                data?.vp_buffer != null && data.vs_buffer != null && data.is_buffer != null)
                return true;

            if (source.gobject.vbuffer == null || source.gobject.ibuffer == null)
            {
                data = null;
                return false;
            }

            data = new geomMeshData
            {
                hash = source.metaData.Hash,
                vp_buffer = source.gobject.vbuffer,
                vs_buffer = source.gobject.vbuffer,
                is_buffer = source.gobject.ibuffer,
                n_buffer = source.gobject.nbuffer
            };
            return true;
        }

        private static void addFaces(Mesh source, byte[] indices, Assimp.Mesh target, int vertexCount)
        {
            int indexSize = source.metaData.indicesLength == DrawElementsType.UnsignedInt ? 4 : 2;
            int indexCount = Math.Min(source.metaData.batchcount, indices.Length / indexSize);
            int baseVertex = source.type == MVCore.TYPES.COLLISION
                ? source.metaData.vertrstart_physics : source.metaData.vertrstart_graphics;

            using BinaryReader reader = new BinaryReader(new MemoryStream(indices, false));
            for (int i = 0; i + 2 < indexCount; i += 3)
            {
                int a = readIndex(reader, indexSize) - baseVertex;
                int b = readIndex(reader, indexSize) - baseVertex;
                int c = readIndex(reader, indexSize) - baseVertex;
                if ((uint)a >= vertexCount || (uint)b >= vertexCount || (uint)c >= vertexCount)
                    throw new InvalidDataException($"Mesh {source.Name} contains an index outside its vertex range.");

                Assimp.Face face = new Assimp.Face();
                face.Indices.Add(a);
                face.Indices.Add(b);
                face.Indices.Add(c);
                target.Faces.Add(face);
            }
        }

        private static int readIndex(BinaryReader reader, int size)
        {
            return size == 4 ? checked((int)reader.ReadUInt32()) : reader.ReadUInt16();
        }

        private static System.Numerics.Vector3 readVector3(GeomObject geometry, geomMeshData data, bufInfo info,
            int vertex, int componentLimit = 3)
        {
            byte[] buffer = info.stream == 0 ? data.vp_buffer : data.vs_buffer;
            int defaultStride = info.stream == 0 ? geometry.vp_size : geometry.vx_size;
            int stride = info.stride > 0 ? info.stride : defaultStride;
            int offset = checked(vertex * stride + info.offset);
            if (offset < 0 || offset >= buffer.Length)
                throw new EndOfStreamException("Vertex attribute offset is outside the geometry buffer.");

            if (info.type == VertexAttribPointerType.Int2101010Rev)
            {
                if (offset + sizeof(uint) > buffer.Length)
                    throw new EndOfStreamException("Packed vertex attribute is truncated.");
                uint packed = BitConverter.ToUInt32(buffer, offset);
                float x = _2sComplement.toInt(packed & 0x3ff, 10) / 511.0f;
                float y = _2sComplement.toInt((packed >> 10) & 0x3ff, 10) / 511.0f;
                float z = _2sComplement.toInt((packed >> 20) & 0x3ff, 10) / 511.0f;
                return normalized(new System.Numerics.Vector3(x, y, z));
            }

            int components = Math.Min(Math.Min(info.count, componentLimit), 3);
            float[] values = new float[3];
            int componentSize = info.type == VertexAttribPointerType.Float ? sizeof(float) : sizeof(ushort);
            if (offset + components * componentSize > buffer.Length)
                throw new EndOfStreamException("Vertex attribute is truncated.");

            for (int i = 0; i < components; i++)
            {
                values[i] = info.type switch
                {
                    VertexAttribPointerType.Float => BitConverter.ToSingle(buffer, offset + i * sizeof(float)),
                    VertexAttribPointerType.HalfFloat => MVCore.Utils.Half.decompress(
                        BitConverter.ToUInt16(buffer, offset + i * sizeof(ushort))),
                    _ => throw new NotSupportedException("Unsupported Assimp vertex attribute type " + info.type)
                };
            }
            return new System.Numerics.Vector3(values[0], values[1], values[2]);
        }

        private static System.Numerics.Vector3 readGeneratedNormal(byte[] buffer, int vertex)
        {
            int offset = vertex * 3 * sizeof(float);
            return new System.Numerics.Vector3(BitConverter.ToSingle(buffer, offset),
                BitConverter.ToSingle(buffer, offset + sizeof(float)),
                BitConverter.ToSingle(buffer, offset + 2 * sizeof(float)));
        }

        private static System.Numerics.Vector3 normalized(System.Numerics.Vector3 value)
        {
            float length = (float)Math.Sqrt(value.X * value.X + value.Y * value.Y + value.Z * value.Z);
            return length > 0 ? new System.Numerics.Vector3(value.X / length, value.Y / length, value.Z / length) : value;
        }

        private static System.Numerics.Vector3 cross(System.Numerics.Vector3 a, System.Numerics.Vector3 b)
        {
            return normalized(new System.Numerics.Vector3(a.Y * b.Z - a.Z * b.Y,
                a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X));
        }

        //Decode NMS semantic 11 exactly like Simple_VS.glsl. The packed value contains the
        //octahedral normal, tangent angle and bitangent handedness; losing it makes a mixed
        //native/Assimp export relight every original normal-mapped mesh incorrectly.
        private static void readPackedFrame(GeomObject geometry, geomMeshData data, int vertex,
            out System.Numerics.Vector3 normal, out System.Numerics.Vector3 tangent, out float sign)
        {
            byte[] buffer = geometry.packedFrameStream == 0 ? data.vp_buffer : data.vs_buffer;
            int stride = geometry.packedFrameStream == 0 ? geometry.vp_size : geometry.vx_size;
            int offset = checked(vertex * stride + geometry.packedFrameOffset);
            if (offset < 0 || offset + sizeof(uint) > buffer.Length)
                throw new EndOfStreamException("Packed normal/tangent frame is truncated.");

            uint packed = BitConverter.ToUInt32(buffer, offset);
            float x = (packed & 1023u) / 1023.0f * 2.0f - 1.0f;
            float y = ((packed >> 10) & 1023u) / 1023.0f * 2.0f - 1.0f;
            normal = new System.Numerics.Vector3(x, y, 1.0f - Math.Abs(x) - Math.Abs(y));
            if (normal.Z < 0.0f)
            {
                float oldX = normal.X;
                float oldY = normal.Y;
                normal.X = (1.0f - Math.Abs(oldY)) * (oldX >= 0.0f ? 1.0f : -1.0f);
                normal.Y = (1.0f - Math.Abs(oldX)) * (oldY >= 0.0f ? 1.0f : -1.0f);
            }
            normal = normalized(normal);

            float angle = ((packed >> 20) & 255u) / 256.0f * 0.78539816f;
            float major = (float)Math.Cos(angle);
            float minor = (float)Math.Sin(angle);
            bool uMajor = ((packed >> 30) & 1u) != 0;
            float u = (uMajor ? major : minor) * (((packed >> 29) & 1u) != 0 ? 1.0f : -1.0f);
            float v = (uMajor ? minor : major) * (((packed >> 28) & 1u) != 0 ? -1.0f : 1.0f);
            System.Numerics.Vector3 first = Math.Abs(normal.Z) > Math.Abs(normal.Y)
                ? normalized(new System.Numerics.Vector3(normal.Z, 0.0f, -normal.X))
                : normalized(new System.Numerics.Vector3(normal.Y, -normal.X, 0.0f));
            System.Numerics.Vector3 second = cross(normal, first);
            tangent = normalized(u * first + v * second);
            sign = (packed & 0x80000000u) != 0 ? -1.0f : 1.0f;
        }

        private void addBones(Mesh source, geomMeshData data, Assimp.Mesh target, int vertexCount)
        {
            if (!source.Skinned || source.meshVao.BoneRemapIndices == null ||
                source.gobject.bufInfo.Count <= 6 || source.gobject.bufInfo[5] == null ||
                source.gobject.bufInfo[6] == null)
                return;

            Dictionary<int, Assimp.Bone> bones = new Dictionary<int, Assimp.Bone>();
            for (int local = 0; local < source.meshVao.BoneRemapIndicesCount; local++)
            {
                int jointIndex = source.meshVao.BoneRemapIndices[local];
                Joint joint = null;
                if (source.parentScene != null)
                {
                    foreach (Joint candidate in source.parentScene.jointDict.Values)
                        if (candidate.jointIndex == jointIndex) { joint = candidate; break; }
                }
                if (joint == null)
                    continue;
                if (!visibleNodeNames.Contains(joint.Name))
                    continue;

                Assimp.Bone bone = new Assimp.Bone
                {
                    Name = joint.Name,
                    OffsetMatrix = MathUtils.convertMatrix(joint.invBMat)
                };
                bones.Add(local, bone);
                target.Bones.Add(bone);
            }

            bufInfo indexInfo = source.gobject.bufInfo[5];
            bufInfo weightInfo = source.gobject.bufInfo[6];
            for (int vertex = 0; vertex < vertexCount; vertex++)
            {
                int influenceCount = Math.Min(4, Math.Min(indexInfo.count, weightInfo.count));
                for (int influence = 0; influence < influenceCount; influence++)
                {
                    int localBone = readIntegerComponent(source.gobject, data, indexInfo, vertex, influence);
                    float weight = readFloatComponent(source.gobject, data, weightInfo, vertex, influence);
                    if (weight > 0 && bones.TryGetValue(localBone, out Assimp.Bone bone))
                        bone.VertexWeights.Add(new Assimp.VertexWeight(vertex, weight));
                }
            }

            //A bone with no weights is not a valid aiBone and some native exporters assume at least
            //one weight exists. Bone remap tables commonly contain entries unused by this mesh.
            for (int i = target.Bones.Count - 1; i >= 0; i--)
            {
                if (target.Bones[i].VertexWeightCount == 0)
                {
                    bones.Remove(bones.First(pair => ReferenceEquals(pair.Value, target.Bones[i])).Key);
                    target.Bones.RemoveAt(i);
                }
            }

            //Unchecked joints remove their influences. Renormalize the weights that remain so importers
            //still receive a valid skin for the visible part of the skeleton.
            float[] totals = new float[vertexCount];
            foreach (Assimp.Bone bone in bones.Values)
                foreach (Assimp.VertexWeight weight in bone.VertexWeights)
                    totals[weight.VertexID] += weight.Weight;
            foreach (Assimp.Bone bone in bones.Values)
            {
                for (int i = 0; i < bone.VertexWeights.Count; i++)
                {
                    Assimp.VertexWeight weight = bone.VertexWeights[i];
                    if (totals[weight.VertexID] > 0.0f)
                        weight.Weight /= totals[weight.VertexID];
                    bone.VertexWeights[i] = weight;
                }
            }
        }

        private void exportAnimations(Model model)
        {
            foreach (IComponent component in model.Components)
            {
                if (!(component is AnimComponent animationComponent))
                    continue;
                foreach (AnimData animation in animationComponent.Animations)
                    if (exportedAnimations.Add(animation))
                        exportAnimation(animation, model.parentScene);
            }

            foreach (Model child in model.Children)
                exportAnimations(child);
        }

        private void exportAnimation(AnimData source, MVCore.GMDL.Scene ownerScene)
        {
            source.ensureLoaded();
            AnimMetadata metadata = source.animMeta;
            if (metadata == null || metadata.FrameCount <= 0)
            {
                CallBacks.Log("Skipping animation " + source.Anim + ": its animation data could not be loaded");
                return;
            }

            int clipStart = Math.Clamp(source.FrameStart, 0, metadata.FrameCount - 1);
            int clipFrameCount = Math.Min(source.clipFrameCount, metadata.FrameCount - clipStart);
            if (clipFrameCount <= 0)
                return;

            double framesPerSecond = Math.Max(RenderState.renderSettings.AnimFPS, 1);
            if (source.Speed > 0.0f)
                framesPerSecond *= source.Speed;

            string animationName = string.IsNullOrWhiteSpace(source.Anim) ? "Animation" : source.Anim;
            string uniqueName = animationName;
            for (int suffix = 2; !exportedAnimationNames.Add(uniqueName); suffix++)
                uniqueName = animationName + "_" + suffix;

            //Assimp's interchange exporters consume animation key times most consistently as seconds.
            //Using frame numbers here made Collada imports appear roughly FPS times too long in Blender.
            double secondsPerFrame = 1.0 / framesPerSecond;
            Assimp.Animation result = new Assimp.Animation
            {
                Name = uniqueName,
                TicksPerSecond = 1.0,
                DurationInTicks = Math.Max(clipFrameCount - 1, 0) * secondsPerFrame
            };

            for (int nodeIndex = 0; nodeIndex < metadata.NodeCount; nodeIndex++)
            {
                string nodeName = metadata.NodeData[nodeIndex].Node;
                if (!visibleNodeNames.Contains(nodeName))
                    continue;
                if (ownerScene != null && !source.drivesNode(nodeName, ownerScene.nodeDict))
                    continue;
                if (!metadata.anim_positions.ContainsKey(nodeName) ||
                    !metadata.anim_rotations.ContainsKey(nodeName) ||
                    !metadata.anim_scales.ContainsKey(nodeName))
                    continue;

                OpenTK.Mathematics.Vector3[] positions = metadata.anim_positions[nodeName];
                OpenTK.Mathematics.Quaternion[] rotations = metadata.anim_rotations[nodeName];
                OpenTK.Mathematics.Vector3[] scales = metadata.anim_scales[nodeName];
                Model animatedNode = null;
                ownerScene?.nodeDict.TryGetValue(nodeName, out animatedNode);
                int channelFrameCount = Math.Min(clipFrameCount,
                    Math.Min(positions.Length, Math.Min(rotations.Length, scales.Length)) - clipStart);
                if (channelFrameCount <= 0)
                {
                    CallBacks.Log("Skipping truncated animation channel " + nodeName + " in " + source.Anim);
                    continue;
                }

                Assimp.NodeAnimationChannel channel = new Assimp.NodeAnimationChannel { NodeName = nodeName };
                for (int frame = 0; frame < channelFrameCount; frame++)
                {
                    int sourceFrame = clipStart + frame;
                    OpenTK.Mathematics.Vector3 position = positions[sourceFrame];
                    OpenTK.Mathematics.Quaternion rotation = rotations[sourceFrame];
                    OpenTK.Mathematics.Vector3 scale = scales[sourceFrame];

                    //The runtime converts additive clips to deltas and applies those deltas on the bind pose.
                    //It then applies AnimPoseComponent offsets to every clip. Bake the same standalone pose here.
                    if (source.Additive && animatedNode != null)
                    {
                        source.toAdditiveDelta(ref position, ref scale, ref rotation, nodeName);
                        rotation = animatedNode.__localRotation.ExtractRotation() * rotation;
                        position = animatedNode.__localPosition + position;
                        scale = animatedNode.__localScale * scale;
                    }
                    if (animatedNode != null)
                    {
                        rotation *= animatedNode.poseRotation;
                        position += animatedNode.posePosition;
                        scale *= animatedNode.poseScale;
                    }
                    if (!isFinite(position) || !isFinite(rotation) || !isFinite(scale))
                    {
                        CallBacks.Log("Skipping invalid animation channel " + nodeName + " in " + source.Anim +
                            ": frame " + sourceFrame + " contains a non-finite transform");
                        channel = null;
                        break;
                    }

                    float rotationLengthSquared = rotation.LengthSquared;
                    if (rotationLengthSquared < 0.0000001f)
                        rotation = OpenTK.Mathematics.Quaternion.Identity;
                    else
                        rotation = OpenTK.Mathematics.Quaternion.Normalize(rotation);

                    double keyTime = frame * secondsPerFrame;
                    channel.PositionKeys.Add(new Assimp.VectorKey(keyTime,
                        MathUtils.convertVector(position)));
                    channel.RotationKeys.Add(new Assimp.QuaternionKey(keyTime,
                        MathUtils.convertQuaternion(rotation)));
                    channel.ScalingKeys.Add(new Assimp.VectorKey(keyTime,
                        MathUtils.convertVector(scale)));
                }
                if (channel != null)
                    result.NodeAnimationChannels.Add(channel);
            }

            if (result.NodeAnimationChannelCount > 0)
            {
                scene.Animations.Add(result);
                CallBacks.Log($"Prepared animation {result.Name}: {result.NodeAnimationChannelCount} channels, " +
                    $"{clipFrameCount} frames, {result.DurationInTicks:0.###} seconds" +
                    (source.Additive ? " (additive pose baked)" : ""));
            }
            else
                CallBacks.Log("Skipping animation " + source.Anim +
                    ": it has no channels for checked nodes in the exported subtree");
        }

        public static void AddColladaAnimationClips(string filename, Assimp.Scene exportedScene)
        {
            if (exportedScene?.AnimationCount <= 0)
                return;

            XDocument document = XDocument.Load(filename, LoadOptions.PreserveWhitespace);
            XElement root = document.Root;
            if (root == null)
                return;
            XNamespace ns = root.Name.Namespace;
            XElement animationsLibrary = root.Element(ns + "library_animations");
            if (animationsLibrary == null || root.Element(ns + "library_animation_clips") != null)
                return;

            List<XElement> animationElements = animationsLibrary.Elements(ns + "animation").ToList();
            XElement clipsLibrary = new XElement(ns + "library_animation_clips");
            int added = 0;
            foreach (Assimp.Animation animation in exportedScene.Animations)
            {
                XElement animationElement = animationElements.FirstOrDefault(element =>
                    string.Equals((string)element.Attribute("name"), animation.Name,
                        StringComparison.OrdinalIgnoreCase));
                string animationId = (string)animationElement?.Attribute("id");
                if (string.IsNullOrWhiteSpace(animationId))
                    continue;

                double ticksPerSecond = animation.TicksPerSecond > 0.0 ? animation.TicksPerSecond : 1.0;
                double end = animation.DurationInTicks / ticksPerSecond;
                clipsLibrary.Add(new XElement(ns + "animation_clip",
                    new XAttribute("id", animationId + "-clip"),
                    new XAttribute("name", animation.Name),
                    new XAttribute("start", "0"),
                    new XAttribute("end", end.ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
                    new XElement(ns + "instance_animation", new XAttribute("url", "#" + animationId))));
                added++;
            }

            if (added == 0)
                return;
            animationsLibrary.AddAfterSelf(clipsLibrary);
            document.Save(filename, SaveOptions.DisableFormatting);
            CallBacks.Log($"Added {added} Collada animation clip declaration(s) for separate importer actions");
        }

        private static bool isFinite(OpenTK.Mathematics.Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }

        private static bool isFinite(OpenTK.Mathematics.Quaternion value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) &&
                float.IsFinite(value.Z) && float.IsFinite(value.W);
        }

        private static bool isFinite(System.Numerics.Vector3 value)
        {
            return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
        }

        private static int readIntegerComponent(GeomObject geometry, geomMeshData data, bufInfo info,
            int vertex, int component)
        {
            byte[] buffer = info.stream == 0 ? data.vp_buffer : data.vs_buffer;
            int stride = info.stride > 0 ? info.stride : (info.stream == 0 ? geometry.vp_size : geometry.vx_size);
            int size = info.type switch
            {
                VertexAttribPointerType.UnsignedByte => 1,
                VertexAttribPointerType.UnsignedShort => 2,
                VertexAttribPointerType.UnsignedInt => 4,
                _ => throw new NotSupportedException("Unsupported blend-index type " + info.type)
            };
            int offset = checked(vertex * stride + info.offset + component * size);
            if (offset < 0 || offset + size > buffer.Length)
                throw new EndOfStreamException("Blend indices are truncated.");
            return size switch
            {
                1 => buffer[offset],
                2 => BitConverter.ToUInt16(buffer, offset),
                _ => checked((int)BitConverter.ToUInt32(buffer, offset))
            };
        }

        private static float readFloatComponent(GeomObject geometry, geomMeshData data, bufInfo info,
            int vertex, int component)
        {
            byte[] buffer = info.stream == 0 ? data.vp_buffer : data.vs_buffer;
            int stride = info.stride > 0 ? info.stride : (info.stream == 0 ? geometry.vp_size : geometry.vx_size);
            int size = info.type switch
            {
                VertexAttribPointerType.Float => 4,
                VertexAttribPointerType.HalfFloat => 2,
                VertexAttribPointerType.UnsignedByte => 1,
                VertexAttribPointerType.UnsignedShort => 2,
                _ => throw new NotSupportedException("Unsupported blend-weight type " + info.type)
            };
            int offset = checked(vertex * stride + info.offset + component * size);
            if (offset < 0 || offset + size > buffer.Length)
                throw new EndOfStreamException("Blend weights are truncated.");
            return info.type switch
            {
                VertexAttribPointerType.Float => BitConverter.ToSingle(buffer, offset),
                VertexAttribPointerType.HalfFloat => MVCore.Utils.Half.decompress(BitConverter.ToUInt16(buffer, offset)),
                VertexAttribPointerType.UnsignedByte => info.normalize ? buffer[offset] / 255.0f : buffer[offset],
                _ => info.normalize ? BitConverter.ToUInt16(buffer, offset) / 65535.0f : BitConverter.ToUInt16(buffer, offset)
            };
        }

        private static float readTangentSign(GeomObject geometry, geomMeshData data, bufInfo info, int vertex)
        {
            if (info.count <= 3)
                return 1.0f;
            if (info.type != VertexAttribPointerType.Int2101010Rev)
            {
                float value = readFloatComponent(geometry, data, info, vertex, 3);
                return value < 0.0f ? -1.0f : 1.0f;
            }

            byte[] buffer = info.stream == 0 ? data.vp_buffer : data.vs_buffer;
            int stride = info.stride > 0 ? info.stride : (info.stream == 0 ? geometry.vp_size : geometry.vx_size);
            int offset = checked(vertex * stride + info.offset);
            if (offset < 0 || offset + sizeof(uint) > buffer.Length)
                throw new EndOfStreamException("Packed tangent is truncated.");
            int signedTwoBits = _2sComplement.toInt(BitConverter.ToUInt32(buffer, offset) >> 30, 2);
            return signedTwoBits < 0 ? -1.0f : 1.0f;
        }
    }
}
