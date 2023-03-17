

using System;
using engine;
using Microsoft.VisualBasic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using static engine.Logger;

namespace Boom
{
    public class AudioPlaybackEngine : IDisposable
    {
        public static readonly AudioPlaybackEngine Instance = new AudioPlaybackEngine(44100, 2);

        private object _lo = new();
        private readonly IWavePlayer outputDevice;
        private readonly MixingSampleProvider mixer;

        private int _nSounds = 0;

        private SortedDictionary<string, CachedSound> _cachedSoundDictionary = new();
        private SortedDictionary<Sound, Sound> _runningSounds = new();

        public void FindCachedSound(string Uri, Action<Boom.CachedSound> actionOnHaveSound)
        {
            // Async this really instead of just wasting threads.
            CachedSound bCachedSound = null;
            lock (_lo)
            {
                if (_cachedSoundDictionary.ContainsKey(Uri))
                {
                    bCachedSound = _cachedSoundDictionary[Uri];
                }
                else
                {
                    Wonder($"Loading new sound {Uri}");
                    bCachedSound = new CachedSound(Uri);
                    _cachedSoundDictionary[Uri] = bCachedSound;
                }
            }

            actionOnHaveSound( bCachedSound );
        }
        

        private ISampleProvider ConvertToRightChannelCount(ISampleProvider input)
        {
            if (input.WaveFormat.Channels == mixer.WaveFormat.Channels)
            {
                return input;
            }

            if (input.WaveFormat.Channels == 1 && mixer.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }

            throw new NotImplementedException("Not yet implemented this channel count conversion");
        }

        
        public void StopSound(in Sound sound)
        {
            lock (_lo)
            {
                if (!_runningSounds.ContainsKey(sound))
                {
                    Warning($"Stopping already stopped sound.");
                    return;
                }

                _runningSounds.Remove(sound);

                --_nSounds;
            }
            Trace($"Stopping sound, now {_nSounds}");
            mixer.RemoveMixerInput(sound);
        }

        public void PlaySound(in Sound sound)
        {
            lock (_lo)
            {
                if (_runningSounds.ContainsKey(sound))
                {
                    Warning($"Starting an already started sound.");
                    return;
                }
                _runningSounds.Add(sound, sound);

                ++_nSounds;
            }
            Trace($"Starting sound, now {_nSounds}");
            mixer.AddMixerInput(sound);
        }
        

        public void PlaySound(CachedSound sound)
        {
            mixer.AddMixerInput(new ChannelStripProvider(new LoopSampleProvider(new CachedSoundSampleProvider(sound))));
        }


        public void Dispose()
        {
            outputDevice.Dispose();
        }

        public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
        {
            outputDevice = new WaveOutEvent();
            mixer = new Boom.MixingSampleProvider(
                WaveFormat.CreateIeeeFloatWaveFormat(
                    sampleRate,
                    channelCount));
            outputDevice.Init(mixer);
            outputDevice.Play();
        }

    }
}