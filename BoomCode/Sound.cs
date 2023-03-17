
using NAudio.Wave;

namespace Boom
{

    public class Sound : ISampleProvider, IDisposable, IComparable<Sound>
    {
        static private int _nextId = 0;
        public int _id = ++_nextId;
        private CachedSound _cachedSound;
        private CachedSoundSampleProvider _cachedSoundSampleProvider;
        private LoopSampleProvider _loopSampleProvider;
        private ChannelStripProvider _channelStripProvider;


        public float Volume 
        {
            get => _channelStripProvider.Volume;
            set => _channelStripProvider.Volume = value;
        }
        
        public float Pan 
        {
            get => _channelStripProvider.Pan;
            set => _channelStripProvider.Pan = value;
        }

        public float Speed 
        {
            get => _channelStripProvider.Speed;
            set => _channelStripProvider.Speed = value;
        }


        public WaveFormat WaveFormat
        {
            get => _channelStripProvider.WaveFormat;
            set => _channelStripProvider.WaveFormat = value;
        }

        public int CompareTo(Sound? other)
        {
            if (null == other) return 1;
            if (_id < other._id)
            {
                return -1;
            }
            else
            {
                if (_id > other._id)
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            }
        }
        
        public void Dispose()
        {
            // _channelStripProvider.Dispose();
            _channelStripProvider = null;
            // _loopSampleProvider.Dispose();
            _loopSampleProvider = null;
            // _cachedSoundSampleProvider.Dispose();
            _cachedSoundSampleProvider = null;
            // _cachedSound.Dispose();
            _cachedSound = null;

        }

        public int Read(float[] buffer, int offset, int count)
        {
            return _channelStripProvider.Read(buffer, offset, count);
        }

        public Sound(in CachedSound cachedSound)
        {
            _cachedSound = cachedSound;
            _cachedSoundSampleProvider = new CachedSoundSampleProvider(_cachedSound);
            _loopSampleProvider = new LoopSampleProvider(_cachedSoundSampleProvider, 0);
            _channelStripProvider = new ChannelStripProvider(_loopSampleProvider);
        }
    }
}