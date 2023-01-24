namespace Benchmark
{
    internal class Program
    {
        static void Main(string[] args)
        {

            var engine = Karawan.platform.cs1.Platform.EasyCreate(args);

            var scnBenchmark = new Benchmark();
            scnBenchmark.SceneActivate(engine);

            // engine.Execute();

            scnBenchmark.SceneDeactivate();
        }
    }
}
