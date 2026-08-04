# Threading Model

How Joyce distributes work across threads, and the rules that keep a bounded
worker pool from deadlocking on small-core devices.

## Thread roles

| Thread | Constraint | Entry points |
|--------|-----------|--------------|
| **Logical thread** | Only thread allowed to touch the DefaultEcs world (`DefaultEcs.Entity.OkThread`). Must **never block**. | `Engine.QueueMainThreadAction`, `Engine.QueueEventHandler`, `Engine.QueueEntitySetupAction`, `Engine.QueueCleanupAction`, `Engine.RunMainThread` (inlines when already on the logical thread), `Engine.TaskMainThread` (returns a `Task`) |
| **Platform thread** | Owns the OpenGL context; all GL calls happen here. Provided by the platform layer (Silk.NET window loop). | Splash renderer internals |
| **Worker pool** | General-purpose tasks, capped concurrency (see below). | `Engine.Run(...)`, `Engine.TF` (a `TaskFactory` bound to the capped scheduler) |
| Audio etc. | Subsystem-specific threads, out of scope here. | |

## The capped worker pool

`Engine.Run` schedules onto a `LimitedConcurrencyLevelTaskScheduler`
(`JoyceCode/engine/run/LimitedConcurrencyLevelTaskScheduler.cs`), which runs
worker loops on the .NET ThreadPool but caps how many run concurrently
(`max(5, ProcessorCount - 2)`, +2 in DEBUG — see the `Engine` ctor). The cap
exists to protect CPU caches on small-core devices (Android): it bounds
*runnable* threads.

### The managed-blocker protocol

A bounded pool where running tasks may block synchronously on *queued* work is
a thread-starvation deadlock waiting to happen: once all N slots are held by
blocked tasks whose wakeup depends on queued tasks, nothing ever runs again.

The scheduler therefore implements the *managed blocker* pattern (compare Java's
`ForkJoinPool.ManagedBlocker`, Go's syscall parking):

- `BeginBlocking()` — called by a worker about to block. Releases the worker's
  concurrency slot and, if work is queued, starts a compensation worker.
  No-op on non-worker threads and in nested blocking regions.
- `EndBlocking(releasedSlot)` — re-acquires the slot on wake. The count may
  transiently exceed the cap; worker loops shed the oversubscription as they
  finish tasks.

The invariant this buys: **the number of runnable workers never exceeds the
cap, and a blocked task can never prevent the work it waits on from being
scheduled.** Starvation deadlock becomes impossible by construction, so the cap
can be chosen purely from cache/core considerations — not from worst-case
dependency-chain depth.

### Rules for engine and game code

1. **Never block on the logical thread.** `Engine.WaitBlocking` logs a warning
   (DEBUG) when it happens; blocking there stalls the frame even though it
   cannot deadlock the pool.
2. **On pool tasks, route synchronous waits through `Engine.WaitBlocking(...)`**
   (`Action` and `Func<T>` overloads) so the slot is released while waiting.
   Raw `Task.Wait()` / `.Result` / `SemaphoreSlim.Wait()` on a pool task can
   still starve the pool.
3. **Prefer async.** `await` / `WaitAsync().ContinueWith(...)` holds no thread
   at all while waiting (see `ModelCache` for the pattern). `WaitBlocking` is
   the safety net for waits that must stay synchronous.
4. Short, bounded critical sections that never wait on scheduled work (e.g. a
   semaphore guarding a dictionary check, released microseconds later) do not
   need wrapping — the wrap costs two global lock acquisitions and possibly a
   spurious compensation thread.

### Known limits

- Compensation threads are uncapped: a blocked worker's replacement can itself
  block and spawn another. Combined with ThreadPool injection throttling
  (~1 thread/500 ms past min-threads), deep blocking chains degrade to a
  multi-second stall on core-poor machines instead of a deadlock — blocking is
  survivable, not free.
- Cross-scheduler cycles are not covered: a pool task blocking on
  `TaskMainThread(...)` while the logical thread blocks on the pool is a
  genuine deadlock cycle. Rule 1 is the defense.

## Tests

`tests/JoyceCode.Tests/engine/run/ManagedBlockerSchedulerTests.cs` — starvation
resolution under cap 1, cap adherence after blocking round trips, nesting,
no-op behavior off worker threads, misuse hardening.
