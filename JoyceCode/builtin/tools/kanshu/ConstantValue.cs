namespace builtin.tools.kanshu;

public class ConstantValue : Value
{
    public ConstantValue(string constant)
    {
        Constant = constant;
    }
    
    public string Constant { get; set; }
    
    public override string GetBound(Scope _)
    {
        return Constant;
    }

    public override string GetUnbound()
    {
        return Constant;
    }

    public override Value GetBoundValue(Scope _)
    {
        return this;
    }
}