using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CommunityToolkit.Maui.Core.Primitives;
using CommunityToolkit.Maui.Views;
using ManagedDoom.Audio;
using MeltySynth;

namespace ManagedDoom.Maui
{
    public sealed class MauiMusic : IMusic, IDisposable
    {
        private readonly Config config;
        private readonly Wad wad;
        private readonly MediaElement channel;
        private readonly string soundFontPath;
        private readonly string audioDirectory;

        private Bgm current;

        public int MaxVolume => 15;

        public int Volume
        {
            get => config.audio_musicvolume;
            set
            {
                config.audio_musicvolume = Math.Clamp(value, 0, MaxVolume);
                if (channel != null)
                    channel.Volume = config.audio_musicvolume / (float)MaxVolume;
            }
        }

        public MauiMusic(Config config, GameContent content, MediaElement channel, string soundFontPath)
        {
            this.config = config;
            this.wad = content.Wad;
            this.soundFontPath = soundFontPath;
            this.channel = channel;
            channel.ShouldAutoPlay = true;

            current = Bgm.NONE;

            config.audio_musicvolume = Math.Clamp(config.audio_musicvolume, 0, MaxVolume);
            audioDirectory = Path.Combine(ConfigUtilities.GetAppDataDirectory(), "music_clips");
            Directory.CreateDirectory(audioDirectory);

            System.Diagnostics.Debug.WriteLine("Initialized Music.");
        }

        public void StartMusic(Bgm bgm, bool loop)
        {
            if (bgm == current)
                return;

            StopMusic();
            current = bgm;

            var bgmPath = getBgmPath(DoomInfo.BgmNames[(int)bgm]);
            if (File.Exists(bgmPath))
            {
                playMusic(bgm, loop);
            }
            else
            {
                Task.Run(() => unpackBmg(bgmPath)).ContinueWith(_ => playMusic(bgm, loop), TaskScheduler.FromCurrentSynchronizationContext());
            }
        }

        public void StopMusic()
        {
            if (channel?.CurrentState == MediaElementState.Playing)
                channel.Stop();
        }

        public void Dispose()
        {
            System.Diagnostics.Debug.WriteLine("Disposing Music.");
            StopMusic();
        }

        private void playMusic(Bgm bgm, bool loop)
        {
            if (bgm != current)
                return;

            var bgmPath = getBgmPath(DoomInfo.BgmNames[(int)bgm]);
            channel.Source = MediaSource.FromFile(bgmPath);
            channel.Volume = Volume / (float)MaxVolume;
            channel.ShouldLoopPlayback = loop;
            channel.Play();

            System.Diagnostics.Debug.WriteLine($"Playing {current}");
        }

        private string getBgmPath(DoomString bgmName)
        {
            var lump = $"D_{bgmName.ToString().ToUpper()}";
            return Path.Combine(audioDirectory, $"{lump}.wav");
        }

        private void unpackBmg(string bgmPath)
        {
            var lump = Path.GetFileNameWithoutExtension(bgmPath);

            using var musStream = createMusSream(lump);
            using var fs = new FileStream(bgmPath, FileMode.Create, FileAccess.Write);
            musStream.Seek(int.MaxValue, SeekOrigin.Begin);
            musStream.Seek(0, SeekOrigin.Begin);
            musStream.CopyTo(fs);
        }

        private MusStream createMusSream(string lump)
        {
            var data = wad.ReadLump(lump);
            var decoder = createDecoder(data, false);
            return new MusStream(config, decoder, soundFontPath);
        }

        private IDecoder createDecoder(byte[] data, bool loop)
        {
            var isMus = true;
            for (var i = 0; i < MusDecoder.MusHeader.Length; i++)
            {
                if (data[i] != MusDecoder.MusHeader[i])
                {
                    isMus = false;
                    break;
                }
            }

            if (isMus)
                return new MusDecoder(data, loop);

            var isMidi = true;
            for (var i = 0; i < MidiDecoder.MidiHeader.Length; i++)
            {
                if (data[i] != MidiDecoder.MidiHeader[i])
                {
                    isMidi = false;
                    break;
                }
            }

            if (isMidi)
                return new MidiDecoder(data, loop);

            throw new Exception("Unknown music format.");
        }

        private class MusStream : Stream
        {
            private const int blockLength = 2048;

            private readonly Synthesizer synthesizer;
            private readonly float[] left;
            private readonly float[] right;

            private bool headerWritten;
            private bool endOfSequence;

            private MemoryStream memoryStream;
            private volatile IDecoder current;

            public MusStream(Config config, IDecoder decoder, string sfPath)
            {
                var settings = new SynthesizerSettings(MusDecoder.SampleRate);
                settings.BlockSize = MusDecoder.BlockLength;
                settings.EnableReverbAndChorus = config.audio_musiceffect;
                synthesizer = new Synthesizer(sfPath, settings);

                left = new float[blockLength];
                right = new float[blockLength];
                SetDecoder(decoder);
            }

            public override bool CanRead => true;
            public override bool CanSeek => true;
            public override bool CanWrite => false;
            public override long Length => endOfSequence ? memoryStream.Length : -1;
            public override long Position
            {
                get => memoryStream.Position;
                set => throw new NotSupportedException();
            }

            public void SetDecoder(IDecoder decoder)
            {
                if (current != decoder)
                {
                    synthesizer?.Reset();
                    memoryStream?.Dispose();
                    memoryStream = new MemoryStream();
                    headerWritten = false;
                    endOfSequence = false;
                    current = decoder;
                }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                EnsureDataAvailable(Position + count);
                return memoryStream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                var newPosition = origin switch
                {
                    SeekOrigin.Begin => offset,
                    SeekOrigin.Current => Position + offset,
                    SeekOrigin.End => throw new NotSupportedException("Seek from end is not supported."),
                    _ => throw new ArgumentOutOfRangeException(nameof(origin), "Invalid SeekOrigin value."),
                };

                if (newPosition < 0)
                    throw new ArgumentOutOfRangeException(nameof(offset), "Cannot seek to a negative position.");

                EnsureDataAvailable(newPosition);
                return memoryStream.Seek(newPosition, SeekOrigin.Begin);
            }

            private void EnsureDataAvailable(long targetPosition)
            {
                if (memoryStream.Length >= targetPosition || endOfSequence)
                    return;

                var oldPosition = Position;
                do
                {
                    if (!headerWritten)
                    {
                        WriteWavHeader(memoryStream, MusDecoder.SampleRate);
                        headerWritten = true;
                    }

                    if (!current.EndOfSequence)
                    {
                        current.RenderWaveform(synthesizer, left, right);

                        for (int i = 0; i < blockLength; i++)
                        {
                            short leftSample = (short)(left[i] * short.MaxValue);
                            short rightSample = (short)(right[i] * short.MaxValue);

                            WriteSample(memoryStream, leftSample);
                            WriteSample(memoryStream, rightSample);
                        }
                    }
                    else
                    {
                        endOfSequence = true;
                        UpdateWavHeader(memoryStream);
                    }
                }
                while (memoryStream.Length < targetPosition && !endOfSequence);
                memoryStream.Seek(oldPosition, SeekOrigin.Begin);
            }

            public override void Flush() => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException("Setting length is not supported.");
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException("Write operation is not supported.");

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    memoryStream.Dispose();
                }
                base.Dispose(disposing);
            }

            private static void WriteSample(Stream stream, short sample)
            {
                stream.WriteByte((byte)(sample & 0xFF));
                stream.WriteByte((byte)((sample >> 8) & 0xFF));
            }

            private static void WriteWavHeader(Stream stream, int sampleRate)
            {
                // RIFF header
                stream.Write(Encoding.ASCII.GetBytes("RIFF"), 0, 4);
                stream.Write(BitConverter.GetBytes(uint.MaxValue), 0, 4); // Maximum possible file size (0xFFFFFFFF)
                stream.Write(Encoding.ASCII.GetBytes("WAVE"), 0, 4);

                // Format chunk
                stream.Write(Encoding.ASCII.GetBytes("fmt "), 0, 4);
                stream.Write(BitConverter.GetBytes(16), 0, 4); // PCM format chunk size
                stream.Write(BitConverter.GetBytes((short)1), 0, 2); // PCM format
                stream.Write(BitConverter.GetBytes((short)2), 0, 2); // Number of channels (stereo)
                stream.Write(BitConverter.GetBytes(sampleRate), 0, 4); // Sample rate
                stream.Write(BitConverter.GetBytes(sampleRate * 4), 0, 4); // Byte rate
                stream.Write(BitConverter.GetBytes((short)4), 0, 2); // Block align
                stream.Write(BitConverter.GetBytes((short)16), 0, 2); // Bits per sample

                // Data chunk
                stream.Write(Encoding.ASCII.GetBytes("data"), 0, 4);
                stream.Write(BitConverter.GetBytes(uint.MaxValue), 0, 4); // Maximum possible data size (0xFFFFFFFF)
            }

            private static void UpdateWavHeader(Stream stream)
            {
                long fileSize = stream.Length;
                long dataSize = fileSize - 44;

                stream.Seek(4, SeekOrigin.Begin);
                stream.Write(BitConverter.GetBytes((int)(fileSize - 8)), 0, 4); // File size (minus "RIFF" and size field)

                stream.Seek(40, SeekOrigin.Begin);
                stream.Write(BitConverter.GetBytes((int)dataSize), 0, 4); // Data size

                stream.Seek(0, SeekOrigin.End); // Move back to the end of the stream
            }
        }
        private interface IDecoder
        {
            public bool EndOfSequence { get; }
            void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right);
        }

        private class MusDecoder : IDecoder
        {
            public static readonly int SampleRate = 44100;
            public static readonly int BlockLength = SampleRate / 140;

            public static readonly byte[] MusHeader = new byte[]
            {
                (byte)'M',
                (byte)'U',
                (byte)'S',
                0x1A
            };

            private byte[] data;
            private bool loop;

            private int scoreLength;
            private int scoreStart;
            private int channelCount;
            private int channelCount2;
            private int instrumentCount;
            private int[] instruments;

            private MusEvent[] events;
            private int eventCount;

            private int[] lastVolume;
            private int p;
            private int delay;

            private int blockWrote;

            private bool endOfData;

            public bool EndOfSequence => endOfData;

            public MusDecoder(byte[] data, bool loop)
            {
                CheckHeader(data);

                this.data = data;
                this.loop = loop;

                scoreLength = BitConverter.ToUInt16(data, 4);
                scoreStart = BitConverter.ToUInt16(data, 6);
                channelCount = BitConverter.ToUInt16(data, 8);
                channelCount2 = BitConverter.ToUInt16(data, 10);
                instrumentCount = BitConverter.ToUInt16(data, 12);
                instruments = new int[instrumentCount];
                for (var i = 0; i < instruments.Length; i++)
                {
                    instruments[i] = BitConverter.ToUInt16(data, 16 + 2 * i);
                }

                events = new MusEvent[128];
                for (var i = 0; i < events.Length; i++)
                {
                    events[i] = new MusEvent();
                }
                eventCount = 0;

                lastVolume = new int[16];

                Reset();

                blockWrote = BlockLength;
            }

            private static void CheckHeader(byte[] data)
            {
                for (var p = 0; p < MusHeader.Length; p++)
                {
                    if (data[p] != MusHeader[p])
                    {
                        throw new Exception("Invalid format!");
                    }
                }
            }

            public void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right)
            {
                var wrote = 0;
                while (wrote < left.Length)
                {
                    if (blockWrote == synthesizer.BlockSize)
                    {
                        ProcessMidiEvents(synthesizer);
                        blockWrote = 0;
                    }

                    var srcRem = synthesizer.BlockSize - blockWrote;
                    var dstRem = left.Length - wrote;
                    var rem = Math.Min(srcRem, dstRem);

                    synthesizer.Render(left.Slice(wrote, rem), right.Slice(wrote, rem));

                    blockWrote += rem;
                    wrote += rem;
                }
            }

            private void ProcessMidiEvents(Synthesizer synthesizer)
            {
                if (delay > 0)
                {
                    delay--;
                }

                if (delay == 0)
                {
                    delay = ReadSingleEventGroup();
                    SendEvents(synthesizer);

                    if (delay == -1)
                    {
                        synthesizer.NoteOffAll(false);

                        if (loop)
                        {
                            Reset();
                        }
                        else
                        {
                            endOfData = true;
                        }
                    }
                }
            }

            private void Reset()
            {
                for (var i = 0; i < lastVolume.Length; i++)
                {
                    lastVolume[i] = 0;
                }

                p = scoreStart;

                delay = 0;
                endOfData = false;
            }

            private int ReadSingleEventGroup()
            {
                eventCount = 0;
                while (true)
                {
                    var result = ReadSingleEvent();
                    if (result == ReadResult.EndOfGroup)
                    {
                        break;
                    }
                    else if (result == ReadResult.EndOfFile)
                    {
                        return -1;
                    }
                }

                var time = 0;
                while (true)
                {
                    var value = data[p++];
                    time = time * 128 + (value & 127);
                    if ((value & 128) == 0)
                    {
                        break;
                    }
                }

                return time;
            }

            private ReadResult ReadSingleEvent()
            {
                var channelNumber = data[p] & 0xF;

                if (channelNumber == 15)
                {
                    channelNumber = 9;
                }
                else if (channelNumber >= 9)
                {
                    channelNumber++;
                }

                var eventType = (data[p] & 0x70) >> 4;
                var last = (data[p] >> 7) != 0;

                p++;

                var me = events[eventCount];
                eventCount++;

                switch (eventType)
                {
                    case 0: // RELEASE NOTE
                        me.Type = 0;
                        me.Channel = channelNumber;

                        var releaseNote = data[p++];

                        me.Data1 = releaseNote;
                        me.Data2 = 0;

                        break;

                    case 1: // PLAY NOTE
                        me.Type = 1;
                        me.Channel = channelNumber;

                        var playNote = data[p++];
                        var noteNumber = playNote & 127;
                        var noteVolume = (playNote & 128) != 0 ? data[p++] : -1;

                        me.Data1 = noteNumber;
                        if (noteVolume == -1)
                        {
                            me.Data2 = lastVolume[channelNumber];
                        }
                        else
                        {
                            me.Data2 = noteVolume;
                            lastVolume[channelNumber] = noteVolume;
                        }

                        break;

                    case 2: // PITCH WHEEL
                        me.Type = 2;
                        me.Channel = channelNumber;

                        var pitchWheel = data[p++];

                        var pw2 = (pitchWheel << 7) / 2;
                        var pw1 = pw2 & 127;
                        pw2 >>= 7;
                        me.Data1 = pw1;
                        me.Data2 = pw2;

                        break;

                    case 3: // SYSTEM EVENT
                        me.Type = 3;
                        me.Channel = channelNumber;

                        var systemEvent = data[p++];
                        me.Data1 = systemEvent;
                        me.Data2 = 0;

                        break;

                    case 4: // CONTROL CHANGE
                        me.Type = 4;
                        me.Channel = channelNumber;

                        var controllerNumber = data[p++];
                        var controllerValue = data[p++];

                        me.Data1 = controllerNumber;
                        me.Data2 = controllerValue;

                        break;

                    case 6: // END OF FILE
                        return ReadResult.EndOfFile;

                    default:
                        throw new Exception("Unknown event type!");
                }

                if (last)
                {
                    return ReadResult.EndOfGroup;
                }
                else
                {
                    return ReadResult.Ongoing;
                }
            }

            private void SendEvents(Synthesizer synthesizer)
            {
                for (var i = 0; i < eventCount; i++)
                {
                    var me = events[i];
                    switch (me.Type)
                    {
                        case 0: // RELEASE NOTE
                            synthesizer.NoteOff(me.Channel, me.Data1);
                            break;

                        case 1: // PLAY NOTE
                            synthesizer.NoteOn(me.Channel, me.Data1, me.Data2);
                            break;

                        case 2: // PITCH WHEEL
                            synthesizer.ProcessMidiMessage(me.Channel, 0xE0, me.Data1, me.Data2);
                            break;

                        case 3: // SYSTEM EVENT
                            switch (me.Data1)
                            {
                                case 11: // ALL NOTES OFF
                                    synthesizer.NoteOffAll(me.Channel, false);
                                    break;

                                case 14: // RESET ALL CONTROLS
                                    synthesizer.ResetAllControllers(me.Channel);
                                    break;
                            }
                            break;

                        case 4: // CONTROL CHANGE
                            switch (me.Data1)
                            {
                                case 0: // PROGRAM CHANGE
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xC0, me.Data2, 0);
                                    break;

                                case 1: // BANK SELECTION
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x00, me.Data2);
                                    break;

                                case 2: // MODULATION
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x01, me.Data2);
                                    break;

                                case 3: // VOLUME
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x07, me.Data2);
                                    break;

                                case 4: // PAN
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x0A, me.Data2);
                                    break;

                                case 5: // EXPRESSION
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x0B, me.Data2);
                                    break;

                                case 6: // REVERB
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x5B, me.Data2);
                                    break;

                                case 7: // CHORUS
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x5D, me.Data2);
                                    break;

                                case 8: // PEDAL
                                    synthesizer.ProcessMidiMessage(me.Channel, 0xB0, 0x40, me.Data2);
                                    break;
                            }
                            break;
                    }
                }
            }

            private class MusEvent
            {
                public int Type;
                public int Channel;
                public int Data1;
                public int Data2;
            }

            private enum ReadResult
            {
                Ongoing,
                EndOfGroup,
                EndOfFile
            }
        }

        private class MidiDecoder : IDecoder
        {
            public static readonly byte[] MidiHeader = new byte[]
            {
                (byte)'M',
                (byte)'T',
                (byte)'h',
                (byte)'d'
            };

            private MidiFile midi;
            private MidiFileSequencer sequencer;

            private bool loop;

            public bool EndOfSequence => sequencer.EndOfSequence;

            public MidiDecoder(byte[] data, bool loop)
            {
                midi = new MidiFile(new MemoryStream(data));

                this.loop = loop;
            }

            public void RenderWaveform(Synthesizer synthesizer, Span<float> left, Span<float> right)
            {
                if (sequencer == null)
                {
                    sequencer = new MidiFileSequencer(synthesizer);
                    sequencer.Play(midi, loop);
                }

                sequencer.Render(left, right);
            }
        }
    }
}
