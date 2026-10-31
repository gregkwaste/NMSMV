using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;

namespace MVCore.GMDL
{
    public class LODModelComponent : IComponent
    {
        //The component entry of the entity file
        public TkLODComponentData Template;
        private List<LODModelResource> _resources;

        //Properties
        public List<LODModelResource> Resources => _resources;

        public LODModelComponent()
        {
            Template = new TkLODComponentData();
            _resources = new List<LODModelResource>();
        }

        public LODModelComponent(TkLODComponentData data)
        {
            Template = data;
            _resources = new List<LODModelResource>();
        }

        public IComponent Clone()
        {
            //Copies share the entity data
            LODModelComponent lmc = new LODModelComponent(Template);
            lmc._resources.AddRange(_resources);
            return lmc;
        }


    }

    public class LODModelResource
    {
        //The LOD model entry of the entity file
        public TkLODModelResource Template;

        //Properties
        public string Filename
        {
            get
            {
                return Template.LODModel.Filename;
            }
        }

        public LODModelResource(TkLODModelResource res)
        {
            Template = res;
        }
    }
}
