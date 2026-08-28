using System.Buffers.Binary;
using System.Diagnostics;
using System.Text.Json;
using AutomationStudio.Abstractions;

namespace AutomationStudio.Linux.Portal;

/// <summary>
/// Captures the screen via the xdg-desktop-portal Screenshot API
/// (org.freedesktop.portal.Screenshot), the only mechanism that actually works for full-desktop
/// capture on a native Wayland session — confirmed necessary the hard way: XGetImage on this
/// machine's XWayland root window returns BadMatch, because Wayland compositors generally don't
/// expose their framebuffer through XWayland's root window at all (see
/// <see cref="AutomationStudio.Linux.X11.X11ScreenshotService"/>'s doc comment).
/// </summary>
/// <remarks>
/// This shells out to <c>busctl</c> (systemd, present on effectively every modern Linux desktop)
/// rather than talking D-Bus directly from .NET. That was not the first approach: a
/// Tmds.DBus.Protocol-based client was built and tested against this exact portal first, since a
/// native client is obviously preferable to spawning a CLI tool. It correctly sent the Screenshot
/// request, but reliably receiving the result did not work — the spec's own advertised mechanism,
/// the <c>org.freedesktop.portal.Request.Response</c> signal, turned out to be delivered unicast
/// (confirmed via <c>busctl monitor</c>) and, against this machine's COSMIC portal, was only ever
/// actually emitted when the caller had a real <c>AddMatch</c> registered for it — bus-wide
/// monitoring alone (<c>BecomeMonitor</c>, what both <c>busctl monitor</c> and
/// Tmds.DBus.Protocol's <c>MonitorBusAsync</c> use) never triggered it, and the library's
/// AddMatch-based <c>WatchSignalAsync</c> registered correctly (confirmed on the wire) but never
/// invoked its handler — a library-side dispatch gap that resisted isolation without debug symbols.
/// <c>busctl</c>'s JSON output (<c>--json=short</c>) was verified byte-for-byte against this exact
/// flow while debugging that, so it is the proven-correct implementation checked in here — and
/// rather than depend on the flaky Response signal at all, it correlates the standard
/// <c>org.freedesktop.impl.portal.*</c> backend call/reply pair instead (see
/// <see cref="ReadResponseAsync"/>), which was observed reliably in every single test run. The
/// .NET-native path is a reasonable follow-up once it can be debugged with proper tooling instead
/// of black-box reflection.
/// </remarks>
internal sealed class PortalScreenshotService
{
    private const string PortalDestination = "org.freedesktop.portal.Desktop";
    private const string PortalPath = "/org/freedesktop/portal/desktop";
    private const string ScreenshotInterface = "org.freedesktop.portal.Screenshot";

    public async Task<Screenshot> CaptureFullScreenAsync(CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

        using var monitor = StartMonitor();
        try
        {
            // busctl monitor needs a moment to connect and register as a bus monitor before it will
            // see anything; without this, a Screenshot request fired immediately after Process.Start
            // returns can complete before the monitor is actually watching.
            await Task.Delay(TimeSpan.FromMilliseconds(500), timeoutCts.Token).ConfigureAwait(false);

            var requestPath = await CallScreenshotAsync(timeoutCts.Token).ConfigureAwait(false);
            var (resultCode, uri) = await ReadResponseAsync(monitor, requestPath, cancellationToken, timeoutCts.Token)
                .ConfigureAwait(false);

            if (resultCode != 0)
            {
                throw new InvalidOperationException(
                    $"Screenshot portal request did not succeed (result code {resultCode} — 1 means the user " +
                    "cancelled the permission dialog, 2 means some other error).");
            }

            var path = FileUriToPath(uri);
            var pngBytes = await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false);
            TryDeleteTempFile(path);

            var (width, height) = ReadPngDimensions(pngBytes);
            return new Screenshot { PngData = pngBytes, Width = width, Height = height };
        }
        finally
        {
            TryKill(monitor);
        }
    }

    private static Process StartMonitor()
    {
        var startInfo = new ProcessStartInfo("busctl")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("--user");
        startInfo.ArgumentList.Add("--json=short");
        startInfo.ArgumentList.Add("monitor");
        startInfo.ArgumentList.Add(PortalDestination);

        try
        {
            return Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start 'busctl monitor'.");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            throw new CapabilityUnavailableException(
                "Screen capture",
                "'busctl' (systemd) is required for the xdg-desktop-portal screenshot fallback and could not be started.");
        }
    }

    private static async Task<string> CallScreenshotAsync(CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo("busctl")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in new[]
        {
            "--user", "--json=short", "call",
            PortalDestination, PortalPath, ScreenshotInterface, "Screenshot",
            "sa{sv}", "", "1", "interactive", "b", "0"
        })
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start 'busctl call'.");
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"'busctl call' to the Screenshot portal failed: {stderr.Trim()}");
        }

        using var document = JsonDocument.Parse(stdout);
        return document.RootElement.GetProperty("data")[0].GetString()!;
    }

    /// <summary>
    /// Watches raw bus traffic for the result, rather than the public
    /// <c>org.freedesktop.portal.Request.Response</c> signal the portal spec advertises for this:
    /// that signal is delivered unicast (confirmed via <c>busctl monitor</c>), and in testing against
    /// this machine's COSMIC portal it was only ever actually emitted when the caller had registered a
    /// real <c>AddMatch</c> for it first — bus-wide monitoring (<c>busctl monitor</c> /
    /// <c>BecomeMonitor</c>) alone never triggered it, for reasons not fully isolated. What monitoring
    /// reliably sees instead, every single time, is the standard backend contract every portal
    /// implementation follows: the frontend forwards the request to
    /// <c>org.freedesktop.impl.portal.Screenshot.Screenshot(o handle, s app_id, s parent_window,
    /// a{sv} options)</c>, passing our exact request path as the first argument, and that call's
    /// <c>method_return</c> already carries the real result. This correlates that call (by request
    /// path) to its reply (by D-Bus reply-cookie) instead of relying on the Response signal at all.
    /// </summary>
    private static async Task<(uint ResultCode, string Uri)> ReadResponseAsync(
        Process monitor, string requestPath, CancellationToken cancellationToken, CancellationToken timeoutToken)
    {
        uint? pendingReplyCookie = null;

        try
        {
            while (true)
            {
                var line = await monitor.StandardOutput.ReadLineAsync(timeoutToken).ConfigureAwait(false);
                if (line is null)
                {
                    throw new InvalidOperationException("'busctl monitor' exited before the screenshot portal responded.");
                }

                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                var type = root.GetProperty("type").GetString();

                if (pendingReplyCookie is null)
                {
                    if (type != "method_call" ||
                        !root.TryGetProperty("member", out var memberEl) || memberEl.GetString() != "Screenshot" ||
                        !root.TryGetProperty("interface", out var interfaceEl) ||
                        interfaceEl.GetString() != "org.freedesktop.impl.portal.Screenshot")
                    {
                        continue;
                    }

                    var callData = root.GetProperty("payload").GetProperty("data");
                    if (callData.GetArrayLength() == 0 || callData[0].GetString() != requestPath)
                    {
                        continue;
                    }

                    pendingReplyCookie = root.GetProperty("cookie").GetUInt32();
                    continue;
                }

                if (type != "method_return" ||
                    !root.TryGetProperty("reply_cookie", out var replyCookieEl) ||
                    replyCookieEl.GetUInt32() != pendingReplyCookie.Value)
                {
                    continue;
                }

                var resultData = root.GetProperty("payload").GetProperty("data");
                var resultCode = resultData[0].GetUInt32();

                if (!resultData[1].TryGetProperty("uri", out var uriEntry))
                {
                    throw new InvalidOperationException("Screenshot portal backend reply did not include a 'uri' result.");
                }

                return (resultCode, uriEntry.GetProperty("data").GetString()!);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "Timed out waiting for the screenshot portal's response after 30s. If this is the first " +
                "screenshot on this system, check for a permission dialog that needs a click.");
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill();
            }
        }
        catch
        {
            // Best-effort cleanup only.
        }
    }

    /// <summary>
    /// Strips the "file://" prefix directly rather than using <see cref="Uri.LocalPath"/>: the
    /// portal returns URIs like "file:////tmp/screenshot-X.png" (four slashes — an empty authority
    /// followed by an absolute path), and <see cref="System.Uri"/>'s file-URI parsing treats that
    /// leading "////" as a UNC-path marker even on Linux, producing a garbled backslash path
    /// (confirmed live: LocalPath came back as <c>\\tmp\screenshot-X.png</c>). A leading "//" in an
    /// ordinary POSIX path is equivalent to "/", so using the raw remainder directly is correct.
    /// </summary>
    private static string FileUriToPath(string uri)
    {
        const string prefix = "file://";
        if (!uri.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Expected a file:// URI from the screenshot portal, got '{uri}'.");
        }

        return Uri.UnescapeDataString(uri[prefix.Length..]);
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only; leaving the portal's temp file behind is not fatal.
        }
    }

    private static (int Width, int Height) ReadPngDimensions(byte[] png)
    {
        // IHDR immediately follows the 8-byte PNG signature and the 4-byte chunk length + "IHDR" tag.
        if (png.Length < 24)
        {
            throw new InvalidOperationException("Screenshot portal returned a file that is too small to be a valid PNG.");
        }

        var width = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(16, 4));
        var height = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(20, 4));
        return (width, height);
    }
}
