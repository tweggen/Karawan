using System;
using System.Collections.Generic;
using NAudio.Utils;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Boom
{
    /// <summary>
    /// A sample provider mixer, allowing inputs to be added and removed
    /// </summary>
    public class MixingSampleProvider : ISampleProvider
    {
        private readonly List<ISampleProvider> _sources;
        private float[] _sourceBuffer;
        private const int MaxInputs = 1024; // protect ourselves against doing something silly

#if false
        /// <summary>
        /// Returns the mixer inputs (read-only - use AddMixerInput to add an input
        /// </summary>
        public IEnumerable<ISampleProvider> MixerInputs => _sources;
#endif

        /// <summary>
        /// When set to true, the Read method always returns the number
        /// of samples requested, even if there are no inputs, or if the
        /// current inputs reach their end. Setting this to true effectively
        /// makes this a never-ending sample provider, so take care if you plan
        /// to write it out to a file.
        /// </summary>
        public bool ReadFully { get; set; }

        /// <summary>
        /// Adds a new mixer input
        /// </summary>
        /// <param name="mixerInput">Mixer input</param>
        public void AddMixerInput(ISampleProvider mixerInput)
        {
            // we'll just call the lock around add since we are protecting against an AddMixerInput at
            // the same time as a Read, rather than two AddMixerInput calls at the same time
            lock (_sources)
            {
                if (_sources.Count >= MaxInputs)
                {
                    throw new InvalidOperationException("Too many mixer inputs");
                }

                _sources.Add(mixerInput);
            }

            if (WaveFormat == null)
            {
                WaveFormat = mixerInput.WaveFormat;
            }
            else
            {
                if (WaveFormat.SampleRate != mixerInput.WaveFormat.SampleRate ||
                    WaveFormat.Channels != mixerInput.WaveFormat.Channels)
                {
                    throw new ArgumentException("All mixer inputs must have the same WaveFormat");
                }
            }
        }

        /// <summary>
        /// Raised when a mixer input has been removed because it has ended
        /// </summary>
        public event EventHandler<SampleProviderEventArgs> MixerInputEnded;

        /// <summary>
        /// Removes a mixer input
        /// </summary>
        /// <param name="mixerInput">Mixer input to remove</param>
        public void RemoveMixerInput(ISampleProvider mixerInput)
        {
            lock (_sources)
            {
                _sources.Remove(mixerInput);
            }
        }

        /// <summary>
        /// Removes all mixer inputs
        /// </summary>
        public void RemoveAllMixerInputs()
        {
            lock (_sources)
            {
                _sources.Clear();
            }
        }

        /// <summary>
        /// The output WaveFormat of this sample provider
        /// </summary>
        public WaveFormat WaveFormat { get; private set; }

        /// <summary>
        /// Reads samples from this sample provider
        /// </summary>
        /// <param name="buffer">Sample buffer</param>
        /// <param name="offset">Offset into sample buffer</param>
        /// <param name="count">Number of samples required</param>
        /// <returns>Number of samples read</returns>
        public int Read(float[] buffer, int offset, int count)
        {
            /*
             * We need a local copy of the sources.
             */
            List<ISampleProvider> localSources;

            /*
             * And also a local copy of the sources we would like to remove.
             */
            List<ISampleProvider> localSourcesToRemove = new();

            lock (_sources)
            {
                localSources = new List<ISampleProvider>(_sources);
            }

            int outputSamples = 0;
            _sourceBuffer = BufferHelpers.Ensure(_sourceBuffer, count);

            int index = localSources.Count - 1;
            while (index >= 0)
            {
                var source = localSources[index];
                int samplesRead = source.Read(_sourceBuffer, 0, count);
                int outIndex = offset;
                for (int n = 0; n < samplesRead; n++)
                {
                    if (n >= outputSamples)
                    {
                        buffer[outIndex++] = _sourceBuffer[n];
                    }
                    else
                    {
                        buffer[outIndex++] += _sourceBuffer[n];
                    }
                }

                outputSamples = Math.Max(samplesRead, outputSamples);
                if (samplesRead < count)
                {
                    localSourcesToRemove.Add(source);
                }

                index--;
            }

            // optionally ensure we return a full buffer
            if (ReadFully && outputSamples < count)
            {
                int outputIndex = offset + outputSamples;
                while (outputIndex < offset + count)
                {
                    buffer[outputIndex++] = 0;
                }

                outputSamples = count;
            }

            lock (_sources)
            {
                foreach (var source in localSourcesToRemove)
                {
                    MixerInputEnded?.Invoke(this, new SampleProviderEventArgs(source));
                    _sources.Remove(source);
                }
            }

            return outputSamples;
        }

        /// <summary>
        /// Creates a new MixingSampleProvider, with no inputs, but a specified WaveFormat
        /// </summary>
        /// <param name="waveFormat">The WaveFormat of this mixer. All inputs must be in this format</param>
        public MixingSampleProvider(WaveFormat waveFormat)
        {
            if (waveFormat.Encoding != WaveFormatEncoding.IeeeFloat)
            {
                throw new ArgumentException("Mixer wave format must be IEEE float");
            }

            _sources = new List<ISampleProvider>();
            WaveFormat = waveFormat;
        }

        /// <summary>
        /// Creates a new MixingSampleProvider, based on the given inputs
        /// </summary>
        /// <param name="sources">Mixer inputs - must all have the same waveformat, and must
        /// all be of the same WaveFormat. There must be at least one input</param>
        public MixingSampleProvider(IEnumerable<ISampleProvider> sources)
        {
            this._sources = new List<ISampleProvider>();
            foreach (var source in sources)
            {
                AddMixerInput(source);
            }

            if (this._sources.Count == 0)
            {
                throw new ArgumentException("Must provide at least one input in this constructor");
            }
        }


        /// <summary>
        /// SampleProvider event args
        /// </summary>
        public class SampleProviderEventArgs : EventArgs
        {
            /// <summary>
            /// Constructs a new SampleProviderEventArgs
            /// </summary>
            public SampleProviderEventArgs(ISampleProvider sampleProvider)
            {
                SampleProvider = sampleProvider;
            }

            /// <summary>
            /// The Sample Provider
            /// </summary>
            public ISampleProvider SampleProvider { get; private set; }
        }
    }
}