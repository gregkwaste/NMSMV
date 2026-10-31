using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using MVCore.Utils;
using MVCore.Common;
using OpenTK.Graphics.OpenGL4;

namespace MVCore.GMDL
{
    [StructLayout(LayoutKind.Explicit)]
    public struct GLLight
    {
        [FieldOffset(0)]
        public Vector4 position; //w is renderable
        [FieldOffset(16)]
        public Vector4 color; //w is intensity
        [FieldOffset(32)]
        public Vector4 direction; //w is fov
        [FieldOffset(48)]
        public int falloff;
        [FieldOffset(52)]
        public float type;

        public static readonly int SizeInBytes = 64;
    }

    public enum ATTENUATION_TYPE
    {
        QUADRATIC = 0x0,
        CONSTANT,
        LINEAR,
        LINEAR_SQRT,
        COUNT
    }

    public enum LIGHT_TYPE
    {
        POINT = 0x0,
        SPOT,
        COUNT
    }

    public class Light : Model
    {
        //I should expand the light properties here
        public MVector4 color = new MVector4(1.0f);
        //public GLMeshVao main_Vao;
        public float fov = 360.0f;
        public ATTENUATION_TYPE falloff;
        public LIGHT_TYPE light_type;

        public float intensity = 1.0f;
        public Vector3 direction = new Vector3();
        public float radius = 4.472136f;
        public int cookieIndex = -1;
        public float volumetric = 0.0f;
        public int lightLayers = 3;

        //Only populated for lights inheriting a named LOD branch. The scale is kept separate from the
        //authored intensity so it cannot change cluster ranges or ordinary lights.
        public bool lodAttenuationControlled = false;
        public float lodEntryAttenuationScale = 1.0f;
        public float lodExitAttenuationScale = 1.0f;
        public int lodAttenuationLevel = -1;
        public Scene lodAttenuationScene;

        public bool update_changes = false; //Used to prevent unecessary uploads to the UBO

        //Light Projection + View Matrices
        public Matrix4[] lightSpaceMatrices;
        public Matrix4 lightProjectionMatrix;
        public GLLight strct;

        public float getLodAttenuationScale()
        {
            if (!lodAttenuationControlled || lodAttenuationLevel < 0 || lodAttenuationScene == null ||
                RenderState.activeCam == null)
                return 1.0f;

            float[] distances = lodAttenuationScene._LODDistances;
            int entryIndex = lodAttenuationLevel - 1;
            float start = entryIndex >= 0 && entryIndex < distances.Length
                ? distances[entryIndex]
                : 0.0f;
            float end = 0.0f;
            if (lodAttenuationLevel < distances.Length && distances[lodAttenuationLevel] > start)
                end = distances[lodAttenuationLevel];
            else
            {
                float previous = entryIndex > 0 && entryIndex - 1 < distances.Length
                    ? distances[entryIndex - 1]
                    : 0.0f;
                float span = previous > 0.0f ? start - previous : start;
                end = start + Math.Max(span, 1.0f);
            }

            Vector3 scenePosition = (new Vector4(lodAttenuationScene.worldPosition, 1.0f) * RenderState.rotMat).Xyz;
            float cameraDistance = (scenePosition - RenderState.activeCam.Position).Length;
            float t = Math.Clamp((cameraDistance - start) / Math.Max(end - start, 1e-4f), 0.0f, 1.0f);
            t = t * t * (3.0f - 2.0f * t); //Smoothstep over this LOD's distance interval
            return lodEntryAttenuationScale + (lodExitAttenuationScale - lodEntryAttenuationScale) * t;
        }

        //Properties
        public MVector4 Color
        {
            get
            {
                return color;
            }

            set
            {
                catchPropertyChanged(color, new PropertyChangedEventArgs("Vec"));
            }
        }

        public float FOV
        {
            get
            {
                return fov;
            }

            set
            {
                fov = value;
                strct.direction.W = MathUtils.radians(fov);
                update_changes = true;
            }
        }

        public float Intensity
        {
            get
            {
                return intensity;
            }

            set
            {
                intensity = value;
                strct.color.W = value;
                update_changes = true;
            }
        }

        //The first resource light is the environment key light. Its position vector is interpreted as
        //a direction by the lighting and shadow shaders, so expose the three components explicitly to WPF.
        public float KeyDirectionX
        {
            get => localPosition.X;
            set => setKeyDirection(new Vector3(value, localPosition.Y, localPosition.Z));
        }

        public float KeyDirectionY
        {
            get => localPosition.Y;
            set => setKeyDirection(new Vector3(localPosition.X, value, localPosition.Z));
        }

        public float KeyDirectionZ
        {
            get => localPosition.Z;
            set => setKeyDirection(new Vector3(localPosition.X, localPosition.Y, value));
        }

        private void setKeyDirection(Vector3 value)
        {
            localPosition = value;
            worldPosition = value;
            strct.position.Xyz = value;
            update_changes = true;
            NotifyPropertyChanged(nameof(KeyDirectionX));
            NotifyPropertyChanged(nameof(KeyDirectionY));
            NotifyPropertyChanged(nameof(KeyDirectionZ));
        }

        public string Attenuation
        {
            get
            {
                return falloff.ToString();
            }

            set
            {
                Enum.TryParse<ATTENUATION_TYPE>(value, out falloff);
                strct.falloff = (int)falloff;
                update_changes = true;
            }
        }

        public override bool IsRenderable
        {
            get
            {
                return renderable;
            }

            set
            {
                strct.position.W = value ? 1.0f : 0.0f;
                base.IsRenderable = value;
                update_changes = true;
            }
        }

        //Add event handler to catch changes to the Vector property

        private void catchPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            MVector4 t = sender as MVector4;

            //Update struct
            strct.color.X = t.X;
            strct.color.Y = t.Y;
            strct.color.Z = t.Z;
            update_changes = true;
        }


        public Light()
        {
            type = TYPES.LIGHT;
            fov = 360;
            intensity = 1.0f;
            falloff = ATTENUATION_TYPE.CONSTANT;
            light_type = LIGHT_TYPE.POINT;

            //Initialize new MeshVao
            meshVao = new GLMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this); // Add instance

            //Init projection Matrix
            lightProjectionMatrix = Matrix4.CreatePerspectiveFieldOfView(MathUtils.radians(90), 1.0f, 1.0f, 300f);

            //Init lightSpace Matrices
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
            {
                lightSpaceMatrices[i] = Matrix4.Identity * lightProjectionMatrix;
            }

            //Catch changes to MVector from the UI
            color = new MVector4(1.0f);
            color.PropertyChanged += catchPropertyChanged;
        }

        private Light(Light input) : base(input)
        {
            //The Color setter does not copy, the components are set one by one
            color.X = input.color.X;
            color.Y = input.color.Y;
            color.Z = input.color.Z;
            color.W = input.color.W;
            intensity = input.intensity;
            falloff = input.falloff;
            radius = input.radius;
            cookieIndex = input.cookieIndex;
            volumetric = input.volumetric;
            lightLayers = input.lightLayers;
            fov = input.fov;
            light_type = input.light_type;
            strct = input.strct;
            lodAttenuationControlled = input.lodAttenuationControlled;
            lodEntryAttenuationScale = input.lodEntryAttenuationScale;
            lodExitAttenuationScale = input.lodExitAttenuationScale;
            lodAttenuationLevel = input.lodAttenuationLevel;
            lodAttenuationScene = input.lodAttenuationScene;

            //Initialize new MeshVao
            meshVao = new GLMeshVao();
            meshVao.type = TYPES.LIGHT;
            meshVao.vao = new Primitives.LineSegment(1, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.metaData = new MeshMetaData();
            meshVao.metaData.batchcount = 2;
            meshVao.material = RenderState.activeResMgr.GLmaterials["lightMat"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this); //Add instance


            //Copy Matrices
            lightProjectionMatrix = input.lightProjectionMatrix;
            lightSpaceMatrices = new Matrix4[6];
            for (int i = 0; i < 6; i++)
                lightSpaceMatrices[i] = input.lightSpaceMatrices[i];

            update_struct();
            RenderState.activeResMgr.GLlights.Add(this);
        }

        //Helper line (position and direction end point) in the vertex buffer of this light, so that the
        //upload only happens when it changed
        private readonly float[] helperVerts = new float[6];
        private readonly float[] uploadedVerts = new float[6];
        private bool helperUploaded = false;

        public override void updateOwnMeshInfo(bool lod_filter = false)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();
            if (lod_filter || !renderable)
            {
                if (strct.position.W != 0.0f)
                    update_changes = true;
                strct.position.W = 0.0f; //Force not renderable
                if (lod_filter)
                    RenderStats.occludedNum += 1;
            }
            else
            {
                //End Point
                Vector4 ep;
                //Lights with 360 FOV are points
                if (Math.Abs(FOV - 360.0f) <= 1e-4)
                {
                    ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                    light_type = LIGHT_TYPE.POINT;
                }
                else
                {
                    ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                    light_type = LIGHT_TYPE.SPOT;
                }

                ep = ep * _localRotation;
                direction = ep.Xyz; //Set spotlight direction
                update_struct();

                //The light remains active independently of whether its yellow helper marker is displayed.
                if (RenderState.renderViewSettings.RenderLights)
                {
                    //Update Vertex Buffer based on the new data
                    float[] verts = helperVerts;
                    int arraysize = 6 * sizeof(float);

                    //Origin Point
                    verts[0] = worldPosition.X;
                    verts[1] = worldPosition.Y;
                    verts[2] = worldPosition.Z;

                    ep.X += worldPosition.X;
                    ep.Y += worldPosition.Y;
                    ep.Z += worldPosition.Z;

                    verts[3] = ep.X;
                    verts[4] = ep.Y;
                    verts[5] = ep.Z;

                    //The instances are rebuilt for every camera move, but the light only moves with its parents
                    if (!helperUploaded || !verts.AsSpan().SequenceEqual(uploadedVerts))
                    {
                        GL.BindVertexArray(meshVao.vao.vao_id);
                        GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                        //Add verts data, color data should stay the same
                        GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
                        Array.Copy(verts, uploadedVerts, 6);
                        helperUploaded = true;
                        RenderStats.lightUploads++;
                    }

                    //Upload worldMat to the meshVao
                    instanceId = GLMeshBufferManager.addInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity); //Add instance
                }
                else
                    updated = false; //All done
            }

            RenderStats.lightTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            RenderStats.lightCount++;
        }

        public override void update()
        {
            base.update();

            //End Point
            Vector4 ep;
            //Lights with 360 FOV are points
            if (Math.Abs(FOV - 360.0f) <= 1e-4)
            {
                ep = new Vector4(0.0f, 0.0f, 0.0f, 1.0f);
                ep = ep * _localRotation;
                light_type = LIGHT_TYPE.POINT;
            }
            else
            {
                ep = new Vector4(0.0f, 0.0f, -1.0f, 0.0f);
                ep = ep * _localRotation;
                light_type = LIGHT_TYPE.SPOT;
            }

            ep.Normalize();

            direction = ep.Xyz; //Set spotlight direction
            update_struct();

            //Assume that this is a point light for now
            //Right
            lightSpaceMatrices[0] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Left
            lightSpaceMatrices[1] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(-1.0f, 0.0f, 0.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Up
            lightSpaceMatrices[2] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, -1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Down
            lightSpaceMatrices[3] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 1.0f, 0.0f),
                    new Vector3(0.0f, 0.0f, 1.0f));
            //Near
            lightSpaceMatrices[4] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, 1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
            //Far
            lightSpaceMatrices[5] = Matrix4.LookAt(worldPosition,
                    worldPosition + new Vector3(0.0f, 0.0f, -1.0f),
                    new Vector3(0.0f, -1.0f, 0.0f));
        }

        public void update_struct()
        {
            Vector4 old_pos = strct.position;
            strct.position = new Vector4((new Vector4(worldPosition, 1.0f) * RenderState.rotMat).Xyz, renderable ? 1.0f : 0.0f);
            strct.color = new Vector4(Color.Vec.Xyz, intensity);
            strct.direction = new Vector4(direction, MathUtils.radians(fov));
            strct.falloff = (int)falloff;
            strct.type = (light_type == LIGHT_TYPE.SPOT) ? 1.0f : 0.0f;

            if (old_pos != strct.position)
                update_changes = true;
        }

        public override Model Clone()
        {
            return new Light(this);
        }

        //Disposal
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                //Free other resources here
                base.Dispose(true);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }


}
