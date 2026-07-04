namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.Threading.Tasks;

public static class WindowManager
{
    public static bool TrySetAlwaysOnTop(int processId)
    {
        if (!OperatingSystem.IsLinux()) return false;

        return TryWithWmctrl(processId) || TryWithXdotool(processId);
    }

    public static bool TryBringToFront(int processId)
    {
        if (!OperatingSystem.IsLinux()) return false;

        return TryToggleAlwaysOnTopWithWmctrl(processId);
    }

    public static (int X, int Y, int Width, int Height)? GetWindowBounds(int processId)
    {
        if (!OperatingSystem.IsLinux()) return null;

        try
        {
            var windowId = GetWindowId(processId);
            if (string.IsNullOrEmpty(windowId)) return null;

            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"wmctrl -lG | awk '$1 == \\\"{windowId}\\\" {{print $3,$4,$5,$6}}'\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            var output = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            if (proc.ExitCode != 0 || string.IsNullOrEmpty(output)) return null;

            var parts = output.Split(' ');
            if (parts.Length != 4) return null;

            var x = int.Parse(parts[0]);
            var y = int.Parse(parts[1]);
            var width = int.Parse(parts[2]);
            var height = int.Parse(parts[3]);

            return (x, y, width, height);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static bool IsActiveWindowProcess(int processId)
    {
        if (!OperatingSystem.IsLinux()) return false;

        try
        {
            return IsActiveWindowProcess(processId, GetActiveWindowProcessIdOutput());
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsActiveWindowProcess(int processId, string? activeWindowProcessIdOutput) =>
        int.TryParse(activeWindowProcessIdOutput?.Trim(), out var activeProcessId) &&
        activeProcessId == processId;

    public static void SetWindowSkipTaskbar(int processId, string windowTitle)
    {
        if (!OperatingSystem.IsLinux()) return;

        try
        {
            string? windowId = null;
            for (var i = 0; i < 50; ++i)
            {
                var checkProc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "bash",
                        Arguments = $"-c \"wmctrl -l | grep '{windowTitle}' | awk '{{print $1}}'\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true,
                    },
                };
                checkProc.Start();
                windowId = checkProc.StandardOutput.ReadToEnd().Trim();
                checkProc.WaitForExit(1000);

                if (!string.IsNullOrEmpty(windowId)) break;

                Task.Delay(20).Wait();
            }

            if (string.IsNullOrEmpty(windowId)) return;

            var setTypeProc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"xprop -id {windowId} -f _NET_WM_WINDOW_TYPE 32a -set _NET_WM_WINDOW_TYPE _NET_WM_WINDOW_TYPE_DOCK\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            setTypeProc.Start();
            setTypeProc.WaitForExit(5000);
        }
        catch (Exception)
        {
            // Ignore
        }
    }

    private static string? GetActiveWindowProcessIdOutput()
    {
        var proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "xdotool",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
            },
        };
        proc.StartInfo.ArgumentList.Add("getactivewindow");
        proc.StartInfo.ArgumentList.Add("getwindowpid");

        proc.Start();
        var output = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);

        return proc.ExitCode == 0 ? output : null;
    }

    private static bool TryWithWmctrl(int processId)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"wmctrl -i -r $(wmctrl -lp | awk '$3 == {processId} {{print $1; exit}}') -b add,above\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            proc.WaitForExit(5000);

            return proc.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryWithXdotool(int processId)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"xdotool search --pid {processId} windowraise %@ windowfocus %@ set_window --overrideredirect 1\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            proc.WaitForExit(5000);

            return proc.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryToggleAlwaysOnTopWithWmctrl(int processId)
    {
        try
        {
            var windowId = GetWindowId(processId);
            if (string.IsNullOrEmpty(windowId)) return false;

            var raiseAndToggle = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"wmctrl -i -r {windowId} -b remove,above && wmctrl -i -a {windowId} && wmctrl -i -r {windowId} -b add,above\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            raiseAndToggle.Start();
            raiseAndToggle.WaitForExit(5000);

            return raiseAndToggle.ExitCode == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string? GetWindowId(int processId)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "bash",
                    Arguments = $"-c \"wmctrl -lp | awk '$3 == {processId} {{print $1; exit}}'\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };
            proc.Start();
            var windowId = proc.StandardOutput.ReadToEnd().Trim();
            proc.WaitForExit(5000);

            return proc.ExitCode == 0 ? windowId : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
