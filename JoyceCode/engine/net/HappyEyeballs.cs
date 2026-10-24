using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using static engine.Logger;

namespace engine.net;

/// <summary>
/// "Happy Eyeballs" connection racing (RFC 8305) for the game's HttpClient
/// (KAR-469). A bare HttpClient on desktop tries a host's addresses one
/// after the other, IPv6 first, and waits for each connect: when a host
/// publishes an IPv6 address that does not answer, the login times out
/// before IPv4 is ever tried — while a browser, which races the families,
/// works. Here the first address is tried, and if it has not connected
/// within <see cref="FallbackDelay"/> the next one starts in parallel
/// (alternating families); the first connection wins, the rest are
/// cancelled.
/// </summary>
public static class HappyEyeballs
{
    private static readonly engine.Dc _dc = engine.Dc.Engine;

    /// <summary>How long one attempt gets alone before the next starts beside it (RFC 8305 suggests 250 ms).</summary>
    public static readonly TimeSpan FallbackDelay = TimeSpan.FromMilliseconds(250);


    /// <summary>
    /// The order to try addresses in: the families alternate, starting with
    /// the family of the first address (DNS normally lists IPv6 first); within
    /// a family the given order is kept.
    /// </summary>
    public static List<IPAddress> Order(IEnumerable<IPAddress> addresses)
    {
        var result = new List<IPAddress>();
        if (addresses == null) return result;

        var first = new Queue<IPAddress>();
        var second = new Queue<IPAddress>();
        AddressFamily? leading = null;
        foreach (var a in addresses)
        {
            if (a == null) continue;
            leading ??= a.AddressFamily;
            (a.AddressFamily == leading ? first : second).Enqueue(a);
        }

        while (first.Count > 0 || second.Count > 0)
        {
            if (first.Count > 0) result.Add(first.Dequeue());
            if (second.Count > 0) result.Add(second.Dequeue());
        }
        return result;
    }


    /// <summary>
    /// Race <paramref name="connect"/> over <paramref name="ordered"/>: start
    /// the first; start the next when the running ones have not connected
    /// within <paramref name="delay"/>, or at once when one fails. The first
    /// success is returned; the other attempts are cancelled, and any that
    /// still succeed are disposed. Throws the last failure when all fail.
    /// </summary>
    public static async Task<T> ConnectAsync<T>(
        IReadOnlyList<IPAddress> ordered,
        Func<IPAddress, CancellationToken, Task<T>> connect,
        TimeSpan delay,
        CancellationToken cancellationToken) where T : class, IDisposable
    {
        if (ordered == null || ordered.Count == 0)
            throw new SocketException((int)SocketError.HostNotFound);

        using var race = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var running = new List<Task<T>>();
        Exception lastError = null;
        int next = 0;

        void StartNext() => running.Add(connect(ordered[next++], race.Token));

        StartNext();
        while (running.Count > 0)
        {
            Task finished;
            if (next < ordered.Count)
            {
                var timer = Task.Delay(delay, race.Token);
                var any = new List<Task>(running) { timer };
                finished = await Task.WhenAny(any).ConfigureAwait(false);
                if (finished == timer)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    StartNext();
                    continue;
                }
            }
            else
            {
                finished = await Task.WhenAny(running).ConfigureAwait(false);
            }

            var attempt = (Task<T>)finished;
            running.Remove(attempt);
            if (attempt.Status == TaskStatus.RanToCompletion && attempt.Result != null)
            {
                race.Cancel();
                foreach (var loser in running) _disposeWhenDone(loser);
                return attempt.Result;
            }

            lastError = attempt.Exception?.InnerException ?? lastError;
            cancellationToken.ThrowIfCancellationRequested();
            if (next < ordered.Count) StartNext();
        }

        throw lastError ?? new SocketException((int)SocketError.ConnectionRefused);
    }


    private static void _disposeWhenDone<T>(Task<T> attempt) where T : class, IDisposable
    {
        attempt.ContinueWith(t =>
        {
            if (t.Status == TaskStatus.RanToCompletion) t.Result?.Dispose();
            else _ = t.Exception;   // observed: a cancelled or failed loser is expected
        }, TaskScheduler.Default);
    }


    /// <summary>A handler whose connections race the host's addresses.</summary>
    public static SocketsHttpHandler CreateHandler()
    {
        return new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var endpoint = context.DnsEndPoint;
                IPAddress[] addresses = IPAddress.TryParse(endpoint.Host, out var literal)
                    ? new[] { literal }
                    : await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken).ConfigureAwait(false);

                var socket = await ConnectAsync(Order(addresses),
                    (ip, ct) => _connectSocket(ip, endpoint.Port, ct),
                    FallbackDelay, cancellationToken).ConfigureAwait(false);

                Trace(_dc, $"HTTP connect to {endpoint.Host}:{endpoint.Port} via {socket.RemoteEndPoint} "
                           + $"({(socket.AddressFamily == AddressFamily.InterNetworkV6 ? "IPv6" : "IPv4")})");
                return new NetworkStream(socket, ownsSocket: true);
            }
        };
    }


    private static async Task<Socket> _connectSocket(IPAddress ip, int port, CancellationToken cancellationToken)
    {
        var socket = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(new IPEndPoint(ip, port), cancellationToken).ConfigureAwait(false);
            return socket;
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }


    /// <summary>
    /// The game's HttpClient. On Android the platform's native handler stays
    /// (the default there; it brings Android's own connection handling and
    /// trust store); everywhere else the racing handler.
    /// </summary>
    public static HttpClient CreateHttpClient() =>
        OperatingSystem.IsAndroid() ? new HttpClient() : new HttpClient(CreateHandler(), disposeHandler: true);
}
