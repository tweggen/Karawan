using System.Diagnostics;
using System.Collections.Generic;
using System;
using System.IO;

namespace CmdLine
{
    public class InnoResourceWriter
    {
        public SortedDictionary<string, Resource> MapResources;
        public Action<string> Trace = msg => Debug.WriteLine(msg);
        public string DestinationPath = "InnoResources.iss";

        public void Execute()
        {
            /*
             * Source file content
             */
            using (StreamWriter writer = new StreamWriter(DestinationPath))
            {
                foreach (var kvp in MapResources)
                {
                    writer.WriteLine(
                        $"Source: \"..\\..\\..\\..\\..\\nogame\\{kvp.Value.Uri.Replace('/','\\')}\"; DestDir: \"{{app}}\\assets\\\"; DestName: \"{kvp.Key}\"; Flags: ignoreversion");
                }
            }
        }
    }
}