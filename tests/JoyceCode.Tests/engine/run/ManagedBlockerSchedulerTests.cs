using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using engine;
using Xunit;

/*
 * These tests are about synchronous blocking inside a capped task scheduler, so
 * they necessarily use blocking task operations. Every one of them is bounded by
 * a timeout that fails the test rather than hanging the run.
 */
#pragma warning disable xUnit1031

namespace JoyceCode.Tests.engine.run;

/// <summary>
/// Managed blocker protocol of LimitedConcurrencyLevelTaskScheduler.
///
/// The scheduler caps the number of concurrently running workers. A task that
/// blocks synchronously keeps holding its slot, so if every slot is held by a
/// task waiting for work that is still queued, the scheduler deadlocks.
/// BeginBlocking/EndBlocking hand the slot back for the duration of the wait
/// (starting a compensation worker when there is queued work) and take it back
/// afterwards; the worker loop sheds the resulting oversubscription again.
///
/// All waits here have a generous timeout and assert on it, so a regression
/// fails the test instead of hanging the test run.
/// </summary>
public class ManagedBlockerSchedulerTests
{
    private static readonly TimeSpan Generous = TimeSpan.FromSeconds(10);

    private static TaskFactory FactoryFor(LimitedConcurrencyLevelTaskScheduler sched)
        => new TaskFactory(CancellationToken.None,
            TaskCreationOptions.DenyChildAttach,
            TaskContinuationOptions.None,
            sched);


    /// <summary>
    /// Compensation workers are ThreadPool threads, and the pool only injects
    /// roughly one extra thread per 500ms beyond its minimum. On a core-poor
    /// machine that alone can blow the timeouts of the blocking tests, so make
    /// sure the pool can hand out the threads right away.
    /// </summary>
    private static void EnsureThreadPoolThreads(int nNeeded)
    {
        ThreadPool.GetMinThreads(out int nWorkers, out int nCompletionPorts);
        if (nWorkers < nNeeded)
        {
            ThreadPool.SetMinThreads(nNeeded, nCompletionPorts);
        }
    }


    /// <summary>
    /// Waits for a condition instead of sleeping a fixed amount. Returns false
    /// on timeout so the caller can fail the test rather than hang.
    /// </summary>
    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var until = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > until)
            {
                return false;
            }

            Thread.Sleep(1);
        }

        return true;
    }


    /// <summary>
    /// The canonical starvation deadlock: with a single concurrency slot, task A
    /// blocks on something only task B can deliver, and B is queued behind A.
    /// Without the managed blocker this never completes.
    /// </summary>
    [Fact]
    public void BlockingTaskDoesNotStarveQueuedWaker()
    {
        var sched = new LimitedConcurrencyLevelTaskScheduler(1);
        var tf = FactoryFor(sched);

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var aDone = new ManualResetEventSlim(false);
        var bDone = new ManualResetEventSlim(false);
        bool aSawSignal = false;

        Task taskA = tf.StartNew(() =>
        {
            bool released = sched.BeginBlocking();
            try
            {
                aSawSignal = tcs.Task.Wait(Generous);
            }
            finally
            {
                sched.EndBlocking(released);
            }

            aDone.Set();
        });

        Task taskB = tf.StartNew(() =>
        {
            tcs.TrySetResult(true);
            bDone.Set();
        });

        Assert.True(bDone.Wait(Generous), "task B never ran - the scheduler starved");
        Assert.True(aDone.Wait(Generous), "task A never woke up");
        Assert.True(aSawSignal, "task A did not observe the signal from task B");
        Assert.True(Task.WaitAll(new[] { taskA, taskB }, Generous));
    }


    /// <summary>
    /// After the blocking round trip the scheduler must still work normally,
    /// i.e. the slot bookkeeping must have come out even.
    /// </summary>
    [Fact]
    public void SchedulerKeepsWorkingAfterBlockingRoundTrip()
    {
        var sched = new LimitedConcurrencyLevelTaskScheduler(1);
        var tf = FactoryFor(sched);

        var gate = new SemaphoreSlim(0);
        Task blocker = tf.StartNew(() =>
        {
            bool released = sched.BeginBlocking();
            try
            {
                Assert.True(gate.Wait(Generous));
            }
            finally
            {
                sched.EndBlocking(released);
            }
        });

        Task opener = tf.StartNew(() => gate.Release());

        Assert.True(Task.WaitAll(new[] { blocker, opener }, Generous));

        var later = new ManualResetEventSlim(false);
        Task after = tf.StartNew(() => later.Set());
        Assert.True(later.Wait(Generous), "scheduler stopped executing tasks after a blocking round trip");
        Assert.True(after.Wait(Generous));
    }


    /// <summary>
    /// Off the scheduler's own workers there is no slot to give up, so both
    /// calls are no-ops.
    /// </summary>
    [Fact]
    public void BeginBlockingIsNoOpOnForeignThreads()
    {
        var sched = new LimitedConcurrencyLevelTaskScheduler(2);

        // Directly on the test thread.
        Assert.False(sched.BeginBlocking());
        sched.EndBlocking(false);

        // And on a plain, dedicated thread.
        bool result = true;
        Exception caught = null;
        var t = new Thread(() =>
        {
            try
            {
                result = sched.BeginBlocking();
                sched.EndBlocking(result);
            }
            catch (Exception e)
            {
                caught = e;
            }
        });
        t.Start();
        Assert.True(t.Join(Generous));
        Assert.Null(caught);
        Assert.False(result);

        // The scheduler must be unharmed by that.
        var tf = FactoryFor(sched);
        var ran = new ManualResetEventSlim(false);
        Task task = tf.StartNew(() => ran.Set());
        Assert.True(ran.Wait(Generous), "scheduler stopped working after a no-op BeginBlocking");
        Assert.True(task.Wait(Generous));
    }


    /// <summary>
    /// Worker identity is per scheduler instance: a worker of one scheduler
    /// holds no slot of another one, so calling into the other one's blocking
    /// protocol must not touch its accounting.
    /// </summary>
    [Fact]
    public void BlockingProtocolIsScopedToTheOwningScheduler()
    {
        var schedA = new LimitedConcurrencyLevelTaskScheduler(1);
        var schedB = new LimitedConcurrencyLevelTaskScheduler(1);
        var tfA = FactoryFor(schedA);

        bool releasedB = true;
        var done = new ManualResetEventSlim(false);

        Task onA = tfA.StartNew(() =>
        {
            releasedB = schedB.BeginBlocking();
            schedB.EndBlocking(releasedB);
            done.Set();
        });

        Assert.True(done.Wait(Generous));
        Assert.True(onA.Wait(Generous));
        Assert.False(releasedB, "a worker of another scheduler must not be able to release our slot");
        Assert.Equal(0, schedB.DelegatesQueuedOrRunning);

        // Both schedulers must still be operational.
        var ranB = new ManualResetEventSlim(false);
        Task onB = FactoryFor(schedB).StartNew(() => ranB.Set());
        Assert.True(ranB.Wait(Generous), "the foreign scheduler was corrupted");
        Assert.True(onB.Wait(Generous));

        var ranA = new ManualResetEventSlim(false);
        Task moreOnA = tfA.StartNew(() => ranA.Set());
        Assert.True(ranA.Wait(Generous));
        Assert.True(moreOnA.Wait(Generous));
    }


    /// <summary>
    /// A stray EndBlocking(true) from a thread that never held a slot must not
    /// invent one out of thin air: the invented slot would never be given back
    /// and, at a low cap, would stop the scheduler from ever starting a worker
    /// again.
    /// </summary>
    [Fact]
    public void ForeignEndBlockingDoesNotInventASlot()
    {
        var sched = new LimitedConcurrencyLevelTaskScheduler(1);

        // Misuse it from the test thread, which is not one of our workers.
        sched.EndBlocking(true);

        // And from a plain, dedicated thread.
        var t = new Thread(() => sched.EndBlocking(true));
        t.Start();
        Assert.True(t.Join(Generous));

        Assert.Equal(0, sched.DelegatesQueuedOrRunning);

        var tf = FactoryFor(sched);
        var ran = new ManualResetEventSlim(false);
        Task task = tf.StartNew(() => ran.Set());
        Assert.True(ran.Wait(Generous), "scheduler was stalled by a stray EndBlocking");
        Assert.True(task.Wait(Generous));
    }


    /// <summary>
    /// Oversubscription must not become permanent: a worker coming back from a
    /// blocking region is allowed to finish the task it was in - that is the
    /// deliberate transient overshoot - but it must not go on to pull further
    /// work while the scheduler is above its cap.
    ///
    /// So we only measure the concurrency of the queued batch: the blockers are
    /// woken while that batch is being worked off, and if the surplus workers
    /// did not shed themselves they would join in and push the batch beyond the
    /// cap. Afterwards the worker count has to fall back to zero.
    /// </summary>
    [Fact]
    public void ConcurrencyCapIsHonouredWhileOversubscribed()
    {
        const int nMax = 2;
        const int nTasks = 200;

        EnsureThreadPoolThreads(16);

        var sched = new LimitedConcurrencyLevelTaskScheduler(nMax);
        var tf = FactoryFor(sched);

        int nRunning = 0;
        int nPeak = 0;

        void EnterMeasured()
        {
            int nNow = Interlocked.Increment(ref nRunning);
            int nSeen = Volatile.Read(ref nPeak);
            while (nNow > nSeen)
            {
                int nWas = Interlocked.CompareExchange(ref nPeak, nNow, nSeen);
                if (nWas == nSeen) break;
                nSeen = nWas;
            }
        }

        /*
         * Occupy every slot with a task that blocks - and thus hands its slot
         * back - so that the measuring tasks below get workers of their own.
         */
        var gate = new SemaphoreSlim(0);
        var allBlocked = new CountdownEvent(nMax);
        var tasks = new List<Task>();

        for (int i = 0; i < nMax; ++i)
        {
            tasks.Add(tf.StartNew(() =>
            {
                bool released = sched.BeginBlocking();
                try
                {
                    allBlocked.Signal();
                    Assert.True(gate.Wait(Generous));
                }
                finally
                {
                    sched.EndBlocking(released);
                }

                /*
                 * Back from the wait, and the scheduler is now oversubscribed.
                 * Finishing this task over the cap is by design, so it is not
                 * measured - but afterwards this worker must shed itself
                 * instead of taking more of the queued batch.
                 */
                Thread.SpinWait(2000);
            }));
        }

        Assert.True(allBlocked.Wait(Generous), "the blocking tasks never got to run");

        /*
         * Now hand the scheduler a long queue of work and let it get going...
         */
        var allMeasured = new CountdownEvent(nTasks);
        var firstMeasured = new ManualResetEventSlim(false);
        for (int i = 0; i < nTasks; ++i)
        {
            tasks.Add(tf.StartNew(() =>
            {
                EnterMeasured();
                firstMeasured.Set();
                Thread.SpinWait(2000);
                Interlocked.Decrement(ref nRunning);
                allMeasured.Signal();
            }));
        }

        Assert.True(firstMeasured.Wait(Generous), "the queued tasks never started");

        /*
         * ... and only then wake the blockers, so that they come back to a
         * scheduler that is already busy at its cap.
         */
        gate.Release(nMax);

        Assert.True(allMeasured.Wait(Generous));
        Assert.True(Task.WaitAll(tasks.ToArray(), Generous));
        Assert.True(nPeak <= nMax, $"observed {nPeak} tasks running concurrently, cap is {nMax}");

        /*
         * And the oversubscription must have decayed completely.
         */
        Assert.True(WaitUntil(() => 0 == sched.DelegatesQueuedOrRunning, Generous),
            $"worker count did not fall back to zero ({sched.DelegatesQueuedOrRunning} left)");
    }


    /// <summary>
    /// Nested blocking regions on one worker release the slot exactly once:
    /// the outer call reports the release, the inner one does not.
    /// </summary>
    [Fact]
    public void NestedBlockingReleasesTheSlotOnlyOnce()
    {
        var sched = new LimitedConcurrencyLevelTaskScheduler(1);
        var tf = FactoryFor(sched);

        bool outer = false;
        bool inner = false;
        var done = new ManualResetEventSlim(false);
        Exception caught = null;

        Task task = tf.StartNew(() =>
        {
            try
            {
                outer = sched.BeginBlocking();
                try
                {
                    inner = sched.BeginBlocking();
                    try
                    {
                        Thread.Yield();
                    }
                    finally
                    {
                        sched.EndBlocking(inner);
                    }
                }
                finally
                {
                    sched.EndBlocking(outer);
                }
            }
            catch (Exception e)
            {
                caught = e;
            }

            done.Set();
        });

        Assert.True(done.Wait(Generous));
        Assert.Null(caught);
        Assert.True(outer, "the outer BeginBlocking should have released the slot");
        Assert.False(inner, "the nested BeginBlocking must not release a second slot");
        Assert.True(task.Wait(Generous));

        /*
         * The bookkeeping must be even again: further tasks still get executed.
         */
        var laterRan = new CountdownEvent(3);
        var later = new List<Task>();
        for (int i = 0; i < 3; ++i)
        {
            later.Add(tf.StartNew(() => laterRan.Signal()));
        }

        Assert.True(laterRan.Wait(Generous), "scheduler stopped executing tasks after nested blocking");
        Assert.True(Task.WaitAll(later.ToArray(), Generous));
    }


    /// <summary>
    /// Many more blocking tasks than slots, all waiting on one gate that is
    /// only opened once every one of them is inside its blocking region. This
    /// can only complete if the slots are actually handed back, and it also
    /// exercises the shedding of the resulting oversubscription.
    /// </summary>
    [Fact]
    public void ManyBlockedTasksAllCompleteUnderALowCap()
    {
        const int nTasks = 8;

        EnsureThreadPoolThreads(nTasks + 8);

        var sched = new LimitedConcurrencyLevelTaskScheduler(2);
        var tf = FactoryFor(sched);

        var gate = new ManualResetEventSlim(false);
        var allBlocked = new CountdownEvent(nTasks);
        var allDone = new CountdownEvent(nTasks);
        int nWaitsTimedOut = 0;

        var tasks = new List<Task>();
        for (int i = 0; i < nTasks; ++i)
        {
            tasks.Add(tf.StartNew(() =>
            {
                bool released = sched.BeginBlocking();
                try
                {
                    allBlocked.Signal();
                    if (!gate.Wait(Generous))
                    {
                        Interlocked.Increment(ref nWaitsTimedOut);
                    }
                }
                finally
                {
                    sched.EndBlocking(released);
                }

                allDone.Signal();
            }));
        }

        Assert.True(allBlocked.Wait(Generous),
            "not every task got to run - blocked workers did not release their slots");
        gate.Set();
        Assert.True(allDone.Wait(Generous));
        Assert.True(Task.WaitAll(tasks.ToArray(), Generous));
        Assert.Equal(0, nWaitsTimedOut);

        /*
         * Oversubscription must decay: the scheduler is still usable afterwards.
         */
        var epilogue = new CountdownEvent(nTasks);
        var epilogueTasks = new List<Task>();
        for (int i = 0; i < nTasks; ++i)
        {
            epilogueTasks.Add(tf.StartNew(() => epilogue.Signal()));
        }

        Assert.True(epilogue.Wait(Generous));
        Assert.True(Task.WaitAll(epilogueTasks.ToArray(), Generous));
    }
}
