using System;
using System.Collections.Generic;
using NAudio.Wave;

namespace MVCore.Audio
{
    //One playing sound. Gains are set from the render thread and ramped by the mixer, so moving sounds don't click
    public class Voice
    {
        public DecodedSound Sound;
        public bool Loop; //Forever
        public int LoopsLeft = 0; //Further plays of a sound with a finite loop count
        public volatile float TargetLeft = 1.0f;
        public volatile float TargetRight = 1.0f;
        public volatile bool StopRequested = false;
        public volatile bool Finished = false;
        public volatile string EndReason = null; //Why the mixer dropped it: stopped, ended or the error

        internal double Position = 0.0; //In source frames
        internal float Left = -1.0f; //Current gains, -1 until the first buffer
        internal float Right = -1.0f;
    }

    //Mixes the voices to 48 kHz stereo float, resampling each with linear interpolation. Always returns a full buffer,
    //so the output device keeps running with silence
    public class AudioMixer : ISampleProvider
    {
        public const int OutputRate = 48000;
        private readonly List<Voice> voices = new List<Voice>();
        private readonly object voicesLock = new object();

        public WaveFormat WaveFormat { get; } = WaveFormat.CreateIeeeFloatWaveFormat(OutputRate, 2);
        public float MasterVolume = 0.8f;

        public int VoiceCount
        {
            get { lock (voicesLock) return voices.Count; }
        }

        public void Add(Voice v)
        {
            lock (voicesLock)
                voices.Add(v);
        }

        public void StopAll()
        {
            lock (voicesLock)
            {
                foreach (Voice v in voices)
                {
                    v.StopRequested = true;
                    v.EndReason = "stopped";
                    v.Finished = true;
                }
                voices.Clear();
            }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            //No Array.Clear: NAudio passes its byte buffer reinterpreted as float[] (WaveBuffer), so Array.Clear would
            //see a byte array and zero only a quarter of the samples, replaying the rest of the previous buffer
            for (int i = offset; i < offset + count; i++)
                buffer[i] = 0.0f;
            int frames = count / 2;

            lock (voicesLock)
            {
                for (int i = voices.Count - 1; i >= 0; i--)
                {
                    Voice v = voices[i];
                    string end = null;
                    if (v.StopRequested)
                        end = "stopped";
                    else
                    {
                        //An exception here would end the output device's thread and silence everything, so a bad
                        //voice is dropped on its own
                        try
                        {
                            if (!mix(v, buffer, offset, frames))
                                end = "ended";
                        }
                        catch (Exception ex)
                        {
                            end = "error: " + ex.Message;
                        }
                    }
                    if (end != null)
                    {
                        v.EndReason = end;
                        v.Finished = true;
                        voices.RemoveAt(i);
                    }
                }
            }

            for (int i = offset; i < offset + count; i++)
                buffer[i] = Math.Clamp(buffer[i] * MasterVolume, -1.0f, 1.0f);
            return count;
        }

        //False once the voice has played to its end
        private static bool mix(Voice v, float[] buffer, int offset, int frames)
        {
            DecodedSound s = v.Sound;
            if (s.Frames < 2)
                return false;

            double step = (double) s.SampleRate / OutputRate;
            int loopStart = s.HasLoop ? s.LoopStart : 0;
            int loopEnd = s.HasLoop ? s.LoopEnd : s.Frames;
            if (loopEnd - loopStart < 2)
            {
                loopStart = 0;
                loopEnd = s.Frames;
            }

            float targetL = v.TargetLeft, targetR = v.TargetRight;
            if (v.Left < 0.0f)
            {
                v.Left = targetL;
                v.Right = targetR;
            }
            float dl = (targetL - v.Left) / frames;
            float dr = (targetR - v.Right) / frames;

            for (int f = 0; f < frames; f++)
            {
                if ((v.Loop || v.LoopsLeft > 0) && v.Position >= loopEnd - 1)
                {
                    if (!v.Loop)
                        v.LoopsLeft--;
                    v.Position = loopStart + (v.Position - (loopEnd - 1));
                }
                if (v.Position >= s.Frames - 1)
                    return false;

                int idx = (int) v.Position;
                float frac = (float) (v.Position - idx);
                float l = s.Samples[idx * 2] + (s.Samples[idx * 2 + 2] - s.Samples[idx * 2]) * frac;
                float r = s.Samples[idx * 2 + 1] + (s.Samples[idx * 2 + 3] - s.Samples[idx * 2 + 1]) * frac;

                v.Left += dl;
                v.Right += dr;
                buffer[offset + f * 2] += l * v.Left;
                buffer[offset + f * 2 + 1] += r * v.Right;
                v.Position += step;
            }
            return true;
        }
    }
}
