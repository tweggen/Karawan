using System.Numerics;
using engine;
using engine.world;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Main
{
    private object _lo = new();
    private Engine _engine;

    private Style _uiStyle;
    private MenuBar _uiMenuBar;
    
    private Config _uiConfig;
    private Software _uiSoftware;
    private Clusters _uiClusters;
    private Scenes _uiScenes;
    private EntityState _sharedEntityState;
    private Entities _uiEntities;
    private EntityInspector _uiEntityInspector;
    private Monitor _uiMonitor;
    private Assets _uiAssets;


    private bool _isEnginePaused = false;
    

    public unsafe void Render(float dt)
    {
        _uiStyle.Render(dt);
        
        ImGui.SetNextWindowPos(new Vector2(0, 20));
        var mainViewportSize = ImGui.GetMainViewport().Size;
        ImGui.SetNextWindowSize(
            mainViewportSize with {X = 500} - new Vector2(0, 20));
        if (ImGui.Begin("selector", 0
                |ImGuiWindowFlags.NoCollapse
                |ImGuiWindowFlags.NoMove
                |ImGuiWindowFlags.NoResize
                ))
        {
            _uiMenuBar.Render(dt);
            
            {
                var state = _engine.State;
                ImGui.Text($"EngineState: {state.ToString()}");

                switch (state)
                {
                    case Engine.EngineState.Initialized:
                    case Engine.EngineState.Starting:
                    case Engine.EngineState.Stopping:
                    case Engine.EngineState.Stopped:
                        break;
                    case Engine.EngineState.Running:
                        ImGui.Text(state.ToString());
                        if (_isEnginePaused)
                        {
                            if (ImGui.Button("Continue"))
                            {
                                _isEnginePaused = false;
                                _engine.DisablePause();
                            } 
                        }
                        else
                        {
                            if (ImGui.Button("Pause"))
                            {
                                _isEnginePaused = true;
                                _engine.EnablePause();
                            }
                        }
                        break;
                }

                ImGui.SameLine();

                if (ImGui.Button("Flush"))
                {
                    I.Get<MetaGen>().Loader.WorldLoaderReleaseFragments();
                }
            }
            
            if (ImGui.CollapsingHeader("Config"))
            {
                _uiConfig.Render(dt);
            }

            if (ImGui.CollapsingHeader("Assets"))
            {
                _uiAssets.Render(dt);
            }

            if (ImGui.CollapsingHeader("Scenes"))
            {
                _uiScenes.Render(dt);
            }

            if (ImGui.CollapsingHeader("Clusters"))
            {
                _uiClusters.Render(dt);
            }

            if (ImGui.CollapsingHeader("Software"))
            {
                _uiSoftware.Render(dt);
            }

            if (ImGui.CollapsingHeader("Entities"))
            {
                _uiEntities.Render(dt);
            }

            _sharedEntityState.OnUpdate(dt);

            if (ImGui.CollapsingHeader("Inspector", _sharedEntityState.InspectorHeaderFlags))
            {
                _uiEntityInspector.Render(dt);
            }

            if (ImGui.CollapsingHeader("Monitor"))
            {
                _uiMonitor.Render(dt);
            }

            ImGui.EndChild();
        }

        ImGui.End();
    }

    public Main()
    {
        _engine = I.Get<engine.Engine>();

        _uiStyle = new Style(this);

        _uiMenuBar = new MenuBar(this);
        
        _uiConfig = new(this);
        _uiSoftware = new Software(this);
        _uiClusters = new Clusters(this);
        _uiScenes = new Scenes(this);
        _uiMonitor = new Monitor(this);
        _uiAssets = new Assets(this);

        _sharedEntityState = new EntityState(this);
        _uiEntities = new Entities(this, _sharedEntityState);
        _uiEntityInspector = new EntityInspector(this, _sharedEntityState);
    }
}