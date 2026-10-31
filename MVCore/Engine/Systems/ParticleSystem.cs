using System;
using System.Collections.Generic;
using GLSLHelper;
using MVCore.Common;
using MVCore.GMDL;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace MVCore.Engine.Systems
{
    //A bounded viewer approximation for NMS emitter nodes. Runtime scenes retain only MATERIAL and
    //DATA references; the referenced .particle.mXml authoring files are not shipped in the game PAKs.
    public sealed class ParticleSystem : IDisposable
    {
        private const int GlobalParticleLimit = 8192;
        private sealed class Profile
        {
            public string Name = "generic";
            public float Rate = 12.0f;
            public float Lifetime = 2.0f;
            public float Speed = 0.6f;
            public float Spread = 0.25f;
            public float StartSize = 0.12f;
            public float EndSize = 0.35f;
            public float Gravity = 0.0f;
            public bool Additive;
            public int MaxParticles = 192;
        }

        private sealed class Particle
        {
            public Vector3 Position;
            public Vector3 Velocity;
            public float Age;
            public float Lifetime;
            public float Rotation;
            public float RotationSpeed;
            public float StartSize;
            public float EndSize;
        }

        private sealed class EmitterState
        {
            public ParticleEmitter Emitter;
            public Random Random;
            public readonly List<Particle> Particles = new List<Particle>();
            public float SpawnAccumulator;
        }

        private readonly List<EmitterState> states = new List<EmitterState>();
        private ResourceManager resources;
        private int vao;
        private int quadVbo;
        private int instanceVbo;
        private bool initialized;
        private float[] instanceData = Array.Empty<float>();

        public int EmitterCount => states.Count;
        public int ParticleCount
        {
            get
            {
                int count = 0;
                foreach (EmitterState state in states)
                    count += state.Particles.Count;
                return count;
            }
        }

        public void Init(ResourceManager resourceManager)
        {
            resources = resourceManager;
            if (initialized)
                return;

            float[] quad =
            {
                -0.5f, -0.5f, 0.0f, 0.0f,
                 0.5f, -0.5f, 1.0f, 0.0f,
                 0.5f,  0.5f, 1.0f, 1.0f,
                -0.5f, -0.5f, 0.0f, 0.0f,
                 0.5f,  0.5f, 1.0f, 1.0f,
                -0.5f,  0.5f, 0.0f, 1.0f
            };

            vao = GL.GenVertexArray();
            quadVbo = GL.GenBuffer();
            instanceVbo = GL.GenBuffer();
            GL.BindVertexArray(vao);

            GL.BindBuffer(BufferTarget.ArrayBuffer, quadVbo);
            GL.BufferData(BufferTarget.ArrayBuffer, quad.Length * sizeof(float), quad, BufferUsageHint.StaticDraw);
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVbo);
            int stride = 10 * sizeof(float);
            GL.EnableVertexAttribArray(2);
            GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, stride, 0);
            GL.VertexAttribDivisor(2, 1);
            GL.EnableVertexAttribArray(3);
            GL.VertexAttribPointer(3, 2, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
            GL.VertexAttribDivisor(3, 1);
            GL.EnableVertexAttribArray(4);
            GL.VertexAttribPointer(4, 4, VertexAttribPointerType.Float, false, stride, 5 * sizeof(float));
            GL.VertexAttribDivisor(4, 1);
            GL.EnableVertexAttribArray(5);
            GL.VertexAttribPointer(5, 1, VertexAttribPointerType.Float, false, stride, 9 * sizeof(float));
            GL.VertexAttribDivisor(5, 1);

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            initialized = true;
        }

        public void Rebuild(Model root)
        {
            states.Clear();
            if (root != null)
                collect(root);
            CallBacks.Log("[Particles] registered {0} emitter nodes (viewer approximation; authored DATA files unavailable)", states.Count);
        }

        private void collect(Model node)
        {
            ParticleEmitter emitter = node as ParticleEmitter;
            if (emitter != null)
            {
                Profile profile = inferProfile(emitter);
                if (!emitter.RuntimeParametersInitialized)
                {
                    emitter.ProfileName = profile.Name;
                    emitter.EmissionRate = profile.Rate;
                    emitter.ParticleLifetime = profile.Lifetime;
                    emitter.Speed = profile.Speed;
                    emitter.Spread = profile.Spread;
                    emitter.Gravity = profile.Gravity;
                    emitter.StartSize = profile.StartSize;
                    emitter.EndSize = profile.EndSize;
                    emitter.Additive = profile.Additive;
                    emitter.ParticleLimit = profile.MaxParticles;
                    emitter.RuntimeParametersInitialized = true;
                }
                states.Add(new EmitterState
                {
                    Emitter = emitter,
                    Random = new Random(stableHash(emitter.name + "|" + emitter.MaterialPath + "|" + emitter.DataPath))
                });
                CallBacks.Log("[EmitterProfile] node={0}; profile={1}; rate={2:0.#}/s; lifetime={3:0.##}s; size={4:0.###}->{5:0.###}; blend={6}",
                    emitter.name, emitter.ProfileName, emitter.EmissionRate, emitter.ParticleLifetime,
                    emitter.StartSize, emitter.EndSize, emitter.Additive ? "additive" : "alpha");
            }

            foreach (Model child in node.Children)
                collect(child);
        }

        public void CleanUp()
        {
            states.Clear();
        }

        public void update(float dtMilliseconds)
        {
            float dt = MathHelper.Clamp(dtMilliseconds * 0.001f, 0.0f, 0.1f);
            if (dt <= 0.0f)
                return;

            int remainingGlobal = Math.Max(0, GlobalParticleLimit - ParticleCount);
            foreach (EmitterState state in states)
            {
                ParticleEmitter emitter = state.Emitter;
                while (state.Particles.Count > emitter.ParticleLimit)
                    state.Particles.RemoveAt(state.Particles.Count - 1);
                for (int i = state.Particles.Count - 1; i >= 0; --i)
                {
                    Particle particle = state.Particles[i];
                    particle.Age += dt;
                    if (particle.Age >= particle.Lifetime)
                    {
                        state.Particles.RemoveAt(i);
                        continue;
                    }
                    particle.Velocity.Y += emitter.Gravity * dt;
                    particle.Position += particle.Velocity * dt;
                    particle.Rotation += particle.RotationSpeed * dt;
                }

                if (!state.Emitter.ParticlesVisible || !state.Emitter.active)
                {
                    state.SpawnAccumulator = 0.0f;
                    continue;
                }

                state.SpawnAccumulator += emitter.EmissionRate * dt;
                int requested = (int)state.SpawnAccumulator;
                state.SpawnAccumulator -= requested; //Do not bank a burst while an emitter or the global cap is full.
                int spawnCount = Math.Min(Math.Min(requested,
                    emitter.ParticleLimit - state.Particles.Count), remainingGlobal);
                for (int i = 0; i < spawnCount; ++i)
                    spawn(state);
                remainingGlobal -= spawnCount;
            }
        }

        private static Vector3 transformDirection(Vector3 direction, Matrix4 matrix)
        {
            Vector3 result = (new Vector4(direction, 0.0f) * matrix).Xyz;
            return result.LengthSquared > 0.000001f ? result.Normalized() : Vector3.UnitY;
        }

        private void spawn(EmitterState state)
        {
            Random random = state.Random;
            ParticleEmitter emitter = state.Emitter;
            Vector3 direction = transformDirection(Vector3.UnitY, state.Emitter.worldMat);
            Vector3 noise = new Vector3(nextSigned(random), nextSigned(random), nextSigned(random));
            Vector3 velocity = direction * emitter.Speed + noise * emitter.Spread;
            Vector3 jitter = noise * emitter.StartSize * 0.25f;
            float lifetime = emitter.ParticleLifetime * (0.8f + (float)random.NextDouble() * 0.4f);

            state.Particles.Add(new Particle
            {
                Position = state.Emitter.worldPosition + jitter,
                Velocity = velocity,
                Age = 0.0f,
                Lifetime = lifetime,
                Rotation = (float)random.NextDouble() * MathHelper.TwoPi,
                RotationSpeed = nextSigned(random) * 1.5f,
                StartSize = emitter.StartSize * (0.8f + (float)random.NextDouble() * 0.4f),
                EndSize = emitter.EndSize * (0.8f + (float)random.NextDouble() * 0.4f)
            });
        }

        public void render()
        {
            if (!initialized || states.Count == 0 || resources == null || RenderState.activeCam == null ||
                !resources.GLShaders.TryGetValue(SHADER_TYPE.PARTICLE_SHADER, out GLSLShaderConfig shader))
                return;

            GL.UseProgram(shader.program_id);
            GL.BindVertexArray(vao);
            GL.Enable(EnableCap.DepthTest);
            GL.DepthFunc(DepthFunction.Lequal);
            GL.DepthMask(false);
            GL.Disable(EnableCap.CullFace);
            GL.Enable(EnableCap.Blend);
            GL.PolygonMode(TriangleFace.FrontAndBack, PolygonMode.Fill);

            int samplerLocation = shader.getLocation("particleMap");
            GL.Uniform1(samplerLocation, 0);
            Vector3 cameraPosition = RenderState.activeCam.Position;

            foreach (EmitterState state in states)
            {
                if (state.Particles.Count == 0 || state.Emitter.ParticleMaterial == null)
                    continue;
                if (!tryGetDiffuseTexture(state.Emitter.ParticleMaterial, out Texture texture))
                    continue;

                state.Particles.Sort((a, b) =>
                    (b.Position - cameraPosition).LengthSquared.CompareTo((a.Position - cameraPosition).LengthSquared));

                Vector4 colour = materialColour(state.Emitter.ParticleMaterial);
                int floatCount = state.Particles.Count * 10;
                if (instanceData.Length < floatCount)
                    instanceData = new float[Math.Max(floatCount, instanceData.Length * 2 + 640)];

                int offset = 0;
                foreach (Particle particle in state.Particles)
                {
                    float t = particle.Age / particle.Lifetime;
                    float size = MathHelper.Lerp(particle.StartSize, particle.EndSize, t);
                    float alpha = (1.0f - t) * Math.Min(t * 8.0f, 1.0f) * colour.W;
                    instanceData[offset++] = particle.Position.X;
                    instanceData[offset++] = particle.Position.Y;
                    instanceData[offset++] = particle.Position.Z;
                    instanceData[offset++] = size;
                    instanceData[offset++] = particle.Rotation;
                    instanceData[offset++] = colour.X;
                    instanceData[offset++] = colour.Y;
                    instanceData[offset++] = colour.Z;
                    instanceData[offset++] = alpha;
                    instanceData[offset++] = 0.0f; //Texture-array layer; retained for future animation.
                }

                GL.ActiveTexture(TextureUnit.Texture0);
                GL.BindTexture(texture.target, texture.texID);
                GL.BlendFunc(BlendingFactor.SrcAlpha,
                    state.Emitter.Additive ? BlendingFactor.One : BlendingFactor.OneMinusSrcAlpha);
                GL.BindBuffer(BufferTarget.ArrayBuffer, instanceVbo);
                GL.BufferData(BufferTarget.ArrayBuffer, floatCount * sizeof(float), instanceData, BufferUsageHint.StreamDraw);
                GL.DrawArraysInstanced(PrimitiveType.Triangles, 0, 6, state.Particles.Count);
            }

            GL.BindVertexArray(0);
            GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
            GL.DepthMask(true);
            GL.DepthFunc(DepthFunction.Less);
            GL.Disable(EnableCap.Blend);
            GL.Enable(EnableCap.CullFace);
            GL.CullFace(TriangleFace.Back);
            GL.ActiveTexture(TextureUnit.Texture0);
        }

        private static bool tryGetDiffuseTexture(Material material, out Texture texture)
        {
            texture = null;
            if (material.PSamplers.TryGetValue("mpCustomPerMaterial.gDiffuseMap", out Sampler sampler))
                texture = sampler.tex;
            if (texture != null && texture.texID > 0)
                return true;

            foreach (Sampler candidate in material.PSamplers.Values)
            {
                if (candidate.tex != null && candidate.tex.texID > 0)
                {
                    texture = candidate.tex;
                    return true;
                }
            }
            return false;
        }

        private static Vector4 materialColour(Material material)
        {
            if (material.CustomPerMaterialUniforms.TryGetValue("mpCustomPerMaterial.gMaterialColourVec4", out Uniform uniform))
                return uniform.vec.vec4;
            return Vector4.One;
        }

        private static Profile inferProfile(ParticleEmitter emitter)
        {
            string hint = ((emitter.name ?? "") + " " + (emitter.MaterialPath ?? "") + " " +
                           (emitter.DataPath ?? "") + " " + (emitter.ParticleMaterial?.PClass ?? "")).ToUpperInvariant();
            if (hint.Contains("SPARK") || hint.Contains("ELECTRIC"))
                return new Profile { Name = "spark", Rate = 28.0f, Lifetime = 0.75f, Speed = 1.8f,
                    Spread = 0.75f, StartSize = 0.035f, EndSize = 0.012f, Gravity = -1.4f,
                    Additive = true, MaxParticles = 160 };
            if (hint.Contains("BUBBLE"))
                return new Profile { Name = "bubble", Rate = 9.0f, Lifetime = 3.2f, Speed = 0.35f,
                    Spread = 0.08f, StartSize = 0.045f, EndSize = 0.09f, MaxParticles = 128 };
            if (hint.Contains("SMOKE") || hint.Contains("STEAM") || hint.Contains("DUST") || hint.Contains("FOG"))
                return new Profile { Name = "smoke", Rate = 12.0f, Lifetime = 2.6f, Speed = 0.45f,
                    Spread = 0.18f, StartSize = 0.14f, EndSize = 0.55f, MaxParticles = 192 };
            if (hint.Contains("GLOW") || hint.Contains("FIRE") || hint.Contains("FLAME") || hint.Contains("JET"))
                return new Profile { Name = "glow", Rate = 20.0f, Lifetime = 1.1f, Speed = 0.8f,
                    Spread = 0.22f, StartSize = 0.08f, EndSize = 0.22f, Additive = true, MaxParticles = 192 };
            bool additive = hint.Contains("ADDITIVE");
            return new Profile { Name = additive ? "generic-additive" : "generic", Additive = additive };
        }

        private static float nextSigned(Random random)
        {
            return (float)random.NextDouble() * 2.0f - 1.0f;
        }

        private static int stableHash(string text)
        {
            unchecked
            {
                int hash = 17;
                foreach (char c in text)
                    hash = hash * 31 + c;
                return hash;
            }
        }

        public void Dispose()
        {
            CleanUp();
            if (!initialized)
                return;
            GL.DeleteBuffer(instanceVbo);
            GL.DeleteBuffer(quadVbo);
            GL.DeleteVertexArray(vao);
            initialized = false;
        }
    }
}
