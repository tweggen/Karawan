using System;

namespace nogame.config;

public class GameConfig
{
    public const string DefaultUsername = "";
    public const string DefaultPassword = "";

    [LiteDB.BsonId]
    public int Id { get; set; } = 1;

    public string Username { get; set; } = "";
    
    public string Password { get; set; } = "";


    public bool IsValid()
    {
        return
            Username != null
            && Password != null;
    }
    
    public void Fix()
    {
        if (Username == null) Username = DefaultUsername;
        if (Password == null) Password = DefaultPassword;
    }
}
