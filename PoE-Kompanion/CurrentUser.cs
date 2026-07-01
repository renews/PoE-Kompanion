namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

public static class CurrentUser
{
    [DllImport("libc")]
    private static extern uint geteuid();

    public static string GetName()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = CreateWhoamiStartInfo(),
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();

            if (process.ExitCode == 0 && !string.IsNullOrWhiteSpace(output)) return output;
        }
        catch (Exception) { /* nom */ }

        return string.Empty;
    }

    public static string GetEffectiveUserIdSegment() => $"uid-{geteuid()}";

    public static ProcessStartInfo CreateWhoamiStartInfo()
    {
        return new ProcessStartInfo
        {
            FileName = "/usr/bin/whoami",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
    }
}
