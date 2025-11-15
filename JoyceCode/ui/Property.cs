using System;
using System.Numerics;
using System.Reflection;
using engine;
using engine.gongzuo;
using ImGuiNET;
using static engine.Logger;

namespace joyce.ui;

public class Property
{
    public static string DisplayType(Type type)
    {
        string typeString = type.ToString();
        int lastTypeDotIndex = typeString.LastIndexOf('.');
        if (lastTypeDotIndex != -1)
        {
            return typeString.Substring(lastTypeDotIndex + 1);
        }
        else
        {
            return typeString;
        }
    }
    
    
    public static string ValueAsString(Type typeAttr, object obj)
    {
        string strValue = "(not available)";
        
        if (typeAttr == typeof(engine.gongzuo.LuaScriptEntry))
        {
            LuaScriptEntry? luaScriptEntry = (obj as LuaScriptEntry);
            if (null != luaScriptEntry && null != luaScriptEntry.LuaScript)
            {
                strValue = luaScriptEntry.LuaScript;
            }
        }
        else if (typeAttr == typeof(Matrix4x4))
        {
            Matrix4x4 m = (Matrix4x4)(obj);
            strValue =
                $"{m.M11} {m.M12} {m.M13} {m.M14}\n{m.M21} {m.M22} {m.M23} {m.M24}\n{m.M31} {m.M32} {m.M33} {m.M34}\n{m.M41} {m.M42} {m.M43} {m.M44}\n";
        }
        else
        {
            strValue = obj.ToString();
        }

        return strValue;
    }
    

    public static string FieldAsString(
        ComponentInfo componentInfo,
        FieldInfo fieldInfo)
    {
        string strValue = "(not available)";
        if (null == componentInfo || null == fieldInfo || null == componentInfo.Value)
        {
            return strValue;
        }
        
        try
        {
            Type typeAttr = fieldInfo.FieldType;
            object obj = fieldInfo.GetValue(componentInfo.Value);
            if (null == obj)
            {
                strValue = "(null)";
            }
            else
            {
                strValue = ValueAsString(typeAttr, obj);
            }
        }
        catch (Exception e)
        {
            strValue = "(error during conversion)";
        }

        return strValue;
    }
    
    public static void Edit(string key, object currValue, Action<string, object> setFunction)
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
                    "%.2f", 0))
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
                    10, 100, 0))
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
                    10, 100, 0))
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
                    for (int column = 0; column < 4; column++)
                    {
                        float value = newValue[row,column];
                        ImGui.PushID(row * 4 + column);
                        if (ImGui.InputFloat(key, ref value,
                                10f, 100f,
                                "%.2f", 0))
                        {
                            if (value != currentInput[row,column])
                            {
                                Trace($"new Value {value}");
                                setFunction(key, currentInput);
                            }
                        }
                        ImGui.PopID();
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

    

}