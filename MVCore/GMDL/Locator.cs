using System;
using System.Collections.Generic;
using System.Text;
using OpenTK;
using MVCore.Common;



namespace MVCore.GMDL
{
    public class Locator : Model
    {
        public Locator()
        {
            //Set type
            type = TYPES.LOCATOR;
            //Set BBOX
            AABBMIN = new Vector3(-0.1f, -0.1f, -0.1f);
            AABBMAX = new Vector3(0.1f, 0.1f, 0.1f);

            //Assemble geometry in the constructor
            meshVao = Common.RenderState.activeResMgr.GLPrimitiveMeshVaos["default_cross"];
            instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
        }

        public void copyFrom(Locator input)
        {
            base.copyFrom(input); //Copy stuff from base class
        }

        protected Locator(Locator input) : base(input)
        {
            this.copyFrom(input);
        }

        public override GMDL.Model Clone()
        {
            Locator new_s = new Locator();
            new_s.copyFrom(this);

            //Clone children
            foreach (Model child in children)
            {
                Model new_child = child.Clone();
                new_child.parent = new_s;
                new_s.children.Add(new_child);
            }

            return new_s;
        }

        public override void update()
        {
            base.update();
            recalculateAABB(); //Update AABB
        }

        public override void updateMeshInfo()
        {
            if (!renderable)
            {
                base.updateMeshInfo();
                return;
            }

            bool fr_status = Common.RenderState.activeCam.frustum_occlude(meshVao, worldMat * RenderState.rotMat);
            bool occluded_status = !fr_status && Common.RenderState.renderSettings.UseFrustumCulling;

            //Recalculations && Data uploads
            if (!occluded_status)
            {
                instanceId = GLMeshBufferManager.addInstance(ref meshVao, this);
            }

            base.updateMeshInfo();
            updated = false; //All done
        }


        #region IDisposable Support
        protected override void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    meshVao = null; //VAO will be deleted from the resource manager since it is a common mesh
                    base.Dispose(disposing);
                }
                disposed = true;
            }
        }
        #endregion

    }
}
