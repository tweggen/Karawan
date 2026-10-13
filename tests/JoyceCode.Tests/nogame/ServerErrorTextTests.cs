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
    public void KeepsTheFullMessageOnOneLine()
    {
        string body = "{\"error\": \"" + new string('x', 200) + "\\nsecond line\"}";
        string text = ServerErrorText.Describe(HttpStatusCode.BadRequest, "Bad Request", body);
        Assert.Equal(new string('x', 200) + " second line (400)", text);
    }


    [Fact]
    public void RejectsLongPlainTextAsAPage()
    {
        string body = new string('x', ServerErrorText.MaxPlainTextLength + 1);
        Assert.Equal("server answered 502 Bad Gateway",
            ServerErrorText.Describe(HttpStatusCode.BadGateway, "Bad Gateway", body));
    }


    [Fact]
    public void StatusLineClipsOnceToTheLoginScreen()
    {
        string text = ServerErrorText.StatusLine("login failed: " + new string('x', 200) + "\nsecond");
        Assert.Equal(ServerErrorText.StatusLineLength, text.Length);
        Assert.StartsWith("login failed: xxx", text);
        Assert.EndsWith("...", text);
        Assert.DoesNotContain("\n", text);
    }


    /**
     * Every line exli causes fits the status line unclipped: the length
     * was set from the longest of them.
     */
    [Theory]
    [InlineData("login failed: I'm sorry, I do not recognize you. (401)")]
    [InlineData("access refused: I'm sorry, I don't remember you. Please log in again.")]
    [InlineData("server rejected the request: gameTitle is required (400)")]
    [InlineData("online save: could not store the save game (500)")]
    [InlineData("online save: 404 page not found (404)")]
    public void ExliLinesFitTheStatusLine(string line)
    {
        Assert.Equal(line, ServerErrorText.StatusLine(line));
    }


    /*
     * The exact bodies exli sends, before (plain text) and after KAR-423 (JSON).
     */
    private const string _oldWrongPassword = "I'm sorry, I do not recognize you.";
    private const string _newWrongPassword = "{\"error\":\"I'm sorry, I do not recognize you.\"}";
    private const string _oldForgotten = "I'm sorry, I don't remember you. Would you mind to login again, obtaining a fresh token?";
    private const string _newForgotten = "{\"error\":\"I'm sorry, I don't remember you. Please log in again.\"}";
    private const string _oldNoSave = "Hi player@local.test, I'm afraid you didn't save any game yet.";
    private const string _newNoSave = "{\"error\":\"no saved game yet\"}";
    private const string _traefikNotFound = "404 page not found\n";


    [Fact]
    public void WrongPasswordFromTheOldServerDoesNotReadLikeSuccess()
    {
        Assert.Equal("login failed: I'm sorry, I do not recognize you.",
            ServerErrorText.DescribeLogin(HttpStatusCode.OK, "OK", _oldWrongPassword));
    }


    [Fact]
    public void WrongPasswordFromTheNewServerIsALoginFailure()
    {
        Assert.Equal("login failed: I'm sorry, I do not recognize you. (401)",
            ServerErrorText.DescribeLogin(HttpStatusCode.Unauthorized, "Unauthorized", _newWrongPassword));
    }


    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":\"expected user.email and user.password\"}",
        "server rejected the request: expected user.email and user.password (400)")]
    [InlineData(HttpStatusCode.NotFound, _traefikNotFound,
        "server rejected the request: 404 page not found (404)")]
    public void OtherClientErrorsAreNoLoginFailure(HttpStatusCode status, string body, string expected)
    {
        Assert.Equal(expected, ServerErrorText.DescribeLogin(status, null, body));
    }


    [Theory]
    [InlineData(_oldForgotten, "I'm sorry, I don't remember you. Would you mind to login again, obtaining a fresh token? (403)")]
    [InlineData(_newForgotten, "I'm sorry, I don't remember you. Please log in again. (403)")]
    public void QuotesAForgottenToken(string body, string expected)
    {
        Assert.Equal(expected, ServerErrorText.Describe(HttpStatusCode.Forbidden, "Forbidden", body));
    }


    [Theory]
    [InlineData(_oldNoSave)]
    [InlineData(_newNoSave)]
    public void ExlisNotFoundMeansNoSaveYet(string body)
    {
        Assert.True(ServerErrorText.IsNoSaveYet(HttpStatusCode.NotFound, body));
    }


    [Theory]
    [InlineData(HttpStatusCode.NotFound, _traefikNotFound)]
    [InlineData(HttpStatusCode.NotFound, "{\"errors\":{\"detail\":\"Not Found\"}}")]
    [InlineData(HttpStatusCode.NotFound, "")]
    [InlineData(HttpStatusCode.NotFound, null)]
    [InlineData(HttpStatusCode.OK, _newNoSave)]
    [InlineData(HttpStatusCode.BadGateway, _oldNoSave)]
    public void AnyOtherAnswerIsNoNoSave(HttpStatusCode status, string body)
    {
        Assert.False(ServerErrorText.IsNoSaveYet(status, body));
    }


    [Fact]
    public void AProxysNotFoundIsReportedWithItsText()
    {
        Assert.Equal("404 page not found (404)",
            ServerErrorText.Describe(HttpStatusCode.NotFound, "Not Found", _traefikNotFound));
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
