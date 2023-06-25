using System.ComponentModel;
using NAudio.Wave;
using static engine.Logger;

namespace Boom.NAudio;


/// <summary>
/// Stream for looping playback
/// </summary>
public class LoopSampleProvider : ISampleProvider
{
    private CachedSoundSampleProvider _sourceSampleProvider;
    private int _sourcePosition = 0;

    /// <summary>
    /// Creates a new Loop stream
    /// </summary>
    /// <param name="sourceStream">The stream to read from. Note: the Read method of this stream should return 0 when it reaches the end
    /// or else we will not loop to the start again.</param>
    public LoopSampleProvider(CachedSoundSampleProvider sourceSampleProvider)
    {
        _sourceSampleProvider = sourceSampleProvider;
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
        int totalFloatsRead = 0;

        while (totalFloatsRead < count)
        {
            int floatsRead = _sourceSampleProvider.Read(buffer, offset + totalFloatsRead, count - totalFloatsRead);
            if (floatsRead == 0)
            {
                if (!EnableLooping)
                {
                    // something wrong with the source stream
                    break;
                }
                else
                {
                    _sourcePosition = 0;
                    _sourceSampleProvider.Rewind();
                }
            }

            totalFloatsRead += floatsRead;
        }

        if (totalFloatsRead != count)
        {
            Error($"Returned less bytes than demanded: totalBytesRead {totalFloatsRead} != count {count}");
        }
        
        return totalFloatsRead;
    }
}