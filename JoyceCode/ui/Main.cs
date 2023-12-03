using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Numerics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using engine;
using engine.gongzuo;
using engine.world;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Main
{
    private object _lo = new();
    private Engine _engine;

    private int _currentEntityId = -1;
    private string _currentClusterId = "";


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


    public void _propEdit(string key, object currValue, Action<string, object> setFunction)
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

//ImGui.Text(kvp.Value);

    public unsafe void Render(float dt)
    {
        _setStyle();
        
        ImGui.SetNextWindowPos(new Vector2(0, 20), ImGuiCond.Appearing);
        ImGui.SetNextWindowSize(
            ImGui.GetMainViewport().Size with {X = 500} - new Vector2(0, 20),
            ImGuiCond.Appearing);
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

                if (ImGui.Button("Regenerate"))
                {
                    MetaGen.Instance().Loader.WorldLoaderReleaseFragments();
                }
            }

            if (ImGui.CollapsingHeader("Config"))
            {
                if (ImGui.TreeNode("Global"))
                {
                    var dict = engine.GlobalSettings.Instance().Dictionary;
                    foreach (var kvp in dict)
                    {
                        ImGui.Text(kvp.Key);
                        ImGui.SameLine();
                        ImGui.Text(kvp.Value);
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("Props"))
                {
                    var dict = engine.Props.Instance().Dictionary;
                    foreach (var kvp in dict)
                    {
                        _propEdit(kvp.Key,kvp.Value, (key, newValue) => Props.Set(key, newValue) );
                    }

                    ImGui.TreePop();
                }
            }

            if (ImGui.CollapsingHeader("Scenes"))
            {
                var sceneKeys = _engine.SceneSequencer.GetAvailableScenes();
                foreach (var sceneKey in sceneKeys)
                {
                    ImGui.Text(sceneKey);
                }
            }

            if (ImGui.CollapsingHeader("Clusters"))
            {
                if (ImGui.BeginListBox("Clusters"))
                {
                    var clusterList = new List<ClusterDesc>(ClusterList.Instance().GetClusterList());
                    clusterList.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
                    foreach (var clusterDesc in clusterList)
                    {
                        var id = clusterDesc.Id;
                        ImGui.PushID(id);

                        bool isSelected = _currentClusterId == id;
                        string clusterString = clusterDesc.Name;

                        if (ImGui.Selectable(clusterString, isSelected))
                        {
                            _currentClusterId = id;
                        }
                        
                        if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(0))
                        {
                            _engine.QueueMainThreadAction(() =>
                            {
                                _engine.BeamTo(clusterDesc.FindStartPosition());
                            });
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }

                        ImGui.PopID();
                    }
                }
                ImGui.EndListBox();
            }

            if (ImGui.CollapsingHeader("Software"))
            {
                if (ImGui.TreeNode("Modules"))
                {
                    var modules = _engine.GetModules();
                    foreach (var module in modules)
                    {
                        if (ImGui.TreeNode(module.GetType().ToString()))
                        {
                            foreach (PropertyInfo property in module.GetType().GetProperties())
                            {
                                string propName = property.Name;
                                object propValue = property.GetValue(module);
                                if (propValue != null)
                                {
                                    _propEdit(propName, propValue, 
                                        (key, newValue) => property.SetValue(module, newValue) );
                                }
                                else
                                {
                                    ImGui.Text($"{propName}: null");
                                }
                            }

                            ImGui.TreePop();
                        }
                    }

                    ImGui.TreePop();
                }

                if (ImGui.TreeNode("Implementations"))
                {
                    var types = I.Instance.GetTypes();
                    foreach (var type in types)
                    {
                        ImGui.Text(type.ToString());
                    }

                    ImGui.TreePop();
                }
            }

            if (ImGui.CollapsingHeader("Entities"))
            {
                var entities = _engine.Entities;
                ImGui.Text($"Total {entities.Length} entities.");
                ImGui.Text($"{_engine.Simulation.Statics.Count} statics, {_engine.Simulation.Bodies.ActiveSet.Count} active bodies.");

                byte[] abText = new byte[128];
                ImGui.InputText("Filter", abText, 128);
                string utfString = Encoding.UTF8.GetString(abText, 0, abText.Length).TrimEnd((Char)0);
                
                if (ImGui.BeginListBox("Entities"))
                {
                    foreach (var entity in entities)
                    {
                        var id = entity.GetId();
                        
                        bool isSelected = _currentEntityId == id;
                        string entityString;
                        if (entity.Has<engine.joyce.components.EntityName>())
                        {
                            string entityName = entity.Get<engine.joyce.components.EntityName>().Name;
                            string displayName;
                            int lastDot = entityName.LastIndexOf('.'); 
                            if (lastDot != -1)
                            {
                                displayName = entityName.Substring(lastDot+1);
                            }
                            else
                            {
                                displayName = entityName;
                            }
                            entityString = $"#{id} {displayName}";
                        }
                        else
                        {
                            entityString = entity.ToString();
                        }


                        if (abText[0] != 0 && !entityString.Contains(utfString))
                        {
                            continue;
                        }
                        
                        ImGui.PushID(id);

                        
                        
                        if (ImGui.Selectable(entityString, isSelected))
                        {
                            _currentEntityId = entity.GetId();
                        }

                        if (isSelected)
                        {
                            ImGui.SetItemDefaultFocus();
                        }

                        ImGui.PopID();
                    }
                }
                ImGui.EndListBox();
            }

            if (ImGui.CollapsingHeader("Inspector"))
            {
                if (-1 == _currentEntityId)
                {
                    ImGui.Text("(nothing selected)");
                }
                else
                {
                    DefaultEcs.Entity entity = _engine.GetEcsWorld().FindEntity(_currentEntityId);
                    if (!entity.IsAlive)
                    {
                        ImGui.Text("(entity has ceased)");
                    }
                    else
                    {
                        engine.EntityComponentTypeReader reader = new(entity);
                        _engine.GetEcsWorld().ReadAllComponentTypes(reader);
                        
                        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2f, 2f));
                        if (ImGui.BeginTable("split", 2, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.Resizable))
                        {
                            int componentIndex = 0;
                            foreach (var (strType, componentInfo) in reader.DictComponentTypes)
                            {

                                ImGui.PushID(10 + componentIndex);
                                ImGui.TableNextRow();
                                ImGui.TableSetColumnIndex(0);
                                ImGui.AlignTextToFramePadding();
                                

                                string displayType;
                                string typeString = componentInfo.Type.ToString();
                                int lastTypeDotIndex = typeString.LastIndexOf('.');
                                if (lastTypeDotIndex != -1)
                                {
                                    displayType = typeString.Substring(lastTypeDotIndex + 1);
                                }
                                else
                                {
                                    displayType = typeString;
                                }

                                bool treeNodeResult = ImGui.TreeNodeEx("field", 0,displayType);

                                ImGui.TableSetColumnIndex(1);
                                // ImGui.SetNextItemWidth(Single.MinValue);
                                ImGui.Text(componentInfo.ValueAsString);
                                ImGui.NextColumn();
                                
                                ImGui.PopID();

                                if (treeNodeResult)
                                {
                                    System.Reflection.FieldInfo[] fields = componentInfo.Type.GetFields();

                                    foreach (var fieldInfo in fields)
                                    {
                                        Type typeAttr = fieldInfo.FieldType;
                                        string strValue = "(not available)";
                                        try
                                        {
                                            if (typeAttr == typeof(engine.gongzuo.LuaScriptEntry))
                                            {
                                                strValue = (fieldInfo.GetValue(componentInfo.Value) as LuaScriptEntry).LuaScript;
                                            }
                                            else
                                            {
                                                strValue = fieldInfo.GetValue(componentInfo.Value).ToString();
                                            }
                                        }
                                        catch (Exception e)
                                        {

                                        }
                                        ImGuiTreeNodeFlags treeNodeFlags =
                                            ImGuiTreeNodeFlags.Leaf
                                            | ImGuiTreeNodeFlags.NoTreePushOnOpen;

                                        ImGui.TableNextRow();
                                        ImGui.TableSetColumnIndex(0);
                                        ImGui.AlignTextToFramePadding();

                                        ImGui.TreeNodeEx("value", treeNodeFlags, fieldInfo.Name);

                                        ImGui.TableSetColumnIndex(1);
                                        // ImGui.SetNextItemWidth(Single.MinValue);
                                        ImGui.Text(strValue);
                                        ImGui.NextColumn();

                                    }

                                    ImGui.TreePop();
                                }

                                ++componentIndex;
                            }

                            ImGui.EndTable();
                        }
                    }

                    ImGui.PopStyleVar();
                }
            }

            if (ImGui.CollapsingHeader("Monitor"))
            {
                var frameTimings = _engine.FrameDurations;

                int count = frameTimings.Length;

                fixed (float* pTiming = &frameTimings[0])
                {
                    ImGui.PlotLines("Time per frame", ref *pTiming, count);
                }

            }

            ImGui.EndChild();
        }

        ImGui.End();
    }

    public Main(Engine engine0)
    {
        _engine = engine0;

#if false
        var io = ImGui.GetIO();
        int hadFonts = io.Fonts.Fonts.Size;
        
        io.Fonts.AddFontFromFileTTF(engine.GlobalSettings.Get("Engine.ResourcePath") + "Prototype.ttf", 12f);
        var font = io.Fonts.Fonts[hadFonts];
        io 

        // auto& io = ImGui::GetIO();
        // io.Fonts->AddFontFromFileTTF(R"(E:\_asset\font\DroidSansFallback.ttf)", 24.0f);
        // io.FontDefault = io.Fonts->Fonts[1];
        // m_pImGui->UpdateFontsTexture();
#endif

    }
}