﻿using NAudio.Wave;

namespace Boom.NAudio;

/**
 * Implement a sample provider around a cached sound source.
 */
public class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound cachedSound;
    private long position;

    public CachedSoundSampleProvider(CachedSound cachedSound)
    {
        position = 0;
        this.cachedSound = cachedSound;
    }

    public void Rewind()
    {
        position = 0;
    }

    public int Read(float[] buffer, int offset, int count)
    {
        var availableSamples = cachedSound.AudioData.Length - position;
        var samplesToCopy = Math.Min(availableSamples, count);
        Array.Copy(cachedSound.AudioData, position, buffer, offset, samplesToCopy);
        position += samplesToCopy;
        return (int)samplesToCopy;
    }

    public WaveFormat WaveFormat
    {
        get { return cachedSound.WaveFormat; }
    }
}