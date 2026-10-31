using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MVCore.Audio;
using MVCore.Common;
using MVCore.GMDL;
using NAudio.Wave;
using OpenTK.Mathematics;

namespace MVCore.Engine.Systems
{
    //Plays the sounds of the loaded entities: AudioComponent ambients, emitters and anim triggers, and the PlayAudio
    //actions the ActionSystem runs. Sounds are found and decoded on a worker thread and mixed with a simple 3D model:
    //volume falls off with the camera's distance up to the component's MaxDistance, and pans with the camera's right axis
    public class AudioSystem : IDisposable
    {
        private const float DefaultMaxDistance = 50.0f;
        private const float MinDistanceGain = 0.1f; //Far sounds stay faintly audible, the viewer camera is often far away

        //One play of an event. Its voices arrive from the worker once the event is resolved and decoded
        public class SoundInstance
        {
            public string Event;
            public Model Emitter;
            public Model EmitterNode; //Where it plays, the emitter itself unless an emitter names a node
            public float MaxDistance;
            public bool Loop;
            public readonly List<Voice> Voices = new List<Voice>();
            public volatile bool Loaded = false;
            public volatile bool Stopped = false;
            internal float GainLeft = 1.0f;
            internal float GainRight = 1.0f;
            internal bool Hidden = false;
            public bool FromAction = false; //Started by a PlayAudio action
            //Wwise objects its Play chains went through, filled by the worker before Loaded
            public readonly HashSet<uint> UsedObjects = new HashSet<uint>();

            public string EndReason
            {
                get
                {
                    lock (Voices)
                        return Voices.Count == 0 ? "no voice" : string.Join(", ", Voices.Select(v => v.EndReason ?? "playing"));
                }
            }

            public bool Done
            {
                get
                {
                    if (Stopped)
                        return true;
                    if (!Loaded)
                        return false;
                    lock (Voices)
                        return Voices.All(v => v.Finished);
                }
            }

            public void Stop()
            {
                Stopped = true;
                lock (Voices)
                {
                    foreach (Voice v in Voices)
                        v.StopRequested = true;
                }
            }
        }

        //Frame tracking of one anim trigger, so it fires when its animation crosses FrameStart
        private class TriggerTrack
        {
            public int LastFrame = -1;
            public int LastCompleted = 0;
            public bool WasPlaying = false;
        }

        private class AudioSceneState
        {
            public AudioComponent Component;
            public readonly List<SoundInstance> Loops = new List<SoundInstance>();
            public readonly Dictionary<AudioAnimTrigger, TriggerTrack> Tracks = new Dictionary<AudioAnimTrigger, TriggerTrack>();
        }

        private readonly List<Model> audioScenes = new List<Model>();
        private readonly Dictionary<Model, AudioSceneState> sceneStates = new Dictionary<Model, AudioSceneState>();
        private readonly List<SoundInstance> instances = new List<SoundInstance>();
        private readonly HashSet<string> loggedOnce = new HashSet<string>();
        public bool Running { get; private set; } = false;

        private readonly AudioLibrary library = new AudioLibrary();
        private readonly AudioMixer mixer = new AudioMixer();
        private WaveOutEvent output;
        private volatile string outputStopped = null; //Set by the device thread when playback stops on its own
        //Wwise objects stopped by the Stop actions of played events, applied on the next update
        private readonly ConcurrentQueue<(uint target, SoundInstance by)> stopRequests = new ConcurrentQueue<(uint, SoundInstance)>();
        private bool outputFailed = false;
        private readonly BlockingCollection<System.Action> work = new BlockingCollection<System.Action>();
        private Thread worker;
        private Dictionary<string, string> fileSnapshot;
        private string unpackDir;

        //Every [Audio] line, for an event log
        public event System.Action<string> EventLogged;

        public AudioSystem()
        {
            library.Log = log;
        }

        public float MasterVolume
        {
            get { return mixer.MasterVolume; }
            set { mixer.MasterVolume = Math.Clamp(value, 0.0f, 1.0f); }
        }

        public void CleanUp()
        {
            stopAll();
            audioScenes.Clear();
            sceneStates.Clear();
            loggedOnce.Clear();
            //The next scene may come from other PAKs, and the handles shouldn't outlive the scene
            if (worker != null && !work.IsAddingCompleted)
                work.Add(library.ReleaseArchives);
        }

        //Turning audio on starts the ambients and emitters of every entity, turning it off silences everything
        public void SetRunning(bool run)
        {
            if (run == Running)
                return;
            Running = run;

            if (run)
            {
                log("[Audio] Audio started, " + audioScenes.Count + " entities with sounds");
                foreach (Model m in audioScenes)
                    startLoops(m);
            }
            else
            {
                stopAll();
                log("[Audio] Audio stopped");
            }
        }

        public void Add(Model scn)
        {
            if (sceneStates.ContainsKey(scn)) //Scenes are re-scanned after a reference loads
                return;
            int id = scn.hasComponent(typeof(AudioComponent));
            if (id < 0 || !(scn.Components[id] is AudioComponent ac))
                return;

            audioScenes.Add(scn);
            sceneStates[scn] = new AudioSceneState { Component = ac };
            log("[Audio] " + scn.Name + ": " + ac.Description +
                string.Concat(ac.AnimTriggers.Select(t => "\n[Audio]   anim trigger " + t.Description)) +
                string.Concat(ac.Emitters.Select(e => "\n[Audio]   emitter " + e.Description)));
            if (Running)
                startLoops(scn);
        }

        public void Remove(Model scn)
        {
            if (!sceneStates.TryGetValue(scn, out AudioSceneState st))
                return;
            foreach (SoundInstance si in st.Loops)
                si.Stop();
            //The game plays the Shutdown sound when the entity goes away
            if (Running && st.Component.Shutdown != null)
                PlayEvent(st.Component.Shutdown, scn, false, "shutdown");
            audioScenes.Remove(scn);
            sceneStates.Remove(scn);
        }

        public void update(float dt)
        {
            if (!Running)
                return;

            if (RenderState.renderSettings.ToggleAnimations)
            {
                foreach (Model m in audioScenes)
                    updateAnimTriggers(m, sceneStates[m]);
            }

            //The device stopped on its own (error, device change): start a new one, the mixer still has the voices
            string stopped = outputStopped;
            if (stopped != null)
            {
                outputStopped = null;
                log("[Audio] Output device stopped (" + stopped + "), restarting it");
                output?.Dispose();
                output = null;
                ensureOutput();
            }

            while (stopRequests.TryDequeue(out var stop))
            {
                foreach (SoundInstance si in instances)
                {
                    //Events often stop and restart the same sound, the sounds of the stopping event itself stay
                    if (si != stop.by && si.Loaded && !si.Stopped && si.UsedObjects.Contains(stop.target))
                    {
                        log("[Audio] " + stop.by.Event + " stops " + si.Event + " on " + (si.Emitter?.Name ?? "scene"));
                        si.Stop();
                    }
                }
            }

            for (int i = instances.Count - 1; i >= 0; i--)
            {
                SoundInstance si = instances[i];
                if (si.Done)
                {
                    //Loops only end when stopped, anything else is worth knowing
                    if (si.Loop && !si.Stopped)
                        log("[Audio] Loop " + si.Event + " on " + (si.Emitter?.Name ?? "scene") + " ended: " + si.EndReason);
                    instances.RemoveAt(i);
                    continue;
                }
                updateGains(si);
            }
        }

        //A GcPlayAudioAction run by the ActionSystem
        public void PlayAction(Model m, PlayAudioAction action)
        {
            if (action.Sound == null)
                return;
            if (!Running)
            {
                logOnce("audiooff", "[Audio] Audio is off, PlayAudio actions are silent");
                return;
            }
            PlayEvent(action.Sound, m, false, "PlayAudio action").FromAction = true;
        }

        //Stops the sounds PlayAudio actions started, when action emulation stops or restarts. Ambients, emitters and
        //anim triggers keep playing, actions that run again start their sounds again
        public void StopActionSounds()
        {
            int stopped = 0;
            foreach (SoundInstance si in instances)
            {
                if (si.FromAction && !si.Stopped)
                {
                    si.Stop();
                    stopped++;
                }
            }
            if (stopped > 0)
                log("[Audio] Stopped " + stopped + " sound(s) started by actions");
        }

        //Plays a Wwise event at the node. Loads asynchronously, the returned instance can be stopped before it sounds
        public SoundInstance PlayEvent(string eventName, Model emitter, bool loop, string reason, Model emitterNode = null)
        {
            SoundInstance si = new SoundInstance
            {
                Event = eventName,
                Emitter = emitter,
                EmitterNode = emitterNode ?? emitter,
                MaxDistance = maxDistanceOf(emitter),
                Loop = loop
            };
            updateGains(si);
            instances.Add(si);
            log("[Audio] " + (emitter?.Name ?? "scene") + ": " + eventName + (loop ? " (loop)" : "") + " (" + reason + ")");

            if (fileSnapshot == null)
            {
                //Snapshot on the render thread, the worker never touches the shared resource manager
                fileSnapshot = new Dictionary<string, string>();
                foreach (KeyValuePair<string, string> kv in RenderState.activeResMgr.NMSFileToArchiveMap)
                {
                    if (kv.Key.EndsWith(".BNK", StringComparison.OrdinalIgnoreCase) ||
                        kv.Key.EndsWith(".WEM", StringComparison.OrdinalIgnoreCase))
                        fileSnapshot[kv.Key] = kv.Value;
                }
                unpackDir = RenderState.settings.UnpackDir;
                if (!library.Indexed)
                    log("[Audio] Indexing the soundbanks, the first sounds may take a moment");
            }
            if (work.IsAddingCompleted) //Shutting down
                return si;
            ensureWorker();
            work.Add(() => load(si));
            return si;
        }

        private void load(SoundInstance si)
        {
            try
            {
                library.Index(fileSnapshot, unpackDir);
                if (si.Stopped)
                    return;

                foreach (uint target in library.StopTargets(si.Event))
                    stopRequests.Enqueue((target, si));

                List<(uint media, uint codec, int loops)> media = library.ResolveEvent(si.Event, out string error, si.UsedObjects);
                if (error != null)
                {
                    logOnce("event/" + si.Event, "[Audio] " + si.Event + ": " + error);
                    return;
                }

                foreach (var (mediaID, codec, loops) in media)
                {
                    DecodedSound sound = library.LoadSound(mediaID, codec);
                    if (sound == null)
                        continue;

                    //Sounds set to loop forever in Wwise (or WEMs with a loop region) play until a Stop action, the node
                    //is hidden or removed, or audio is turned off. Loop counts above 1 replay the sound that many times
                    bool loop = si.Loop || loops == 0 || sound.HasLoop;
                    if (loop && !si.Loop)
                        log("[Audio] " + si.Event + ": media " + mediaID + " loops forever" +
                            (sound.HasLoop ? " (region " + sound.LoopStart + "-" + sound.LoopEnd + ")" : ""));
                    else if (loops > 1)
                        log("[Audio] " + si.Event + ": media " + mediaID + " plays " + loops + " times");
                    Voice v = new Voice
                    {
                        Sound = sound, Loop = loop, LoopsLeft = loop ? 0 : loops - 1,
                        TargetLeft = si.GainLeft, TargetRight = si.GainRight
                    };
                    lock (si.Voices)
                    {
                        if (si.Stopped)
                            return;
                        si.Voices.Add(v);
                    }
                    if (!ensureOutput())
                        return;
                    mixer.Add(v);
                }
            }
            catch (Exception ex)
            {
                log("[Audio] " + si.Event + " failed: " + ex.Message);
            }
            finally
            {
                si.Loaded = true;
            }
        }

        private void ensureWorker()
        {
            if (worker != null)
                return;
            worker = new Thread(() =>
            {
                foreach (System.Action job in work.GetConsumingEnumerable())
                {
                    try
                    {
                        job();
                    }
                    catch (Exception ex)
                    {
                        log("[Audio] " + ex.Message);
                    }
                }
            });
            worker.IsBackground = true;
            worker.Name = "Audio Loader";
            worker.Start();
        }

        private bool ensureOutput()
        {
            if (output != null)
                return true;
            if (outputFailed)
                return false;
            try
            {
                //Three short buffers: low latency, and a GC pause or a busy loader can't starve the device as easily as with two
                output = new WaveOutEvent { DesiredLatency = 100, NumberOfBuffers = 3 };
                output.PlaybackStopped += (s, e) =>
                {
                    if (!work.IsAddingCompleted) //Not shutting down
                        outputStopped = e.Exception?.Message ?? "no error";
                };
                output.Init(mixer);
                output.Play();
                return true;
            }
            catch (Exception ex)
            {
                outputFailed = true;
                output = null;
                log("[Audio] No audio output device: " + ex.Message);
                return false;
            }
        }

        private void startLoops(Model m)
        {
            AudioSceneState st = sceneStates[m];
            AudioComponent ac = st.Component;
            foreach (SoundInstance si in st.Loops)
                si.Stop();
            st.Loops.Clear();
            st.Tracks.Clear();

            if (ac.AmbientState != null)
                logOnce(m.Name + "/state", "[Audio] " + m.Name + ": AmbientState " + ac.AmbientState + " is not emulated");
            if (ac.Ambient != null)
                st.Loops.Add(PlayEvent(ac.Ambient, m, true, "ambient"));

            foreach (AudioEmitter em in ac.Emitters)
            {
                Model node = null;
                if (!string.IsNullOrEmpty(em.Node))
                {
                    m.findNode(em.Node, ref node);
                    if (node == null)
                        logOnce(m.Name + "/node/" + em.Node, "[Audio] " + m.Name + ": emitter node " + em.Node +
                                " not found, it plays at the entity");
                }
                st.Loops.Add(PlayEvent(em.Sound, m, true, "emitter", node));
            }
        }

        private void stopAll()
        {
            foreach (SoundInstance si in instances)
                si.Stop();
            instances.Clear();
            foreach (AudioSceneState st in sceneStates.Values)
            {
                st.Loops.Clear();
                st.Tracks.Clear();
            }
            mixer.StopAll();
        }

        private void updateAnimTriggers(Model m, AudioSceneState st)
        {
            foreach (AudioAnimTrigger at in st.Component.AnimTriggers)
            {
                AnimData ad = findAnimation(m, at.Anim);
                if (ad == null)
                {
                    logOnce(m.Name + "/anim/" + at.Anim, "[Audio] " + m.Name + ": animation " + at.Anim + " not found, " +
                            at.Sound + " never plays");
                    continue;
                }

                if (!st.Tracks.TryGetValue(at, out TriggerTrack tr))
                    st.Tracks[at] = tr = new TriggerTrack { LastCompleted = ad.CompletedCount };

                bool fire = false;
                bool playing = ad.IsPlaying;
                int frame = ad.ActiveFrame;
                if (playing)
                {
                    //Started, restarted or looped since the last update
                    if (!tr.WasPlaying || frame < tr.LastFrame)
                    {
                        if (tr.WasPlaying && at.FrameStart > tr.LastFrame)
                            fire = true; //The end of the previous cycle went past the frame
                        tr.LastFrame = -1;
                    }
                    if (at.FrameStart > tr.LastFrame && at.FrameStart <= frame)
                        fire = true;
                    tr.LastFrame = frame;
                }
                else
                {
                    //A one-shot that ended between updates went past every frame left
                    if (tr.WasPlaying && ad.CompletedCount != tr.LastCompleted && at.FrameStart > tr.LastFrame)
                        fire = true;
                    tr.LastFrame = -1;
                }
                tr.WasPlaying = playing;
                tr.LastCompleted = ad.CompletedCount;

                if (fire)
                    PlayEvent(at.Sound, m, false, at.Anim + " frame " + at.FrameStart);
            }
        }

        private void updateGains(SoundInstance si)
        {
            Camera cam = RenderState.activeCam;
            Model node = si.EmitterNode;
            float gain = 1.0f, pan = 0.0f;

            if (cam != null && node != null)
            {
                Vector3 dir = node.worldPosition - cam.Position;
                float d = dir.Length;
                float att = Math.Clamp(1.0f - d / si.MaxDistance, 0.0f, 1.0f);
                gain = Math.Max(att * att, MinDistanceGain);
                if (d > 0.01f)
                    pan = Math.Clamp(Vector3.Dot(dir / d, cam.Right), -1.0f, 1.0f);
                if (!node.IsRenderable)
                    gain = 0.0f; //Hidden nodes are silent
                if (si.Hidden != !node.IsRenderable)
                {
                    si.Hidden = !node.IsRenderable;
                    log("[Audio] " + si.Event + " on " + node.Name + (si.Hidden ? " muted, the node is hidden" : " unmuted"));
                }
            }

            //Equal power pan, unity at the center
            float angle = (pan + 1.0f) * MathF.PI / 4.0f;
            si.GainLeft = gain * Math.Min(1.0f, MathF.Cos(angle) * 1.4142f);
            si.GainRight = gain * Math.Min(1.0f, MathF.Sin(angle) * 1.4142f);
            lock (si.Voices)
            {
                foreach (Voice v in si.Voices)
                {
                    v.TargetLeft = si.GainLeft;
                    v.TargetRight = si.GainRight;
                }
            }
        }

        //MaxDistance of the closest AudioComponent at or above the node
        private float maxDistanceOf(Model m)
        {
            for (Model n = m; n != null; n = n.parent)
            {
                if (sceneStates.TryGetValue(n, out AudioSceneState st) && st.Component.MaxDistance > 0.0f)
                    return st.Component.MaxDistance;
            }
            return DefaultMaxDistance;
        }

        //The animations usually sit on the same node as the sounds, otherwise on the closest ancestor
        private static AnimData findAnimation(Model m, string anim)
        {
            for (Model n = m; n != null; n = n.parent)
            {
                if (n.animComponentID >= 0)
                    return (n.Components[n.animComponentID] as AnimComponent)?.getAnimation(anim ?? "");
            }
            return null;
        }

        private void logOnce(string key, string message)
        {
            lock (loggedOnce)
            {
                if (!loggedOnce.Add(key))
                    return;
            }
            log(message);
        }

        private void log(string message)
        {
            CallBacks.Log(message);
            EventLogged?.Invoke(message);
        }

        public void Dispose()
        {
            work.CompleteAdding();
            worker?.Join(2000);
            output?.Stop();
            output?.Dispose();
            output = null;
            library.Dispose();
        }
    }
}
