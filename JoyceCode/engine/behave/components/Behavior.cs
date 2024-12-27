using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using builtin;
using engine.joyce;
using static engine.Logger;

namespace engine.behave.components;

[engine.IsPersistable]
public struct Behavior
{
    [JsonInclude] public IBehavior Provider;
    [JsonInclude] public short MaxDistance = 150;
    [JsonInclude] public ushort Flags;

    [Flags]
    public enum BehaviorFlags
    {
        InRange = 0x0001,
        DontVisibInRange = 0x0002,
        DontCallBehave = 0x0004,
        DontEstimateMotion = 0x0008,

        /**
         * This character shall not be automatically be cleaned up,
         * it is part of a mission.
         */
        MissionCritical = 0x0010,
    }

    public bool MayBePurged()
    {
        return 0 == (Flags & (ushort)BehaviorFlags.MissionCritical);
    }


    public override string ToString()
    {
        return $"Provider={Provider.GetType()}";
    }

    public Behavior(IBehavior provider)
    {
        Provider = provider;
    }
}