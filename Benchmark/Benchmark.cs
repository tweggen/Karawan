using System;
using System.Collections.Generic;

namespace Benchmark
{

    public class Benchmark : engine.IScene
    {
        private List<String> _listSummary;

        private double _getTime()
        {
            return (double)DateTime.Now.Ticks / 10000000;
        }

        private void _transferTexture(bool alsoDownload)
        {
            const int nRepeats = 20;
            const int nIterations = 10;

            _listSummary.Add(String.Format("Running Texture upload benchmark."));
            _listSummary.Add(String.Format("#1 Performing {0} repititions of uploading {1} times an RGBA32 4k * 4k texture.",
                nRepeats, nIterations));

            Karawan.platform.cs1.splash.TextureManager textureManager = new();

            double minTime = 100000000;
            double maxTime = 0;
            double averageTime = 0;

            for (int n = 0; n < nRepeats; ++n)
            {
                double now = _getTime();
                for (int i = 0; i < nIterations; ++i)
                {
                    var jTexture = new engine.joyce.Texture("joyce://64MB");
                    textureManager.FindRlTexture(jTexture);
                    if (alsoDownload)
                    {
                        textureManager.LoadBackTexture(jTexture);
                    } else
                    {
                        textureManager.PurgeTexture(jTexture);
                    }
                }
                double then = _getTime();
                double duration = then - now;
                minTime = Math.Min(duration, minTime);
                maxTime = Math.Max(duration, maxTime);
                averageTime += duration;

                _listSummary.Add(String.Format("Upload {2}of {0} * 64MB took {1,4}s.", nIterations, duration, alsoDownload ? "and download " : ""));
            }
            averageTime /= nRepeats;

            _listSummary.Add(String.Format("Upload {5}of {0}*{1} times 64MB took average {2,4}s, min {3,4}s, max {4,4}s.",
                nRepeats, nIterations, averageTime, minTime, maxTime, alsoDownload ? "and download " : ""));
            _listSummary.Add(String.Format("Average bandwidth {0} MB/s, min {1} MB/s, max {2} MB/s.", (64 * nIterations) / averageTime, (64 * nIterations) / minTime, (64 * nIterations) / maxTime));
        }


        public void SceneActivate(engine.Engine engine)
        {
            _transferTexture(false);
            _transferTexture(true);
        }

        public void SceneDeactivate()
        {
            foreach(var str in _listSummary)
            {
                Console.WriteLine(str);
            }
        }

        public void SceneOnLogicalFrame(float dt)
        {
        }

        public void SceneOnPhysicalFrame(float dt)
        {
        }

        public Benchmark()
        {
            _listSummary = new();
        }

    }

}