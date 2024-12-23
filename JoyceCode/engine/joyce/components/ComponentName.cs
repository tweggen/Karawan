namespace engine.joyce.components;

[engine.IsPersistable]

public struct EntityName
{
    public string Name { get; set; }

    public override string ToString()
    {
        return $"Name: \"{Name}\"";
    }

    public EntityName(string name)
    {
        Name = name;
    }
}