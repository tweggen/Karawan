namespace engine.inputs;

/**
 * Identity and capability only - NOT a source of events.
 *
 * ScanCode deliberately does not live here any more. Key presses are things that HAPPEN,
 * so they travel on engine.news.EventQueue and carry their ScanCode in the event payload;
 * a device describes what EXISTS. Silk's IKeyboard carried KeyDown/KeyUp and that shape
 * was copied here initially; WP-6.3 splits the two jobs apart deliberately.
 */
public interface IKeyboard : IDevice
{
}
