namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;

public static class BackgroundProcessStartInfoFactory
{
    public static ProcessStartInfo Create(string executablePath, int parentProcessId)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add("--bg");
        startInfo.ArgumentList.Add(parentProcessId.ToString(CultureInfo.InvariantCulture));

        startInfo.Environment["APPIMAGELAUNCHER_DISABLE"] = "1";
        startInfo.Environment["DISPLAY"] = Environment.GetEnvironmentVariable("DISPLAY") ?? ":0";
        startInfo.Environment["XAUTHORITY"] = Environment.GetEnvironmentVariable("XAUTHORITY") ??
                                              Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".Xauthority");
        startInfo.Environment["DISABLE_VIRTUAL_KEYBOARD"] = Environment.GetEnvironmentVariable("DISABLE_VIRTUAL_KEYBOARD") ?? "0";

        return startInfo;
    }
}
