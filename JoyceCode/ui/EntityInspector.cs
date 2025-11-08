using System;
using System.Numerics;
using engine.gongzuo;
using ImGuiNET;
namespace joyce.ui;

public class EntityInspector : APart
{
    private EntityState _sharedEntityState;
    
    public override void Render(float dt)
    {
        if (-1 == _sharedEntityState.CurrentEntityId)
        {
            ImGui.Text("(nothing selected)");
        }
        else
        {
            DefaultEcs.Entity entity = _engine.GetEcsWorldDangerous().FindEntity(_sharedEntityState.CurrentEntityId);
            if (!entity.IsAlive)
            {
                ImGui.Text("(entity has ceased)");
            }
            else
            {
                engine.EntityComponentTypeReader reader = new(entity);
                _engine.GetEcsWorldDangerous().ReadAllComponentTypes(reader);

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

                        bool treeNodeResult = ImGui.TreeNodeEx("field", 0, displayType);

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
                                        strValue = (fieldInfo.GetValue(componentInfo.Value) as LuaScriptEntry)
                                            .LuaScript;
                                    }
                                    else if (typeAttr == typeof(Matrix4x4))
                                    {
                                        Matrix4x4 m = (Matrix4x4)(fieldInfo.GetValue(componentInfo.Value));
                                        strValue =
                                            $"{m.M11} {m.M12} {m.M13} {m.M14}\n{m.M21} {m.M22} {m.M23} {m.M24}\n{m.M31} {m.M32} {m.M33} {m.M34}\n{m.M41} {m.M42} {m.M43} {m.M44}\n";
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

    
    public EntityInspector(Main uiMain, EntityState sharedEntityState) : base(uiMain)
    {
        _sharedEntityState = sharedEntityState;
    }
}

