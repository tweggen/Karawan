using System;
using System.Collections.Generic;
using System.Text;

namespace engine.tools
{
    public class NameGenerator
    {
        private static object _lockObject = new();
        private static NameGenerator _instance;
            
        private float _pSylOpen = 0.75f;
        private string[] _arrSylOpen = {
            "b", "d", "f", "g", "j", "k", "l", "m", "n", "p", "r", "s", "sh", "t", "v", "y"
        };
        private float _pSylInMod = 0.2f;
        private string[] _arrSylInMod = {
            "l", "r", "s", "v", "y"
        };
        private float _pSylVow = 0.95f;
        private string[] _arrSylVow = {
            "a", "e", "i", "o", "u", "ee"
        };
        private float _pSylClose = 0.4f;
        private string[] _arrSylClose = {
            "ng", "kh", "f", "k", "l", "m", "p", "r", "s", "t"
        };

        private string _createPhoneme(in engine.RandomSource rnd, float prob, string [] arr)
        {
            var r = rnd.getFloat();
            if(r<prob) {
                int idx = (int)(r* arr.Length * (0.999/prob));
                return arr[idx];
            } else {
                return "";
            }
        }

        private string _createSyllable(in engine.RandomSource rnd)
        {
            string strSyl = "";
            strSyl += _createPhoneme(rnd, _pSylOpen, _arrSylOpen);
            strSyl += _createPhoneme(rnd, _pSylInMod, _arrSylInMod);
            strSyl += _createPhoneme(rnd, _pSylVow, _arrSylVow);
            strSyl += _createPhoneme(rnd, _pSylClose, _arrSylClose);
            return strSyl;
        }

        private string _createRawWord(in engine.RandomSource rnd)
        {
            float r = rnd.getFloat();
            int n;
            if (r < 0.05)
            {
                n = 1;
            }
            else if (r < 0.4)
            {
                n = 2;
            }
            else if (r < 0.7)
            {
                n = 3;
            }
            else if (r < 0.85)
            {
                n = 4;
            }
            else if (r < 0.95)
            {
                n = 5;
            }
            else
            {
                n = 6;
            }
            string s = "";
            for (int i=0; i<n; i++)
            {
                s += _createSyllable(rnd);
            }
            return s;
        }

        public string CreateWord(in engine.RandomSource rnd)
        {
            string lower = _createRawWord(rnd);
            string first = lower.Substring(0, 1);
            string remainder = lower.Substring(1);
            string name = first.ToUpper() + remainder;
            return name;
        }

        public static NameGenerator Instance()
        {
            lock(_lockObject)
            {
                if( _instance == null )
                {
                    _instance = new NameGenerator();
                }
                return _instance;
            }
        }

        private NameGenerator()
        {
        }
    }
}
