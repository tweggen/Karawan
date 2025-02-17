using System;
using static engine.Logger;

namespace builtin.tools.kanshu;

public class BoundValue : Value
{
    public BoundValue(string bindingName)
    {
        BindingName = bindingName;
    }
    public string BindingName { get; set; }

    public override string GetBound(Scope scope)
    {
        if (scope.HasBinding(BindingName, out var boundValue))
        {
            return boundValue;
        }

        ErrorThrow<ArgumentException>($"Unable to find binding for {BindingName}");
        return default;
    }

    public override string GetUnbound()
    {
        ErrorThrow<ArgumentException>($"Unable to return unbound value for {BindingName}, is a binding.");
        return default;
    }

    public override Value GetBoundValue(Scope scope)
    {
        return new ConstantValue(GetBound(scope));
    }
}