using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using engine.meta;
using static engine.Logger;

namespace engine.world;

public class MetaGenLoader
{
    private MetaGen _metaGen;
    
    private ExecDesc _loadExecDesc(JsonNode node)
    {
        engine.meta.ExecDesc.ExecMode execMode = ExecDesc.ExecMode.Task;

        var modeNode = node?["mode"];
        if (modeNode is JsonValue)
        {
            JsonValue jvModeNode = modeNode as JsonValue;
            if (jvModeNode.TryGetValue<string>(out var str))
            {
                switch (str)
                {
                    case "constant":
                        execMode = ExecDesc.ExecMode.Constant;
                        break;
                    case "parallel":
                        execMode = ExecDesc.ExecMode.Parallel;
                        break;
                    case "applyParallel":
                        execMode = ExecDesc.ExecMode.ApplyParallel;
                        break;
                    case "sequence":
                        execMode = ExecDesc.ExecMode.Sequence;
                        break;
                    default:
                    case "task":
                        execMode = ExecDesc.ExecMode.Task;
                        break;
                }
            }
            else if (jvModeNode.TryGetValue<int>(out var num))
            {
                execMode = (ExecDesc.ExecMode)num;
            }
        }

        string comment          = node?["comment"]?.GetValue<string>();
        string configCondition  = node?["configCondition"]?.GetValue<string>();
        string selector         = node?["selector"]?.GetValue<string>();
        string target           = node?["target"]?.GetValue<string>();
        string implementation   = node?["implementation"]?.GetValue<string>();

        List<ExecDesc> children = null;
        if (node?["children"] is JsonArray arr)
        {
            children = new List<ExecDesc>();
            foreach (var childNode in arr)
            {
                children.Add(_loadExecDesc(childNode));
            }
            if (children.Count == 0) children = null;
        }

        return new ExecDesc()
        {
            Mode = execMode,
            Comment = comment,
            ConfigCondition = configCondition,
            Selector = selector,
            Target = target,
            Children = children,
            Implementation = implementation
        };
    }


    private ExecDesc _loadFragmentOperators(JsonNode node)
    {
        try
        {
            var edRoot = _loadExecDesc(node);
            I.Get<MetaGen>().EdRoot = edRoot;
        }
        catch (Exception e)
        {
            Warning($"Error reading fragment operators: {e}.");
        }

        return new ExecDesc();
    }


    private void _loadBuildingOperators(JsonNode node)
    {
        try
        {
            if (node is JsonArray arr)
            {
                foreach (var opNode in arr)
                {
                    string className = opNode?["className"]?.GetValue<string>();
                    try
                    {
                        IWorldOperator wop = I.Get<engine.casette.Loader>()
                            .CreateFactoryMethod(null, opNode)() as IWorldOperator;
                        I.Get<MetaGen>().WorldBuildingOperators.Add(wop);
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to instantiate world building operator {className}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading implementations: {e}");
        }
    }


    private void _loadClusterOperators(JsonNode node)
    {
        try
        {
            if (node is JsonArray arr)
            {
                foreach (var opNode in arr)
                {
                    string className = opNode?["className"]?.GetValue<string>();
                    try
                    {
                        IClusterOperator cop = I.Get<engine.casette.Loader>()
                            .CreateFactoryMethod(null, opNode)() as IClusterOperator;
                        I.Get<MetaGen>().ClusterOperators.Add(cop);
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to instantiate cluster operator {className}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading implementations: {e}");
        }
    }


    private void _loadPopulatingOperators(JsonNode node)
    {
        try
        {
            if (node is JsonArray arr)
            {
                foreach (var opNode in arr)
                {
                    string className = opNode?["className"]?.GetValue<string>();
                    try
                    {
                        IWorldOperator wop = I.Get<engine.casette.Loader>()
                            .CreateFactoryMethod(null, opNode)() as IWorldOperator;
                        I.Get<MetaGen>().WorldPopulatingOperators.Add(wop);
                    }
                    catch (Exception e)
                    {
                        Warning($"Unable to instantiate world populating operator {className}: {e}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Warning($"Error reading implementations: {e}");
        }
    }


    private void _loadMetaGen(JsonNode node)
    {
        try
        {
            if (node?["buildingOperators"] is JsonArray buildingOps)
            {
                _loadBuildingOperators(buildingOps);
            }

            if (node?["clusterOperators"] is JsonArray clusterOps)
            {
                _loadClusterOperators(clusterOps);
            }

            if (node?["populatingOperators"] is JsonArray populatingOps)
            {
                _loadPopulatingOperators(populatingOps);
            }

            if (node?["fragmentOperators"] != null)
            {
                _loadFragmentOperators(node["fragmentOperators"]);
            }
        }
        catch (Exception e)
        {
            Warning($"Unable to setup metagen: {e}");
        }
    }

    private void _whenLoaded(string path, JsonNode? node)
    {
        if (null != node)
        {
            _loadMetaGen(node);
        }
    }
    

    public MetaGenLoader(MetaGen metaGen)
    {
        _metaGen = metaGen;
        I.Get<engine.casette.Loader>().WhenLoaded("/metaGen", _whenLoaded);
    }
}