namespace engine;


/**
 * Something that can be executed in the game.
 * This might be lua or native, or a rest call.
 */
public class GameAction
{
    public string Action { get; set; }

    public GameAction(string action)
    {
        Action = action;
    }
}