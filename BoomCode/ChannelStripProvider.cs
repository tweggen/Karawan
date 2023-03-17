using engine;
using NAudio.Wave;
using static engine.Logger;

namespace Boom
{

    public class ChannelStripProvider : ISampleProvider
    {
        private object _lo = new();
        
        public WaveFormat InWaveFormat;
        public WaveFormat WaveFormat { get; set; }
        public ISampleProvider SourceSampleProvider;

        private bool _recomputeResampler = true;
        private bool _recomputeMixer = true;

        private float _lastVolume = 0f;
        private float _volume = 1f;

        public float Volume
        {
            get => _volume;
            set
            {
                _recomputeMixer = true;
                _volume = value;
            }
        }

        private float _lastPan = 0f;
        private float _pan = 0f;

        public float Pan
        {
            get => _pan;
            set
            {
                _recomputeMixer = true;
                _pan = value;
            }
        }

        private float _lastSpeed = 1f;
        private float _speed = 1f;

        public float Speed
        {
            get => _speed;
            set
            {
                _recomputeResampler = true;
                _speed = value;
            }
        }

        private Boom.WdlResampler _wdlResampler = new();

        public float OutSamplingRate { get; set; } = 44100f;

        private float _lastLeft = 0f;
        private float _lastRight = 0f;
        
        public int Read(float[] buffer, int offset, int count)
        {
            if (0 == count)
            {
                return 0;
            }
            
            float left, right;
            bool recomputeMixer = true;
            bool recomputeResampler = true;
            float inSamplingRate;
            float outSamplingRate;
            float speed;
            int inChannels, outChannels;

            lock (_lo)
            {
                float validatedPan = Math.Max(-1f, Math.Min(1f, Pan));
                left = 1.0f - validatedPan;
                right = 1.0f + validatedPan;
                left *= Volume;
                right *= Volume;

                inSamplingRate = InWaveFormat.SampleRate;
                outSamplingRate = OutSamplingRate;
                speed = _speed;
                if (speed < 0.001f)
                {
                    speed = 0.001f;
                }
                recomputeResampler = _recomputeResampler;
                _recomputeResampler = false;
                // ignore _recomputeMixer
                _recomputeMixer = false;
                inChannels = InWaveFormat.Channels;
                outChannels = WaveFormat.Channels;
            }

            if (recomputeResampler)
            {
                _wdlResampler.SetRates(inSamplingRate * speed, outSamplingRate);
            }
            
            int framesRequested = count / outChannels;

            /*
             * This buffer for original sample data will be allocated by the resampler.
             */
            float[] inBuffer;
            
            /*
             * We need to compute an output buffer for the resampler containing the same
             * number of channels as the input.
             */
            float[] resampleOutBuffer;
            int outOffset;

            if (inChannels == outChannels)
            {
                /*
                 * Same number of channels, now resampling required.
                 */
                resampleOutBuffer = buffer;
                outOffset = offset;
            }
            else
            {
                resampleOutBuffer = new float[offset + framesRequested*outChannels];
                outOffset = 0;
            }
            
            /*
             * ... also the offset into the source data will be computed.
             */
            int inBufferOffset;

#if true
            float inEffectiveRate = inSamplingRate * speed;
            int inNeeded = (framesRequested * (int)inEffectiveRate + (int)outSamplingRate/2) / (int)outSamplingRate;
            inBufferOffset = 0;
            inBuffer = new float[inNeeded];
            int totalSamplesRead = 0;
            int totalSamplesRequested = inNeeded * inChannels;
            while (totalSamplesRead < totalSamplesRequested)
            {
                int inRead = SourceSampleProvider.Read(inBuffer, inBufferOffset+totalSamplesRead, inNeeded * inChannels-totalSamplesRead) / inChannels;
                totalSamplesRead += inRead;
            }

            int inAvailable = totalSamplesRequested;
            // TXWTODO: test inAvailable
            int outAvailable = framesRequested;
            for (int n = 0; n < framesRequested; n++)
            {
                int inOffset = Int32.Min(inAvailable-1,(n*(int)inEffectiveRate) / (int)outSamplingRate);
                resampleOutBuffer[outOffset+n] = inBuffer[inOffset];
            }
#else
            /*
             * First ask the resampler how many frames would be needed. 
             */
            int inNeeded = _wdlResampler.ResamplePrepare(framesRequested, inChannels, out inBuffer, out inBufferOffset);
            /*
             * Then read that number of frames from the source.
             */
            int inAvailable = SourceSampleProvider.Read(inBuffer, inBufferOffset, inNeeded * inChannels) / inChannels;
            /*
             * Now resample them to the resampling buffer. Note, that the output buffer still does contain the source number
             * of channels.
             */
            int outAvailable = _wdlResampler.ResampleOut(resampleOutBuffer, outOffset, inAvailable, framesRequested, inChannels);
#endif

            /*
             * We might or might not have the same number of channels in the output buffer than in the input buffer.
             * No matter which way, apply volume and pan and copy if req'd.
             */
            if (resampleOutBuffer == buffer)
            {
                /*
                 * This is stereo, apply pan and volume in-place.
                 */
                for (int n = 0; n < outAvailable; ++n)
                {
                    float l = _lastLeft + (float)((left - _lastLeft) * n) / (float)outAvailable;
                    float r = _lastRight + (float)((right - _lastRight) * n) / (float)outAvailable;
                    buffer[offset + n * 2 + 0] = l * buffer[offset + n * 2 + 0];
                    buffer[offset + n * 2 + 1] = r * buffer[offset + n * 2 + 1];
                }
            }
            else
            {
                /*
                 * This is mono to stereo, apply volume and pan while copying.
                 */
                /*
                 * This is stereo, apply pan and volume in-place.
                 */
                for (int n = 0; n < outAvailable; ++n)
                {
                    float scale = (float)n / (float)outAvailable;
                    float sourceValue = resampleOutBuffer[n];
                    float l = _lastLeft + (float)(left - _lastLeft) * scale;
                    float r = _lastRight + (float)(right - _lastRight) * scale;
                    buffer[offset + n * 2 + 0] = l * sourceValue;
                    buffer[offset + n * 2 + 1] = r * sourceValue;
                }
            }

            _lastLeft = left;
            _lastRight = right;
            
            return outAvailable*outChannels;
        }


#if false
        private int _readStereo(float[] buffer, int offset, int count)
        {
            
        }
#endif
        
        
        public ChannelStripProvider(in ISampleProvider sourceSampleProvider)
        {
            SourceSampleProvider = sourceSampleProvider;
            InWaveFormat = sourceSampleProvider.WaveFormat;
            
            int channels = InWaveFormat.Channels;
            switch (channels)
            {
                case 1:
                    break;
                case 2:
                    break;
                default:
                    throw new NotImplementedException("Only Mono sources are supported.");
            }
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
                (int)OutSamplingRate, 2);

            _wdlResampler.SetMode(true, 2, false);
            _wdlResampler.SetFilterParms();
            _wdlResampler.SetFeedMode(false); // output driven
            _wdlResampler.SetRates(InWaveFormat.SampleRate / Speed, OutSamplingRate);
        }
    }
}