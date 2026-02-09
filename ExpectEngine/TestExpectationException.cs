namespace ExpectEngine;

public class TestExpectationException : Exception
{
    public TestExpectationException(string message) : base(message) { }
}

public class TestTimeoutException : TestExpectationException
{
    public TestTimeoutException(string message) : base(message) { }
}
