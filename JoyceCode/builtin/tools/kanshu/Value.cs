namespace builtin.tools.kanshu;

public abstract class Value
{
    public abstract string GetBound(Scope scope);
    public abstract string GetUnbound();

    public abstract Value GetBoundValue(Scope scope);
}