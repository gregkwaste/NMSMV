using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using OpenTK.Graphics.OpenGL4;

namespace MVCore.GMDL
{
    public class Joint : Model
    {
        public int jointIndex;
        public Vector3 color;

        //Add a bunch of shit for posing
        //public Vector3 _localPosePosition = new Vector3(0.0f);
        //public Matrix4 _localPoseRotation = Matrix4.Identity;
        //public Vector3 _localPoseScale = new Vector3(1.0f);
        public Matrix4 BindMat = Matrix4.Identity; //This is the local Bind Matrix related to the parent joint
        public Matrix4 invBMat = Matrix4.Identity; //This is the inverse of the local Bind Matrix related to the parent
                                                   //DO NOT MIX WITH THE gobject.invBMat which is reverts the transformation to the global space

        //Props
        public Matrix4 localPoseMatrix
        {
            get { return _localPoseMatrix; }
            set { _localPoseMatrix = value; updated = true; }
        }

        public Joint()
        {
            type = TYPES.JOINT;
        }

        protected Joint(Joint input) : base(input)
        {
            this.jointIndex = input.jointIndex;
            this.BindMat = input.BindMat;
            this.invBMat = input.invBMat;
            this.color = input.color;

            meshVao = new GLMeshVao();
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
            GLMeshBufferManager.setInstanceWorldMat(meshVao, instanceId, Matrix4.Identity);
            meshVao.type = TYPES.JOINT;
            meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the joint GLMeshVAOs
            meshVao.vao = new Primitives.LineSegment(children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];
        }

        public override void updateMeshInfo()
        {
            //We do not apply frustum occlusion on joint objects
            if (renderable && (children.Count > 0))
            {
                //Update Vertex Buffer based on the new positions
                float[] verts = new float[2 * children.Count * 3];
                int arraysize = 2 * children.Count * 3 * sizeof(float);

                for (int i = 0; i < children.Count; i++)
                {
                    verts[i * 6 + 0] = worldPosition.X;
                    verts[i * 6 + 1] = worldPosition.Y;
                    verts[i * 6 + 2] = worldPosition.Z;
                    verts[i * 6 + 3] = children[i].worldPosition.X;
                    verts[i * 6 + 4] = children[i].worldPosition.Y;
                    verts[i * 6 + 5] = children[i].worldPosition.Z;
                }

                meshVao.metaData.batchcount = 2 * children.Count;

                GL.BindVertexArray(meshVao.vao.vao_id);
                GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                //Add verts data, color data should stay the same
                GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
                instanceId = GLMeshBufferManager.addInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity);
            }

            base.updateMeshInfo();
        }

        public override Model Clone()
        {
            Joint j = new Joint();
            j.copyFrom(this);

            j.jointIndex = this.jointIndex;
            j.BindMat = this.BindMat;
            j.invBMat = this.invBMat;
            j.color = this.color;

            j.meshVao = new GLMeshVao();
            j.instanceId = GLMeshBufferManager.addInstance(ref j.meshVao, j);
            GLMeshBufferManager.setInstanceWorldMat(j.meshVao, j.instanceId, Matrix4.Identity);
            j.meshVao.type = TYPES.JOINT;
            j.meshVao.metaData = new MeshMetaData();
            //TODO: Find a place to keep references from the Joint GLMeshVAOs
            j.meshVao.vao = new Primitives.LineSegment(this.children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            j.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["JointMat"];

            //Clone children
            foreach (Model child in children)
            {
                Model new_child = child.Clone();
                new_child.parent = j;
                j.children.Add(new_child);
            }

            return j;
        }

        //DIsposal
        protected override void Dispose(bool disposing)
        {
            //Dispose GL Stuff
            meshVao?.Dispose();
            base.Dispose(disposing);
        }
    }


}
