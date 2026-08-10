namespace engine.inputs;

public interface IDevicePart
{
    public string Name { get; }
}

public interface IButton : IDevicePart
{
}

public interface IThumbstick : IDevicePart
{
}

public interface ITrigger : IDevicePart
{
}

public interface IMotor : IDevicePart
{
}

public interface IKey : IDevicePart
{
}


public interface IWheel : IDevicePart
{
}


