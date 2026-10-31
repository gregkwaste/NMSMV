using System;
using libMBIN.NMS.Toolkit;
using OpenTK.Mathematics;

namespace MVCore.GMDL
{
    //TkRotationComponentData: the node spins around Axis. Driven by the RotationSystem while animations are on
    public class RotationComponent : IComponent
    {
        public TkRotationComponentData Template;
        public float Angle = 0.0f; //Current spin in radians

        public RotationComponent(TkRotationComponentData rcd)
        {
            Template = rcd;
        }

        public IComponent Clone()
        {
            //Copies share the entity data
            return new RotationComponent(Template);
        }

        //Exposed Properties

        public Vector3 Axis
        {
            get
            {
                Vector3 axis = new Vector3(Template.Axis.X, Template.Axis.Y, Template.Axis.Z);
                return axis.LengthSquared > 0.0f ? axis.Normalized() : Vector3.UnitY;
            }
        }

        //Taken as radians per second
        public float Speed
        {
            get { return Template.Speed; }
        }

        public bool AlwaysUpdate
        {
            get { return Template.AlwaysUpdate; }
        }

        public string AxisText
        {
            get { return string.Format("{0:0.##}, {1:0.##}, {2:0.##}", Template.Axis.X, Template.Axis.Y, Template.Axis.Z); }
        }

        public string Description
        {
            get { return "axis (" + AxisText + "), speed " + Speed; }
        }
    }
}
