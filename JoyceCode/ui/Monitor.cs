using ImGuiNET;

namespace joyce.ui;

public class Monitor : APart
{
    public override void Render(float dt)
    {             
        var frameTimings = _engine.FrameDurations;

        int count = frameTimings.Length;

        unsafe
        {
            fixed (float* pTiming = &frameTimings[0])
            {
                ImGui.PlotLines("Time per frame", ref *pTiming, count);
            }
        }

    }

    public Monitor(Main uiMain) : base(uiMain)
    {
    }

}