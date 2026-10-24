using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using engine.net;
using Xunit;

namespace JoyceCode.Tests.engine.net;

/// <summary>
/// KAR-469: the game's HTTP connections race IPv6 against IPv4, so an IPv6
/// address that never answers (silicondesert.io on 2026-10-24) cannot time
/// the login out.
/// </summary>
public class HappyEyeballsTests
{
    private static readonly IPAddress V6a = IPAddress.Parse("2a01:4f8:c0c:ac65::1");
    private static readonly IPAddress V6b = IPAddress.Parse("2a01:4f8:c0c:ac65::2");
    private static readonly IPAddress V4a = IPAddress.Parse("91.98.142.40");
    private static readonly IPAddress V4b = IPAddress.Parse("91.98.142.41");

    private sealed class Conn : IDisposable
    {
        public Conn(IPAddress ip) => Address = ip;
        public IPAddress Address { get; }
        public bool Disposed { get; private set; }
        public void Dispose() => Disposed = true;
    }


    /* A fake network: per address, how long a connect takes, or that it hangs or is refused. */
    private sealed class FakeNet
    {
        public readonly Dictionary<IPAddress, TimeSpan?> Latency = new();     // null = hangs
        public readonly HashSet<IPAddress> Refused = new();
        public readonly ConcurrentQueue<IPAddress> Started = new();
        public readonly ConcurrentQueue<Conn> Made = new();

        public async Task<Conn> Connect(IPAddress ip, CancellationToken ct)
        {
            Started.Enqueue(ip);
            if (Refused.Contains(ip)) throw new SocketException((int)SocketError.ConnectionRefused);
            var latency = Latency.GetValueOrDefault(ip, TimeSpan.Zero);
            if (latency == null) await Task.Delay(Timeout.Infinite, ct);
            else if (latency > TimeSpan.Zero) await Task.Delay(latency.Value, CancellationToken.None);
            var c = new Conn(ip);
            Made.Enqueue(c);
            return c;
        }
    }


    private static Task<Conn> Race(FakeNet net, params IPAddress[] addresses) =>
        HappyEyeballs.ConnectAsync(HappyEyeballs.Order(addresses), net.Connect, HappyEyeballs.FallbackDelay,
            CancellationToken.None);


    [Fact]
    public void Order_AlternatesFamilies_StartingWithTheFirst()
    {
        Assert.Equal(new[] { V6a, V4a, V6b, V4b }, HappyEyeballs.Order(new[] { V6a, V6b, V4a, V4b }));
        Assert.Equal(new[] { V4a, V6a, V4b }, HappyEyeballs.Order(new[] { V4a, V4b, V6a }));
        Assert.Equal(new[] { V4a, V4b }, HappyEyeballs.Order(new[] { V4a, V4b }));
        Assert.Empty(HappyEyeballs.Order(null));
    }


    [Fact]
    public async Task AHangingIpv6_LosesToIpv4_WellWithinASecond()
    {
        var net = new FakeNet();
        net.Latency[V6a] = null;          // published, never answers

        var sw = Stopwatch.StartNew();
        var conn = await Race(net, V6a, V4a);

        Assert.Equal(V4a, conn.Address);
        Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"took {sw.Elapsed}");
        Assert.True(sw.Elapsed >= HappyEyeballs.FallbackDelay - TimeSpan.FromMilliseconds(20), $"took {sw.Elapsed}");
    }


    [Fact]
    public async Task AWorkingIpv6_Wins_AndIpv4IsNeverTried()
    {
        var net = new FakeNet();
        var conn = await Race(net, V6a, V4a);

        Assert.Equal(V6a, conn.Address);
        Assert.Equal(new[] { V6a }, net.Started);
    }


    [Fact]
    public async Task ARefusedIpv6_StartsIpv4AtOnce()
    {
        var net = new FakeNet();
        net.Refused.Add(V6a);

        var sw = Stopwatch.StartNew();
        var conn = await Race(net, V6a, V4a);

        Assert.Equal(V4a, conn.Address);
        Assert.True(sw.Elapsed < HappyEyeballs.FallbackDelay, $"waited {sw.Elapsed}");
    }


    [Fact]
    public async Task SingleFamilyHosts_Work()
    {
        Assert.Equal(V4a, (await Race(new FakeNet(), V4a)).Address);
        Assert.Equal(V6a, (await Race(new FakeNet(), V6a)).Address);
    }


    [Fact]
    public async Task AllFailing_ThrowsTheLastError()
    {
        var net = new FakeNet();
        net.Refused.Add(V6a);
        net.Refused.Add(V4a);

        var e = await Assert.ThrowsAsync<SocketException>(() => Race(net, V6a, V4a));
        Assert.Equal(SocketError.ConnectionRefused, e.SocketErrorCode);
        await Assert.ThrowsAsync<SocketException>(() => Race(net));
    }


    [Fact]
    public async Task ALateLoser_IsDisposed()
    {
        var net = new FakeNet();
        net.Latency[V6a] = TimeSpan.FromMilliseconds(600);    // slow, but it does connect
        net.Latency[V4a] = TimeSpan.FromMilliseconds(10);

        var winner = await Race(net, V6a, V4a);
        Assert.Equal(V4a, winner.Address);

        await Task.Delay(900);
        var loser = net.Made.Single(c => c.Address.Equals(V6a));
        Assert.True(loser.Disposed);
        Assert.False(winner.Disposed);
    }


    [Fact]
    public async Task CallerCancellation_Stops()
    {
        var net = new FakeNet();
        net.Latency[V6a] = null;
        net.Latency[V4a] = null;
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            HappyEyeballs.ConnectAsync(HappyEyeballs.Order(new[] { V6a, V4a }), net.Connect,
                HappyEyeballs.FallbackDelay, cts.Token));
    }


    /*
     * Real sockets: an IPv6 address that goes nowhere (2001:db8::/32 is
     * reserved for documentation; it hangs or is unreachable, depending on
     * the host's routes) against a live IPv4 listener.
     */
    [Fact]
    public async Task RealSockets_ADeadIpv6LosesToALiveIpv4()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        try
        {
            async Task<Socket> connect(IPAddress ip, CancellationToken ct)
            {
                var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try { await socket.ConnectAsync(new IPEndPoint(ip, port), ct); return socket; }
                catch { socket.Dispose(); throw; }
            }

            var sw = Stopwatch.StartNew();
            using var socket = await HappyEyeballs.ConnectAsync(
                HappyEyeballs.Order(new[] { IPAddress.Parse("2001:db8::1"), IPAddress.Loopback }),
                connect, HappyEyeballs.FallbackDelay, CancellationToken.None);

            Assert.Equal(AddressFamily.InterNetwork, socket.AddressFamily);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(1), $"took {sw.Elapsed}");
        }
        finally
        {
            listener.Stop();
        }
    }


    /*
     * End to end through HttpClient: a server listening on IPv4 loopback only,
     * reached as "localhost" (resolves to ::1 and 127.0.0.1 on most hosts).
     */
    [Fact]
    public async Task HttpClient_ReachesAnIpv4OnlyServerByName()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var serve = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync();
            var stream = client.GetStream();
            var buffer = new byte[4096];
            await stream.ReadAsync(buffer);
            var body = "{\"ok\":true}";
            var response = $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n{body}";
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
        });

        try
        {
            using var http = new HttpClient(HappyEyeballs.CreateHandler()) { Timeout = TimeSpan.FromSeconds(5) };
            var sw = Stopwatch.StartNew();
            var text = await http.GetStringAsync($"http://localhost:{port}/");
            Assert.Equal("{\"ok\":true}", text);
            Assert.True(sw.Elapsed < TimeSpan.FromSeconds(2), $"took {sw.Elapsed}");
            await serve;
        }
        finally
        {
            listener.Stop();
        }
    }
}
