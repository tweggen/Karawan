﻿using System;
using System.Collections.Generic;
using System.Text;

namespace engine
{
    /**
     * Random seed generator for all kinds of objects,
     */
    public class RandomSource
    {
        private string _seed0;
        private int _randomSeed;

        private void next()
        {
            _randomSeed = (_randomSeed* 16807) & 0x7fffffff;;
        }

        public float getFloat()
        {
            next();
            return (float)_randomSeed / 2147483647f;
        }

        public int get8()
        {
            next();
            int result8 = _randomSeed >> 23;
            return result8;
        }

        public int get16()
        {
            next();
            return _randomSeed >> 15;
        }


        public int get24()
        {
            next();
            return _randomSeed >> 7;
        }

        public int getDegrees()
        {
            return _randomSeed % 360;
        }


        public void clear()
        {
            int l = _seed0.Length;
            _randomSeed = 0;
            for (int i=0; i<l; ++i)
            {
                _randomSeed += (int)_seed0[i] * (i * 147) + 1065434;
                _randomSeed = _randomSeed & 0x7fffffff;
            }
        }

        public RandomSource(string str)
        {
            _seed0 = str;
            clear();
        }
    }
}
