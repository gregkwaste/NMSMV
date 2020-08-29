using System;
using System.Collections.Generic;
using System.Text;
using libMBIN.NMS.Toolkit;

namespace MVCore.GMDL
{
    public class LODModelComponent : Component
    {
        private List<LODModelResource> _resources;

        //Properties
        public List<LODModelResource> Resources => _resources;

        public LODModelComponent()
        {
            _resources = new List<LODModelResource>();
        }

        public override Component Clone()
        {
            LODModelComponent lmc = new LODModelComponent();
            return lmc;
        }


    }

    public class LODModelResource
    {
        private string _filename;
        private float _crossFadeTime;
        private float _crossFadeoverlap;

        //Properties
        public string Filename
        {
            get
            {
                return _filename;
            }
        }

        public LODModelResource(TkLODModelResource res)
        {
            _filename = res.LODModel.Filename;
            _crossFadeTime = res.CrossFadeTime;
            _crossFadeoverlap = res.CrossFadeOverlap;
        }
    }
}
