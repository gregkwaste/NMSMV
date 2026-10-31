using System;
using System.Collections.Generic;
using System.IO;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL4;
using MVCore.GMDL;

namespace MVCore
{
    //Clustered lighting for the scene lights. The view is split into screen tiles and exponential depth
    //slices, and every cluster lists the lights that reach it. The deferred light pass and the forward
    //(transparent) pass look up the cluster
    //of each pixel (calcSceneLighting in common_lighting.glsl), so any number of lights can be in the
    //scene while every pixel still shades a bounded number of them.
    //Light 0 is the key light of the environment lighting and stays in the per frame UBO
    public class LightClusters : IDisposable
    {
        //A fixed grid keeps CPU cluster construction bounded regardless of viewport resolution.
        private const int TILES_X = 24;
        private const int TILES_Y = 16;
        public const int DEPTH_SLICES = 16;
        //Local storage is sized dynamically up to this emergency ceiling. Normal scenes, including
        //NexusCore, retain every spatially intersecting light; silently selecting a different top-K set in
        //adjacent tiles creates much more objectionable artifacts than the extra lighting work. Global
        //lights bypass spotlight-cone filtering, so exact lists keep scene lights local.
        public const int GLOBAL_LIGHT_BUDGET = 0;
        public const int MAX_LIGHTS_PER_CLUSTER = 1024;

        //SSBO binding points, as declared in common_lighting.glsl
        public const int LIGHTS_BINDING = 2;
        public const int CLUSTERS_BINDING = 3;
        public const int INDICES_BINDING = 4;
        private const int FILTERED_CLUSTERS_BINDING = 6;
        private const int FILTERED_INDICES_BINDING = 7;

        //Must match kLightCutoff in common_lighting.glsl: below it a light adds nothing visible
        private const float LIGHT_CUTOFF = 3e-3f;
        private const int LIGHT_FLOATS = 16; //Light struct in std430
        private const int HEADER_UINTS = 8; //clusterGrid and clusterParams

        //Stats of the last update
        public int activeLightCount { get; private set; }
        public int globalLightCount { get; private set; }
        public int constantLightCount { get; private set; }
        public float avgLightsPerCluster { get; private set; }
        public int maxLightsPerCluster { get; private set; }
        public float avgCandidateLightsPerCluster { get; private set; }
        public int maxCandidateLightsPerCluster { get; private set; }
        public float avgPostFilterLightsPerCluster { get; private set; }
        public int maxPostFilterLightsPerCluster { get; private set; }
        public int overflowClusterCount { get; private set; }
        public long droppedLightAssignments { get; private set; }
        public int tilesX { get; private set; } = 24;
        public int tilesY { get; private set; } = 16;
        public int clusterCount => tilesX * tilesY * DEPTH_SLICES;
        public double updateMs { get; private set; }
        private readonly System.Diagnostics.Stopwatch timer = new();

        private int lightsSSBO, clustersSSBO, indicesSSBO;
        private int filteredClustersSSBO, filteredIndicesSSBO;
        private int lightsCapacity, clustersCapacity, indicesCapacity; //Bytes
        private int filteredClustersCapacity, filteredIndicesCapacity;
        private int filterProgram;
        private int projInvLocation, viewLocation, nearLocation, farLocation;

        private struct ViewLight
        {
            public Light light;
            public Vector3 center; //View space
            public float radius; //View space
            public float range; //Scene space
            public float peak; //Brightest color channel times intensity
            public int falloff;
        }

        //Reused between frames to avoid allocations
        private float[] lightData = new float[LIGHT_FLOATS];
        private uint[] clusterData = new uint[HEADER_UINTS];
        private uint[] indexData = new uint[0];
        private uint[] packedIndexData = new uint[0];
        private int[] clusterCounts = new int[0];
        private int[] clusterCandidateCounts = new int[0];
        private uint[] filteredClusterData = new uint[HEADER_UINTS];
        private Vector3[] clusterMin = new Vector3[0];
        private Vector3[] clusterMax = new Vector3[0];
        private Vector3[] lineNear = new Vector3[0];
        private Vector3[] lineFar = new Vector3[0];
        private readonly List<ViewLight> viewLights = new();
        private static readonly Comparison<ViewLight> brightestFirst = (a, b) => b.peak.CompareTo(a.peak);

        public LightClusters()
        {
            lightsSSBO = GL.GenBuffer();
            clustersSSBO = GL.GenBuffer();
            indicesSSBO = GL.GenBuffer();
            filteredClustersSSBO = GL.GenBuffer();
            filteredIndicesSSBO = GL.GenBuffer();
            filterProgram = createComputeProgram("Shaders/LightClusterFilter_CS.glsl");
            projInvLocation = GL.GetUniformLocation(filterProgram, "projMatInv");
            viewLocation = GL.GetUniformLocation(filterProgram, "viewMat");
            nearLocation = GL.GetUniformLocation(filterProgram, "cameraNear");
            farLocation = GL.GetUniformLocation(filterProgram, "cameraFar");
        }

        private static int createComputeProgram(string path)
        {
            string source = File.ReadAllText(path);
            int shader = GL.CreateShader(ShaderType.ComputeShader);
            GL.ShaderSource(shader, source);
            GL.CompileShader(shader);
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int compiled);
            string shaderLog = GL.GetShaderInfoLog(shader);
            if (compiled == 0)
            {
                GL.DeleteShader(shader);
                throw new InvalidOperationException("Failed to compile " + path + ":\n" + shaderLog);
            }

            int program = GL.CreateProgram();
            GL.AttachShader(program, shader);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int linked);
            string programLog = GL.GetProgramInfoLog(program);
            GL.DetachShader(program, shader);
            GL.DeleteShader(shader);
            if (linked == 0)
            {
                GL.DeleteProgram(program);
                throw new InvalidOperationException("Failed to link " + path + ":\n" + programLog);
            }
            return program;
        }

        //Distance where the light falls below the cutoff, matching calcLightAttenuation. Infinity for
        //constant falloff
        private static float lightRange(int falloff, float peak)
        {
            float q = peak / LIGHT_CUTOFF;
            switch (falloff)
            {
                case 0: return MathF.Sqrt(q); //Quadratic
                case 2: return q; //Linear
                case 3: return q * q; //Square root
                default: return float.PositiveInfinity; //Constant
            }
        }

        //Rebuilds the cluster grid for this frame and uploads it. lights[0] is the key light and is skipped.
        //Positions are in the rotated scene space, like the gbuffer
        public void update(List<Light> lights, Camera cam, int width, int height)
        {
            timer.Restart();
            rebuilt = false;
            width = Math.Max(width, 1);
            height = Math.Max(height, 1);
            int gx = TILES_X;
            int gy = TILES_Y;
            tilesX = gx;
            tilesY = gy;
            float tileW = (float) width / gx;
            float tileH = (float) height / gy;
            int gz = DEPTH_SLICES;
            int clusterCount = gx * gy * gz;

            float n = Math.Max(cam.zNear, 1e-6f);
            float f = Math.Max(cam.zFar, n * 1.001f);
            float logScale = gz / MathF.Log(f / n);
            float logBias = -MathF.Log(n) * logScale;

            //The view space of the projection. Equal to lookMat for the perspective camera and includes the
            //uniform scale of the orthographic one
            Matrix4 proj = cam.projMat;
            Matrix4 view = cam.viewMat * cam.projMatInv;
            float viewScale = view.Row0.Xyz.Length;

            //Lights that are on and reach the view
            viewLights.Clear();
            constantLightCount = 0;
            for (int i = 1; i < lights.Count; i++)
            {
                GLLight s = lights[i].strct;
                if (s.position.W < 1.0f) //Switched off, hidden or another LOD
                    continue;

                float peak = Math.Max(s.color.X, Math.Max(s.color.Y, s.color.Z)) * s.color.W;
                if (!(peak > LIGHT_CUTOFF))
                    continue;

                ViewLight vl;
                vl.light = lights[i];
                vl.center = (new Vector4(s.position.Xyz, 1.0f) * view).Xyz;
                vl.range = lightRange(s.falloff, peak);
                vl.radius = vl.range * viewScale;
                vl.peak = peak;
                vl.falloff = s.falloff;

                //Depth range of the sphere against the clip planes
                if (-vl.center.Z + vl.radius < n || -vl.center.Z - vl.radius > f)
                    continue;

                viewLights.Add(vl);
                if (s.falloff == (int) ATTENUATION_TYPE.CONSTANT)
                    constantLightCount++;
            }

            //Brightest first only matters for the 1024-light emergency ceiling; normal scenes retain all.
            viewLights.Sort(brightestFirst);
            activeLightCount = viewLights.Count;
            globalLightCount = Math.Min(GLOBAL_LIGHT_BUDGET, activeLightCount);

            //Nothing moved since the last frame: the uploaded clusters are still valid
            writeLights();
            if (!changedSinceLastUpdate(view, proj, width, height))
            {
                bindBuffers();
                updateMs = timer.Elapsed.TotalMilliseconds;
                return;
            }

            buildClusterBounds(cam.projMatInv, gx, gy, gz, n, f);

            int clusterStride = Math.Max(1, Math.Min(MAX_LIGHTS_PER_CLUSTER, activeLightCount - globalLightCount));
            int slots = clusterCount * clusterStride;
            if (indexData.Length < slots)
                indexData = new uint[slots];
            if (clusterCounts.Length < clusterCount)
            {
                clusterCounts = new int[clusterCount];
                clusterCandidateCounts = new int[clusterCount];
            }
            Array.Clear(clusterCounts, 0, clusterCount);
            Array.Clear(clusterCandidateCounts, 0, clusterCount);

            //The global prefix is deliberately omitted from local lists. The shader evaluates it before
            //the cluster list and still performs the exact per-pixel range test.
            for (int i = globalLightCount; i < viewLights.Count; i++)
                addLightToClusters(i, viewLights[i], proj, gx, gy, gz, n, f, logScale, logBias,
                                   width, height, tileW, tileH, clusterStride);

            gatherStats(clusterCount, clusterStride);

            upload(lightsSSBO, ref lightsCapacity, lightData, viewLights.Count * LIGHT_FLOATS * sizeof(float), LIGHTS_BINDING);
            uploadClusters(gx, gy, gz, clusterCount, globalLightCount, clusterStride,
                           logScale, logBias, tileW, tileH);
            updateMs = timer.Elapsed.TotalMilliseconds;
            rebuilt = true;
        }

        //True when the last update rebuilt the clusters instead of reusing them
        public bool rebuilt { get; private set; }

        //Camera, viewport and light data of the last rebuild
        private Matrix4 lastView, lastProj;
        private int lastWidth = -1, lastHeight = -1, lastLightFloats = -1;
        private float[] lastLightData = new float[0];

        private bool changedSinceLastUpdate(Matrix4 view, Matrix4 proj, int width, int height)
        {
            int floats = viewLights.Count * LIGHT_FLOATS;
            if (view == lastView && proj == lastProj && width == lastWidth && height == lastHeight &&
                floats == lastLightFloats && lightData.AsSpan(0, floats).SequenceEqual(lastLightData.AsSpan(0, floats)))
                return false;

            lastView = view;
            lastProj = proj;
            lastWidth = width;
            lastHeight = height;
            lastLightFloats = floats;
            if (lastLightData.Length < floats)
                lastLightData = new float[lightData.Length];
            Array.Copy(lightData, lastLightData, floats);
            return true;
        }

        private void bindBuffers()
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, LIGHTS_BINDING, lightsSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, CLUSTERS_BINDING, clustersSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, INDICES_BINDING, indicesSSBO);
        }

        //Conservatively removes spotlights whose authored cones cannot intersect each cluster. The
        //result is valid for opaque and transparent geometry because it depends only on cluster bounds.
        public void filterSpotCones(Camera cam)
        {
            bindBuffers();
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, FILTERED_CLUSTERS_BINDING, filteredClustersSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, FILTERED_INDICES_BINDING, filteredIndicesSSBO);

            //Cluster and light buffers are unchanged on reuse frames, so preserve the previous result.
            if (rebuilt)
            {
                GL.UseProgram(filterProgram);
                Matrix4 projInv = cam.projMatInv;
                Matrix4 view = cam.viewMat * cam.projMatInv;
                GL.UniformMatrix4(projInvLocation, false, ref projInv);
                GL.UniformMatrix4(viewLocation, false, ref view);
                GL.Uniform1(nearLocation, Math.Max(cam.zNear, 1e-6f));
                GL.Uniform1(farLocation, Math.Max(cam.zFar, cam.zNear * 1.001f));

                GL.DispatchCompute(tilesX, tilesY, DEPTH_SLICES);
                GL.MemoryBarrier(MemoryBarrierFlags.ShaderStorageBarrierBit);
            }

            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, CLUSTERS_BINDING, filteredClustersSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, INDICES_BINDING, filteredIndicesSSBO);
        }

        //Read the compact count fields only when frame statistics are logged. This avoids a per-frame
        //GPU readback while reporting the actual compute-pass result rather than an estimate.
        public void refreshPostFilterStats()
        {
            int uints = HEADER_UINTS + 2 * clusterCount;
            if (filteredClustersCapacity < uints * sizeof(uint))
                return;
            if (filteredClusterData.Length < uints)
                filteredClusterData = new uint[uints];

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, filteredClustersSSBO);
            GL.GetBufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero,
                                uints * sizeof(uint), filteredClusterData);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);

            long total = 0;
            int maximum = 0;
            for (int cluster = 0; cluster < clusterCount; ++cluster)
            {
                int count = (int) filteredClusterData[HEADER_UINTS + 2 * cluster + 1];
                total += count;
                maximum = Math.Max(maximum, count);
            }
            avgPostFilterLightsPerCluster = clusterCount > 0 ? (float) total / clusterCount : 0.0f;
            maxPostFilterLightsPerCluster = maximum;
        }

        public void bindFilteredBuffers()
        {
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, LIGHTS_BINDING, lightsSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, CLUSTERS_BINDING, filteredClustersSSBO);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, INDICES_BINDING, filteredIndicesSSBO);
        }

        private void gatherStats(int clusterCount, int clusterStride)
        {
            long total = 0;
            long candidateTotal = 0;
            int max = 0;
            int candidateMax = 0;
            int overflow = 0;
            for (int c = 0; c < clusterCount; c++)
            {
                total += clusterCounts[c];
                max = Math.Max(max, clusterCounts[c]);
                int candidates = clusterCandidateCounts[c];
                candidateTotal += candidates;
                candidateMax = Math.Max(candidateMax, candidates);
                if (candidates > clusterStride)
                    overflow++;
            }

            avgLightsPerCluster = clusterCount > 0 ? (float) total / clusterCount : 0.0f;
            maxLightsPerCluster = max;
            avgCandidateLightsPerCluster = clusterCount > 0 ? (float) candidateTotal / clusterCount : 0.0f;
            maxCandidateLightsPerCluster = candidateMax;
            overflowClusterCount = overflow;
            droppedLightAssignments = candidateTotal - total;
        }

        //View space line through every tile corner, from the near to the far plane, and the bounds of
        //every cluster
        private void buildClusterBounds(Matrix4 projInv, int gx, int gy, int gz, float n, float f)
        {
            int lines = (gx + 1) * (gy + 1);
            if (lineNear.Length < lines)
            {
                lineNear = new Vector3[lines];
                lineFar = new Vector3[lines];
            }

            for (int j = 0; j <= gy; j++)
            {
                float ndcY = 2.0f * j / gy - 1.0f;
                for (int i = 0; i <= gx; i++)
                {
                    float ndcX = 2.0f * i / gx - 1.0f;
                    int l = j * (gx + 1) + i;
                    lineNear[l] = unproject(projInv, ndcX, ndcY, -1.0f);
                    lineFar[l] = unproject(projInv, ndcX, ndcY, 1.0f);
                }
            }

            int clusterCount = gx * gy * gz;
            if (clusterMin.Length < clusterCount)
            {
                clusterMin = new Vector3[clusterCount];
                clusterMax = new Vector3[clusterCount];
            }

            Span<int> corners = stackalloc int[4];
            for (int k = 0; k < gz; k++)
            {
                //Fraction of the near to far segment at the slice depths. The view depth is linear along it
                float t0 = (sliceDepth(k, gz, n, f) - n) / (f - n);
                float t1 = (sliceDepth(k + 1, gz, n, f) - n) / (f - n);
                for (int y = 0; y < gy; y++)
                {
                    for (int x = 0; x < gx; x++)
                    {
                        corners[0] = y * (gx + 1) + x;
                        corners[1] = corners[0] + 1;
                        corners[2] = corners[0] + gx + 1;
                        corners[3] = corners[2] + 1;

                        Vector3 min = new Vector3(float.MaxValue);
                        Vector3 max = new Vector3(float.MinValue);
                        for (int c = 0; c < 4; c++)
                        {
                            Vector3 a = lineNear[corners[c]];
                            Vector3 d = lineFar[corners[c]] - a;
                            Vector3 p0 = a + t0 * d;
                            Vector3 p1 = a + t1 * d;
                            min = Vector3.ComponentMin(min, Vector3.ComponentMin(p0, p1));
                            max = Vector3.ComponentMax(max, Vector3.ComponentMax(p0, p1));
                        }

                        int idx = (k * gy + y) * gx + x;
                        clusterMin[idx] = min;
                        clusterMax[idx] = max;
                    }
                }
            }
        }

        private static float sliceDepth(int k, int gz, float n, float f)
        {
            return n * MathF.Pow(f / n, (float) k / gz);
        }

        private static Vector3 unproject(Matrix4 projInv, float x, float y, float z)
        {
            Vector4 p = new Vector4(x, y, z, 1.0f) * projInv;
            return p.Xyz / p.W;
        }

        private void addLightToClusters(int light, ViewLight vl, Matrix4 proj,
                                        int gx, int gy, int gz, float n, float f, float logScale, float logBias,
                                        int width, int height, float tileW, float tileH, int clusterStride)
        {
            Vector3 c = vl.center;
            float r = vl.radius;
            bool finite = !float.IsInfinity(r);

            int k0 = 0, k1 = gz - 1;
            int x0 = 0, x1 = gx - 1, y0 = 0, y1 = gy - 1;
            if (finite)
            {
                k0 = sliceIndex(Math.Max(-c.Z - r, n), gz, logScale, logBias);
                k1 = sliceIndex(Math.Min(-c.Z + r, f), gz, logScale, logBias);

                //Screen rectangle of the sphere's bounding box. A box reaching behind the eye has no finite
                //projection, then all tiles are tested
                float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
                bool projected = true;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 p = new Vector3((i & 1) == 0 ? c.X - r : c.X + r,
                                            (i & 2) == 0 ? c.Y - r : c.Y + r,
                                            (i & 4) == 0 ? c.Z - r : c.Z + r);
                    Vector4 clip = new Vector4(p, 1.0f) * proj;
                    if (clip.W <= 1e-6f)
                    {
                        projected = false;
                        break;
                    }
                    float px = (clip.X / clip.W * 0.5f + 0.5f) * width;
                    float py = (clip.Y / clip.W * 0.5f + 0.5f) * height;
                    minX = Math.Min(minX, px);
                    maxX = Math.Max(maxX, px);
                    minY = Math.Min(minY, py);
                    maxY = Math.Max(maxY, py);
                }

                if (projected)
                {
                    //Use the same conservative margin for the off-screen rejection; otherwise the
                    //approximate box projection could reject a sphere that still reaches an edge tile.
                    if (maxX < -tileW || maxY < -tileH || minX > width + tileW || minY > height + tileH)
                        return; //Off screen
                    //The extrema of a projected sphere are not necessarily corners of its bounding box.
                    //Expand by one tile and let the exact sphere/cluster test below reject the extras.
                    x0 = Math.Clamp((int) MathF.Floor(minX / tileW) - 1, 0, gx - 1);
                    x1 = Math.Clamp((int) MathF.Floor(maxX / tileW) + 1, 0, gx - 1);
                    y0 = Math.Clamp((int) MathF.Floor(minY / tileH) - 1, 0, gy - 1);
                    y1 = Math.Clamp((int) MathF.Floor(maxY / tileH) + 1, 0, gy - 1);
                }
            }

            //This loop runs for every light and cluster when the camera moves, so it is kept to plain
            //float math (the Vector3 helpers are calls in Debug builds)
            float r2 = finite ? r * r : float.PositiveInfinity;
            float cx = c.X, cy = c.Y, cz = c.Z;
            for (int k = k0; k <= k1; k++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    int idx = (k * gy + y) * gx + x0;
                    for (int x = x0; x <= x1; x++, idx++)
                    {
                        int count = clusterCounts[idx];
                        float d2;

                        if (!finite)
                        {
                            //Constant lights intersect every cluster. Avoid the AABB work for what is
                            //usually the largest group in imported scenes.
                            clusterCandidateCounts[idx]++;
                            d2 = 0.0f;
                        }
                        else
                        {
                            //Sphere against the cluster box
                            Vector3 bmin = clusterMin[idx];
                            Vector3 bmax = clusterMax[idx];
                            float dx = cx < bmin.X ? bmin.X - cx : (cx > bmax.X ? cx - bmax.X : 0.0f);
                            float dy = cy < bmin.Y ? bmin.Y - cy : (cy > bmax.Y ? cy - bmax.Y : 0.0f);
                            float dz = cz < bmin.Z ? bmin.Z - cz : (cz > bmax.Z ? cz - bmax.Z : 0.0f);
                            d2 = dx * dx + dy * dy + dz * dz;
                            if (d2 > r2)
                                continue;

                            //Count before applying the local budget. This is the representative occupancy;
                            //clusterCounts alone is capped and previously hid the overflow entirely.
                            clusterCandidateCounts[idx]++;

                        }

                        if (count < clusterStride)
                        {
                            int slot = idx * clusterStride + count;
                            indexData[slot] = (uint) light;
                            clusterCounts[idx] = count + 1;
                        }
                    }
                }
            }
        }

        private static int sliceIndex(float depth, int gz, float logScale, float logBias)
        {
            return Math.Clamp((int) MathF.Floor(MathF.Log(depth) * logScale + logBias), 0, gz - 1);
        }

        private void writeLights()
        {
            int count = viewLights.Count;
            if (lightData.Length < count * LIGHT_FLOATS)
                lightData = new float[Math.Max(count * LIGHT_FLOATS, 2 * lightData.Length)];

            //Same order as viewLights, which the cluster lists index
            int o = 0;
            foreach (ViewLight vl in viewLights)
                o = writeLight(vl.light.strct, vl.range, vl.light.getLodAttenuationScale(), o);
        }

        private int writeLight(GLLight s, float range, float lodAttenuationScale, int o)
        {
            //Same layout as the Light struct in common_structs.glsl
            lightData[o + 0] = s.position.X;
            lightData[o + 1] = s.position.Y;
            lightData[o + 2] = s.position.Z;
            lightData[o + 3] = s.position.W;
            lightData[o + 4] = s.color.X;
            lightData[o + 5] = s.color.Y;
            lightData[o + 6] = s.color.Z;
            lightData[o + 7] = s.color.W;
            Vector3 direction = s.direction.Xyz;
            float directionLengthSquared = direction.LengthSquared;
            if (directionLengthSquared > 1e-12f)
                direction *= 1.0f / MathF.Sqrt(directionLengthSquared);
            else
                direction = -Vector3.UnitZ;

            lightData[o + 8] = direction.X;
            lightData[o + 9] = direction.Y;
            lightData[o + 10] = direction.Z;
            //Scene-light shaders need the cone boundary, not the authored angle. Precomputing it here
            //avoids normalizing the direction and evaluating cos() for every candidate pixel.
            lightData[o + 11] = s.type >= 1.0f ? MathF.Cos(0.5f * s.direction.W) : -1.0f;
            lightData[o + 12] = s.falloff;
            lightData[o + 13] = s.type;
            lightData[o + 14] = float.IsInfinity(range) ? float.MaxValue : range;
            lightData[o + 15] = lodAttenuationScale;
            return o + LIGHT_FLOATS;
        }

        private void uploadClusters(int gx, int gy, int gz, int clusterCount, int globalCount, int clusterStride,
                                    float logScale, float logBias, float tileW, float tileH)
        {
            int clusterUints = HEADER_UINTS + 2 * clusterCount;
            if (clusterData.Length < clusterUints)
                clusterData = new uint[clusterUints];

            //Header: clusterGrid and clusterParams. The global prefix is shared by all pixels and local
            //indices are packed tightly after cluster construction.
            clusterData[0] = (uint) gx;
            clusterData[1] = (uint) gy;
            clusterData[2] = (uint) gz;
            clusterData[3] = (uint) globalCount;
            clusterData[4] = BitConverter.SingleToUInt32Bits(tileW);
            clusterData[5] = BitConverter.SingleToUInt32Bits(logScale);
            clusterData[6] = BitConverter.SingleToUInt32Bits(logBias);
            clusterData[7] = BitConverter.SingleToUInt32Bits(tileH);

            int packedCount = 0;
            for (int i = 0; i < clusterCount; i++)
                packedCount += clusterCounts[i];
            if (packedIndexData.Length < packedCount)
                packedIndexData = new uint[Math.Max(packedCount, 2 * packedIndexData.Length)];

            int offset = 0;
            for (int i = 0; i < clusterCount; i++)
            {
                int count = clusterCounts[i];
                clusterData[HEADER_UINTS + 2 * i] = (uint) offset;
                clusterData[HEADER_UINTS + 2 * i + 1] = (uint) count;
                Array.Copy(indexData, i * clusterStride, packedIndexData, offset, count);
                offset += count;
            }

            upload(clustersSSBO, ref clustersCapacity, clusterData, clusterUints * sizeof(uint), CLUSTERS_BINDING);
            upload(indicesSSBO, ref indicesCapacity, packedIndexData, packedCount * sizeof(uint), INDICES_BINDING);
            //The compute pass preserves the spatial-list offsets and replaces only counts and indices.
            upload(filteredClustersSSBO, ref filteredClustersCapacity, clusterData,
                   clusterUints * sizeof(uint), FILTERED_CLUSTERS_BINDING);
            ensureOutputBuffer(filteredIndicesSSBO, ref filteredIndicesCapacity,
                               packedCount * sizeof(uint), FILTERED_INDICES_BINDING);
        }

        private static void ensureOutputBuffer(int ssbo, ref int capacity, int bytes, int binding)
        {
            int size = Math.Max(bytes, 16);
            if (size > capacity)
            {
                capacity = Math.Max(size, 2 * capacity);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
                GL.BufferData(BufferTarget.ShaderStorageBuffer, (IntPtr) capacity, IntPtr.Zero,
                              BufferUsageHint.StreamDraw);
                GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            }
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, binding, ssbo);
        }

        //Orphans the buffer every frame, so the driver does not wait for the GPU to finish the last one
        private static void upload<T>(int ssbo, ref int capacity, T[] data, int bytes, int binding) where T : struct
        {
            int size = Math.Max(bytes, 16); //An empty buffer can not be bound
            if (size > capacity)
                capacity = Math.Max(size, 2 * capacity);

            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, ssbo);
            GL.BufferData(BufferTarget.ShaderStorageBuffer, (IntPtr) capacity, IntPtr.Zero, BufferUsageHint.StreamDraw);
            if (bytes > 0)
                GL.BufferSubData(BufferTarget.ShaderStorageBuffer, IntPtr.Zero, (IntPtr) bytes, data);
            GL.BindBuffer(BufferTarget.ShaderStorageBuffer, 0);
            GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, binding, ssbo);
        }

        public void Dispose()
        {
            GL.DeleteBuffer(lightsSSBO);
            GL.DeleteBuffer(clustersSSBO);
            GL.DeleteBuffer(indicesSSBO);
            GL.DeleteBuffer(filteredClustersSSBO);
            GL.DeleteBuffer(filteredIndicesSSBO);
            GL.DeleteProgram(filterProgram);
            lightsSSBO = clustersSSBO = indicesSSBO = 0;
            filteredClustersSSBO = filteredIndicesSSBO = filterProgram = 0;
        }
    }
}
