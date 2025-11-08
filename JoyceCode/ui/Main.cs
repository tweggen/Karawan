using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using builtin.jt;
using engine;
using engine.editor.components;
using engine.gongzuo;
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


    public static void PropEdit(string key, object currValue, Action<string, object> setFunction)
    {
        if (currValue is bool)
        {
            bool value = (bool)currValue;
            
            if (ImGui.Checkbox(key, ref value))
            {
                if (value != (bool)currValue)
                {
                    Trace($"new Value {value}");
                    setFunction(key, value);
                }
            }
        }
        else if (currValue is float)
        {
            float currentInput = (float)currValue;
                
            if (ImGui.InputFloat(key, ref currentInput,
                    10f, 100f,
                    "%.2f",
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (currentInput != (float)currValue)
                {
                    Trace($"new Value {currentInput}");
                    setFunction(key, currentInput);
                }
            }
        }
        else if (currValue is int)
        {
            var currentInput = (int)currValue;
                
            if (ImGui.InputInt(key, ref currentInput,
                    10, 100,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (currentInput != (int)currValue)
                {
                    Trace($"new Value {currentInput}");
                    setFunction(key, currentInput);
                }
            }
        }
        else if (currValue is uint)
        {
            int currentInput = (int)(uint)currValue;
                
            if (ImGui.InputInt(key, ref currentInput,
                    10, 100,
                    ImGuiInputTextFlags.EnterReturnsTrue))
            {
                if (currentInput != (int)(uint)currValue)
                {
                    Trace($"new Value {currentInput}");
                    setFunction(key, currentInput);
                }
            }
        }
        else if (currValue is string)
        {
            string currentInput = (string)currValue;
            
            if (ImGui.InputText(key, ref currentInput, 1024))
            {
                if (currentInput != (string)currValue)
                {
                    Trace($"new Value {currentInput}");
                    setFunction(key, currentInput);
                }
            }
        }
        else if (currValue is Vector3)
        {
            Vector3 currentInput = (Vector3)currValue;
            var newValue = currentInput;
            
            if (ImGui.InputFloat3(key, ref currentInput))
            {
                if (currentInput != newValue)
                {
                    Trace($"new Value {currentInput}");
                    setFunction(key, currentInput);
                }
            }
        }
        else if (currValue is Vector4)
        {
            Vector4 currentInput = (Vector4)currValue;
            var newValue = currentInput;
            
            if (ImGui.InputFloat4(key, ref currentInput))
            {
                if (currentInput != newValue)
                {
                    Trace($"new Value {currentInput}");
                    setFunction(key, currentInput);
                }
            }
        }
        else if (currValue is Quaternion)
        {
            Quaternion currentInputQuat = (Quaternion)currValue;
            Vector4 currentInputVec = new(currentInputQuat.X, currentInputQuat.Y, currentInputQuat.Z, currentInputQuat.W);
            var newValue = currentInputVec;
            
            if (ImGui.InputFloat4(key, ref currentInputVec))
            {
                if (currentInputVec != newValue)
                {
                    Trace($"new Value {currentInputVec}");
                    Quaternion newQuat = new(currentInputVec.X, currentInputVec.Y, currentInputVec.Z, currentInputVec.W);
                    setFunction(key, newQuat);
                }
            }
#if false
            if (ImGui.BeginTable("table_padding", 3, ImGuiTableFlags.BordersOuterV | ImGuiTableFlags.BordersInnerV))
            {
                ImGui.TableNextRow();
                for (int column = 0; column < 3; column++)
                {
                    ImGui.TableSetColumnIndex(column);

                    float value = newValue[column];
                    if (ImGui.InputFloat(key, ref value,
                            10f, 100f,
                            "%.2f",
                            ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        if (value != currentInput[column])
                        {
                            Trace($"new Value {value}");
                            setFunction(key, currentInput);
                        }
                    }
                }
                ImGui.EndTable();
            }
#endif
        }
        else if (currValue is Matrix4x4)
        {
            Matrix4x4 currentInput = (Matrix4x4)currValue;
            var newValue = currentInput;

            if (ImGui.BeginTable("table_padding", 4, ImGuiTableFlags.BordersOuterV | ImGuiTableFlags.BordersInnerV))
            {
                for (int row = 0; row < 4; row++)
                {
                    ImGui.TableNextRow();
                    for (int column = 0; column < 3; column++)
                    {
                        float value = newValue[row,column];
                        if (ImGui.InputFloat(key, ref value,
                                10f, 100f,
                                "%.2f",
                                ImGuiInputTextFlags.EnterReturnsTrue))
                        {
                            if (value != currentInput[row,column])
                            {
                                Trace($"new Value {value}");
                                setFunction(key, currentInput);
                            }
                        }
                    }
                }
                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.Text($"Can't parse \"{currValue}\"");
        }
    }

    
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
#if false
                var state = _engine.State;
                switch (state)
                {
                    case Engine.EngineState.Initialized:
                    case Engine.EngineState.Starting:
                    case Engine.EngineState.Stopping:
                        ImGui.Text(state.ToString());
                        break;
                    case Engine.EngineState.Running:
                        if (ImGui.Button("Pause"))
                        {
                            _engine.SetEngineState(Engine.EngineState.Stopped);
                        }
                        break;
                    case Engine.EngineState.Stopped:
                        if (ImGui.Button("Continue"))
                        {
                            _engine.SetEngineState(Engine.EngineState.Running);
                        }
                        break;
                }

                ImGui.SameLine();
#endif

                if (ImGui.Button("Regenerate"))
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