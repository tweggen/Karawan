using NAudio.Wave;

namespace Boom
{

    /// <summary>
    /// Stream for looping playback
    /// </summary>
    public class LoopSampleProvider : ISampleProvider
    {
        private ISampleProvider _sourceSampleProvider;
        private int _sourcePosition;
        private int _loopLength;

        /// <summary>
        /// Creates a new Loop stream
        /// </summary>
        /// <param name="sourceStream">The stream to read from. Note: the Read method of this stream should return 0 when it reaches the end
        /// or else we will not loop to the start again.</param>
        public LoopSampleProvider(ISampleProvider sourceSampleProvider, int length)
        {
            _sourceSampleProvider = sourceSampleProvider;
            _loopLength = length;
            this.EnableLooping = true;
        }

        /// <summary>
        /// Use this to turn looping on or off
        /// </summary>
        public bool EnableLooping { get; set; }

        /// <summary>
        /// Return source stream's wave format
        /// </summary>
        public WaveFormat WaveFormat
        {
            get { return _sourceSampleProvider.WaveFormat; }
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int totalBytesRead = 0;

            while (totalBytesRead < count)
            {
                _sourcePosition = (offset+totalBytesRead) % _loopLength;

                int bytesRead = _sourceSampleProvider.Read(buffer, _sourcePosition, count - totalBytesRead);
                if (bytesRead == 0)
                {
                    if (!EnableLooping)
                    {
                        // something wrong with the source stream
                        break;
                    }
                }

                totalBytesRead += bytesRead;
            }

            return totalBytesRead;
        }
    }
}