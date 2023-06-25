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

            int totalSamples = vorbisReader.SampleRate * vorbisReader.Channels;
            var floatReadBuffer = new float[totalSamples];
            var shortReadBuffer = new short[totalSamples];
            // var wholeFile = new List<float>((int)(vorbisReader.TotalSamples));
            int samplesRead;
            while ((samplesRead = vorbisReader.ReadSamples(floatReadBuffer, 0, floatReadBuffer.Length)) > 0)
            {
                for (int i = 0; i < samplesRead; ++i)
                {
                    shortReadBuffer[i] = (short)(32767f * floatReadBuffer[i]);
                }
                _al.BufferData(_alBuffer, bufferFormat, shortReadBuffer, rate);
            }
            
            // TXWTODO: Check for completeness
        }

    }

    
}