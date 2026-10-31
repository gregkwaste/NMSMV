using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using libHGPAK.HGPAK;
using MVCore.Common;
using WEMSharp;

namespace MVCore.Audio
{
    //Decoded audio, always stereo float at the WEM's sample rate
    public class DecodedSound
    {
        public float[] Samples; //Interleaved L R
        public int SampleRate;
        public int Frames;
        public bool HasLoop; //Loop region from the WEM's smpl chunk, in frames
        public int LoopStart;
        public int LoopEnd;
        public long Bytes => Samples.LongLength * sizeof(float);
    }

    //Finds and decodes the sounds of Wwise events from the game's soundbanks. Event names hash to IDs, the event's
    //Play actions point at sounds or containers in the banks' HIRC sections and the sounds name the .WEM media, which is
    //either a loose AUDIO/<id>.WEM or embedded in a bank. Not thread safe, the AudioSystem calls it from its worker thread
    public class AudioLibrary : IDisposable
    {
        public const string CodebooksFile = "packed_codebooks_aoTuV_603.bin";
        private const long MaxCacheBytes = 512L * 1024 * 1024;

        //Audio files by uppercase path: the PAK they are in and their path in it, or a null PAK for files in the
        //unpacked folder
        private readonly Dictionary<string, (string pak, string entry)> files = new Dictionary<string, (string, string)>();
        private readonly Dictionary<uint, WwiseObject> objects = new Dictionary<uint, WwiseObject>();
        private readonly Dictionary<uint, WwiseEmbeddedMedia> embedded = new Dictionary<uint, WwiseEmbeddedMedia>();
        private readonly Dictionary<uint, string> looseWems = new Dictionary<uint, string>();
        private readonly Dictionary<string, Archive> archives = new Dictionary<string, Archive>();
        private readonly Dictionary<uint, DecodedSound> cache = new Dictionary<uint, DecodedSound>();
        private readonly HashSet<uint> failedMedia = new HashSet<uint>();
        private long cacheBytes = 0;
        private CodebookLibrary codebooks;
        private bool codebooksMissing = false;
        private string lastBankPath;
        private byte[] lastBank;
        private readonly Random rng = new Random();
        private readonly HashSet<uint> loggedProps = new HashSet<uint>();
        private readonly HashSet<uint> loggedObjects = new HashSet<uint>();
        public static bool DumpObjects = false;

        public bool Indexed { get; private set; } = false;
        //Writes every converted Ogg and decoded WAV to Temp/Audio, to check the conversion in an external player
        public static bool DumpDecoded = false;
        public int BankCount { get; private set; } = 0;

        public Action<string> Log = s => CallBacks.Log(s);

        //Reads every bank once. fileMap is a snapshot of the resource manager's file to PAK map
        public void Index(Dictionary<string, string> fileMap, string unpackDir)
        {
            if (Indexed)
                return;
            Indexed = true;
            Stopwatch clock = Stopwatch.StartNew();

            foreach (KeyValuePair<string, string> kv in fileMap)
            {
                if (isAudioFile(kv.Key))
                    files[normalize(kv.Key)] = (kv.Value, kv.Key);
            }

            //Unpacked files take precedence, like they do for models
            string unpackedAudio = string.IsNullOrEmpty(unpackDir) ? null : Path.Combine(unpackDir, "AUDIO");
            if (unpackedAudio != null && Directory.Exists(unpackedAudio))
            {
                foreach (string f in Directory.EnumerateFiles(unpackedAudio, "*.*", SearchOption.AllDirectories))
                {
                    if (isAudioFile(f))
                        files[normalize(Path.GetRelativePath(unpackDir, f))] = (null, f);
                }
            }

            foreach (string f in files.Keys)
            {
                if (!f.EndsWith(".WEM"))
                    continue;
                if (uint.TryParse(Path.GetFileNameWithoutExtension(f), out uint id))
                    looseWems[id] = f;
            }

            //Objects shared between banks keep the first copy read: the common banks first, then English, then the
            //other languages' localised banks
            foreach (string f in files.Keys.Where(k => k.EndsWith(".BNK")).OrderBy(bankOrder).ThenBy(k => k))
            {
                try
                {
                    byte[] bank = readFile(f);
                    if (BankCount == 0)
                        Log("[Audio] Bank version " + WwiseBank.Version(bank) + " (" + f + ")");
                    WwiseBank.Read(bank, f, objects, embedded);
                    BankCount++;
                }
                catch (Exception ex)
                {
                    Log("[Audio] Failed to read bank " + f + ": " + ex.Message);
                }
            }

            Log("[Audio] Indexed " + BankCount + " banks, " + objects.Count + " objects, " + looseWems.Count +
                " streamed and " + embedded.Count + " embedded media in " + clock.ElapsedMilliseconds + " ms");
            if (BankCount == 0)
                Log("[Audio] No .BNK files found in the PAKs or in the unpacked AUDIO folder, sounds can't be resolved");
        }

        private static int bankOrder(string path)
        {
            if (!path.Contains('('))
                return 0; //Language folders are named like ENGLISH(US)
            return path.Contains("ENGLISH") ? 1 : 2;
        }

        private static bool isAudioFile(string path)
        {
            return path.EndsWith(".BNK", StringComparison.OrdinalIgnoreCase) ||
                   path.EndsWith(".WEM", StringComparison.OrdinalIgnoreCase);
        }

        private static string normalize(string path)
        {
            return path.Replace('\\', '/').TrimStart('/').ToUpperInvariant();
        }

        //Media IDs and codecs that one play of the event starts. Names starting with # are numeric event IDs
        //Event ID of a name, names starting with # are numeric IDs
        private static uint eventID(string eventName)
        {
            if (eventName.StartsWith("#"))
            {
                uint.TryParse(eventName.Substring(1), out uint n);
                return n;
            }
            return WwiseBank.HashName(eventName);
        }

        //Objects the event's Stop actions stop (Stop_E 0x0102 and Stop_E_O 0x0103). Sounds started by any event whose
        //Play chain went through one of them stop
        public List<uint> StopTargets(string eventName)
        {
            List<uint> targets = new List<uint>();
            if (!objects.TryGetValue(eventID(eventName), out WwiseObject ev) || ev.Type != WwiseObject.Event)
                return targets;
            foreach (uint actionID in WwiseBank.EventActions(ev))
            {
                if (!objects.TryGetValue(actionID, out WwiseObject action) || action.Type != WwiseObject.Action)
                    continue;
                ushort type = WwiseBank.ActionType(action);
                if (type == 0x0102 || type == 0x0103)
                    targets.Add(WwiseBank.ActionTarget(action));
            }
            return targets;
        }

        //used collects every object the Play chains went through, for the Stop actions of later events
        //loops is the Sound's Wwise loop count: 1 plays once, 0 loops forever
        public List<(uint media, uint codec, int loops)> ResolveEvent(string eventName, out string error, HashSet<uint> used = null)
        {
            List<(uint, uint, int)> media = new List<(uint, uint, int)>();
            error = null;

            uint id = eventID(eventName);
            if (!objects.TryGetValue(id, out WwiseObject ev) || ev.Type != WwiseObject.Event)
            {
                error = "event not found in the banks (ID " + id + ")";
                return media;
            }

            HashSet<uint> visited = used ?? new HashSet<uint>();
            int plays = 0;
            foreach (uint actionID in WwiseBank.EventActions(ev))
            {
                if (!objects.TryGetValue(actionID, out WwiseObject action) || action.Type != WwiseObject.Action)
                    continue;
                if (WwiseBank.ActionType(action) != 0x0403) //Play. Stop, SetState, SetSwitch etc. have no sound
                    continue;
                plays++;
                collect(WwiseBank.ActionTarget(action), media, visited, 0);
            }

            //Events without a Play action (stops, state changes) are not errors
            if (media.Count == 0 && plays > 0)
                error = "no playable Vorbis or PCM source under the event";
            return media;
        }

        private void collect(uint id, List<(uint, uint, int)> media, HashSet<uint> visited, int depth)
        {
            if (depth > 16 || !visited.Add(id) || !objects.TryGetValue(id, out WwiseObject obj))
                return;
            //TEMP: raw HIRC bodies of the played chains, to locate the loop settings of this bank version
            if (DumpObjects && loggedObjects.Add(id))
                Log("[Audio] Obj " + id + " type " + obj.Type + " len " + obj.Body.Length + ": " + BitConverter.ToString(obj.Body).Replace("-", ""));

            switch (obj.Type)
            {
                case WwiseObject.Sound:
                case WwiseObject.MusicTrack:
                {
                    int loops = 1;
                    if (obj.Type == WwiseObject.Sound)
                    {
                        Dictionary<byte, uint> props = WwiseBank.SoundProps(obj, objects);
                        if (props != null && props.TryGetValue(WwiseBank.PropLoop, out uint count))
                            loops = (int) Math.Min(count, 1000);
                        else if (props == null && loggedProps.Add(id))
                            Log("[Audio] Sound " + id + ": properties not found, it plays once");
                    }
                    List<uint> sources = WwiseBank.Sources(obj, out uint codec);
                    if (sources.Count > 0)
                        media.Add((sources[0], codec, loops)); //Tracks with several sources switch between them, play the first
                    else if (obj.Body.Length >= 8)
                        Log("[Audio] Sound " + id + " uses codec plugin 0x" + BitConverter.ToUInt32(obj.Body, 4).ToString("X8") +
                            ", only Vorbis and PCM are supported");
                    break;
                }
                case WwiseObject.LayerContainer:
                case WwiseObject.MusicSegment:
                    //Every child plays together
                    foreach (uint child in WwiseBank.ContainerChildren(obj, objects))
                        collect(child, media, visited, depth + 1);
                    break;
                case WwiseObject.RanSeqContainer:
                case WwiseObject.SwitchContainer:
                case WwiseObject.MusicSwitch:
                case WwiseObject.MusicRanSeq:
                case WwiseObject.ActorMixer:
                {
                    //One child plays. Switches depend on game state the viewer doesn't have, so they pick at random too
                    List<uint> children = WwiseBank.ContainerChildren(obj, objects);
                    if (children.Count > 0)
                        collect(children[rng.Next(children.Count)], media, visited, depth + 1);
                    break;
                }
            }
        }

        //Decoded audio of a media ID, null when it can't be found or decoded
        public DecodedSound LoadSound(uint mediaID, uint codec)
        {
            if (cache.TryGetValue(mediaID, out DecodedSound cached))
                return cached;
            if (failedMedia.Contains(mediaID))
                return null;

            DecodedSound sound = null;
            try
            {
                byte[] wem = readMedia(mediaID);
                if (wem == null)
                    Log("[Audio] Media " + mediaID + " not found as a loose .WEM or in a bank");
                else if (isPCM(wem))
                    sound = decodePCM(wem);
                else
                    sound = decodeVorbis(wem, mediaID);
            }
            catch (Exception ex)
            {
                Log("[Audio] Failed to decode media " + mediaID + ": " + ex.Message);
                sound = null;
            }

            if (sound == null)
            {
                failedMedia.Add(mediaID);
                return null;
            }

            if (cacheBytes + sound.Bytes > MaxCacheBytes)
            {
                cache.Clear();
                cacheBytes = 0;
            }
            cache[mediaID] = sound;
            cacheBytes += sound.Bytes;
            return sound;
        }

        private byte[] readMedia(uint mediaID)
        {
            //Prefetched media has only its start in the bank, the loose file is complete
            if (looseWems.TryGetValue(mediaID, out string path))
                return readFile(path);

            if (embedded.TryGetValue(mediaID, out WwiseEmbeddedMedia em))
            {
                if (lastBankPath != em.Bank)
                {
                    lastBank = readFile(em.Bank);
                    lastBankPath = em.Bank;
                }
                if (em.Offset + (long) em.Size > lastBank.Length)
                    return null;
                byte[] wem = new byte[em.Size];
                Buffer.BlockCopy(lastBank, (int) em.Offset, wem, 0, (int) em.Size);
                return wem;
            }
            return null;
        }

        private byte[] readFile(string path)
        {
            var (pak, entry) = files[path];
            if (pak == null)
                return File.ReadAllBytes(entry);

            //Own archive handles: the shared ones are used and disposed by the render thread
            if (!archives.TryGetValue(pak, out Archive arc))
            {
                FileStream fs = new FileStream(pak, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                arc = new Archive(fs, true);
                archives[pak] = arc;
            }

            using (Stream s = arc.ExtractFile(entry))
            using (MemoryStream ms = new MemoryStream())
            {
                s.Seek(0, SeekOrigin.Begin);
                s.CopyTo(ms);
                return ms.ToArray();
            }
        }

        //Closes the PAK handles between bursts of loading, so the files aren't kept open
        public void ReleaseArchives()
        {
            foreach (Archive arc in archives.Values)
                arc.Dispose();
            archives.Clear();
            lastBank = null;
            lastBankPath = null;
        }

        private DecodedSound decodeVorbis(byte[] wemData, uint mediaID)
        {
            CodebookLibrary books = loadCodebooks();
            using (WEMFile wem = new WEMFile(new MemoryStream(wemData), WEMForcePacketFormat.NoForcePacketFormat))
            {
                //Without the codebook library only WEMs with inline codebooks convert
                OggStream ogg = books != null ? wem.GenerateOGGStream(books, false, false)
                                              : wem.GenerateOGGStream((CodebookLibrary) null, true, false);
                ogg.Flush();
                Stream oggData = ogg.BaseStream;
                oggData.Seek(0, SeekOrigin.Begin);
                if (DumpDecoded)
                    dump(mediaID + ".ogg", ((MemoryStream) oggData).ToArray());

                List<float> pcm = new List<float>();
                int channels, rate;
                using (NVorbis.VorbisReader reader = new NVorbis.VorbisReader(oggData, true))
                {
                    channels = reader.Channels;
                    rate = reader.SampleRate;
                    float[] buffer = new float[4096 * channels];
                    int read;
                    while ((read = reader.ReadSamples(buffer, 0, buffer.Length)) > 0)
                        pcm.AddRange(new ArraySegment<float>(buffer, 0, read));
                }

                DecodedSound sound = toStereo(pcm.ToArray(), channels, rate);
                Log("[Audio] Media " + mediaID + ": " + channels + " ch, " + rate + " Hz, " + sound.Frames + " frames (" +
                    (sound.Frames / (float) Math.Max(rate, 1)).ToString("0.00") + " s, WEM says " + wem.SampleCount + " samples)" +
                    (wem.HasLoop ? ", loop " + wem.LoopStart + "-" + wem.LoopEnd : ""));
                if (DumpDecoded)
                    dumpWav(mediaID + ".wav", sound);
                if (wem.HasLoop && wem.LoopEnd > wem.LoopStart)
                {
                    sound.HasLoop = true;
                    sound.LoopStart = (int) Math.Min(wem.LoopStart, (uint) sound.Frames);
                    sound.LoopEnd = (int) Math.Min(wem.LoopEnd, (uint) sound.Frames);
                }
                return sound;
            }
        }

        private void dump(string name, byte[] data)
        {
            try
            {
                Directory.CreateDirectory(Path.Combine("Temp", "Audio"));
                File.WriteAllBytes(Path.Combine("Temp", "Audio", name), data);
            }
            catch (Exception ex)
            {
                Log("[Audio] Dump of " + name + " failed: " + ex.Message);
            }
        }

        //16-bit stereo WAV of the decoded samples
        private void dumpWav(string name, DecodedSound s)
        {
            using (MemoryStream ms = new MemoryStream())
            using (BinaryWriter w = new BinaryWriter(ms))
            {
                int dataSize = s.Frames * 4;
                w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                w.Write(36 + dataSize);
                w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                w.Write(16);
                w.Write((short) 1);
                w.Write((short) 2);
                w.Write(s.SampleRate);
                w.Write(s.SampleRate * 4);
                w.Write((short) 4);
                w.Write((short) 16);
                w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                w.Write(dataSize);
                for (int i = 0; i < s.Frames * 2; i++)
                    w.Write((short) Math.Clamp(s.Samples[i] * 32767.0f, -32768.0f, 32767.0f));
                w.Flush();
                dump(name, ms.ToArray());
            }
        }

        private CodebookLibrary loadCodebooks()
        {
            if (codebooks != null || codebooksMissing)
                return codebooks;

            foreach (string dir in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
            {
                string path = Path.Combine(dir, CodebooksFile);
                if (File.Exists(path))
                {
                    codebooks = new CodebookLibrary(path);
                    return codebooks;
                }
            }

            codebooksMissing = true;
            Log("[Audio] " + CodebooksFile + " not found next to the viewer's executable. Vorbis WEMs that use the " +
                "external codebooks (all recent Wwise versions) can't be decoded without it");
            return null;
        }

        private static bool isPCM(byte[] wem)
        {
            int fmt = findChunk(wem, "fmt ", out _);
            if (fmt < 0 || fmt + 16 > wem.Length)
                return false;
            ushort format = BitConverter.ToUInt16(wem, fmt);
            ushort bits = BitConverter.ToUInt16(wem, fmt + 14);
            return (format == 1 || format == 0xFFFE) && bits == 16;
        }

        private static DecodedSound decodePCM(byte[] wem)
        {
            int fmt = findChunk(wem, "fmt ", out _);
            int data = findChunk(wem, "data", out int dataSize);
            if (data < 0)
                return null;
            int channels = BitConverter.ToUInt16(wem, fmt + 2);
            int rate = (int) BitConverter.ToUInt32(wem, fmt + 4);
            int count = Math.Min(dataSize, wem.Length - data) / 2;
            float[] pcm = new float[count];
            for (int i = 0; i < count; i++)
                pcm[i] = BitConverter.ToInt16(wem, data + i * 2) / 32768.0f;
            return toStereo(pcm, channels, rate);
        }

        //Offset of a RIFF chunk's data
        private static int findChunk(byte[] riff, string name, out int size)
        {
            size = 0;
            int pos = 12;
            while (pos + 8 <= riff.Length)
            {
                string tag = System.Text.Encoding.ASCII.GetString(riff, pos, 4);
                int chunkSize = (int) BitConverter.ToUInt32(riff, pos + 4);
                if (tag == name)
                {
                    size = chunkSize;
                    return pos + 8;
                }
                pos += 8 + chunkSize + (chunkSize & 1);
            }
            return -1;
        }

        //Downmixes to stereo. Wwise channel order: L R C LFE, then the back and side pairs
        private static DecodedSound toStereo(float[] pcm, int channels, int rate)
        {
            channels = Math.Max(channels, 1);
            int frames = pcm.Length / channels;
            float[] stereo = new float[frames * 2];

            float[] wl = new float[channels];
            float[] wr = new float[channels];
            if (channels == 1)
            {
                wl[0] = wr[0] = 1.0f;
            }
            else
            {
                bool hasCenter = channels == 3 || channels >= 5;
                bool hasLFE = channels >= 6;
                for (int c = 0; c < channels; c++)
                {
                    if (c < 2)
                    {
                        (c == 0 ? wl : wr)[c] = 1.0f;
                        continue;
                    }
                    if (hasCenter && c == 2)
                    {
                        wl[c] = wr[c] = 0.707f;
                        continue;
                    }
                    if (hasLFE && c == 3)
                        continue;
                    //Back and side pairs alternate left, right
                    int pairIndex = c - 2 - (hasCenter ? 1 : 0) - (hasLFE ? 1 : 0);
                    (pairIndex % 2 == 0 ? wl : wr)[c] = 0.707f;
                }
            }

            float norm = 1.0f / Math.Max(1.0f, wl.Sum());
            for (int f = 0; f < frames; f++)
            {
                float l = 0.0f, r = 0.0f;
                for (int c = 0; c < channels; c++)
                {
                    float s = pcm[f * channels + c];
                    l += s * wl[c];
                    r += s * wr[c];
                }
                stereo[f * 2] = l * norm;
                stereo[f * 2 + 1] = r * norm;
            }

            return new DecodedSound { Samples = stereo, SampleRate = rate, Frames = frames };
        }

        public void Dispose()
        {
            ReleaseArchives();
            cache.Clear();
            cacheBytes = 0;
        }
    }
}
