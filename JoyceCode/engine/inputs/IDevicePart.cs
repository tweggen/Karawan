namespace engine.inputs;

/**
 * What KIND of control a device part is.
 *
 * Present so a bindings UI can group and render controls without type-testing its way
 * through the interface hierarchy, and so a serialised binding can name a control kind
 * without depending on a CLR type name.
 */
public enum DevicePartKind
{
    /** Digital, two-state. Gamepad face/shoulder buttons, mouse buttons. */
    Button,

    /** Digital, two-state, and identified by physical position. See IKey. */
    Key,

    /** Analogue, two axes, each nominally -1..+1 and centred at 0. */
    Thumbstick,

    /** Analogue, one axis, nominally 0..+1 and resting at 0. */
    Trigger,

    /** Analogue or notched, one or two axes. Mouse wheels, some flight controls. */
    Wheel,

    /** OUTPUT, not input. See IMotor - it does not fit the event model at all. */
    Motor,
}


/**
 * A single control on a device: capability description, never a source of events.
 *
 * WHY THIS EXISTS AT ALL
 *
 * It used to carry nothing but a Name, which made it decoration. The split agreed in
 * WP-6.3 gives it a job: engine.news.EventQueue reports everything that HAPPENS, and
 * engine.inputs describes what EXISTS. A bindings UI needs the second - it has to
 * enumerate "what can I bind on this pad?" without waiting for the user to press
 * something first.
 *
 * DELIBERATELY NOT HERE
 *
 * No events. Silk's device interfaces carry KeyDown/KeyUp and that shape was copied in
 * when engine.inputs was first extracted; WP-6.3 removes it on purpose. A C# event fires
 * immediately on the producing thread, while queue events drain later on the logical
 * thread - mixing the two gives two orderings and two threads for one conceptual stream.
 *
 * No current VALUE either. "Is this button down right now?" is state, and state
 * reconstructed from a second channel is state that can disagree with the first one.
 * Read the queue.
 */
public interface IDevicePart
{
    /**
     * Display name, in whatever form the platform provides. For SHOWING to a user, never
     * for lookup - it is not stable across platforms, drivers or locales.
     */
    public string Name { get; }

    public DevicePartKind Kind { get; }
}


/** Digital, two-state. */
public interface IButton : IDevicePart
{
}


/**
 * A keyboard key, identified by PHYSICAL POSITION.
 *
 * The ScanCode is the bindable identity; Name is what the user's layout prints on it and
 * is display-only. On AZERTY, the key with ScanCode.W is named "Z". Binding on Name would
 * be the exact defect ScanCode exists to prevent.
 */
public interface IKey : IDevicePart
{
    public ScanCode ScanCode { get; }
}


/** Analogue, two axes, each nominally -1..+1, centred at 0. */
public interface IThumbstick : IDevicePart
{
}


/** Analogue, one axis, nominally 0..+1, resting at 0. */
public interface ITrigger : IDevicePart
{
}


/** Analogue or notched. Mouse wheels and similar. */
public interface IWheel : IDevicePart
{
}


/**
 * Rumble. OUTPUT - and therefore the odd one out of this whole file.
 *
 * Everything else here describes something the user can DO, which eventually arrives as a
 * queue event. A motor is the reverse: the game drives it, and no event ever results. It
 * genuinely does not fit the queue model, and pretending otherwise would bend the model
 * for one case.
 *
 * So this is a CAPABILITY MARKER only: it says the device has a motor, which is what a
 * settings screen needs in order to offer a "vibration" toggle. Actuation is deliberately
 * NOT designed here - inventing SetIntensity() now would front-run WP-6.4, which owns the
 * question of how the game addresses controls, and an output API guessed in advance is an
 * output API somebody has to live with.
 */
public interface IMotor : IDevicePart
{
}
