using System.Diagnostics;
using Silk.NET.OpenAL;
using static engine.Logger;

namespace Boom.OpenAL;

public class OGGSound : IDisposable
{
    private AL _al;
    private string _url;
    private uint _alBuffer = 0;

    public string Url
    {
        get => _url;
    }

    public uint ALBuffer
    {
        get => _alBuffer;
    }
    
    
    public void Dispose()
    {
        if (0 != _alBuffer)
        {
            _al.DeleteBuffer(_alBuffer);
            _alBuffer = 0;
        }
    }


    public OGGSound(AL al, string url)
    {
        _al = al;
        _url = url;
        _alBuffer = _al.GenBuffer();
        
        System.IO.Stream streamAudiofile = engine.Assets.Open(_url);
        using (var vorbisReader = new NVorbis.VorbisReader(streamAudiofile))
        {
            int rate = vorbisReader.SampleRate;
            BufferFormat bufferFormat;
            if (vorbisReader.Channels == 1)
            {
                bufferFormat = BufferFormat.Mono16;
            } 
            else if (vorbisReader.Channels == 2)
            {
                bufferFormat = BufferFormat.Stereo16;
                
            }
            else
            {
                ErrorThrow($"Unsupported ogg file with {vorbisReader.Channels} channels.", (m) => new ArgumentException(m));
                return;
            }
            // Trace($"Sound {url} has {vorbisReader.Channels} channels.");

            int partSamples = vorbisReader.SampleRate * vorbisReader.Channels;
            // var wholeFile = new List<float>((int)(vorbisReader.TotalSamples));
            int samplesRead;

            long allSamples = vorbisReader.TotalSamples * vorbisReader.Channels;
            var floatReadBuffer = new float[partSamples];
            var shortReadBuffer = new short[allSamples];
            long position = 0;
            long avail = allSamples;
            while (true)
            {
                int readNow = Int32.Min(floatReadBuffer.Length, (int)avail);
                if (0 == readNow)
                {
                    break;
                }
                samplesRead =
                    vorbisReader.ReadSamples(floatReadBuffer, 0, readNow);
                if (samplesRead <= 0)
                {
                    break;
                }
                for (int i = 0; i < samplesRead; ++i)
                {
                    shortReadBuffer[i+position] = (short)(32767f * floatReadBuffer[i]);
                }

                position += samplesRead;
                avail -= samplesRead;
            }
            _al.BufferData(_alBuffer, bufferFormat, shortReadBuffer, rate);
            
            // TXWTODO: Check for completeness
        }

    }

    
}