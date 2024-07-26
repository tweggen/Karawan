using System;

namespace nogame.config;

public class GameConfig
{
    [LiteDB.BsonId]
    public int Id { get; set; } = 1;

    public string Username { get; set; } = "";
    
    public string Password { get; set; } = "";


    public bool IsValid() => true;
    public void Fix()
    {
    }
}