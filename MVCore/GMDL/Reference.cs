using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;


namespace MVCore.GMDL
{
    public class Reference : Locator
    {
        public Model ref_scene; //holds the referenced scene

        public Reference()
        {
            type = TYPES.REFERENCE;
        }

        public Reference(Reference input)
        {
            //Copy info
            base.copyFrom(input);

            ref_scene = input.ref_scene.Clone();
            ref_scene.parent = this;
            children.Add(ref_scene);
        }

        public void copyFrom(Reference input)
        {
            base.copyFrom(input); //Copy base stuff
            this.ref_scene = input.ref_scene;
        }

        public override Model Clone()
        {
            return new Reference(this);
        }

        public override TkSceneNodeData ExportTemplate(bool keepRenderable)
        {
            throw new NotImplementedException("Functionality not yet supported");
        }


        public override void setParentScene(Scene animscene)
        {
            //DO NOTHING
        }

        //Deconstructor
        protected override void Dispose(bool disposing)
        {
            if (disposed)
                return;

            if (disposing)
            {
                handle.Dispose();

                //Free other resources here
                base.Dispose(disposing);
            }

            //Free unmanaged resources
            disposed = true;
        }
    }
}
