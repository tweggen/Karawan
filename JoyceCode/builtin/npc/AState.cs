namespace builtin.npc;


/**
 * Current state of an npc.
 * A state can contain several members to modify the current NPCs properties:
 *
 * - Behavior to use
 * - flag if physics is dynamic or kinematic
 * - animation state
 * - flag to terminate NPC
 * - sound to play on entry
 * - the Navigator to attach to
 *
 * Some things might be available in a superordinate space, such
 * as the entity's private state, available navigators etc. .
 * 
 * // TXWTODO: State tree? entity Property map?
 *
 * State implementations probably will emit events. FSM or strategy machine
 * will catch events and eventually trigger state transitions.
 */
public class AState
{
    
}