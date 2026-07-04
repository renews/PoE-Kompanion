namespace PoEKompanion;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
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
            var compositorFocusedWindowOutput = GetCompositorFocusedWindowOutput();
            return !string.IsNullOrWhiteSpace(compositorFocusedWindowOutput)
                ? IsActiveWindowProcess(processId, compositorFocusedWindowOutput)
                : IsActiveWindowProcess(processId, GetActiveWindowProcessIdOutput());
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static bool IsActiveWindowProcess(int processId, string? activeWindowProcessIdOutput) =>
        TryParseActiveWindowProcessId(activeWindowProcessIdOutput, out var activeProcessId) && activeProcessId == processId ||
        IsPathOfExileWindow(activeWindowProcessIdOutput);

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

    private static string? GetActiveWindowProcessIdOutput() =>
        RunCommand("xdotool", "getactivewindow", "getwindowpid");

    private static string? GetCompositorFocusedWindowOutput()
    {
        foreach (var provider in GetFocusedWindowProviderNames())
        {
            var output = provider switch
            {
                "niri" => GetNiriFocusedWindowOutput(),
                "hyprland" => GetHyprlandFocusedWindowOutput(),
                "sway" => GetSwayFocusedWindowOutput(),
                _ => null,
            };

            if (!string.IsNullOrWhiteSpace(output)) return output;
        }

        return null;
    }

    private static IEnumerable<string> GetFocusedWindowProviderNames()
    {
        var providers = new List<string>();

        if (IsDesktopSession("niri") || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NIRI_SOCKET")))
        {
            AddProvider(providers, "niri");
        }

        if (IsDesktopSession("hyprland") || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("HYPRLAND_INSTANCE_SIGNATURE")))
        {
            AddProvider(providers, "hyprland");
        }

        if (IsDesktopSession("sway") || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SWAYSOCK")))
        {
            AddProvider(providers, "sway");
        }

        AddProvider(providers, "niri");
        AddProvider(providers, "hyprland");
        AddProvider(providers, "sway");

        return providers;
    }

    private static string? GetNiriFocusedWindowOutput() =>
        RunCommand("niri", "msg", "--json", "focused-window");

    private static string? GetHyprlandFocusedWindowOutput() =>
        RunCommand("hyprctl", "-j", "activewindow");

    private static string? GetSwayFocusedWindowOutput() =>
        RunCommand("swaymsg", "-t", "get_tree");

    private static void AddProvider(List<string> providers, string provider)
    {
        if (!providers.Contains(provider))
        {
            providers.Add(provider);
        }
    }

    private static bool IsDesktopSession(string compositorName) =>
        ContainsEnvironmentValue("XDG_CURRENT_DESKTOP", compositorName) ||
        ContainsEnvironmentValue("XDG_SESSION_DESKTOP", compositorName) ||
        ContainsEnvironmentValue("DESKTOP_SESSION", compositorName);

    private static bool ContainsEnvironmentValue(string variableName, string expectedValue) =>
        Environment.GetEnvironmentVariable(variableName)?.Contains(expectedValue, StringComparison.OrdinalIgnoreCase) == true;

    private static string? RunCommand(string fileName, params string[] arguments)
    {
        try
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                },
            };

            foreach (var argument in arguments)
            {
                proc.StartInfo.ArgumentList.Add(argument);
            }

            proc.Start();
            var outputTask = proc.StandardOutput.ReadToEndAsync();
            if (!proc.WaitForExit(5000))
            {
                try
                {
                    proc.Kill(true);
                }
                catch (Exception) { /* nom */ }

                return null;
            }

            return proc.ExitCode == 0 ? outputTask.GetAwaiter().GetResult() : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private static bool TryParseActiveWindowProcessId(string? activeWindowProcessIdOutput, out int processId)
    {
        processId = 0;
        var trimmedOutput = activeWindowProcessIdOutput?.Trim();

        if (string.IsNullOrEmpty(trimmedOutput)) return false;
        if (int.TryParse(trimmedOutput, out processId)) return true;

        try
        {
            using var document = JsonDocument.Parse(trimmedOutput);
            if (document.RootElement.TryGetProperty("pid", out var pidElement) &&
                pidElement.TryGetInt32(out processId))
            {
                return true;
            }

            return TryParseFocusedProcessId(document.RootElement, out processId);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsPathOfExileWindow(string? activeWindowProcessIdOutput)
    {
        var trimmedOutput = activeWindowProcessIdOutput?.Trim();
        if (string.IsNullOrEmpty(trimmedOutput) || int.TryParse(trimmedOutput, out _)) return false;

        try
        {
            using var document = JsonDocument.Parse(trimmedOutput);
            return IsPathOfExileWindow(document.RootElement, true);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsPathOfExileWindow(JsonElement element, bool assumeActive)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var isActive = assumeActive ||
                           IsBooleanPropertyTrue(element, "focused") ||
                           IsBooleanPropertyTrue(element, "is_focused");

            if (isActive && HasPathOfExileWindowIdentity(element)) return true;

            foreach (var property in element.EnumerateObject())
            {
                if (IsPathOfExileWindow(property.Value, false)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (IsPathOfExileWindow(item, false)) return true;
            }
        }

        return false;
    }

    private static bool HasPathOfExileWindowIdentity(JsonElement element)
    {
        if (HasPathOfExileWindowIdentity(element, "app_id") ||
            HasPathOfExileWindowIdentity(element, "class") ||
            HasPathOfExileWindowIdentity(element, "initialClass") ||
            HasPathOfExileWindowIdentity(element, "initial_class") ||
            HasPathOfExileWindowIdentity(element, "instance"))
        {
            return true;
        }

        if (!element.TryGetProperty("window_properties", out var windowProperties) ||
            windowProperties.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return HasPathOfExileWindowIdentity(windowProperties, "class") ||
               HasPathOfExileWindowIdentity(windowProperties, "instance");
    }

    private static bool HasPathOfExileWindowIdentity(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var value = property.GetString();
        if (string.IsNullOrWhiteSpace(value)) return false;

        return value.Equals("steam_app_2694490", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("steam_app_238960", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("PathOfExile", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBooleanPropertyTrue(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) &&
        property.ValueKind == JsonValueKind.True;

    private static bool TryParseFocusedProcessId(JsonElement element, out int processId)
    {
        processId = 0;

        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("focused", out var focusedElement) &&
                focusedElement.ValueKind == JsonValueKind.True &&
                element.TryGetProperty("pid", out var pidElement) &&
                pidElement.TryGetInt32(out processId))
            {
                return true;
            }

            foreach (var property in element.EnumerateObject())
            {
                if (TryParseFocusedProcessId(property.Value, out processId)) return true;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (TryParseFocusedProcessId(item, out processId)) return true;
            }
        }

        return false;
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
