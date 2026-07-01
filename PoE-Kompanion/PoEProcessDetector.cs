namespace PoEKompanion;

using System;
using System.Linq;

public static class PoEProcessDetector
{
    private static readonly string[] GameExecutableNames =
    [
        "PathOfExileSteam.exe",
        "PathOfExile_x64Steam.exe",
        "PathOfExile.exe",
        "PathOfExile_x64.exe",
    ];

    public static bool IsPathOfExileProcess(string processName, string? commandLine)
    {
        if (processName.StartsWith("PathOfExile", StringComparison.OrdinalIgnoreCase)) return true;

        return processName.Equals("Main", StringComparison.OrdinalIgnoreCase) &&
               commandLine is not null &&
               GameExecutableNames.Any(name => commandLine.Contains(name, StringComparison.OrdinalIgnoreCase));
    }
}
