using System;

namespace builtin.tools.Lindenmayer;

public class Op
{
    public string Name;
    public Func<Params, Params> Transformation;

    public Op(
        in string name,
        in Func<Params, Params> transformation
    ) {
        Name = name;
        Transformation = transformation;
    }
}