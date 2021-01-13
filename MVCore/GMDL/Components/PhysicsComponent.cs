using System;
using libMBIN.NMS.Toolkit;

namespace MVCore.GMDL
{
    public class PhysicsData : TkPhysicsData
    {
        public PhysicsData() : base()
        {

        }

        public PhysicsData(TkPhysicsData pd)
        {
            AngularDamping = pd.AngularDamping;
            Friction = pd.Friction;
            RollingFriction = pd.RollingFriction;
            Gravity = pd.Gravity;
            LinearDamping = pd.LinearDamping;
            Mass = pd.Mass;
        }

        //Properties

        public float PMass
        {
            get { return Mass; }
            set { Mass = value; }
        }

        public float PGravity
        {
            get { return Gravity; }
            set { Gravity = value; }
        }

        public float PAngularDamping
        {
            get { return AngularDamping; }
            set { AngularDamping = value; }
        }

        public float PFriction
        {
            get { return Friction; }
            set { Friction = value; }
        }

        public float PRollingFriction
        {
            get { return RollingFriction; }
            set { RollingFriction = value; }
        }

        public float PLinearDamping
        {
            get { return LinearDamping; }
            set { LinearDamping = value; }
        }

    }

    
    public class PhysicsComponent : Component
    {
        private TkPhysicsComponentData _template;
        public PhysicsData data;

        //Default Constructor
        public PhysicsComponent()
        {
            _template = new TkPhysicsComponentData();
            data = new PhysicsData();
        }

        public PhysicsComponent(PhysicsComponent pc)
        {
            _template = new TkPhysicsComponentData()
            {
                AllowTeleporter = pc._template.AllowTeleporter,
                BlockTeleporter = pc._template.BlockTeleporter,
                Data = pc._template.Data,
                SpinOnCreate = pc._template.SpinOnCreate,
                DisableGravity = pc._template.DisableGravity,
                InvisibleForInteraction = pc._template.InvisibleForInteraction,
                CameraInvisible = pc._template.CameraInvisible,
                NoPlayerCollide = pc._template.NoPlayerCollide,
                NoVehicleCollide = pc._template.NoVehicleCollide,
                IgnoreModelOwner = pc._template.IgnoreModelOwner,
                Floor = pc._template.Floor,
                Climbable = pc._template.Climbable,
                TriggerVolume = pc._template.TriggerVolume,
                SurfaceProperties = pc._template.SurfaceProperties,
                VolumeTriggerType = pc._template.VolumeTriggerType,
                RagdollData = pc._template.RagdollData
            };
            data = new PhysicsData(pc.data);
        }

        public PhysicsComponent(TkPhysicsComponentData pcd)
        {
            _template = pcd;
            data = new PhysicsData(pcd.Data);
        }

        public override Component Clone()
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
            get { return _template.Climbable; }
        }

        public bool Floor
        {
            get { return _template.Floor; }
        }

        public bool IgnoreModelOwner
        {
            get { return _template.IgnoreModelOwner; }
        }

        public bool TriggerVolume
        {
            get { return _template.TriggerVolume; }
        }

        public bool NoVehicleCollide
        {
            get { return _template.NoVehicleCollide; }
        }

        public bool NoPlayerCollide
        {
            get { return _template.NoPlayerCollide; }
        }

        public bool CameraInvisible
        {
            get { return _template.CameraInvisible; }
        }

        public bool InvisibleForInteraction
        {
            get { return _template.InvisibleForInteraction; }
        }

        public bool AllowTeleporter
        {
            get { return _template.AllowTeleporter; }
        }

        public bool BlockTeleporter
        {
            get { return _template.BlockTeleporter; }
        }

        public bool DisableGravity
        {
            get { return _template.DisableGravity; }
        }

        public float SpinOnCreate
        {
            get { return _template.SpinOnCreate; }
        }

        
    }
}
