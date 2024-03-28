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
                writer.WriteLine("<Project Sdk=\"Microsoft.NET.Sdk\">");
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