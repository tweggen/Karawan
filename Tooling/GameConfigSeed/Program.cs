using System;
using System.IO;
using LiteDB;
using nogame.config;

/*
 * Usage: GameConfigSeed <rwpath> <eMail> <password> [webToken]
 *
 * Replaces <rwpath>/gameconfig.db with one GameConfig holding the given
 * credentials. Mirrors what engine.DBStorage and nogame.config.Module write:
 * file "gameconfig.db", user version 3, collection named after the type.
 * Never point this at your real RW path; it overwrites the stored login.
 */
if (args.Length < 3 || args.Length > 4)
{
    Console.Error.WriteLine("usage: GameConfigSeed <rwpath> <eMail> <password> [webToken]");
    return 2;
}

const int DbVersion = 3; // engine.DBStorage.DbVersion

string rwPath = args[0];
Directory.CreateDirectory(rwPath);
string path = Path.Combine(rwPath, "gameconfig.db");
File.Delete(path);

using (var db = new LiteDatabase(path))
{
    db.UserVersion = DbVersion;
    db.GetCollection<GameConfig>().Insert(new GameConfig
    {
        Username = args[1],
        Password = args[2],
        WebToken = args.Length > 3 ? args[3] : "",
        LoginMode = (int)GameConfig.Mode.LoginGlobally
    });
    db.Commit();
}

Console.WriteLine($"Wrote {path} for {args[1]}" + (args.Length > 3 ? " with a preset web token." : "."));
return 0;
