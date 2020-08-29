using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.InteropServices;
using OpenTK;
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

        public bool update_changes = false; //Used to prevent unecessary uploads to the UBO

        //Light Projection + View Matrices
        public Matrix4[] lightSpaceMatrices;
        public Matrix4 lightProjectionMatrix;
        public GLLight strct;

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

        protected Light(Light input) : base(input)
        {
            Color = input.Color;
            intensity = input.intensity;
            falloff = input.falloff;
            fov = input.fov;
            strct = input.strct;

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

        public override void updateMeshInfo()
        {
            if (RenderState.renderViewSettings.RenderLights && renderable)
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

                //Update Vertex Buffer based on the new data
                float[] verts = new float[6];
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

                GL.BindVertexArray(meshVao.vao.vao_id);
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                //Add verts data, color data should stay the same
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);

                //Uplod worldMat to the meshVao
                instanceId = GLMeshBufferManager.addInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity); //Add instance
            }

            base.updateMeshInfo();
            updated = false; //All done
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
