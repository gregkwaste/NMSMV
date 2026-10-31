using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;


namespace MVCore.GMDL
{
    public class Reference : Locator
    {
        public String ref_scene_filepath { get;} //holds the referenced scene file path
        private bool _isRefLoaded = false;

        public override string Name
        {
            get
            {
                if (_isRefLoaded)
                    return name + " - (Loaded)";
                else
                    return name + " - (Unloaded)";
            }
            set
            {
                base.Name = value;
            }
        }
        
        public bool isRefLoaded
        {
            get
            {
                return _isRefLoaded;
            }

            set
            {
                _isRefLoaded = value;
                NotifyPropertyChanged("isRefLoaded");
                NotifyPropertyChanged("Name");
            }
        }
        
        public Reference(string path)
        {
            type = TYPES.REFERENCE;
            ref_scene_filepath = path;
        }

        private Reference(Reference input)
        {
            //Copy info
            base.copyFrom(input);
            ref_scene_filepath = input.ref_scene_filepath;
            isRefLoaded = input.isRefLoaded;
        }

        public override Model Clone()
        {
            //The loaded scene comes along, so references inside a copied scene stay loaded
            Reference r = new Reference(this);
            foreach (Model child in Children)
                r.AddChild(child.Clone());
            return r;
        }

        public override void setParentScene(Scene animscene)
        {
            //DO NOTHING
        }

    }
}
