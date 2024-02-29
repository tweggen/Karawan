using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepuPhysics;

namespace engine.physics.actions;


public class Player
{
    private object _lo = new();
    private JsonDocument _jd;

    private int _nextAction = -1;
    private int _nActions = 0;

    public JsonSerializerOptions JsonSerializerOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };
    
    private SortedDictionary<string, Type> _mapActions = new();
    
    
    public void LoadFromFile(string path)
    {
        string strJsonDump = File.ReadAllText(path);
        _jd = JsonDocument.Parse(strJsonDump);
        _nextAction = 0;
        _nActions = _jd.RootElement.GetProperty("actions").GetArrayLength();

        _nextAction = 0;
        _nActions = _jd.RootElement.GetProperty("actions").GetArrayLength();
    }


    /**
     * Apply all actions until the next timestep.
     * Then consume the timestep and return.
     */
    public void PerformNextChunk(Simulation simulation)
    {
        if (-1 != _nextAction)
        {
            bool haveTimestep = false;
            bool foundTimestep = false;
            for (; _nextAction < _nActions; _nextAction++)
            {
                var je = _jd.RootElement.GetProperty("actions")[_nextAction];
                string strType = je.GetProperty("type").ToString();

                if (strType == typeof(Timestep).ToString())
                {
                    foundTimestep = true;
                    break;
                }

                Type typeAction = _mapActions[strType];

                actions.ABase physAction = je.Deserialize(typeAction, JsonSerializerOptions) as ABase;

                physAction.Execute(null, simulation);
            }
        }
    }
    
    
    private void _addAction(Type t)
    {
        lock (_lo)
        {
            _mapActions.Add(t.ToString(), t);
        }
    }


    private void _addActions()
    {
        _addAction(typeof(CreateDynamic));
        _addAction(typeof(CreateKinematic));
        _addAction(typeof(CreateSphereShape));
        _addAction(typeof(DynamicSnapshot));
        _addAction(typeof(RemoveBody));
        _addAction(typeof(SetBodyAngularVelocity));
        _addAction(typeof(SetBodyAwake));
        _addAction(typeof(SetBodyLinearVelocity));
        _addAction(typeof(SetBodyPoseOrientation));
        _addAction(typeof(SetBodyPosePosition));
        _addAction(typeof(Timestep));
    }


    public Player()
    {
        _addActions();
    }
}