namespace engine.joyce.components;

public struct EntityName
{
    public string Name;

    public override string ToString()
    {
        return $"Name: \"{Name}\"";
    }

    public EntityName(string name)
    {
        Name = name;
    }
}