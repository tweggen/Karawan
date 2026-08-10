using System.Collections.Generic;

namespace engine.inputs;

/**
 * What input devices EXIST right now. Enumeration and capability only.
 *
 * The other half of the WP-6.3 split: engine.news.EventQueue reports everything that
 * HAPPENS - keys, mouse, gamepad, and device attach/detach - while this reports what is
 * there to be bound. Nothing here is subscribable, deliberately.
 *
 * DEVICE ARRIVAL IS NOT HERE
 *
 * There used to be an `event Action<IDevice, bool>? OnConnectionChanged`. It is gone;
 * arrival and departure travel as Event.INPUT_DEVICE_ATTACHED / INPUT_DEVICE_DETACHED.
 *
 * That is a race fix, not tidiness. A C# event fires synchronously on whichever thread
 * the platform noticed the device on, while queue events drain later on the logical
 * thread. With the two split across channels, a newly attached gamepad's first axis event
 * could be processed BEFORE the game was told the gamepad existed - a reordering that
 * appears only when a device is hot-plugged during play, which is exactly when nobody is
 * looking. One channel gives one ordering and one thread by construction.
 *
 * THREADING CONTRACT - READ BEFORE IMPLEMENTING
 *
 * These collections are read from the LOGICAL thread while the platform mutates its
 * device list on its own thread. Implementations must therefore publish an IMMUTABLE
 * SNAPSHOT and swap it atomically on change - build the new list, then assign the
 * reference - rather than mutating a shared collection in place.
 *
 * The property type says so: IReadOnlyList, not IEnumerable. A caller that starts a
 * foreach cannot then have the collection change underneath it, so there is no
 * InvalidOperationException to hit at 3am and no lock in the read path. A caller wanting
 * a stable view across several properties should copy the references it needs first;
 * each property is individually consistent, the set of them is not transactional.
 */
public interface IContext
{
    IReadOnlyList<IGamepad> Gamepads { get; }

    IReadOnlyList<IKeyboard> Keyboards { get; }

    IReadOnlyList<IMouse> Mice { get; }

    /**
     * Anything recognised as a device but not one of the above - flight sticks, wheels,
     * pedal boards. Present so an unknown device is enumerable rather than invisible.
     */
    IReadOnlyList<IDevice> Others { get; }
}
