

using System;
using Microsoft.VisualBasic;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using static engine.Logger;

namespace Boom
{
    public class AudioPlaybackEngine : IDisposable
    {
        private readonly IWavePlayer outputDevice;
        private readonly MixingSampleProvider mixer;

        public AudioPlaybackEngine(int sampleRate = 44100, int channelCount = 2)
        {
            outputDevice = new WaveOutEvent();
            mixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channelCount));
            mixer.ReadFully = true;
            outputDevice.Init(mixer);
            outputDevice.Play();
        }

        #if false
        public void PlaySound(string fileName)
        {
            
            var input = new AudioFileReader(fileName);
            /*
             * Add this input. As soon the stream has finished, it is
             * removed from the mixer.
             */
            AddMixerInput(new AutoDisposeFileReader(input));
        }
#endif

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
            Trace($"Stopping sound.");
            mixer.RemoveMixerInput(sound);
        }

        public void PlaySound(in Sound sound)
        {
            Trace($"Starting sound.");
            mixer.AddMixerInput(sound);
        }

        public void PlaySound(CachedSound sound)
        {
            mixer.AddMixerInput(new CachedSoundSampleProvider(sound));
        }


        public void Dispose()
        {
            outputDevice.Dispose();
        }

        public static readonly AudioPlaybackEngine Instance = new AudioPlaybackEngine(44100, 2);
    }
}