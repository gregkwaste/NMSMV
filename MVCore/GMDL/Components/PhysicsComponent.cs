using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.Collections.Generic;
using System.Text;
using System.Windows.Input;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using OpenTK.Graphics.OpenGL;
using libMBIN.NMS.GameComponents;

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

        public PhysicsComponent(TkPhysicsComponentData pcd)
        {
            _template = pcd;
            data = new PhysicsData(pcd.Data);
        
        }

        public override Component Clone()
        {
            throw new Exception("Not Implemented");
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
