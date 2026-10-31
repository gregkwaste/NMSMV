using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using libMBIN;

namespace MVCore.GMDL
{
    //Reads the audio templates by field name, so the viewer builds against libMBIN versions whose audio types differ
    //(event names have been strings, NMSStrings, enums and ID structs)
    public static class AudioFields
    {
        public static object Get(object obj, string name)
        {
            if (obj == null)
                return null;
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            Type t = obj.GetType();
            FieldInfo f = t.GetField(name, flags);
            if (f != null)
                return f.GetValue(obj);
            PropertyInfo p = t.GetProperty(name, flags);
            return (p != null && p.GetIndexParameters().Length == 0) ? p.GetValue(obj) : null;
        }

        public static bool Has(object obj, string name)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            return obj != null && (obj.GetType().GetField(name, flags) != null || obj.GetType().GetProperty(name, flags) != null);
        }

        //Wwise event name, "#<id>" for numeric event IDs, null when unset
        public static string EventName(object v)
        {
            switch (v)
            {
                case null:
                    return null;
                case string s:
                    s = s.Trim();
                    return s.Length == 0 ? null : s;
                case Enum e:
                {
                    string s = e.ToString();
                    if (s.Length == 0 || char.IsDigit(s[0]) || s.StartsWith("INVALID", StringComparison.OrdinalIgnoreCase))
                        return null;
                    return s;
                }
                case uint u:
                    return u == 0 ? null : "#" + u;
                case int i:
                    return i == 0 ? null : "#" + (uint) i;
            }

            foreach (string member in new[] { "Value", "AkEvent", "Event", "Name", "ID", "Id" })
            {
                if (Has(v, member))
                {
                    string s = EventName(Get(v, member));
                    if (s != null)
                        return s;
                }
            }

            string str = v.ToString();
            return (str == v.GetType().FullName || string.IsNullOrWhiteSpace(str)) ? null : str.Trim();
        }

        public static string Text(object v)
        {
            if (v == null)
                return "";
            if (v is string s)
                return s;
            if (Has(v, "Value"))
                return Get(v, "Value")?.ToString() ?? "";
            return v.ToString();
        }

        public static float Number(object v, float fallback = 0.0f)
        {
            try
            {
                return v == null ? fallback : Convert.ToSingle(v);
            }
            catch
            {
                return fallback;
            }
        }

        public static IEnumerable<object> Items(object v)
        {
            if (v == null || v is string)
                yield break;
            if (v is IEnumerable list)
            {
                foreach (object o in list)
                    if (o != null)
                        yield return o;
            }
            else
                yield return v;
        }
    }

    //TkAudioAnimTrigger: the Sound plays when Anim reaches FrameStart
    public class AudioAnimTrigger
    {
        public string Sound { get; set; }
        public string Anim { get; set; }
        public int FrameStart { get; set; }
        public string Description => Sound + " @ " + Anim + " frame " + FrameStart;
    }

    //An emitter of the component, played as a loop at its node, or at the entity when it names none
    public class AudioEmitter
    {
        public string Sound { get; set; }
        public string Node { get; set; }
        public string Description => Sound + (string.IsNullOrEmpty(Node) ? "" : " at " + Node);
    }

    //TkAudioComponentData: an Ambient loop while the entity exists, a Shutdown sound when it goes away, sounds on
    //animation frames and positional emitters
    public class AudioComponent : IComponent
    {
        public NMSTemplate Template;
        public string Ambient { get; private set; }
        public string AmbientState { get; private set; }
        public string Shutdown { get; private set; }
        public float MaxDistance { get; private set; }
        public List<AudioAnimTrigger> AnimTriggers { get; } = new List<AudioAnimTrigger>();
        public List<AudioEmitter> Emitters { get; } = new List<AudioEmitter>();

        public AudioComponent(NMSTemplate t)
        {
            Template = t;
            Ambient = AudioFields.EventName(AudioFields.Get(t, "Ambient"));
            AmbientState = AudioFields.EventName(AudioFields.Get(t, "AmbientState"));
            Shutdown = AudioFields.EventName(AudioFields.Get(t, "Shutdown"));
            MaxDistance = AudioFields.Number(AudioFields.Get(t, "MaxDistance"));

            foreach (object at in AudioFields.Items(AudioFields.Get(t, "AnimTriggers")))
            {
                string sound = AudioFields.EventName(AudioFields.Get(at, "Sound"));
                if (sound == null)
                    continue;
                AnimTriggers.Add(new AudioAnimTrigger
                {
                    Sound = sound,
                    Anim = AudioFields.Text(AudioFields.Get(at, "Anim")),
                    FrameStart = (int) AudioFields.Number(AudioFields.Get(at, "FrameStart"))
                });
            }

            //The emitter layout varies between versions: the first member that names an event is the sound, the first
            //that names a node, locator or joint is where it plays
            foreach (object em in AudioFields.Items(AudioFields.Get(t, "Emitters")))
            {
                string sound = null, node = null;
                foreach (FieldInfo f in em.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    string n = f.Name.ToLowerInvariant();
                    if (sound == null && (n.Contains("event") || n.Contains("sound") || n.Contains("audio") || n.Contains("ambient")))
                        sound = AudioFields.EventName(f.GetValue(em));
                    else if (node == null && (n.Contains("node") || n.Contains("locator") || n.Contains("joint")))
                        node = AudioFields.Text(f.GetValue(em));
                }
                if (sound != null)
                    Emitters.Add(new AudioEmitter { Sound = sound, Node = node });
            }
        }

        public IComponent Clone()
        {
            //Copies share the entity data
            return new AudioComponent(Template);
        }

        public string Description
        {
            get
            {
                List<string> parts = new List<string>();
                if (Ambient != null)
                    parts.Add("Ambient " + Ambient);
                if (AnimTriggers.Count > 0)
                    parts.Add(AnimTriggers.Count + " anim trigger(s)");
                if (Emitters.Count > 0)
                    parts.Add(Emitters.Count + " emitter(s)");
                return parts.Count == 0 ? "no sounds" : string.Join(", ", parts);
            }
        }
    }

    //GcPlayAudioAction
    public class PlayAudioAction : Action
    {
        public PlayAudioAction(NMSTemplate t) : base(t)
        {
            Sound = AudioFields.EventName(AudioFields.Get(t, "Sound"));
        }

        public string Sound { get; private set; }

        public override string Description => "PlayAudio " + (Sound ?? "(none)");
    }
}
