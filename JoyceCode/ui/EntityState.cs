using ImGuiNET;
using engine;
using engine.editor.components;
using ImGuiNET;
namespace joyce.ui;

public class EntityState
{
    private Main _uiMain;
    private Engine _engine;
    
    public int CurrentEntityId = -1;
    public DefaultEcs.Entity CurrentEntity = default;
    public DefaultEcs.Entity PreviousEntity = default;
    public ImGuiTreeNodeFlags InspectorHeaderFlags = 0;

    public void OnUpdate(float dt)
    {
        if (CurrentEntity != PreviousEntity)
        {
            var currentEntity = CurrentEntity;
            var previousEntity = PreviousEntity;
            if (currentEntity != default)
            {
                InspectorHeaderFlags |= ImGuiTreeNodeFlags.DefaultOpen;
            }
            PreviousEntity = CurrentEntity;

            _engine.QueueMainThreadAction(() =>
            {
                if (previousEntity != default)
                {
                    if (previousEntity.Has<engine.editor.components.Highlight>())
                    {
                        previousEntity.Remove<engine.editor.components.Highlight>();
                    }
                }

                if (currentEntity != default)
                {
                    _engine.QueueMainThreadAction(() =>
                    {
                        currentEntity.Set(new engine.editor.components.Highlight()
                        {
                            Flags = (byte)Highlight.StateFlags.IsSelected,
                            Color = 0xff33ffcc
                        });
                    });
                }
            });
        }
    }


    public EntityState(Main uiMain)
    {
        _engine = I.Get<Engine>();
        _uiMain = uiMain;
    }
}