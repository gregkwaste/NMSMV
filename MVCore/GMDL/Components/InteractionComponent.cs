using System;
using libMBIN;
using libMBIN.NMS.GameComponents;

namespace MVCore.GMDL
{
    //GcInteractionComponentData and GcSimpleInteractionComponentData. Only the part the action emulation uses:
    //interacting sends TriggerAction as a GoToState to the entity, or to the whole scene when BroadcastTriggerAction is set
    public class InteractionComponent : IComponent
    {
        public NMSTemplate Template;
        public string TriggerAction { get; }
        public bool BroadcastTriggerAction { get; }
        public float InteractDistance { get; } //How close the player has to be to interact
        public string TypeName { get; }
        public string InteractionKind { get; } //Interaction type of the entity (Portal, CallShip, Ladder...)

        public InteractionComponent(GcInteractionComponentData icd)
        {
            Template = icd;
            TypeName = "GcInteractionComponentData";
            TriggerAction = icd.TriggerAction;
            BroadcastTriggerAction = icd.BroadcastTriggerAction;
            InteractDistance = icd.InteractDistance;
            InteractionKind = "";
        }

        public InteractionComponent(GcSimpleInteractionComponentData sicd)
        {
            Template = sicd;
            TypeName = "GcSimpleInteractionComponentData";
            TriggerAction = sicd.TriggerAction;
            BroadcastTriggerAction = sicd.BroadcastTriggerAction;
            InteractDistance = sicd.InteractDistance;
            InteractionKind = sicd.SimpleInteractionType.ToString();
        }

        public IComponent Clone()
        {
            //Copies share the entity data
            if (Template is GcSimpleInteractionComponentData sicd)
                return new InteractionComponent(sicd);
            return new InteractionComponent((GcInteractionComponentData) Template);
        }

        public string Description
        {
            get
            {
                string kind = InteractionKind.Length > 0 ? InteractionKind + " " : "";
                if (string.IsNullOrEmpty(TriggerAction))
                    return kind + "no TriggerAction";
                return kind + "→ " + TriggerAction + (BroadcastTriggerAction ? " (broadcast)" : "") + ", " + InteractDistance + "m";
            }
        }
    }
}
