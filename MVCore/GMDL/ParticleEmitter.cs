using System;
using System.Reflection;
using libMBIN;
using libMBIN.NMS;
using libMBIN.NMS.Toolkit;
using OpenTK.Mathematics;

namespace MVCore.GMDL
{
    //Scene EMITTER nodes are locators with an authored particle material and a reference to the
    //authoring-time particle definition. The shipped game archives do not contain that definition,
    //so ParticleSystem supplies a conservative viewer profile while retaining both original paths.
    public class ParticleEmitter : Locator
    {
        private bool particlesVisible = true;

        public string MaterialPath { get; set; } = "";
        public string DataPath { get; set; } = "";
        public Material ParticleMaterial { get; set; }

        private float emissionRate;
        private float particleLifetime = 1.0f;
        private float speed;
        private float spread;
        private float gravity;
        private float startSize;
        private float endSize;
        private int particleLimit;

        //ParticleSystem selects the initial preset; Object Info can edit every value used by simulation.
        public string ProfileName { get; internal set; } = "unassigned";
        public bool RuntimeParametersInitialized { get; internal set; }
        public float EmissionRate
        {
            get { return emissionRate; }
            set { emissionRate = MathHelper.Clamp(value, 0.0f, 10000.0f); }
        }
        public float ParticleLifetime
        {
            get { return particleLifetime; }
            set { particleLifetime = MathHelper.Clamp(value, 0.01f, 3600.0f); }
        }
        public float Speed
        {
            get { return speed; }
            set { speed = MathHelper.Clamp(value, -100.0f, 100.0f); }
        }
        public float Spread
        {
            get { return spread; }
            set { spread = MathHelper.Clamp(value, 0.0f, 100.0f); }
        }
        public float Gravity
        {
            get { return gravity; }
            set { gravity = MathHelper.Clamp(value, -100.0f, 100.0f); }
        }
        public float StartSize
        {
            get { return startSize; }
            set { startSize = MathHelper.Clamp(value, 0.0f, 1000.0f); }
        }
        public float EndSize
        {
            get { return endSize; }
            set { endSize = MathHelper.Clamp(value, 0.0f, 1000.0f); }
        }
        public bool Additive { get; set; }
        public int ParticleLimit
        {
            get { return particleLimit; }
            set { particleLimit = System.Math.Clamp(value, 0, 8192); }
        }

        public string MaterialClass => ParticleMaterial?.PClass ?? "<none>";

        public bool ParticlesVisible
        {
            get { return particlesVisible; }
            set
            {
                if (particlesVisible == value)
                    return;
                particlesVisible = value;
            }
        }

        public ParticleEmitter()
        {
            type = TYPES.EMITTER;
        }

        public override void updateOwnMeshInfo(bool lod_filter = false)
        {
            ParticlesVisible = renderable && !lod_filter;
            base.updateOwnMeshInfo(lod_filter);
        }

        //The DATA attribute is an authoring-file reference and is not useful when a node is
        //exported from the viewer. Export creates a new particle definition beside the scene and
        //points the exported node at it instead.
        public void SetExportParticleDataPath(string path)
        {
            DataPath = path ?? "";
        }

        public TkParticleData CreateParticleTemplate()
        {
            TkParticleData data = new TkParticleData();
            initializeTemplateFields(data);

            data.StartEnabled = IsRenderable;
            data.GPURender = true;
            data.MaxCount = ParticleLimit;
            data.EmitterDirection.X = 0.0f;
            data.EmitterDirection.Y = 1.0f;
            data.EmitterDirection.Z = 0.0f;
            data.EmitterSpreadAngleMin = 0.0f;
            data.EmitterSpreadAngle = Spread;

            setFixed(data.EmissionRate, EmissionRate);
            setFixed(data.EmitterLife, 0.0f);
            setFixed(data.ParticleLife, ParticleLifetime);
            setFixed(data.ParticleSpeedMultiplier, Speed);
            setFixed(data.ParticleGravity, Gravity);
            setFixed(data.ParticleDamping, 0.0f);
            setFixed(data.ParticleDrag, 0.0f);
            setFixed(data.ParticleSizeY, 1.0f);
            setFixed(data.AlphaThreshold, 0.0f);
            setFixed(data.Rotation, 0.0f);
            setFixed(data.TrackEmitterPosition, 0.0f);
            setFixed(data.BurstData.BurstAmount, 0.0f);
            setFixed(data.BurstData.BurstInterval, 0.0f);

            setCurve(data.ParticleSize.GeneralSize, StartSize, EndSize);
            data.ParticleSize.Min = Math.Min(StartSize, EndSize);
            data.ParticleSize.Max = Math.Max(StartSize, EndSize);

            data.ColourStart.R = data.ColourStart.G = data.ColourStart.B = data.ColourStart.A = 1.0f;
            data.ColourMiddle.R = data.ColourMiddle.G = data.ColourMiddle.B = data.ColourMiddle.A = 1.0f;
            data.ColourEnd.R = data.ColourEnd.G = data.ColourEnd.B = data.ColourEnd.A = 1.0f;
            return data;
        }

        private static void setFixed(TkEmitterFloatProperty property, float value)
        {
            property.Authoring = TkEmitterFloatProperty.AuthoringEnum.FixedValue;
            property.FixedValue = value;
            property.MinRandomValue = value;
            property.MaxRandomValue = value;
            property.CurveStartValue = value;
            property.CurveMidValue = value;
            property.CurveEndValue = value;
        }

        private static void setCurve(TkEmitterFloatProperty property, float start, float end)
        {
            property.Authoring = TkEmitterFloatProperty.AuthoringEnum.Curves;
            property.CurveStartValue = start;
            property.CurveMidValue = (start + end) * 0.5f;
            property.CurveEndValue = end;
            property.CurveBlendMidpoint = 0.5f;
            property.Curve1Shape.Curve = TkCurveType.CurveEnum.Linear;
            property.Curve2Shape.Curve = TkCurveType.CurveEnum.Linear;
        }

        //libMBIN templates do not populate their nested records in their constructors. Build the
        //complete default graph so both writers can serialize it. Fixed-size fields use the size in
        //the schema, while an EmptyNode is libMBIN's MBIN representation of an absent NextStage.
        private static void initializeTemplateFields(object instance, int depth = 0)
        {
            if (instance == null || depth > 16)
                return;

            foreach (FieldInfo field in instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                object value = field.GetValue(instance);
                if (value == null)
                {
                    Type fieldType = field.FieldType;
                    if (fieldType == typeof(NMSTemplate))
                    {
                        value = new EmptyNode();
                    }
                    else if (fieldType.IsArray)
                    {
                        NMSAttribute schema = field.GetCustomAttribute<NMSAttribute>();
                        value = Array.CreateInstance(fieldType.GetElementType(), schema?.Size ?? 0);
                    }
                    else if (!fieldType.IsAbstract && fieldType.GetConstructor(System.Type.EmptyTypes) != null)
                    {
                        value = Activator.CreateInstance(fieldType);
                    }

                    if (value != null)
                        field.SetValue(instance, value);
                }

                if (value != null && !field.FieldType.IsValueType &&
                    field.FieldType != typeof(string) && !field.FieldType.IsArray)
                    initializeTemplateFields(value, depth + 1);
            }
        }

        public override Model Clone()
        {
            ParticleEmitter clone = new ParticleEmitter();
            clone.copyFrom(this);
            clone.MaterialPath = MaterialPath;
            clone.DataPath = DataPath;
            clone.ParticleMaterial = ParticleMaterial;
            clone.ParticlesVisible = ParticlesVisible;
            clone.ProfileName = ProfileName;
            clone.RuntimeParametersInitialized = RuntimeParametersInitialized;
            clone.EmissionRate = EmissionRate;
            clone.ParticleLifetime = ParticleLifetime;
            clone.Speed = Speed;
            clone.Spread = Spread;
            clone.Gravity = Gravity;
            clone.StartSize = StartSize;
            clone.EndSize = EndSize;
            clone.Additive = Additive;
            clone.ParticleLimit = ParticleLimit;

            foreach (Model child in Children)
                clone.AddChild(child.Clone());

            return clone;
        }
    }
}
