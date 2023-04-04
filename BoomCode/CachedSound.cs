using NAudio.Wave;
using static engine.Logger;

namespace Boom
{
    public class CachedSound
    {
        public float[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }

        public string Url;
        
        public CachedSound(string audioFileName)
        {
            Url = audioFileName;
            System.IO.Stream streamAudiofile = engine.Assets.Open(audioFileName);
            using (var vorbisReader = new NVorbis.VorbisReader(streamAudiofile))
            {
                var readBuffer = new float[vorbisReader.SampleRate * vorbisReader.Channels];
                var wholeFile = new List<float>((int)(vorbisReader.TotalSamples));
                int samplesRead;
                while ((samplesRead = vorbisReader.ReadSamples(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeFile.AddRange(readBuffer.Take(samplesRead));
                }

                WaveFormat = new WaveFormat(vorbisReader.SampleRate, 16, vorbisReader.Channels);
                AudioData = wholeFile.ToArray();
                if (vorbisReader.TotalSamples != AudioData.Length)
                {
                    Warning($"Decoded more or less samples than the ogg file has: Ogg" +
                            $" TotalSamples=={vorbisReader.TotalSamples} != AudioData.Count=={AudioData.Length}");
                }
            }
#if false
            using (var audioFileReader = new AudioFileReader(audioFileName))
            {
                // TODO: could add resampling in here if required
                WaveFormat = audioFileReader.WaveFormat;
                var wholeFile = new List<float>((int)(audioFileReader.Length / 4));
                var readBuffer = new float[audioFileReader.WaveFormat.SampleRate * audioFileReader.WaveFormat.Channels];
                int samplesRead;
                while ((samplesRead = audioFileReader.Read(readBuffer, 0, readBuffer.Length)) > 0)
                {
                    wholeFile.AddRange(readBuffer.Take(samplesRead));
                }

                AudioData = wholeFile.ToArray();
            }
#endif
        }
    }
}