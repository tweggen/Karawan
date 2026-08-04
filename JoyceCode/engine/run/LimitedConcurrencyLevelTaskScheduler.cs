using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace engine;


/// <summary>
/// Provides a task scheduler that ensures a maximum concurrency level while
/// running on top of the thread pool.
/// </summary>
/// <remarks>
/// <para>
/// In addition to the MSDN sample this scheduler implements a "managed blocker"
/// protocol, modelled after Java's <c>ForkJoinPool.ManagedBlocker</c>.
/// </para>
/// <para>
/// The scheduler only ever runs <see cref="_maxDegreeOfParallelism"/> worker
/// loops at a time. A task that blocks <i>synchronously</i> (SemaphoreSlim.Wait,
/// Task.Wait, ...) keeps holding its concurrency slot for the whole duration of
/// the block. If all slots end up held by tasks whose wakeup depends on work
/// that is still sitting in <see cref="_tasks"/>, the scheduler
/// starvation-deadlocks: nobody can run the waker because nobody will give up a
/// slot.
/// </para>
/// <para>
/// The protocol against that is:
/// <code>
///     bool released = scheduler.BeginBlocking();
///     try { /* the blocking wait */ }
///     finally { scheduler.EndBlocking(released); }
/// </code>
/// <see cref="BeginBlocking"/> hands the calling worker's slot back and, if
/// there is queued work waiting for it, immediately starts a compensation
/// worker in its place. <see cref="EndBlocking"/> re-takes a slot when the wait
/// is over. Re-taking may transiently push the number of running workers above
/// the configured maximum; this is intended and decays on its own, because the
/// worker loop sheds surplus workers as soon as they come around to picking
/// their next item.
/// </para>
/// <para>
/// Both calls are no-ops on threads that are not workers of this scheduler, and
/// nested blocking regions on one thread only release/re-acquire once (the
/// nesting depth is tracked per thread). Therefore it is always safe to call
/// them, whatever thread the code happens to run on.
/// </para>
/// <para>
/// Known design limit: compensation workers are not capped. A compensation
/// worker may itself block and spawn another one, so a chain of blocking tasks
/// can end up requesting arbitrarily many ThreadPool threads. Since the
/// ThreadPool only injects roughly one thread per 500 ms once it is past its
/// minimum thread count, a deep blocking chain on a core-poor machine turns
/// what used to be a deadlock into a multi-second stall. In other words: this
/// protocol removes the deadlock, it does not make synchronous blocking cheap.
/// Prefer async waiting where you can.
/// </para>
/// </remarks>
public class LimitedConcurrencyLevelTaskScheduler : TaskScheduler
{
   // The scheduler whose worker loop the current thread is running, if any.
   // Instance identity matters: with a plain bool, a worker of scheduler A
   // calling B.BeginBlocking() would corrupt B's slot accounting, and A's
   // workers would inline B's tasks.
   [ThreadStatic]
   private static LimitedConcurrencyLevelTaskScheduler _currentThreadScheduler;

   // How many nested blocking regions the current thread is inside of.
   // Only the outermost one releases/re-acquires the concurrency slot.
   [ThreadStatic]
   private static int _currentThreadBlockingDepth;

  // The list of tasks to be executed
   private readonly LinkedList<Task> _tasks = new LinkedList<Task>(); // protected by lock(_tasks)

   // The maximum concurrency level allowed by this scheduler.
   private readonly int _maxDegreeOfParallelism;

   // Indicates whether the scheduler is currently processing work items.
   private int _delegatesQueuedOrRunning = 0;

   // Number of worker loops currently accounted for. Exposed for tests only.
   internal int DelegatesQueuedOrRunning
   {
       get { lock (_tasks) return _delegatesQueuedOrRunning; }
   }

   // Creates a new instance with the specified degree of parallelism.
   public LimitedConcurrencyLevelTaskScheduler(int maxDegreeOfParallelism)
   {
       if (maxDegreeOfParallelism < 1) throw new ArgumentOutOfRangeException("maxDegreeOfParallelism");
       _maxDegreeOfParallelism = maxDegreeOfParallelism;
   }

   // Queues a task to the scheduler.
   protected sealed override void QueueTask(Task task)
   {
      // Add the task to the list of tasks to be processed.  If there aren't enough
      // delegates currently queued or running to process tasks, schedule another.
       lock (_tasks)
       {
           _tasks.AddLast(task);
           if (_delegatesQueuedOrRunning < _maxDegreeOfParallelism)
           {
               ++_delegatesQueuedOrRunning;
               NotifyThreadPoolOfPendingWork();
           }
       }
   }

   /// <summary>
   /// Announce that the calling thread is about to block synchronously.
   /// </summary>
   /// <remarks>
   /// If the caller is one of this scheduler's workers and is not already
   /// inside a blocking region, its concurrency slot is handed back and, if
   /// there is queued work, a compensation worker is started right away.
   /// </remarks>
   /// <returns>
   /// True if this very call released a concurrency slot. Pass the value
   /// verbatim to <see cref="EndBlocking"/> from a finally block.
   /// </returns>
   public bool BeginBlocking()
   {
       /*
        * Not one of our workers? Then there is no slot to give up. Note that we
        * deliberately do not track a depth in that case either - EndBlocking(false)
        * is a plain no-op.
        */
       if (_currentThreadScheduler != this)
       {
           return false;
       }

       /*
        * Already blocking? Then our slot is released already. Still count the
        * nesting level so that the matching EndBlocking calls pair up.
        */
       if (_currentThreadBlockingDepth > 0)
       {
           ++_currentThreadBlockingDepth;
           return false;
       }

       ++_currentThreadBlockingDepth;

       lock (_tasks)
       {
           /*
            * Give our slot back...
            */
           --_delegatesQueuedOrRunning;

           /*
            * ... and, if somebody is waiting for it, immediately spend it on a
            * compensation worker. Note that NotifyThreadPoolOfPendingWork
            * requires the slot to be accounted for before it is called.
            */
           if (_tasks.Count > 0 && _delegatesQueuedOrRunning < _maxDegreeOfParallelism)
           {
               ++_delegatesQueuedOrRunning;
               NotifyThreadPoolOfPendingWork();
           }
       }

       return true;
   }


   /// <summary>
   /// Announce that the blocking wait started by <see cref="BeginBlocking"/> is over.
   /// </summary>
   /// <param name="releasedSlot">The exact value returned by the matching BeginBlocking call.</param>
   public void EndBlocking(bool releasedSlot)
   {
       /*
        * Only our own workers can have released one of our slots. Taking a slot
        * back on behalf of a thread that is not running a worker loop would
        * consume capacity that nobody ever gives back, which eventually stalls
        * the scheduler for good - so ignore such calls entirely.
        */
       if (_currentThreadScheduler != this)
       {
           return;
       }

       if (_currentThreadBlockingDepth > 0)
       {
           --_currentThreadBlockingDepth;
       }

       if (!releasedSlot)
       {
           return;
       }

       lock (_tasks)
       {
           /*
            * Take a slot back. This may push us above _maxDegreeOfParallelism
            * for a while; the worker loop sheds the surplus on its next round.
            */
           ++_delegatesQueuedOrRunning;
       }
   }


   // Inform the ThreadPool that there's work to be executed for this scheduler.
   private void NotifyThreadPoolOfPendingWork()
   {
       ThreadPool.UnsafeQueueUserWorkItem(_ =>
       {
           // Note that the current thread is now processing work items of this
           // scheduler. This is necessary to enable inlining of tasks into this
           // thread, and it identifies us as the owner of a concurrency slot.
           _currentThreadScheduler = this;

           // The thread comes from the pool and may have been left in a blocking
           // region by buggy code in a previous life. Start from a clean slate.
           _currentThreadBlockingDepth = 0;

           // Whether we already handed our concurrency slot back.
           bool slotReturned = false;

           try
           {
               // Process all available items in the queue.
               while (true)
               {
                   Task item;
                   lock (_tasks)
                   {
                       // If EndBlocking pushed us above the configured maximum,
                       // shed this worker again. This is safe: after decrementing
                       // there still are at least _maxDegreeOfParallelism (>= 1)
                       // other workers around, so the queue keeps being drained.
                       if (_delegatesQueuedOrRunning > _maxDegreeOfParallelism)
                       {
                           --_delegatesQueuedOrRunning;
                           slotReturned = true;
                           break;
                       }

                       // When there are no more items to be processed,
                       // note that we're done processing, and get out.
                       if (_tasks.Count == 0)
                       {
                           --_delegatesQueuedOrRunning;
                           slotReturned = true;
                           break;
                       }

                       // Get the next item from the queue
                       item = _tasks.First.Value;
                       _tasks.RemoveFirst();
                   }

                   // Execute the task we pulled out of the queue
                   base.TryExecuteTask(item);
               }
           }
           finally
           {
               /*
                * Abnormal exit - TryExecuteTask threw, or the thread is being
                * torn down. Our slot would be lost forever, and once that has
                * happened _maxDegreeOfParallelism times the scheduler would
                * never run anything again. So return the slot here, and pass it
                * straight on to a fresh worker if work is still queued.
                *
                * If we are still inside a blocking region (a BeginBlocking
                * without its EndBlocking, e.g. because the task threw out of
                * one), our slot has already been handed back by BeginBlocking
                * and must not be counted down twice.
                */
               if (!slotReturned && 0 == _currentThreadBlockingDepth)
               {
                   lock (_tasks)
                   {
                       --_delegatesQueuedOrRunning;
                       if (_tasks.Count > 0 && _delegatesQueuedOrRunning < _maxDegreeOfParallelism)
                       {
                           ++_delegatesQueuedOrRunning;
                           NotifyThreadPoolOfPendingWork();
                       }
                   }
               }

               // We're done processing items on the current thread.
               _currentThreadScheduler = null;
               _currentThreadBlockingDepth = 0;
           }
       }, null);
   }

   // Attempts to execute the specified task on the current thread.
   protected sealed override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
   {
       // If this thread isn't already processing a task of ours, we don't support inlining
       if (_currentThreadScheduler != this) return false;

       // If the task was previously queued, remove it from the queue
       if (taskWasPreviouslyQueued)
          // Try to run the task.
          if (TryDequeue(task))
            return base.TryExecuteTask(task);
          else
             return false;
       else
          return base.TryExecuteTask(task);
   }

   // Attempt to remove a previously scheduled task from the scheduler.
   protected sealed override bool TryDequeue(Task task)
   {
       lock (_tasks) return _tasks.Remove(task);
   }

   // Gets the maximum concurrency level supported by this scheduler.
   public sealed override int MaximumConcurrencyLevel { get { return _maxDegreeOfParallelism; } }

   // Gets an enumerable of the tasks currently scheduled on this scheduler.
   protected sealed override IEnumerable<Task> GetScheduledTasks()
   {
       bool lockTaken = false;
       try
       {
           Monitor.TryEnter(_tasks, ref lockTaken);
           if (lockTaken) return _tasks;
           else throw new NotSupportedException();
       }
       finally
       {
           if (lockTaken) Monitor.Exit(_tasks);
       }
   }
}
