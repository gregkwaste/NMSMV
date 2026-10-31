using System;
using libMBIN.NMS.Toolkit;

namespace MVCore.GMDL
{
    public class PhysicsData
    {
        //The physics data of the entity file
        public TkPhysicsData Template;

        public PhysicsData()
        {
            Template = new TkPhysicsData();
        }

        public PhysicsData(TkPhysicsData pd)
        {
            Template = pd;
        }

        //Properties

        public float PMass
        {
            get { return Template.Mass; }
            set { Template.Mass = value; }
        }

        public float PGravity
        {
            get { return Template.Gravity; }
            set { Template.Gravity = value; }
        }

        public float PAngularDamping
        {
            get { return Template.AngularDamping; }
            set { Template.AngularDamping = value; }
        }

        public float PFriction
        {
            get { return Template.Friction; }
            set { Template.Friction = value; }
        }

        public float PRollingFriction
        {
            get { return Template.RollingFriction; }
            set { Template.RollingFriction = value; }
        }

        public float PLinearDamping
        {
            get { return Template.LinearDamping; }
            set { Template.LinearDamping = value; }
        }

    }

    
    public class PhysicsComponent : IComponent
    {
        //The component entry of the entity file
        public TkPhysicsComponentData Template;
        public PhysicsData data;

        //Default Constructor
        public PhysicsComponent()
        {
            Template = new TkPhysicsComponentData();
            Template.Data = new TkPhysicsData();
            data = new PhysicsData(Template.Data);
        }

        //Copies share the entity data
        public PhysicsComponent(PhysicsComponent pc) : this(pc.Template)
        {
        }

        public PhysicsComponent(TkPhysicsComponentData pcd)
        {
            Template = pcd;
            data = new PhysicsData(pcd.Data);
        }

        public IComponent Clone()
        {
            return new PhysicsComponent(this);
        }

        //Exposed Properties
        public PhysicsData Data
        {
            get { return data; }
        }

        public bool Climbable
        {
            get { return Template.Climbable; }
        }

        public bool Floor
        {
            get { return Template.Floor; }
        }

        public bool IgnoreModelOwner
        {
            get { return Template.IgnoreModelOwner; }
        }

        public bool TriggerVolume
        {
            get { return Template.TriggerVolume; }
        }

        public bool NoVehicleCollide
        {
            get { return Template.NoVehicleCollide; }
        }

        public bool NoPlayerCollide
        {
            get { return Template.NoPlayerCollide; }
        }

        public bool CameraInvisible
        {
            get { return Template.CameraInvisible; }
        }

        public bool InvisibleForInteraction
        {
            get { return Template.InvisibleForInteraction; }
        }

        public bool AllowTeleporter
        {
            get { return Template.AllowTeleporter; }
        }

        public bool BlockTeleporter
        {
            get { return Template.BlockTeleporter; }
        }

        public bool DisableGravity
        {
            get { return Template.DisableGravity; }
        }

        public float SpinOnCreate
        {
            get { return Template.SpinOnCreate; }
        }

        
    }
}
