namespace PoEKompanion.Tests;

using System;
using System.Linq;
using System.Reflection;

using PoEKompanion;

internal static class Program
{
    private static int Main()
    {
        AssertEqual(
            "1.0.0",
            typeof(PoEProcessDetector).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            "Assembly informational version should match release version");

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
            "SteamLaunch AppId=2694490 -- /home/user/.local/share/Steam/steamapps/common/Path of Exile 2/PathOfExileSteam.exe --nopatch",
            "Steam launch helper should not match");

        AssertDoesNotMatch(
            "python3",
            "/home/user/.local/share/Steam/steamapps/common/Proton - Experimental/proton waitforexitandrun /home/user/.local/share/Steam/steamapps/common/Path of Exile 2/PathOfExileSteam.exe --nopatch",
            "Proton launcher should not match");

        AssertEqual("1.2.3.4", ProcNetTcpAddressParser.HexToIp("04030201"), "IPv4 proc address should parse");
        AssertEqual("::1", ProcNetTcpAddressParser.HexToIp("00000000000000000000000001000000"), "IPv6 proc address should parse");
        AssertEqual(false, ProcNetTcpAddressParser.IsRemoteAddress("::1"), "IPv6 loopback should be ignored");
        AssertEqual(true, ProcNetTcpAddressParser.IsRemoteAddress("2600:1419:6200:61::17df:d327"), "IPv6 remote should be tracked");

        var backgroundStartInfo = BackgroundProcessStartInfoFactory.Create("/tmp/PoEKompanion", 1234);
        AssertEqual("/tmp/PoEKompanion", backgroundStartInfo.FileName, "Background helper should start as the app binary");
        AssertSequenceEqual(["--bg", "1234"], backgroundStartInfo.ArgumentList, "Background helper arguments");
        AssertEqual(false, backgroundStartInfo.UseShellExecute, "Background helper should not use a shell");
        AssertEqual(true, backgroundStartInfo.CreateNoWindow, "Background helper should not create a window");
        AssertEqual("1", backgroundStartInfo.Environment["APPIMAGELAUNCHER_DISABLE"], "Background helper should disable AppImageLauncher");

        var directKillStartInfo = SocketKillProcessStartInfoFactory.CreateDirect("1.2.3.4", 443);
        AssertEqual("/usr/bin/ss", directKillStartInfo.FileName, "Direct socket kill should call ss");
        AssertSequenceEqual(["-K", "dst", "1.2.3.4", "dport", "443"], directKillStartInfo.ArgumentList, "Direct socket kill arguments");
        AssertEqual(false, directKillStartInfo.UseShellExecute, "Direct socket kill should not use a shell");
        AssertEqual(true, directKillStartInfo.RedirectStandardOutput, "Direct socket kill should capture stdout");
        AssertEqual(true, directKillStartInfo.RedirectStandardError, "Direct socket kill should capture stderr");

        var helperKillStartInfo = SocketKillProcessStartInfoFactory.CreateHelper(5678, "1.2.3.4", 443);
        AssertEqual("sudo", helperKillStartInfo.FileName, "Helper socket kill should use sudo");
        AssertSequenceEqual(
            ["-n", "/usr/local/libexec/poe-kompanion-kill-connection", "5678", "1.2.3.4", "443"],
            helperKillStartInfo.ArgumentList,
            "Helper socket kill arguments");
        AssertEqual(false, helperKillStartInfo.UseShellExecute, "Helper socket kill should not use a shell");
        AssertEqual(true, helperKillStartInfo.RedirectStandardOutput, "Helper socket kill should capture stdout");
        AssertEqual(true, helperKillStartInfo.RedirectStandardError, "Helper socket kill should capture stderr");

        AssertEqual(
            "/tmp/AppDir/usr/share/poe-kompanion/poe-kompanion-kill-connection",
            LogoutHelperInstaller.GetBundledHelperPath("/tmp/AppDir"),
            "Bundled helper path should live in the AppDir");
        AssertEqual(true, LogoutHelperInstaller.IsValidUserName("appuser"), "Simple Linux username should be valid");
        AssertEqual(false, LogoutHelperInstaller.IsValidUserName("bad user"), "Username with spaces should be invalid");
        AssertEqual(
            "# Managed by PoE Kompanion.\nappuser ALL=(root) NOPASSWD: /usr/local/libexec/poe-kompanion-kill-connection *\n",
            LogoutHelperInstaller.CreateSudoersContent("appuser"),
            "Sudoers content should allow only the fixed helper path");
        var autoInstallStartInfo = LogoutHelperInstaller.CreateAutoInstallStartInfo("/tmp/PoE.AppImage", "appuser");
        AssertEqual("pkexec", autoInstallStartInfo.FileName, "Auto install should use pkexec for graphical auth");
        AssertSequenceEqual(
            ["/tmp/PoE.AppImage", "--install-logout-helper", "appuser"],
            autoInstallStartInfo.ArgumentList,
            "Auto install arguments");
        AssertEqual(false, autoInstallStartInfo.UseShellExecute, "Auto install should not use a shell");
        AssertEqual(true, autoInstallStartInfo.RedirectStandardOutput, "Auto install should capture stdout");
        AssertEqual(true, autoInstallStartInfo.RedirectStandardError, "Auto install should capture stderr");

        var whoamiStartInfo = CurrentUser.CreateWhoamiStartInfo();
        AssertEqual("/usr/bin/whoami", whoamiStartInfo.FileName, "Current user should be resolved through whoami");
        AssertEqual(false, whoamiStartInfo.UseShellExecute, "whoami should not use a shell");
        AssertEqual(true, whoamiStartInfo.RedirectStandardOutput, "whoami should capture stdout");
        AssertEqual(true, CurrentUser.GetEffectiveUserIdSegment().StartsWith("uid-"), "Fallback user segment should be UID-based");

        AssertEqual(
            "/run/user/1000/poe-kompanion/server.sock",
            UnixSocketIpc.GetServerSocketPath("/run/user/1000"),
            "Server IPC socket should live under the user runtime directory");
        AssertEqual(
            "/run/user/1000/poe-kompanion/client.sock",
            UnixSocketIpc.GetClientSocketPath("/run/user/1000"),
            "Client IPC socket should live under the user runtime directory");
        AssertEqual(
            "/tmp/poe-kompanion-appuser/server.sock",
            UnixSocketIpc.GetServerSocketPath(string.Empty, "appuser"),
            "Fallback IPC socket should be namespaced by user");

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

    private static void AssertSequenceEqual(string[] expected, System.Collections.ObjectModel.Collection<string> actual, string message)
    {
        if (expected.Length == actual.Count && expected.Select((value, index) => actual[index] == value).All(matches => matches)) return;

        throw new InvalidOperationException($"{message}: expected [{string.Join(", ", expected)}], got [{string.Join(", ", actual)}]");
    }
}
