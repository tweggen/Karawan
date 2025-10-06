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


    public string WebToken { get; set; } = "";


    public enum Mode 
    {
        LoginGlobally = 0,
        LoginLocally = 1
    }

    public int LoginMode { get; set; } = (int) Mode.LoginGlobally;
    
    public bool IsValid()
    {
        return
            Username != null
            && Password != null
            && WebToken != null;
    }
    
    
    public void Fix()
    {
        if (Username == null) Username = DefaultUsername;
        if (Password == null) Password = DefaultPassword;
        if (WebToken == null) WebToken = "";
    }
}
