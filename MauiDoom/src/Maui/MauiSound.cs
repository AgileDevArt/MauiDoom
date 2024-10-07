//
// Rewritten Copyright:
// Original Code by Id Software, Inc. (1993-1996)
// Modified by Nobuaki Tanaka (2019-2020)
// Rewritten by [Your Name], [Year]
//
// This program is free software; you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation; either version 2 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using ManagedDoom.Audio;
using CommunityToolkit.Maui.Views;
using CommunityToolkit.Maui.Core.Primitives;

namespace ManagedDoom.Maui
{
    public sealed class MauiSound : ISound, IDisposable
    {
        private const int ChannelCount = 8;
        private const float ClipDist = 1200f;
        private const float CloseDist = 160f;
        private const float Attenuator = ClipDist - CloseDist;

        private readonly float FastDecay = (float)Math.Pow(0.5, 1.0 / (35f / 5f));
        private readonly float SlowDecay = (float)Math.Pow(0.5, 1.0 / 35f);

        private readonly Config config;
        private readonly Dictionary<Sfx, string> audioClipPaths;
        private readonly float[] amplitudes;

        private readonly DoomRandom random;

        private readonly string audioDirectory;
        private readonly List<MediaElement> channels;
        private readonly MediaElement uiChannel;
        private readonly ChannelInfo[] infos;

        private Sfx uiReserved;

        private Mobj listener;

        private float masterVolumeDecay;

        private DateTime lastUpdate;


        public MauiSound(Config config, GameContent content, MediaElement[] channels)
        {
            try
            {
                System.Diagnostics.Debug.Write("Initialize sound: ");
                this.config = config;
                config.audio_soundvolume = Math.Clamp(config.audio_soundvolume, 0, MaxVolume);

                audioClipPaths = new Dictionary<Sfx, string>();
                amplitudes = new float[DoomInfo.SfxNames.Length];

                if (config.audio_randompitch)
                    random = new DoomRandom();

                audioDirectory = Path.Combine(ConfigUtilities.GetAppDataDirectory(), "audio_clips");
                Directory.CreateDirectory(audioDirectory);

                // Load audio clips from WAD files and save as WAV files
                for (var i = 0; i < DoomInfo.SfxNames.Length; i++)
                {
                    var sfx = DoomInfo.SfxNames[i];
                    var name = "DS" + sfx.ToString().ToUpper();
                    var lump = content.Wad.GetLumpNumber(name);
                    if (lump == -1)
                        continue;

                    var samples = GetSamples(content.Wad, name, out var sampleRate, out var sampleCount);
                    if (samples.IsEmpty)
                        continue;

                    // Convert raw samples to WAV format and save
                    string wavPath = Path.Combine(audioDirectory, $"{sfx}.wav");
                    if (!File.Exists(wavPath))
                        ConvertToWav(samples.ToArray(), sampleRate, 1, wavPath);

                    Enum.TryParse(sfx.ToString().ToUpper(), out Sfx sfxEnum);
                    audioClipPaths[sfxEnum] = wavPath;

                    amplitudes[i] = GetAmplitude(samples, sampleRate, sampleCount);
                }

                this.channels = new List<MediaElement>();
                infos = new ChannelInfo[ChannelCount];
                for (var i = 0; i < ChannelCount; i++)
                {
                    //channels[i].Panning = 0;
                    channels[i].ShouldAutoPlay = true;
                    this.channels.Add(channels[i]);
                    infos[i] = new ChannelInfo();
                }

                uiChannel = channels[8];
                //uiChannel.Panning = 0;
                uiChannel.ShouldAutoPlay = true;
                uiReserved = Sfx.NONE;

                masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;
                lastUpdate = DateTime.MinValue;
                System.Diagnostics.Debug.WriteLine("OK");
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine($"Failed. {e.Message}");
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Converts raw PCM samples to WAV format and saves to the specified path.
        /// </summary>
        /// <param name="pcmData">Raw PCM byte data.</param>
        /// <param name="sampleRate">Sample rate of the audio.</param>
        /// <param name="channels">Number of audio channels.</param>
        /// <param name="wavPath">Path to save the WAV file.</param>
        private void ConvertToWav(byte[] pcmData, int sampleRate, int channels, string wavPath)
        {
            using (var stream = new FileStream(wavPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                // RIFF header
                writer.Write(new char[] { 'R', 'I', 'F', 'F' });
                writer.Write(36 + pcmData.Length); // File size - 8 bytes
                writer.Write(new char[] { 'W', 'A', 'V', 'E' });

                // fmt subchunk
                writer.Write(new char[] { 'f', 'm', 't', ' ' });
                writer.Write(16); // Subchunk1Size for PCM
                writer.Write((short)1); // AudioFormat (1 = PCM)
                writer.Write((short)channels);
                writer.Write(sampleRate);
                writer.Write(sampleRate * channels * 1); // ByteRate (sampleRate * channels * bytesPerSample)
                writer.Write((short)(channels * 1)); // BlockAlign (channels * bytesPerSample)
                writer.Write((short)8); // BitsPerSample

                // data subchunk
                writer.Write(new char[] { 'd', 'a', 't', 'a' });
                writer.Write(pcmData.Length);
                writer.Write(pcmData);
            }
        }

        private static Span<byte> GetSamples(Wad wad, string name, out int sampleRate, out int sampleCount)
        {
            var data = wad.ReadLump(name);

            if (data.Length < 8)
            {
                sampleRate = -1;
                sampleCount = -1;
                return Span<byte>.Empty;
            }

            sampleRate = BitConverter.ToUInt16(data, 2);
            sampleCount = BitConverter.ToInt32(data, 4);

            var offset = 8;
            if (ContainsDmxPadding(data))
            {
                offset += 16;
                sampleCount -= 32;
            }

            if (sampleCount > 0 && offset + sampleCount <= data.Length)
            {
                return data.AsSpan(offset, sampleCount);
            }
            else
            {
                sampleRate = -1;
                sampleCount = -1;
                return Span<byte>.Empty;
            }
        }

        // Check if the data contains pad bytes.
        // If the first and last 16 samples are the same,
        // the data should contain pad bytes.
        // https://doomwiki.org/wiki/Sound
        private static bool ContainsDmxPadding(byte[] data)
        {
            var sampleCount = BitConverter.ToInt32(data, 4);
            if (sampleCount < 32)
            {
                return false;
            }
            else
            {
                var first = data[8];
                for (var i = 1; i < 16; i++)
                {
                    if (data[8 + i] != first)
                    {
                        return false;
                    }
                }

                var last = data[8 + sampleCount - 1];
                for (var i = 1; i < 16; i++)
                {
                    if (data[8 + sampleCount - i - 1] != last)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static float GetAmplitude(Span<byte> samples, int sampleRate, int sampleCount)
        {
            var max = 0;
            if (sampleCount > 0)
            {
                var count = Math.Min(sampleRate / 5, sampleCount);
                for (var t = 0; t < count; t++)
                {
                    var a = samples[t] - 128;
                    if (a < 0)
                    {
                        a = (byte)(-a);
                    }
                    if (a > max)
                    {
                        max = a;
                    }
                }
            }
            return (float)max / 128f;
        }

        public void SetListener(Mobj listener)
        {
            this.listener = listener;
        }

        public void Update()
        {
            var now = DateTime.Now;
            if ((now - lastUpdate).TotalSeconds < 0.01)  
                return; // Don't update so frequently (for timedemo).

            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                var channel = channels[i];
                if (info.Playing != Sfx.NONE)
                {
                    if (channel.CurrentState is MediaElementState.None or MediaElementState.Stopped or MediaElementState.Failed or MediaElementState.Paused)
                    {
                        info.Playing = Sfx.NONE;
                        if (info.Reserved == Sfx.NONE)
                        {
                            info.Source = null;
                            //channel.Panning = 0;
                            channel.Volume = 0;
                        }
                    }
                    else
                    {
                        info.Priority *= (info.Type == SfxType.Diffuse) ? SlowDecay : FastDecay;
                        SetParam(channel, info);
                    }
                }

                if (info.Reserved != Sfx.NONE)
                {
                    if (info.Playing != Sfx.NONE)
                        channel.Stop();

                    channel.Source = audioClipPaths.ContainsKey(info.Reserved) ? audioClipPaths[info.Reserved] : string.Empty;
                    channel.Volume = 0.01f * masterVolumeDecay * info.Volume;
                    //channel.Panning = GetPanning(info);
                    channel.Play();

                    info.Playing = info.Reserved;
                    info.Reserved = Sfx.NONE;
                }
            }

            // Handle UI reserved sound
            if (uiReserved != Sfx.NONE)
            {
                if (uiChannel.CurrentState is MediaElementState.Playing or MediaElementState.Opening or MediaElementState.Buffering)
                    uiChannel.Stop();

                uiChannel.Source = audioClipPaths.ContainsKey(uiReserved) ? audioClipPaths[uiReserved] : string.Empty;
                uiChannel.Volume = masterVolumeDecay;
                //uiChannel.Panning = 0; // Center
                uiChannel.Play();

                uiReserved = Sfx.NONE;
            }

            lastUpdate = now;
        }

        public void StartSound(Sfx sfx)
        {
            if (!audioClipPaths.ContainsKey(sfx))
                return;

            uiReserved = sfx;
        }

        public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume = 100)
        {
            if (!audioClipPaths.ContainsKey(sfx))
                return;

            var x = (mobj.X - listener.X).ToFloat();
            var y = (mobj.Y - listener.Y).ToFloat();
            var dist = MathF.Sqrt(x * x + y * y);

            var priority = type != SfxType.Diffuse 
                ? amplitudes[(int)sfx] * GetDistanceDecay(dist) * volume 
                : volume;

            // Check if the sound is already playing on a channel for this mobj and type
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Source == mobj && info.Type == type)
                {
                    info.Reserved = sfx;
                    info.Priority = priority;
                    info.Volume = volume;
                    return;
                }
            }

            // Find a free channel
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Reserved == Sfx.NONE && info.Playing == Sfx.NONE)
                {
                    info.Reserved = sfx;
                    info.Priority = priority;
                    info.Source = mobj;
                    info.Type = type;
                    info.Volume = volume;
                    return;
                }
            }

            // Find the channel with the lowest priority
            var minPriority = float.MaxValue;
            var minChannel = -1;
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Priority < minPriority)
                {
                    minPriority = info.Priority;
                    minChannel = i;
                }
            }

            // Replace the channel if the new priority is higher
            if (minChannel != -1 && priority >= minPriority)
            {
                var info = infos[minChannel];
                info.Reserved = sfx;
                info.Priority = priority;
                info.Source = mobj;
                info.Type = type;
                info.Volume = volume;
            }
        }

        public void StopSound(Mobj mobj)
        {
            for (var i = 0; i < infos.Length; i++)
            {
                var info = infos[i];
                if (info.Source != mobj)
                    continue;

                info.Source = null;
                info.Volume /= 5;
                channels[i].Stop();
            }
        }

        public void Reset()
        {
            if (random != null)
                random.Clear();


            for (var i = 0; i < infos.Length; i++)
            {
                channels[i].Stop();
                infos[i].Clear();
            }

            uiChannel.Stop();
            listener = null;
        }

        public void Pause()
        {
            for (var i = 0; i < channels.Count; i++)
                if (channels[i].CurrentState == MediaElementState.Playing && infos[i].Playing != Sfx.NONE)
                    channels[i].Pause();

            if (uiChannel.CurrentState == MediaElementState.Playing)
                uiChannel.Pause();
        }

        public void Resume()
        {
            for (var i = 0; i < channels.Count; i++)
                if (channels[i].CurrentState == MediaElementState.Paused && infos[i].Playing != Sfx.NONE)
                    channels[i].Play();

            if (uiChannel.CurrentState == MediaElementState.Paused && uiReserved != Sfx.NONE)
                uiChannel.Play();
        }

        private void SetParam(MediaElement channel, ChannelInfo info)
        {
            if (info.Type == SfxType.Diffuse)
            {
                //channel.Panning = 0f; // Center
                channel.Volume = 0.01f * masterVolumeDecay * info.Volume;
            }
            else
            {
                float sourceX, sourceY;

                if (info.Source == null)
                {
                    sourceX = info.LastX.ToFloat();
                    sourceY = info.LastY.ToFloat();
                }
                else
                {
                    sourceX = info.Source.X.ToFloat();
                    sourceY = info.Source.Y.ToFloat();
                }

                var x = sourceX - listener.X.ToFloat();
                var y = sourceY - listener.Y.ToFloat();

                if (Math.Abs(x) < 16f && Math.Abs(y) < 16f)
                {
                    //channel.Panning = 0f; // Center
                    channel.Volume = 0.01f * masterVolumeDecay * info.Volume;
                }
                else
                {
                    var dist = MathF.Sqrt(x * x + y * y);
                    var angle = MathF.Atan2(y, x) - (float)listener.Angle.ToRadian();
                    var pan = MathF.Sin(angle); // Simple panning based on angle
                    //channel.Panning = pan;
                    channel.Volume = 0.01f * masterVolumeDecay * GetDistanceDecay(dist) * info.Volume;
                }
            }
        }

        private float GetDistanceDecay(float dist) 
            => dist >= CloseDist
                ? MathF.Max((ClipDist - dist) / Attenuator, 0f)
                : 1f;

        private float GetPitch(SfxType type, Sfx sfx)
        {
            if (random == null)
                return 1.0f;

            return sfx switch
            {
                Sfx.ITEMUP or Sfx.TINK or Sfx.RADIO => 1.0f,
                _ => type == SfxType.Voice
                    ? 1.0f + 0.075f * ((float)(random.Next() - 128) / 128f)
                    : 1.0f + 0.025f * ((float)(random.Next() - 128) / 128f),
            };
        }

        private float GetPanning(ChannelInfo info)
        {
            if (info.Source == null)
            {
                return 0f;
            }

            var x = info.Source.X.ToFloat() - listener.X.ToFloat();
            var y = info.Source.Y.ToFloat() - listener.Y.ToFloat();

            var angle = MathF.Atan2(y, x);
            var pan = MathF.Sin(angle); // Simple left-right panning based on angle

            return pan;
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("Shutdown sound.");
            foreach (var channel in channels)
                channel?.Stop();

            uiChannel?.Stop();
        }

        public int MaxVolume => 15;

        public int Volume
        {
            get => config.audio_soundvolume;
            set
            {
                config.audio_soundvolume = value;
                masterVolumeDecay = (float)config.audio_soundvolume / MaxVolume;

                for (var i = 0; i < channels.Count; i++)
                    if (infos[i].Playing != Sfx.NONE)
                        channels[i].Volume = 0.01f * masterVolumeDecay * infos[i].Volume;

                if (uiReserved != Sfx.NONE)
                    uiChannel.Volume = masterVolumeDecay;
            }
        }

        /// <summary>
        /// Converts degrees to radians.
        /// </summary>
        private float DegreesToRadians(float degrees) => degrees * (float)Math.PI / 180f;

        /// <summary>
        /// Represents information about an audio channel.
        /// </summary>
        private class ChannelInfo
        {
            public Sfx Reserved;
            public Sfx Playing;
            public float Priority;

            public Mobj Source;
            public SfxType Type;
            public int Volume;
            public Fixed LastX;
            public Fixed LastY;

            public void Clear()
            {
                Reserved = Sfx.NONE;
                Playing = Sfx.NONE;
                Priority = 0;
                Source = null;
                Type = 0;
                Volume = 0;
                LastX = Fixed.Zero;
                LastY = Fixed.Zero;
            }
        }        
    }

    public class SoundDispatcher : ISound, IDisposable
    {
        readonly MauiSound _sound;
        public int MaxVolume => _sound.MaxVolume;

        public SoundDispatcher(MauiSound sound) => _sound = sound;

        public int Volume
        {
            get => _sound.Volume;
            set => MainThread.BeginInvokeOnMainThread(() => _sound.Volume = value);
        }

        public void Pause() => MainThread.BeginInvokeOnMainThread(_sound.Pause);
        public void Reset() => MainThread.BeginInvokeOnMainThread(_sound.Reset);
        public void Resume() => MainThread.BeginInvokeOnMainThread(_sound.Resume);
        public void SetListener(Mobj listener) => MainThread.BeginInvokeOnMainThread(() => _sound.SetListener(listener));
        public void StartSound(Sfx sfx) => MainThread.BeginInvokeOnMainThread(() => _sound.StartSound(sfx));
        public void StartSound(Mobj mobj, Sfx sfx, SfxType type) => MainThread.BeginInvokeOnMainThread(() => _sound.StartSound(mobj, sfx, type));
        public void StartSound(Mobj mobj, Sfx sfx, SfxType type, int volume) => MainThread.BeginInvokeOnMainThread(() => _sound.StartSound(mobj, sfx, type, volume));
        public void StopSound(Mobj mobj) => MainThread.BeginInvokeOnMainThread(() => _sound.StopSound(mobj));
        public void Update() => MainThread.BeginInvokeOnMainThread(_sound.Update);
        public void Dispose() => MainThread.BeginInvokeOnMainThread(_sound.Dispose);
    }
}
