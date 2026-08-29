using System.Diagnostics;

namespace AutomationStudio.Linux.Atspi;

/// <summary>
/// Whether AT-SPI is actually likely to work here, not just whether the D-Bus mechanics would
/// succeed. Confirmed live on this dev machine: the AT-SPI registry answers fine either way, but
/// GTK/GNOME toolkits only bridge their widgets into it when
/// <c>org.gnome.desktop.interface toolkit-accessibility</c> is enabled — off by default on a
/// freshly-installed system. With it off, every real find would come back empty despite every
/// D-Bus call succeeding, which would be a much more confusing failure mode than reporting the
/// capability as Restricted up front.
/// </summary>
internal static class AtspiCapabilityCheck
{
    /// <summary>
    /// Best-effort and GNOME-specific (gsettings) — there is no universal cross-desktop-environment
    /// way to ask "will toolkits actually bridge into AT-SPI". KDE/other environments use their own
    /// mechanisms; on those this simply can't confirm the setting and reports Restricted rather than
    /// guessing Available.
    /// </summary>
    public static bool? IsToolkitAccessibilityEnabled()
    {
        try
        {
            var startInfo = new ProcessStartInfo("gsettings")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("get");
            startInfo.ArgumentList.Add("org.gnome.desktop.interface");
            startInfo.ArgumentList.Add("toolkit-accessibility");

            using var process = Process.Start(startInfo);
            if (process is null)
            {
                return null;
            }

            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit(2000);

            if (process.ExitCode != 0)
            {
                return null;
            }

            return string.Equals(output, "true", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return null;
        }
    }
}
