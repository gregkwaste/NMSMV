using System;
using System.Collections.Generic;
using System.Text;
using OpenTK.Mathematics;
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
            set { _localPoseMatrix = value; updated = true; Common.RenderState.instancesDirty = true; }
        }

        public Joint()
        {
            type = TYPES.JOINT;
        }

        //Lines to the children as last uploaded. Joints are rebuilt for every camera move but only move
        //with the skeleton, so the upload only happens when a line changed
        private float[] verts;
        private float[] uploadedVerts;

        public override void updateOwnMeshInfo(bool lod_filter=false)
        {
            long t0 = System.Diagnostics.Stopwatch.GetTimestamp();

            //We do not apply frustum occlusion on joint objects
            if (Common.RenderState.renderViewSettings.RenderJoints && !lod_filter && renderable && (Children.Count > 0))
            {
                //Update Vertex Buffer based on the new positions
                int floats = 2 * Children.Count * 3;
                int arraysize = floats * sizeof(float);
                if (verts == null || verts.Length != floats)
                {
                    verts = new float[floats];
                    uploadedVerts = null;
                }

                for (int i = 0; i < Children.Count; i++)
                {
                    verts[i * 6 + 0] = worldPosition.X;
                    verts[i * 6 + 1] = worldPosition.Y;
                    verts[i * 6 + 2] = worldPosition.Z;
                    verts[i * 6 + 3] = Children[i].worldPosition.X;
                    verts[i * 6 + 4] = Children[i].worldPosition.Y;
                    verts[i * 6 + 5] = Children[i].worldPosition.Z;
                }

                meshVao.metaData.batchcount = 2 * Children.Count;

                if (uploadedVerts == null || !verts.AsSpan().SequenceEqual(uploadedVerts))
                {
                    GL.BindVertexArray(meshVao.vao.vao_id);
                    GL.BindBuffer(BufferTarget.ArrayBuffer, meshVao.vao.vertex_buffer_object);
                    //Add verts data, color data should stay the same
                    GL.BufferSubData(BufferTarget.ArrayBuffer, (IntPtr)0, (IntPtr)arraysize, verts);
                    uploadedVerts = (float[]) verts.Clone();
                    Common.RenderStats.jointUploads++;
                }
                instanceId = GLMeshBufferManager.addInstance(meshVao, this, Matrix4.Identity, Matrix4.Identity, Matrix4.Identity);
            }

            Common.RenderStats.jointTicks += System.Diagnostics.Stopwatch.GetTimestamp() - t0;
            Common.RenderStats.jointCount++;
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
            j.meshVao.vao = new Primitives.LineSegment(Children.Count, new Vector3(1.0f, 0.0f, 0.0f)).getVAO();
            j.meshVao.material = Common.RenderState.activeResMgr.GLmaterials["jointMat"];
            
            //Clone children
            foreach (Model child in Children)
            {
                Model new_child = child.Clone();
                j.AddChild(new_child);
            }

            return j;
        }

        //Disposal
        protected override void Dispose(bool disposing)
        {
            //Dispose GL Stuff
            meshVao?.Dispose();
            base.Dispose(disposing);
        }
    }


}
