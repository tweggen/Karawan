using NAudio.Wave;


namespace Boom
{

    public class ChannelStripProvider
    {
        private object _lo = new();
        
        public WaveFormat InWaveFormat;
        public WaveFormat WaveFormat;
        public ISampleProvider SourceSampleProvider;

        private bool _recomputeResampler = true;
        private bool _recomputeMixer = true;
        
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
            int channels;

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
                channels = InWaveFormat.Channels;
            }

            if (recomputeResampler)
            {
                _wdlResampler.SetRates(inSamplingRate / speed, outSamplingRate);
            }

            int framesRequested = count / channels;

            /*
             * This buffer for original sample data will be allocated by the resampler.
             */
            float[] inBuffer;
            
            /*
             * We need to compute an output buffer for the resampler containing the same
             * number of channels as the input.
             */
            float[] resampleOutBuffer;

            if (channels == WaveFormat.Channels)
            {
                /*
                 * Same number of channels, now resampling required.
                 */
                resampleOutBuffer = buffer;
            }
            else
            {
                resampleOutBuffer = new float[framesRequested*channels];
            }
            
            /*
             * ... also the offset into the source data will be computed.
             */
            int inBufferOffset;

            /*
             * First ask the resampler how many samples would be needed. 
             */
            int inNeeded = _wdlResampler.ResamplePrepare(framesRequested, channels, out inBuffer, out inBufferOffset);
            /*
             * Then read that number of samples from the source.
             */
            int inAvailable = SourceSampleProvider.Read(inBuffer, inBufferOffset, inNeeded * channels) / channels;
            /*
             * Now resample them to the resampling buffer. Note, that the output buffer still does contain the source number
             * of channels.
             */
            int outAvailable = _wdlResampler.ResampleOut(resampleOutBuffer, offset, inAvailable, framesRequested, channels);
            
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
                    buffer[n * 2 + 0] = left * buffer[n * 2 + 0];
                    buffer[n * 2 + 1] = right * buffer[n * 2 + 1];
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
                    float sourceValue = resampleOutBuffer[n];
                    buffer[n * 2 + 0] = left * sourceValue;
                    buffer[n * 2 + 1] = right * sourceValue;
                }
            }
            return outAvailable * WaveFormat.Channels;
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