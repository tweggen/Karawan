using System.Collections.Generic;
using System.Linq;
using engine;

namespace nogame.config;

public class Module : AModule
{
    private const string DbGameConfig = "gameconfig";

    private GameConfig _gameConfig = null;
    public GameConfig GameConfig
    {
        get => _gameConfig; 
    }
    
    
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new SharedModule<DBStorage>()
    };


    private void _saveGameConfigInternal(GameConfig gameConfig)
    {
        List<GameConfig> listGameConfigs = new List<GameConfig>();
        listGameConfigs.Add(gameConfig);
        M<DBStorage>().StoreCollection(DbGameConfig, listGameConfigs);
    }

    
    private GameConfig _loadGameConfig()
    {
        GameConfig gameConfig = null;
        System.Collections.Generic.IEnumerable<GameConfig> listGameConfigs;
        bool haveGameConfig = M<DBStorage>().LoadCollection(DbGameConfig, out listGameConfigs);
        if (haveGameConfig)
        {
            haveGameConfig = false;
            foreach (var gc in listGameConfigs)
            {
                haveGameConfig = true;
                gameConfig = gc;
                break;
            }
        }
        if (false == haveGameConfig)
        {
            gameConfig = new GameConfig();
            _saveGameConfigInternal(gameConfig);
        }
        else
        {
            if (!gameConfig.IsValid())
            {
                gameConfig.Fix();
            }
        }

        return gameConfig;
    }


    public void Save()
    {
        _saveGameConfigInternal(GameConfig);
    }
    
    
    
    public override void ModuleDeactivate()
    {
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }


    public override void ModuleActivate()
    {
        base.ModuleActivate();

        {
            var gameConfig = _loadGameConfig();
            _gameConfig = gameConfig;
        }
        
        _engine.AddModule(this);
    }
}