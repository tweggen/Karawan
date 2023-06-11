

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
        private readonly IWavePlayer _outputDevice;
        private readonly MixingSampleProvider _mixer;

        private int _nSounds = 0;

        private SortedDictionary<string, CachedSound> _cachedSoundDictionary = new();
        private SortedDictionary<Sound, Sound> _runningSounds = new();

        private bool _traceStartStop()
        {
            return engine.GlobalSettings.Get("boom.AudioPlaybackEngine.TraceStartStop") == "true";
        }

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
            if (input.WaveFormat.Channels == _mixer.WaveFormat.Channels)
            {
                return input;
            }

            if (input.WaveFormat.Channels == 1 && _mixer.WaveFormat.Channels == 2)
            {
                return new MonoToStereoSampleProvider(input);
            }

            throw new NotImplementedException("Not yet implemented this channel count conversion");
        }

        
        public void StopSound(Sound sound)
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
            if( _traceStartStop() ) Trace($"Stopping sound, now {_nSounds}");
            _mixer.FadeoutMixerInput(sound);
        }

        public void PlaySound(Sound sound)
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
            if( _traceStartStop() ) Trace($"Starting sound, now {_nSounds}");
            _mixer.AddMixerInput(sound);
        }
        

        public void PlaySound(CachedSound sound)
        {
            _mixer.AddMixerInput(new CachedSoundSampleProvider(sound));
        }


        public void Dispose()
        {
            _outputDevice.Dispose();
        }

        public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
        {
#if false
            WaveOutEvent outputDevice = new NAudio.Wave.WaveOutEvent();
            outputDevice.DesiredLatency = 200;
            outputDevice.NumberOfBuffers = 2;
#endif
#if true
            DirectSoundOut outputDevice = new NAudio.Wave.DirectSoundOut(40);
#endif
            _mixer = new Boom.MixingSampleProvider(
                WaveFormat.CreateIeeeFloatWaveFormat(
                    sampleRate,
                    channelCount));
            outputDevice.Init(_mixer);
            _outputDevice = outputDevice;
            outputDevice.Play();
        }

    }
}