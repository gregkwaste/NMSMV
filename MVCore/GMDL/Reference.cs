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
            //Copy main info
            TkSceneNodeData cpy = new TkSceneNodeData();

            cpy.Transform = nms_template.Transform;
            cpy.Attributes = nms_template.Attributes;
            cpy.Type = nms_template.Type;
            cpy.Name = nms_template.Name;
            cpy.NameHash = nms_template.NameHash;

            //The only difference with references is that the first children of the reference object is a copy of the
            //actual scene

            if (children.Count > 1)
                cpy.Children = new List<TkSceneNodeData>();

            for (int i = 1; i < children.Count; i++)
            {
                Model child = children[i];
                if (!child.renderable && keepRenderable)
                    continue;
                else if (child.nms_template != null)
                    cpy.Children.Add(child.ExportTemplate(keepRenderable));
            }

            return cpy;
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
