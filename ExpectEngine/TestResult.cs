namespace ExpectEngine;

public enum TestOutcome
{
    Pass,
    Fail,
    Timeout,
    Error
}

public sealed class TestResult
{
    public TestOutcome Outcome { get; }
    public string Message { get; }
    public int StepIndex { get; }
    public TimeSpan Elapsed { get; }
    public List<string> Log { get; }

    public TestResult(TestOutcome outcome, string message, int stepIndex,
                      TimeSpan elapsed, List<string> log)
    {
        Outcome = outcome;
        Message = message;
        StepIndex = stepIndex;
        Elapsed = elapsed;
        Log = log ?? new List<string>();
    }

    public int ExitCode => Outcome == TestOutcome.Pass ? 0 : 1;
}
