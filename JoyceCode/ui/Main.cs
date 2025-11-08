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

    private Config _uiConfig;
    private Software _uiSoftware;
    private Clusters _uiClusters;
    private Scenes _uiScenes;
    private EntityState _sharedEntityState;
    private Entities _uiEntities;
    private EntityInspector _uiEntityInspector;
    private Monitor _uiMonitor;
    private Assets _uiAssets;


    private void _setColorScheme()
    {
        var style = ImGui.GetStyle();
        var colors = style.Colors;
        colors[(int)ImGuiCol.Text]                   = new Vector4(1.00f, 1.00f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.TextDisabled]           = new Vector4(0.50f, 0.50f, 0.50f, 1.00f);
        colors[(int)ImGuiCol.WindowBg]               = new Vector4(0.06f, 0.06f, 0.06f, 0.94f);
        colors[(int)ImGuiCol.ChildBg]                = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int)ImGuiCol.PopupBg]                = new Vector4(0.08f, 0.08f, 0.08f, 0.94f);
        colors[(int)ImGuiCol.Border]                 = new Vector4(0.43f, 0.43f, 0.50f, 0.50f);
        colors[(int)ImGuiCol.BorderShadow]           = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int)ImGuiCol.FrameBg]                = new Vector4(0.44f, 0.44f, 0.44f, 0.60f);
        colors[(int)ImGuiCol.FrameBgHovered]         = new Vector4(0.57f, 0.57f, 0.57f, 0.70f);
        colors[(int)ImGuiCol.FrameBgActive]          = new Vector4(0.76f, 0.76f, 0.76f, 0.80f);
        colors[(int)ImGuiCol.TitleBg]                = new Vector4(0.04f, 0.04f, 0.04f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive]          = new Vector4(0.16f, 0.16f, 0.16f, 1.00f);
        colors[(int)ImGuiCol.TitleBgCollapsed]       = new Vector4(0.00f, 0.00f, 0.00f, 0.60f);
        colors[(int)ImGuiCol.MenuBarBg]              = new Vector4(0.14f, 0.14f, 0.14f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarBg]            = new Vector4(0.02f, 0.02f, 0.02f, 0.53f);
        colors[(int)ImGuiCol.ScrollbarGrab]          = new Vector4(0.31f, 0.31f, 0.31f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarGrabHovered]   = new Vector4(0.41f, 0.41f, 0.41f, 1.00f);
        colors[(int)ImGuiCol.ScrollbarGrabActive]    = new Vector4(0.51f, 0.51f, 0.51f, 1.00f);
        colors[(int)ImGuiCol.CheckMark]              = new Vector4(0.13f, 0.75f, 0.55f, 0.80f);
        colors[(int)ImGuiCol.SliderGrab]             = new Vector4(0.13f, 0.75f, 0.75f, 0.80f);
        colors[(int)ImGuiCol.SliderGrabActive]       = new Vector4(0.13f, 0.75f, 1.00f, 0.80f);
        colors[(int)ImGuiCol.Button]                 = new Vector4(0.13f, 0.75f, 0.55f, 0.40f);
        colors[(int)ImGuiCol.ButtonHovered]          = new Vector4(0.13f, 0.75f, 0.75f, 0.60f);
        colors[(int)ImGuiCol.ButtonActive]           = new Vector4(0.13f, 0.75f, 1.00f, 0.80f);
        colors[(int)ImGuiCol.Header]                 = new Vector4(0.13f, 0.75f, 0.55f, 0.40f);
        colors[(int)ImGuiCol.HeaderHovered]          = new Vector4(0.13f, 0.75f, 0.75f, 0.60f);
        colors[(int)ImGuiCol.HeaderActive]           = new Vector4(0.13f, 0.75f, 1.00f, 0.80f);
        colors[(int)ImGuiCol.Separator]              = new Vector4(0.13f, 0.75f, 0.55f, 0.40f);
        colors[(int)ImGuiCol.SeparatorHovered]       = new Vector4(0.13f, 0.75f, 0.75f, 0.60f);
        colors[(int)ImGuiCol.SeparatorActive]        = new Vector4(0.13f, 0.75f, 1.00f, 0.80f);
        colors[(int)ImGuiCol.ResizeGrip]             = new Vector4(0.13f, 0.75f, 0.55f, 0.40f);
        colors[(int)ImGuiCol.ResizeGripHovered]      = new Vector4(0.13f, 0.75f, 0.75f, 0.60f);
        colors[(int)ImGuiCol.ResizeGripActive]       = new Vector4(0.13f, 0.75f, 1.00f, 0.80f);
        colors[(int)ImGuiCol.Tab]                    = new Vector4(0.13f, 0.75f, 0.55f, 0.80f);
        colors[(int)ImGuiCol.TabHovered]             = new Vector4(0.13f, 0.75f, 0.75f, 0.80f);
        colors[(int)ImGuiCol.TabActive]              = new Vector4(0.13f, 0.75f, 1.00f, 0.80f);
        colors[(int)ImGuiCol.TabUnfocused]           = new Vector4(0.18f, 0.18f, 0.18f, 1.00f);
        colors[(int)ImGuiCol.TabUnfocusedActive]     = new Vector4(0.36f, 0.36f, 0.36f, 0.54f);
        colors[(int)ImGuiCol.DockingPreview]         = new Vector4(0.13f, 0.75f, 0.55f, 0.80f);
        colors[(int)ImGuiCol.DockingEmptyBg]         = new Vector4(0.13f, 0.13f, 0.13f, 0.80f);
        colors[(int)ImGuiCol.PlotLines]              = new Vector4(0.61f, 0.61f, 0.61f, 1.00f);
        colors[(int)ImGuiCol.PlotLinesHovered]       = new Vector4(1.00f, 0.43f, 0.35f, 1.00f);
        colors[(int)ImGuiCol.PlotHistogram]          = new Vector4(0.90f, 0.70f, 0.00f, 1.00f);
        colors[(int)ImGuiCol.PlotHistogramHovered]   = new Vector4(1.00f, 0.60f, 0.00f, 1.00f);
        colors[(int)ImGuiCol.TableHeaderBg]          = new Vector4(0.19f, 0.19f, 0.20f, 1.00f);
        colors[(int)ImGuiCol.TableBorderStrong]      = new Vector4(0.31f, 0.31f, 0.35f, 1.00f);
        colors[(int)ImGuiCol.TableBorderLight]       = new Vector4(0.23f, 0.23f, 0.25f, 1.00f);
        colors[(int)ImGuiCol.TableRowBg]             = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);
        colors[(int)ImGuiCol.TableRowBgAlt]          = new Vector4(1.00f, 1.00f, 1.00f, 0.07f);
        colors[(int)ImGuiCol.TextSelectedBg]         = new Vector4(0.26f, 0.59f, 0.98f, 0.35f);
        colors[(int)ImGuiCol.DragDropTarget]         = new Vector4(1.00f, 1.00f, 0.00f, 0.90f);
        colors[(int)ImGuiCol.NavHighlight]           = new Vector4(0.26f, 0.59f, 0.98f, 1.00f);
        colors[(int)ImGuiCol.NavWindowingHighlight]  = new Vector4(1.00f, 1.00f, 1.00f, 0.70f);
        colors[(int)ImGuiCol.NavWindowingDimBg]      = new Vector4(0.80f, 0.80f, 0.80f, 0.20f);
        colors[(int)ImGuiCol.ModalWindowDimBg]       = new Vector4(0.80f, 0.80f, 0.80f, 0.35f);        
    }


    private void _setStyle()
    {
        var style = ImGui.GetStyle();

        style.GrabRounding = 8f; 
        style.FrameRounding = 8f;
        _setColorScheme();
        style.Alpha = 0.8f;
        style.IndentSpacing = 8f;
        style.WindowBorderSize = 0f;
        style.ChildBorderSize = 0f;
        style.PopupBorderSize = 1f;
        style.FrameBorderSize = 0f;
        style.TabBorderSize = 0f;
    }


    public void SetStyle()
    {
        _setStyle();
    }


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
                    //Props.Set(kvp.Key, value);
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
                // ImGui.Text(((float)kvp.Value).ToString());
                if (currentInput != (float)currValue)
                {
                    Trace($"new Value {currentInput}");
                    setFunction(key, currentInput);
                    // 
                }
            }
        }
        else
        {
            ImGui.Text($"Can't parse \"{currValue}\"");
        }
    }

    
    public unsafe void Render(float dt)
    {
        _setStyle();
        
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
            if (ImGui.BeginMainMenuBar())
            {
                if (ImGui.BeginMenu("joyce"))
                {
                    ImGui.MenuItem("About...", false);

                    ImGui.EndMenu();
                }
                if (ImGui.BeginMenu("File"))
                {
                    ImGui.MenuItem("New", false);
                    ImGui.MenuItem("Open...", false);
                    ImGui.MenuItem("Save", false);
                    ImGui.MenuItem("Save as...", false);
                    
                    ImGui.EndMenu();
                }

                ImGui.EndMainMenuBar();
            }

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