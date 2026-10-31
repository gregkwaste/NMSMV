using System;
using System.Collections.Generic;
using MVCore.GMDL;
using OpenTK.Mathematics;

namespace MVCore.Engine.Systems
{
    //Spins the nodes with a RotationComponent on top of their rest rotation
    public class RotationSystem
    {
        private readonly List<Model> rotationScenes = new List<Model>();
        private readonly List<Model> activeScenes = new List<Model>();
        public IReadOnlyList<Model> RotationScenes => rotationScenes;
        public IReadOnlyList<Model> ActiveScenes => activeScenes; //Nodes rotated in the last update
        private readonly Dictionary<Model, Matrix4> restRotation = new Dictionary<Model, Matrix4>();
        public void CleanUp()
        {
            rotationScenes.Clear();
            activeScenes.Clear();
            restRotation.Clear();
        }

        public void update(float dt)
        {
            activeScenes.Clear();
            foreach (Model m in rotationScenes)
            {
                RotationComponent rc = m.Components[m.hasComponent(typeof(RotationComponent))] as RotationComponent;
                if (rc == null || rc.Speed == 0.0f)
                    continue;

                rc.Angle = (rc.Angle + rc.Speed * dt / 1000.0f) % MathHelper.TwoPi;
                //Like skeletal animation, a rotating part changes an existing instance rather than scene
                //membership. The frame loop propagates the transform and updates its existing buffer slot.
                m._localRotation = Matrix4.CreateFromAxisAngle(rc.Axis, rc.Angle) * restRotation[m];
                m.updated = true;
                activeScenes.Add(m);
            }
        }

        public void Remove(Model m)
        {
            rotationScenes.Remove(m);
            activeScenes.Remove(m);
            restRotation.Remove(m);
        }

        public void Add(Model m)
        {
            if (m.hasComponent(typeof(RotationComponent)) < 0 || rotationScenes.Contains(m)) //Scenes are re-scanned after a reference loads
                return;
            rotationScenes.Add(m);
            restRotation[m] = m.localRotation;
        }
    }
}
