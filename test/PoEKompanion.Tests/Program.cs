namespace PoEKompanion.Tests;

using System;

using PoEKompanion;

internal static class Program
{
    private static int Main()
    {
        AssertMatches(
            "Main",
            @"S:\common\Path of Exile 2\PathOfExileSteam.exe --nopatch",
            "Proton Path of Exile 2 game process should match");

        AssertMatches(
            "Main",
            @"S:\common\Path of Exile 2\PathOfExile_x64Steam.exe --nopatch",
            "Proton Path of Exile 2 x64 Steam game process should match");

        AssertMatches(
            "PathOfExileSteam",
            string.Empty,
            "Native Path of Exile Steam process name should match");

        AssertMatches(
            "PathOfExile_x64",
            string.Empty,
            "Native Path of Exile x64 process name should match");

        AssertMatches(
            "Main",
            @"S:\common\Path of Exile\PathOfExile_x64.exe --nopatch",
            "Proton Path of Exile 1 standalone game process should match");

        AssertDoesNotMatch(
            "reaper",
            "SteamLaunch AppId=2694490 -- /home/coder/.local/share/Steam/steamapps/common/Path of Exile 2/PathOfExileSteam.exe --nopatch",
            "Steam launch helper should not match");

        AssertDoesNotMatch(
            "python3",
            "/home/coder/.local/share/Steam/steamapps/common/Proton - Experimental/proton waitforexitandrun /home/coder/.local/share/Steam/steamapps/common/Path of Exile 2/PathOfExileSteam.exe --nopatch",
            "Proton launcher should not match");

        AssertEqual("1.2.3.4", ProcNetTcpAddressParser.HexToIp("04030201"), "IPv4 proc address should parse");
        AssertEqual("::1", ProcNetTcpAddressParser.HexToIp("00000000000000000000000001000000"), "IPv6 proc address should parse");
        AssertEqual(false, ProcNetTcpAddressParser.IsRemoteAddress("::1"), "IPv6 loopback should be ignored");
        AssertEqual(true, ProcNetTcpAddressParser.IsRemoteAddress("2600:1419:6200:61::17df:d327"), "IPv6 remote should be tracked");

        return 0;
    }

    private static void AssertMatches(string processName, string commandLine, string message)
    {
        if (PoEProcessDetector.IsPathOfExileProcess(processName, commandLine)) return;

        throw new InvalidOperationException(message);
    }

    private static void AssertDoesNotMatch(string processName, string commandLine, string message)
    {
        if (!PoEProcessDetector.IsPathOfExileProcess(processName, commandLine)) return;

        throw new InvalidOperationException(message);
    }

    private static void AssertEqual<T>(T expected, T actual, string message)
    {
        if (Equals(expected, actual)) return;

        throw new InvalidOperationException($"{message}: expected {expected}, got {actual}");
    }
}
