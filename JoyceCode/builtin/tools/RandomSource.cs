using System;
using System.Numerics;

namespace builtin.tools;

/**
 * Random seed generator for all kinds of objects,
 */
public class RandomSource
{
    private string _seed0;
    private int _randomSeed;
    private bool _mixed;

    private void _next()
    {
        _randomSeed = (_randomSeed * 16807) & 0x7fffffff;
        ;
    }


    public Vector3 GetVector3()
    {
        return new(-1f+2f*GetFloat(), -1f+2f*GetFloat(), -1f+2f*GetFloat());
    }


    public int GetInt(int max)
    {
        _next();
        return Int32.Clamp((int)(((float)_randomSeed / 2147483647f) * (float)max), 0, max);
    }
    
    
    public float GetFloat()
    {
        _next();
        return (float)_randomSeed / 2147483647f;
    }

    public int Get8()
    {
        _next();
        int result8 = _randomSeed >> 23;
        return result8;
    }

    public int Get16()
    {
        _next();
        return _randomSeed >> 15;
    }


    public int Get24()
    {
        _next();
        return _randomSeed >> 7;
    }

    public int GetDegrees()
    {
        return _randomSeed % 360;
    }


    public void Clear()
    {
        if (_mixed)
        {
            _randomSeed = _mixedSeedOf(_seed0);
            return;
        }
        int l = _seed0.Length;
        _randomSeed = 0;
        for (int i = 0; i < l; ++i)
        {
            _randomSeed += (int)_seed0[i] * (i * 147) + 1065434;
            _randomSeed = _randomSeed & 0x7fffffff;
        }
    }

    public RandomSource(string str)
    {
        _seed0 = str;
        Clear();
    }


    private RandomSource(string str, bool mixed)
    {
        _seed0 = str;
        _mixed = mixed;
        Clear();
    }


    /**
     * A source whose seed is well mixed (KAR-492), for families of similar seeds such as
     * "<cluster>-npc-17", "<cluster>-npc-18". The ordinary constructor hashes a string as a
     * weighted sum of its characters and draws by one multiplicative step, so the first draws
     * of such a family lie on a lattice: the start city got 1 drifter and 2 merchants among
     * 279 NPCs. Here the string goes through FNV-1a 64 and SplitMix64; the draws after that
     * are the usual ones.
     *
     * The ordinary constructor is unchanged on purpose: it seeds the world and its streets.
     */
    public static RandomSource Mixed(string str) => new RandomSource(str, mixed: true);


    private static int _mixedSeedOf(string str)
    {
        ulong h = 0xCBF29CE484222325UL;
        foreach (char c in str)
        {
            h ^= c;
            h *= 0x100000001B3UL;
        }
        ulong z = h + 0x9E3779B97F4A7C15UL;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        z ^= z >> 31;
        // Odd and non-zero: the multiplicative step keeps an even seed's trailing zeros forever.
        return (int)((z >> 33) & 0x7fffffffUL) | 1;
    }


    public static readonly RandomSource Instance = new RandomSource("nil");
}