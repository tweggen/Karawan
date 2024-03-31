using System.Diagnostics;
using Silk.NET.OpenAL;
using static engine.Logger;

namespace Boom.OpenAL;

public class OGGSound : IDisposable
{
    private AL _al;
    private string _url;
    private uint _alBuffer = 0xffffffff;

    public string Url
    {
        get => _url;
    }

    public uint ALBuffer
    {
        get => _alBuffer;
    }

    public int CheckError(string msg)
    {
        Silk.NET.OpenAL.AudioError aerr = _al.GetError();
        if (aerr != Silk.NET.OpenAL.AudioError.NoError)
        {
            string astring = "unknown error";
            switch (aerr)
            {
                case AudioError.IllegalCommand:
                    astring = "Illegal Command";
                    break;
                case AudioError.InvalidEnum:
                    astring = "Invalid Enum";
                    break;
                case AudioError.InvalidName:
                    astring = "Invalid Name";
                    break;
                default:
                case AudioError.NoError:
                    astring = "No error";
                    break;
                case AudioError.OutOfMemory:
                    astring = "OutOfMemory";
                    break;
                
            }
            Error($"Encountered audio error {aerr}.");
            return (int) aerr;
        }

        return 0;
    }
    
    
    public void Dispose()
    {
        if (0xffffffff != _alBuffer)
        {
            _al.DeleteBuffer(_alBuffer);
            _alBuffer = 0xffffffff;
        }
    }


    public OGGSound(AL al, string url)
    {
        _al = al;
        _url = url;
        _alBuffer = _al.GenBuffer();
        CheckError("OGGSound: _al.GenBuffer().");
        using(System.IO.Stream streamAudiofile = engine.Assets.Open(_url))
        {
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
                    ErrorThrow($"Unsupported ogg file with {vorbisReader.Channels} channels.",
                        (m) => new ArgumentException(m));
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
                        shortReadBuffer[i + position] = (short)(32767f * floatReadBuffer[i]);
                    }

                    position += samplesRead;
                    avail -= samplesRead;
                }

                Array.Resize(ref floatReadBuffer, 1);
                _al.BufferData(_alBuffer, bufferFormat, shortReadBuffer, rate);
                Array.Resize(ref shortReadBuffer, 1);
                CheckError($"OGGSound: _al.BufferData({_alBuffer}).");
            }
            // TXWTODO: Check for completeness
        }

    }

    
}