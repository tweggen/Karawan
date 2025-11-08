using ImGuiNET;
using System;
using System.Reflection;
using System.Text;
using engine;
using engine.editor.components;
using engine.physics.systems;

using static engine.Logger;

namespace joyce.ui;

public class Entities : APart
{
    private EntityState _sharedEntityState;
    
    private byte[] _currentEntityFilterBytes = new byte[128];
    public override void Render(float dt)
    {
                var entities = _engine.Entities;
                ImGui.Text($"Total {entities.Length} entities.");
                lock (_engine.Simulation)
                {
                    ImGui.Text(
                        $"{_engine.Simulation.Statics.Count} statics, {_engine.Simulation.Bodies.ActiveSet.Count} active bodies.");
                }

                ImGui.InputText("Filter", _currentEntityFilterBytes, (uint) _currentEntityFilterBytes.Length);
                string utf8FilterText = Encoding.UTF8.GetString(_currentEntityFilterBytes, 0, _currentEntityFilterBytes.Length).TrimEnd((Char)0);
                
                if (ImGui.BeginListBox("Entities"))
                {
                    foreach (var entity in entities)
                    {
                        if (!entity.IsAlive) continue;
                        var id = entity.GetId();
                        
                        bool isSelected = _sharedEntityState.CurrentEntityId == id;
                        string entityString;
                        string entityName = "";
                        if (entity.Has<engine.joyce.components.EntityName>())
                        {
                            entityName = entity.Get<engine.joyce.components.EntityName>().Name;
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


                        if (_currentEntityFilterBytes[0] != 0 && !entityName.ToUpper().Contains(utf8FilterText.ToUpper()))
                        {
                            continue;
                        }
                        
                        ImGui.PushID(id);

                        
                        
                        if (ImGui.Selectable(entityString, isSelected))
                        {
                            _sharedEntityState.CurrentEntity = entity;
                            _sharedEntityState.CurrentEntityId = entity.GetId();
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


    public void OnAfterRenderAlways(float dt)
    {


    }
    
    
    public Entities(Main uiMain, EntityState sharedEntityState) : base(uiMain)
    {
        _sharedEntityState = sharedEntityState;
    }

}