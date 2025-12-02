using System;
using System.Text.Json.Nodes;

namespace engine.casette;

public class LoaderSubscription
{
    public string Path;
    public Action<string, JsonNode?> OnTreeData;
}