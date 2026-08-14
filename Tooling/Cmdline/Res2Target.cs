using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace CmdLine
{

    public class Res2Target
    {
        private string[] _args;
        public Action<string> Trace = (msg) => System.Diagnostics.Debug.WriteLine(msg);
        public string CurrentPath;

        public void Help()
        {
            Trace("res2target <gamejson>");
        }

        public int Execute()
        {
            Trace("res2target: Working...");
            try
            {
                GameConfig gc = new GameConfig(Path.Combine(CurrentPath, _args[1])) { CurrentPath = CurrentPath, Trace = Trace, DestinationPath = _args[2] };
                gc.Load();
                gc.LoadIndirectResources();

                Trace($"res2target: Writing android assets...");
                AndroidResourceWriter arw = new AndroidResourceWriter()
                {
                    MapResources = gc.MapResources,
                    Trace = Trace,
                    DestinationPath = System.IO.Path.Combine(_args[2], "./AndroidResources.xml")
                };
                arw.Execute();
                Trace($"res2target: Writing windows setup assets...");
                InnoResourceWriter irw = new InnoResourceWriter()
                {
                    MapResources = gc.MapResources,
                    Trace = Trace,
                    DestinationPath = System.IO.Path.Combine(_args[2], "./InnoResources.iss")
                };
                irw.Execute();

                int nMissing = _reportMissingBakeArtifacts(gc);
                if (nMissing > 0)
                {
                    return 1;
                }

                Trace($"res2target: Done.");
            }
            catch (Exception e)
            {
                Trace($"Exception in Execute: {e}");
            }

            return 0;
        }


        /**
         * Verify that every bake artifact the manifests declare was actually produced.
         *
         * The manifests name files the BAKE writes - mo-{hash} models, ac-{hash}
         * animation collections, sc-{hash} scenarios - and until now nothing checked
         * that any of them exist. On Android the missing file eventually surfaces as
         * an MSBuild "source file not found" for the AndroidAsset; on DESKTOP nothing
         * complained at all, and the first symptom was at runtime, far from the cause:
         *
         *   No fbx importer is available for url man_coat_winter_Rig.fbx
         *
         * which is a true statement about a consequence. The actual cause is that
         * Chushi never wrote the file - most often because nogame.csproj runs the
         * PUBLISHED Chushi binary and a plain `dotnet build` does not refresh it, so a
         * Chushi that predates a bake step silently bakes nothing.
         *
         * Failing here turns "the game starts with no characters" into a build error
         * that names the file and the command that fixes it.
         */
        private int _reportMissingBakeArtifacts(GameConfig gc)
        {
            var missing = new System.Collections.Generic.List<string>();

            foreach (var kvp in gc.MapResources)
            {
                string type = kvp.Value.Type ?? "";
                if (type != "bakedModel"
                    && type != "bakedAnimationCollection"
                    && type != "bakedScenario")
                {
                    continue;
                }

                string uri = kvp.Value.Uri ?? "";
                if (uri.Length == 0)
                {
                    continue;
                }

                /*
                 * The manifests emit Uri verbatim for MSBuild to resolve, so accept a
                 * hit under any plausible root rather than risk failing a build over a
                 * path convention.
                 */
                bool found =
                    File.Exists(uri)
                    || (CurrentPath != null && File.Exists(Path.Combine(CurrentPath, uri)))
                    || File.Exists(Path.Combine(_args[2], Path.GetFileName(uri)));

                if (!found)
                {
                    missing.Add($"{kvp.Key}  ({type}, expected at {uri})");
                }
            }

            if (missing.Count == 0)
            {
                return 0;
            }

            Trace($"res2target: ERROR: {missing.Count} declared bake artifact(s) were never produced:");
            foreach (var m in missing)
            {
                Trace($"res2target:   {m}");
            }

            Trace("res2target: The build tools that produce these run from their PUBLISHED output, which");
            Trace("res2target: `dotnet build` does not refresh. After changing or pulling build-tool code:");
            Trace("res2target:     bash Chushi/build.sh          # mo-, ac-, sc- artifacts");
            Trace("res2target:     bash Tooling/Cmdline/build.sh # the manifests themselves");
            Trace("res2target: then build again. See docs/SYSTEMS/BUILD/PIPELINE.md.");

            return missing.Count;
        }


        public Res2Target(string[] args)
        {
            if (args.Length < 3)
            {
                throw new ArgumentException();
            }

            if (args.Length == 4)
            {
                CurrentPath = args[3];
            }

 
            _args = args;
        }
    }
}