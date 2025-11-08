using ImGuiNET;

namespace joyce.ui;

public class MenuBar : APart
{
    public override void Render(float dt)
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


    }
    
    
    public MenuBar(Main uiMain) : base(uiMain)
    {
    }
}