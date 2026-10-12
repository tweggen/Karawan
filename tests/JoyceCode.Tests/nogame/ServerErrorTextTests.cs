using System;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using nogame.modules;
using Xunit;

namespace JoyceCode.Tests.nogame;


/**
 * The login screen cites the game server's own error message when a login
 * fails. The server's body shape is not pinned, so these cover the shapes
 * ServerErrorText accepts and its fallbacks.
 */
public class ServerErrorTextTests
{
    [Theory]
    [InlineData("{\"error\": \"Invalid email or password\"}", "Invalid email or password (401)")]
    [InlineData("{\"message\": \"Invalid email or password\"}", "Invalid email or password (401)")]
    [InlineData("{\"errors\": {\"detail\": \"Unauthorized\"}}", "Unauthorized (401)")]
    [InlineData("{\"errors\": {\"email\": [\"is invalid\"]}}", "email is invalid (401)")]
    [InlineData("{\"errors\": [\"account locked\"]}", "account locked (401)")]
    [InlineData("Invalid credentials", "Invalid credentials (401)")]
    public void CitesTheServerMessage(string body, string expected)
    {
        Assert.Equal(expected, ServerErrorText.Describe(HttpStatusCode.Unauthorized, "Unauthorized", body));
    }


    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{\"token\": \"\"}")]
    [InlineData("<html><body>Bad Gateway</body></html>")]
    public void FallsBackToTheStatusWithoutAMessage(string body)
    {
        Assert.Equal("server answered 502 Bad Gateway",
            ServerErrorText.Describe(HttpStatusCode.BadGateway, "Bad Gateway", body));
    }


    [Fact]
    public void ClipsLongMessagesToOneLine()
    {
        string body = "{\"error\": \"" + new string('x', 200) + "\\nsecond line\"}";
        string text = ServerErrorText.Describe(HttpStatusCode.BadRequest, "Bad Request", body);
        Assert.Equal(ServerErrorText.MaxLength, text.Length);
        Assert.EndsWith("...", text);
        Assert.DoesNotContain("\n", text);
    }


    [Fact]
    public void NamesTimeoutsAndUnreachableServers()
    {
        Assert.Equal("the server did not answer in time",
            ServerErrorText.Describe(new TaskCanceledException()));
        Assert.StartsWith("server not reachable: ",
            ServerErrorText.Describe(new HttpRequestException("Connection refused")));
        Assert.StartsWith("server not reachable: ",
            ServerErrorText.Describe(new AggregateException(new HttpRequestException("Connection refused"))));
    }
}
