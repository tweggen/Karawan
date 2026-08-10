using System.Collections.Generic;

namespace engine.inputs;

/**
 * Identity and capability. Not a source of events.
 *
 * ScanCode deliberately does not live here. A key press is something that HAPPENS, so it
 * travels on engine.news.EventQueue and carries its ScanCode in the event payload; a
 * device describes what EXISTS. Silk's IKeyboard carried KeyDown/KeyUp and that shape was
 * copied in when engine.inputs was first extracted; WP-6.3 splits the two jobs apart.
 *
 * Keys enumerates what this keyboard HAS, which is what a rebinding screen needs in order
 * to show bindable controls before the user has pressed anything. It is not a state
 * query: there is no "is this down right now?" here, because that state reconstructed
 * from a second channel is state that can disagree with the queue.
 */
public interface IKeyboard : IDevice
{
    IReadOnlyList<IKey> Keys { get; }
}
