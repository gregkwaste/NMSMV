using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MVCore.Audio
{
    //One object of a bank's HIRC section. The body starts with the object's ID
    public class WwiseObject
    {
        public const byte Sound = 2;
        public const byte Action = 3;
        public const byte Event = 4;
        public const byte RanSeqContainer = 5;
        public const byte SwitchContainer = 6;
        public const byte ActorMixer = 7;
        public const byte LayerContainer = 9;
        public const byte MusicSegment = 10;
        public const byte MusicTrack = 11;
        public const byte MusicSwitch = 12;
        public const byte MusicRanSeq = 13;

        public byte Type;
        public uint ID;
        public byte[] Body;
        public string Bank;
    }

    //Media embedded in a bank: DIDX entry, offset from the start of the bank file
    public class WwiseEmbeddedMedia
    {
        public string Bank;
        public uint Offset;
        public uint Size;
    }

    //Reads the parts of a Wwise soundbank (.BNK) the viewer needs: the HIRC objects and the DIDX media index.
    //The media data itself is not kept, AudioLibrary reads it again when a sound needs it
    public static class WwiseBank
    {
        public static uint Version(byte[] bank)
        {
            if (bank.Length >= 12 && Encoding.ASCII.GetString(bank, 0, 4) == "BKHD")
                return BitConverter.ToUInt32(bank, 8);
            return 0;
        }

        public static void Read(byte[] bank, string bankPath, Dictionary<uint, WwiseObject> objects,
                                Dictionary<uint, WwiseEmbeddedMedia> media)
        {
            uint dataOffset = 0;
            List<(uint id, uint offset, uint size)> didx = new List<(uint, uint, uint)>();

            int pos = 0;
            while (pos + 8 <= bank.Length)
            {
                string tag = Encoding.ASCII.GetString(bank, pos, 4);
                uint size = BitConverter.ToUInt32(bank, pos + 4);
                int start = pos + 8;
                if (start + (long) size > bank.Length)
                    break; //Truncated section

                switch (tag)
                {
                    case "DIDX":
                        for (int i = start; i + 12 <= start + size; i += 12)
                            didx.Add((BitConverter.ToUInt32(bank, i), BitConverter.ToUInt32(bank, i + 4),
                                      BitConverter.ToUInt32(bank, i + 8)));
                        break;
                    case "DATA":
                        dataOffset = (uint) start;
                        break;
                    case "HIRC":
                        readHirc(bank, start, (int) size, bankPath, objects);
                        break;
                }
                pos = start + (int) size;
            }

            foreach (var (id, offset, size) in didx)
            {
                if (dataOffset == 0)
                    break;
                //The first bank that embeds a media wins, later ones usually hold the same prefetch data
                if (!media.ContainsKey(id))
                    media[id] = new WwiseEmbeddedMedia { Bank = bankPath, Offset = dataOffset + offset, Size = size };
            }
        }

        private static void readHirc(byte[] bank, int start, int size, string bankPath, Dictionary<uint, WwiseObject> objects)
        {
            int end = start + size;
            uint count = BitConverter.ToUInt32(bank, start);
            int pos = start + 4;
            for (uint i = 0; i < count && pos + 5 <= end; i++)
            {
                byte type = bank[pos];
                uint objSize = BitConverter.ToUInt32(bank, pos + 1);
                int bodyStart = pos + 5;
                if (objSize < 4 || bodyStart + (long) objSize > end)
                    break;

                byte[] body = new byte[objSize];
                Buffer.BlockCopy(bank, bodyStart, body, 0, (int) objSize);
                uint id = BitConverter.ToUInt32(body, 0);

                //Objects shared between banks are identical, keep the first
                if (!objects.ContainsKey(id))
                    objects[id] = new WwiseObject { Type = type, ID = id, Body = body, Bank = bankPath };
                pos = bodyStart + (int) objSize;
            }
        }

        //Wwise IDs of names are the 32-bit FNV-1 hash of the lowercase name
        public static uint HashName(string name)
        {
            uint hash = 2166136261;
            foreach (byte b in Encoding.ASCII.GetBytes(name.ToLowerInvariant()))
            {
                hash *= 16777619;
                hash ^= b;
            }
            return hash;
        }

        //Action IDs of an event. The count is a u32 in old banks and a variable-length int in newer ones, the one that
        //matches the body size is used
        public static List<uint> EventActions(WwiseObject ev)
        {
            List<uint> actions = new List<uint>();
            byte[] b = ev.Body;
            int countSize;
            uint count;

            if (b.Length >= 8 && 8 + (long) BitConverter.ToUInt32(b, 4) * 4 == b.Length)
            {
                count = BitConverter.ToUInt32(b, 4);
                countSize = 4;
            }
            else
            {
                count = readVarUInt(b, 4, out countSize);
                if (4 + countSize + (long) count * 4 > b.Length)
                    return actions;
            }

            for (int i = 0; i < count; i++)
                actions.Add(BitConverter.ToUInt32(b, 4 + countSize + i * 4));
            return actions;
        }

        private static uint readVarUInt(byte[] b, int pos, out int size)
        {
            uint value = 0;
            size = 0;
            while (pos + size < b.Length && size < 5)
            {
                byte cur = b[pos + size];
                size++;
                value = (value << 7) | (uint) (cur & 0x7F);
                if ((cur & 0x80) == 0)
                    break;
            }
            return value;
        }

        //Action type and target. Play is 0x0403
        public static ushort ActionType(WwiseObject action)
        {
            return action.Body.Length >= 6 ? BitConverter.ToUInt16(action.Body, 4) : (ushort) 0;
        }

        public static uint ActionTarget(WwiseObject action)
        {
            return action.Body.Length >= 10 ? BitConverter.ToUInt32(action.Body, 6) : 0;
        }

        //The children list of a container: a u32 count followed by that many IDs. Its offset depends on the bank
        //version and on the optional parameters before it, so the body is searched for the longest run of a count
        //followed by IDs of known objects
        public static List<uint> ContainerChildren(WwiseObject container, Dictionary<uint, WwiseObject> objects)
        {
            byte[] b = container.Body;
            List<uint> best = new List<uint>();
            for (int o = 4; o + 8 <= b.Length; o++)
            {
                uint n = BitConverter.ToUInt32(b, o);
                if (n == 0 || n > 1024 || n <= best.Count || o + 4 + (long) n * 4 > b.Length)
                    continue;

                List<uint> ids = new List<uint>((int) n);
                for (int i = 0; i < n; i++)
                {
                    uint id = BitConverter.ToUInt32(b, o + 4 + i * 4);
                    if (id == container.ID || !objects.ContainsKey(id) || ids.Contains(id))
                        break;
                    ids.Add(id);
                }
                if (ids.Count == n)
                    best = ids;
            }
            return best;
        }

        //Loop count property of Sounds (bank version 154): absent plays once, 0 loops forever, N plays N times
        public const byte PropLoop = 0x4A;

        //Property bundle (AkPropBundle) of a Sound, ID to raw 4-byte value. It follows DirectParentID and the
        //byBitVector byte: a count, the property IDs, then a value each. DirectParentID is the first ID after the
        //source data that names a container or actor-mixer, OverrideBusId before it names a bus and is skipped
        public static Dictionary<byte, uint> SoundProps(WwiseObject sound, Dictionary<uint, WwiseObject> objects)
        {
            byte[] b = sound.Body;
            for (int p = 18; p + 4 <= Math.Min(b.Length, 64); p++)
            {
                uint parent = BitConverter.ToUInt32(b, p);
                if (parent == 0 || parent == sound.ID || !objects.TryGetValue(parent, out WwiseObject po) || !isParentType(po.Type))
                    continue;
                int o = p + 5;
                if (o >= b.Length)
                    return null;
                int n = b[o];
                if (n > 64 || o + 1 + n * 5 > b.Length)
                    return null;
                Dictionary<byte, uint> props = new Dictionary<byte, uint>();
                for (int i = 0; i < n; i++)
                    props[b[o + 1 + i]] = BitConverter.ToUInt32(b, o + 1 + n + i * 4);
                return props;
            }
            return null;
        }

        //Containers and actor-mixers, not buses (8) or tracks
        private static bool isParentType(byte type)
        {
            switch (type)
            {
                case WwiseObject.RanSeqContainer:
                case WwiseObject.SwitchContainer:
                case WwiseObject.ActorMixer:
                case WwiseObject.LayerContainer:
                case WwiseObject.MusicSegment:
                case WwiseObject.MusicSwitch:
                case WwiseObject.MusicRanSeq:
                    return true;
            }
            return false;
        }

        public const uint CodecPCM = 0x00010001;
        public const uint CodecVorbis = 0x00040001;

        //Sources of a Sound or MusicTrack: AkBankSourceData is plugin ID (u32), stream type (u8), source ID (u32), so
        //the body is searched for the codec plugin IDs. Stream type 0 is media embedded in a bank, 1 and 2 have a
        //loose .WEM
        public static List<uint> Sources(WwiseObject obj, out uint codec)
        {
            List<uint> sources = new List<uint>();
            byte[] b = obj.Body;
            codec = 0;
            for (int o = 4; o + 9 <= b.Length; o++)
            {
                uint plugin = BitConverter.ToUInt32(b, o);
                if (plugin != CodecVorbis && plugin != CodecPCM)
                    continue;
                if (b[o + 4] > 2)
                    continue;
                uint source = BitConverter.ToUInt32(b, o + 5);
                if (source == 0 || sources.Contains(source))
                    continue;
                codec = plugin;
                sources.Add(source);
                if (obj.Type == WwiseObject.Sound)
                    break; //A Sound has one source
                o += 13;
            }
            return sources;
        }
    }
}
