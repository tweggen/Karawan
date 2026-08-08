using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;

namespace CmdLine
{

    /**
     * From a resource map, generate an include file to load into an android build
     * of joyce.
     */
    public class AndroidResourceWriter
    {
        public SortedDictionary<string, Resource> MapResources;
        public Action<string> Trace = msg => Debug.WriteLine(msg);
        public string DestinationPath = "AndroidResources.xml";

        public void Execute()
        {
            string dirName = System.IO.Path.GetDirectoryName(DestinationPath);
            System.IO.Directory.CreateDirectory(dirName);

            /*
             * Write xml content:
             * <ItemGroup>
             *   <AndroidAsset Include="Platforms\Android\buildingalphadiffuse2.png">
             *     <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
             *   </AndroidAsset>
             * </ItemGroup>
             */
            using (StreamWriter writer = new StreamWriter(DestinationPath))
            {
                /*
                 * NO Sdk attribute. This file is <Import>ed by Wuka.csproj, and an
                 * Sdk attribute on an imported fragment makes MSBuild import
                 * Sdk.props/Sdk.targets AGAIN, at the point of the <Import> - i.e. in
                 * the middle of Wuka.csproj. Two MSB4011 warnings say so, and the
                 * second one is the damaging half: Wuka.csproj's own implicit bottom
                 * import of Sdk.targets is then SKIPPED, so the .NET SDK, the Android
                 * workload and the MAUI workload all land ~200 lines early and every
                 * static ItemGroup in them sees a Wuka.csproj that stops at the
                 * <Import>: no libSDL3/libmain/libopenal AndroidNativeLibrary, no
                 * AndroidResource, no PackageReference, no ProjectReference.
                 *
                 * `dotnet build` survives it; IDE project evaluators need not.
                 */
                writer.WriteLine("<Project>");
                writer.WriteLine("  <ItemGroup>");
                foreach (var kvp in MapResources)
                {
                    writer.WriteLine($"    <AndroidAsset Include=\"{kvp.Value.Uri}\" LogicalName=\"{kvp.Key}\">");
                    writer.WriteLine("      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>");
                    writer.WriteLine("    </AndroidAsset>");
                }
                writer.WriteLine("  </ItemGroup>");
                writer.WriteLine("</Project>");
            }
        }

    }
}