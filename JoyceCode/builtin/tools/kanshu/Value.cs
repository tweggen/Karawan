using System.Text.Json.Serialization;

namespace builtin.tools.kanshu;

[JsonDerivedType(typeof(ConstantValue))]
[JsonDerivedType(typeof(BoundValue))]
public abstract class Value
{
    public abstract string GetBound(Scope scope);
    public abstract string GetUnbound();

    public abstract Value GetBoundValue(Scope scope);
}