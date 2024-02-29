using System.IO;
using System.Text.Json;

namespace Joyce.engine.physics.actions;

public class Player
{
    private JsonDocument _jd;

    private int _nextAction = -1;
    private int _nActions = 0;
    
    
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
    public void PerformNextChunk()
    {
        if (-1 != _nextAction)
        {
            bool haveTimestep = false;
            bool foundTimestep = false;
            for (; _nextAction < _nActions; _nextAction++)
            {
                var je = _jd.RootElement.GetProperty("actions")[_nextAction];
                switch (je.GetProperty("type").GetString())
                {
                    case "engine.physics.actions.CreateDynamic":
                        break;
                    case "engine.physics.actions.CreateKinematic":
                        break;
                    case "engine.physics.actions.DynamicSnapshot":
                        break;
                    case "engine.physics.actions.Timestep":
                        foundTimestep = true;
                        break;
                    case "engine.physics.actions.SetBodyAwake":
                        break;
                    case "engine.physics.actions.SetBodyAngularVelocity":
                        break;
                    case "engine.physics.actions.SetBodyLinearVelocity":
                        break;
                    case "engine.physics.actions.SetBodyPoseOrientation":
                        break;
                    case "engine.physics.actions.SetBodyPosePosition":
                        break;
                    default:
                        break;
                }

                if (foundTimestep)
                {
                    break;
                }
            }
        }
    }
}