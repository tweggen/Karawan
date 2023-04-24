namespace builtin.tools.Lindenmayer;

public class Instance
{
    private System _system;

    public State State;

    public string ToString()
    {
        if( State != null )
        {
            return $"Instance {{ {State} }}";
        } else {
            return "Instance {{ state: null }}";
        }
    }

    public Instance Clone()
    {
        if( State != null ) {
            return new Instance( _system, State.Clone() );
        } else {
            return new Instance( _system, State );
        }
    }

    public Instance( System system, State state ) {
        _system = system;
        State = state;
    }    
}