using System;
using System.IO;
using System.Numerics;
using engine;
using engine.world;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Main
{
    private static readonly engine.Dc _dc = engine.Dc.UI;

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


    /**
     * Height reserved for the menu bar at the top. Was written as a bare 20 in three
     * places, which is exactly how the pane and the view rectangle drift apart.
     */
    public const float MenuBarHeight = 20f;

    /** Property key, so nogame's DebuggerToggle and this pane cannot disagree. */
    public const string PropLeftPaneWidth = "ui.leftPaneWidth";

    public const float MinLeftPaneWidth = 120f;
    public const float DefaultLeftPaneWidth = 500f;

    /**
     * Width of the left debug pane, in LOGICAL units - the same space the engine's view
     * rectangle uses.
     *
     * It lives in engine.Props rather than in a field because two assemblies need it and
     * neither can see the other: this pane is in JoyceCode, while the F12 toggle that
     * applies it to the view rectangle is nogame's DebuggerToggle. Both used to hardcode
     * 500 independently, so any change had to be made twice or the 3D view and the panel
     * would overlap.
     *
     * Setting it also republishes the engine's view rectangle, so the 3D viewport follows
     * the drag immediately rather than on the next F12.
     */
    public static float LeftPaneWidth
    {
        get
        {
            object v = Props.Get(PropLeftPaneWidth, DefaultLeftPaneWidth);
            float f = v switch
            {
                float ff => ff,
                double dd => (float)dd,
                int ii => ii,
                string ss when float.TryParse(ss, out float ps) => ps,
                _ => DefaultLeftPaneWidth
            };
            return f < MinLeftPaneWidth ? MinLeftPaneWidth : f;
        }
        set
        {
            float clamped = value;
            if (clamped < MinLeftPaneWidth) clamped = MinLeftPaneWidth;

            /*
             * Only the MINIMUM is enforced here. The upper bound needs the window width,
             * which is an ImGui viewport query and therefore only valid inside a frame -
             * and this setter must stay callable from nogame's DebuggerToggle, which runs
             * with no ImGui context. The drag clamps its own maximum, where the viewport
             * size is already in hand.
             */
            Props.Set(PropLeftPaneWidth, clamped);
            I.Get<engine.Engine>().SetViewRectangle(new Vector2(clamped, MenuBarHeight), Vector2.Zero);
        }
    }


    private bool _isEnginePaused = false;


    private string? _strRequestedModal = null;

    public void RequestModal(string strRequestedModal)
    {
        _strRequestedModal = strRequestedModal;
    }

    public unsafe void Render(float dt)
    {
        _uiStyle.Render(dt);
        
        ImGui.SetNextWindowPos(new Vector2(0, MenuBarHeight));
        var mainViewportSize = ImGui.GetMainViewport().Size;
        float paneWidth = LeftPaneWidth;
        ImGui.SetNextWindowSize(new Vector2(paneWidth, mainViewportSize.Y - MenuBarHeight));
        if (ImGui.Begin("selector", 0
                |ImGuiWindowFlags.NoCollapse
                |ImGuiWindowFlags.NoMove
                |ImGuiWindowFlags.NoResize
                //|ImGuiWindowFlags.ChildWindow
                //|ImGuiWindowFlags.NoTitleBar
                ))
        {
            _uiMenuBar.Render(dt);

            if (_strRequestedModal != null)
            {
                ImGui.OpenPopup(_strRequestedModal);
                _strRequestedModal = null;
            }
            /*
            * Render a possible file modal dialog.
            */
            var isOpen = true;
            if (ImGui.BeginPopupModal("save-file", ref isOpen, ImGuiWindowFlags.NoTitleBar))
            {
                var picker = FileDialog.GetFileDialog(this, Path.Combine(Environment.CurrentDirectory));
                bool doClose = false;
                bool wasSelected = picker.Draw();

                if (wasSelected)
                {
                    Trace(_dc, $"selected file {picker.SelectedFile}");
                    doClose = true;
                }
                if (doClose)
                {
                    FileDialog.RemoveFileDialog(this);
                    ImGui.CloseCurrentPopup();
                }
                ImGui.EndPopup();
            }


            
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
            
        }

        ImGui.End();

        _renderSplitter(mainViewportSize);
    }


    /**
     * A drag handle on the right edge of the left pane, so the 3D view can be made wider or
     * narrower at runtime.
     *
     * Invisible by default - it draws only a faint line while hovered or dragged - because
     * the boundary is already obvious from the pane itself and a permanent bar would eat
     * screen space to say something the layout already says.
     *
     * It is an ImGui window rather than something handled in Platform's mouse callbacks so
     * that ImGui's own WantCaptureMouse covers it: while the grip is held, the drag cannot
     * leak through to the game as camera movement. That is the same mechanism that stops a
     * click on a panel button from also steering the ship.
     */
    private void _renderSplitter(Vector2 mainViewportSize)
    {
        const float grip = 8f;

        float paneWidth = LeftPaneWidth;
        float height = mainViewportSize.Y - MenuBarHeight;

        ImGui.SetNextWindowPos(new Vector2(paneWidth - grip * 0.5f, MenuBarHeight));
        ImGui.SetNextWindowSize(new Vector2(grip, height));

        if (ImGui.Begin("##ui-splitter", 0
                | ImGuiWindowFlags.NoTitleBar
                | ImGuiWindowFlags.NoResize
                | ImGuiWindowFlags.NoMove
                | ImGuiWindowFlags.NoScrollbar
                | ImGuiWindowFlags.NoScrollWithMouse
                | ImGuiWindowFlags.NoSavedSettings
                | ImGuiWindowFlags.NoBackground
                | ImGuiWindowFlags.NoBringToFrontOnFocus))
        {
            ImGui.SetCursorPos(Vector2.Zero);
            ImGui.InvisibleButton("##ui-splitter-grip", new Vector2(grip, height));

            bool isHot = ImGui.IsItemHovered() || ImGui.IsItemActive();
            if (isHot)
            {
                ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);

                var dl = ImGui.GetWindowDrawList();
                var p0 = ImGui.GetWindowPos() + new Vector2(grip * 0.5f, 0f);
                dl.AddLine(p0, p0 + new Vector2(0f, height),
                    ImGui.GetColorU32(ImGui.IsItemActive()
                        ? ImGuiCol.SeparatorActive
                        : ImGuiCol.SeparatorHovered), 2f);
            }

            if (ImGui.IsItemActive())
            {
                /*
                 * MouseDelta is in the same LOGICAL units as DisplaySize and as the engine's
                 * view rectangle, so no scaling belongs here. That equivalence only became
                 * true when the HiDPI DisplaySize defect was fixed - before it, this would
                 * have tracked at half speed on a retina display.
                 */
                float dragged = paneWidth + ImGui.GetIO().MouseDelta.X;
                float max = mainViewportSize.X - MinLeftPaneWidth;
                if (max > MinLeftPaneWidth && dragged > max) dragged = max;
                LeftPaneWidth = dragged;
            }
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