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

        public new string Name
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
                name = value;
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
                _isRefLoaded = true;
                NotifyPropertyChanged("isRefLoaded");
                NotifyPropertyChanged("Name");
            }
        }
        
        public Reference(string path)
        {
            type = TYPES.REFERENCE;
            ref_scene_filepath = path;
        }

        public Reference(Reference input)
        {
            //Copy info
            base.copyFrom(input);
            ref_scene_filepath = input.ref_scene_filepath;
            isRefLoaded = input.isRefLoaded;
        }

        public void copyFrom(Reference input)
        {
            //Use the constructor
        }

        public override Model Clone()
        {
            return new Reference(this);
        }

        public override TkSceneNodeData ExportTemplate()
        {
            return new TkSceneNodeData();
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
