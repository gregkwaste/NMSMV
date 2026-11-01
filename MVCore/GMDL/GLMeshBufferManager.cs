using System;
using OpenTK.Mathematics;
using MVCore.Utils;

namespace MVCore.GMDL
{
    public static class GLMeshBufferManager
    {
        public const int instanceData_Float_Offset = 4;

        //Relative Instance Offsets

        //public static int instance_Uniforms_Offset = 0;
        public const int instance_Uniforms_Float_Offset = 0;
        //public static int instance_worldMat_Offset = 64;
        public const int instance_worldMat_Float_Offset = 16;
        //public static int instance_normalMat_Offset = 128;
        public const int instance_normalMat_Float_Offset = 32;
        //public static int instance_worldMatInv_Offset = 192;
        public const int instance_worldMatInv_Float_Offset = 48;
        //public static int instance_isOccluded_Offset = 256;
        public const int instance_isOccluded_Float_Offset = 64;
        //public static int instance_isSelected_Offset = 260;
        public const int instance_isSelected_Float_Offset = 65;
        public const int instance_colourisePrimary_Float_Offset = 68;
        public const int instance_colouriseSecondary_Float_Offset = 72;
        public const int instance_colouriseTernary_Float_Offset = 76;
        public const int instance_colouriseQuaternary_Float_Offset = 80;

        public const int instance_struct_size_floats = 84;

        private static void markDirty(GLMeshVao mesh, int instance_id)
        {
            unchecked { mesh.instanceVersionCounter++; }
            if (mesh.instanceVersionCounter == 0)
                mesh.instanceVersionCounter = 1;
            mesh.instanceVersions[instance_id] = mesh.instanceVersionCounter;
        }

        //Instance Data Format:
        //0-16 : instance WorldMatrix
        //16-17: isOccluded
        //17-18: isSelected
        //18-20: padding

        //Float index where an instance starts in the mesh data buffer. The buffer starts with a
        //4 float header (color, skinned), matching _COMMON_PER_MESH in the shaders
        public static int instanceBase(int instance_id)
        {
            return instanceData_Float_Offset + instance_id * instance_struct_size_floats;
        }

        //Grow the data buffer so that instance_id fits. Doubling keeps reallocations rare
        private static void ensureInstanceCapacity(GLMeshVao mesh, int instance_id)
        {
            int required = instanceBase(instance_id + 1);
            if (required <= mesh.dataBuffer.Length)
                return;

            float[] newBuffer = new float[Math.Max(required, 2 * mesh.dataBuffer.Length)];
            Array.Copy(mesh.dataBuffer, newBuffer, mesh.dataBuffer.Length);
            mesh.dataBuffer = newBuffer;

            int instances = Math.Max(instance_id + 1,
                (newBuffer.Length - instanceData_Float_Offset) / instance_struct_size_floats);
            Array.Resize(ref mesh.instanceVersions, instances);
            for (int ring = 0; ring < mesh.uploadedInstanceVersions.Length; ring++)
                Array.Resize(ref mesh.uploadedInstanceVersions[ring], instances);
        }

        private static void ensureVisibleCapacity(GLMeshVao mesh, int count)
        {
            if (count <= mesh.visibleInstanceIds.Length)
                return;
            Array.Resize(ref mesh.visibleInstanceIds, Math.Max(count, 2 * mesh.visibleInstanceIds.Length));
        }

        private static int registerInstance(GLMeshVao mesh, Model m, Matrix4 worldMat,
                                            Matrix4 worldMatInv, Matrix4 normMat)
        {
            if (mesh.stableInstanceIds.TryGetValue(m, out int existing))
            {
                m.instanceId = existing;
                return existing;
            }

            int instance_id = mesh.stable_instance_count++;
            ensureInstanceCapacity(mesh, instance_id);
            setInstanceWorldMat(mesh, instance_id, worldMat);
            setInstanceWorldMatInv(mesh, instance_id, worldMatInv);
            setInstanceNormalMat(mesh, instance_id, normMat);
            mesh.dataBuffer[instanceBase(instance_id) + instance_isOccluded_Float_Offset] = 0.0f;
            mesh.dataBuffer[instanceBase(instance_id) + instance_isSelected_Float_Offset] = 0.0f;
            writeVector(mesh, instance_id, instance_colourisePrimary_Float_Offset, Vector4.One);
            writeVector(mesh, instance_id, instance_colouriseSecondary_Float_Offset, Vector4.One);
            writeVector(mesh, instance_id, instance_colouriseTernary_Float_Offset, Vector4.One);
            writeVector(mesh, instance_id, instance_colouriseQuaternary_Float_Offset, Vector4.One);
            mesh.instanceRefs.Add(m);
            mesh.stableInstanceIds.Add(m, instance_id);
            m.instanceId = instance_id;
            markDirty(mesh, instance_id);
            return instance_id;
        }

        public static int registerInstance(GLMeshVao mesh, Model m)
        {
            //Joint helper vertices are already in world space. Applying the joint's
            //world matrix again separates bone endpoints and distorts the skeleton.
            if (m is Joint)
                return registerInstance(mesh, m, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity);
            Matrix4 inverse = m.getWorldMatInv();
            return registerInstance(mesh, m, m.worldMat, inverse, Matrix4.Transpose(inverse));
        }

        private static void addVisibleInstance(GLMeshVao mesh, int stable_id)
        {
            ensureVisibleCapacity(mesh, mesh.instance_count + 1);
            mesh.visibleInstanceIds[mesh.instance_count++] = stable_id;
        }

        public static int addInstance(ref GLMeshVao mesh, Model m)
        {
            int instance_id = registerInstance(mesh, m);
            addVisibleInstance(mesh, instance_id);
            return instance_id;
        }

        //Overload with transform overrides
        public static int addInstance(GLMeshVao mesh, Model m, Matrix4 worldMat, Matrix4 worldMatInv, Matrix4 normMat)
        {
            int instance_id = registerInstance(mesh, m, worldMat, worldMatInv, normMat);
            addVisibleInstance(mesh, instance_id);
            return instance_id;
        }

        public static void clearInstances(GLMeshVao mesh)
        {
            mesh.instance_count = 0;
        }

        public static void resetInstances(GLMeshVao mesh)
        {
            foreach (Model m in mesh.instanceRefs)
            {
                if (ReferenceEquals(m.meshVao, mesh))
                    m.instanceId = -1;
            }
            mesh.instanceRefs.Clear();
            mesh.stableInstanceIds.Clear();
            mesh.stable_instance_count = 0;
            clearInstances(mesh);
        }

        public static void setInstanceSelectedStatus(GLMeshVao mesh, int instance_id, bool status)
        {
            float value = status ? 1.0f : 0.0f;
            int offset = instanceBase(instance_id) + instance_isSelected_Float_Offset;
            if (mesh.dataBuffer[offset] == value)
                return;

            unsafe
            {
                mesh.dataBuffer[offset] = value;
            }
            markDirty(mesh, instance_id);
        }

        public static void setInstanceUniform4(GLMeshVao mesh, int instance_id, string un_name, Vector4 un)
        {
            unsafe
            {
                int offset = instanceBase(instance_id) + instance_Uniforms_Float_Offset;
                int uniform_id = 0;
                switch (un_name)
                {
                    case "gUserDataVec4":
                        uniform_id = 0;
                        break;
                    case "gColourisePrimary":
                        uniform_id = instance_colourisePrimary_Float_Offset / 4;
                        break;
                    case "gColouriseSecondary":
                        uniform_id = instance_colouriseSecondary_Float_Offset / 4;
                        break;
                    case "gColouriseTernary":
                        uniform_id = instance_colouriseTernary_Float_Offset / 4;
                        break;
                    case "gColouriseQuaternary":
                        uniform_id = instance_colouriseQuaternary_Float_Offset / 4;
                        break;
                }

                offset += uniform_id * 4;

                if (mesh.dataBuffer[offset] == un.X &&
                    mesh.dataBuffer[offset + 1] == un.Y &&
                    mesh.dataBuffer[offset + 2] == un.Z &&
                    mesh.dataBuffer[offset + 3] == un.W)
                    return;

                mesh.dataBuffer[offset] = un.X;
                mesh.dataBuffer[offset + 1] = un.Y;
                mesh.dataBuffer[offset + 2] = un.Z;
                mesh.dataBuffer[offset + 3] = un.W;
            }
            markDirty(mesh, instance_id);
        }

        public static void setInstanceWorldMat(GLMeshVao mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceBase(instance_id) + instance_worldMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
            markDirty(mesh, instance_id);
        }

        public static void setInstanceWorldMatInv(GLMeshVao mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceBase(instance_id) + instance_worldMatInv_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
            markDirty(mesh, instance_id);
        }

        public static void setInstanceNormalMat(GLMeshVao mesh, int instance_id, Matrix4 mat)
        {
            unsafe
            {
                fixed (float* ar = mesh.dataBuffer)
                {
                    int offset = instanceBase(instance_id) + instance_normalMat_Float_Offset;
                    MathUtils.insertMatToArray16(ar, offset, mat);
                }
            }
            markDirty(mesh, instance_id);
        }

        private static void writeVector(GLMeshVao mesh, int instance_id, int relativeOffset, Vector4 value)
        {
            int offset = instanceBase(instance_id) + relativeOffset;
            mesh.dataBuffer[offset] = value.X;
            mesh.dataBuffer[offset + 1] = value.Y;
            mesh.dataBuffer[offset + 2] = value.Z;
            mesh.dataBuffer[offset + 3] = value.W;
        }


    }
}
