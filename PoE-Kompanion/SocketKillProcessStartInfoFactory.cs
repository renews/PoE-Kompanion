namespace PoEKompanion;

using System.Diagnostics;
using System.Globalization;

public static class SocketKillProcessStartInfoFactory
{
    private const string SS_PATH = "/usr/bin/ss";
    private const string HELPER_PATH = "/usr/local/libexec/poe-kompanion-kill-connection";

    public static ProcessStartInfo CreateDirect(string remoteAddr, int remotePort)
    {
        return Create(
            SS_PATH,
            ["-K", "dst", remoteAddr, "dport", remotePort.ToString(CultureInfo.InvariantCulture)]
        );
    }

    public static ProcessStartInfo CreateHelper(int processId, string remoteAddr, int remotePort)
    {
        return Create(
            "sudo",
            [
                "-n",
                HELPER_PATH,
                processId.ToString(CultureInfo.InvariantCulture),
                remoteAddr,
                remotePort.ToString(CultureInfo.InvariantCulture),
            ]
        );
    }

    private static ProcessStartInfo Create(string fileName, string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return startInfo;
    }
}
