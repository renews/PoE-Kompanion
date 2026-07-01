namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

public static class LogoutHelperInstaller
{
    private const string HELPER_PATH = "/usr/local/libexec/poe-kompanion-kill-connection";
    private const string SUDOERS_PATH = "/etc/sudoers.d/poe-kompanion";
    private const string HELPER_RELATIVE_PATH = "usr/share/poe-kompanion/poe-kompanion-kill-connection";

    [DllImport("libc")]
    private static extern uint geteuid();

    public static async Task<int> RunAsync(string[] args)
    {
        var targetUser = args.Length > 0 ? args[0] : Environment.GetEnvironmentVariable("SUDO_USER");

        if (!OperatingSystem.IsLinux())
        {
            Console.Error.WriteLine("The logout helper installer is only supported on Linux.");
            return 1;
        }

        if (geteuid() != 0)
        {
            Console.Error.WriteLine("Run this command with sudo.");
            return 1;
        }

        if (!IsValidUserName(targetUser))
        {
            Console.Error.WriteLine("Usage: sudo <PoE-Kompanion.AppImage> --install-logout-helper <username>");
            return 2;
        }

        if (!UserExists(targetUser!))
        {
            Console.Error.WriteLine($"User does not exist: {targetUser}");
            return 2;
        }

        var appDir = Environment.GetEnvironmentVariable("POE_KOMPANION_APPDIR");
        var helperSource = GetBundledHelperPath(appDir);
        if (!File.Exists(helperSource))
        {
            Console.Error.WriteLine($"Bundled logout helper not found: {helperSource}");
            return 1;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(HELPER_PATH)!);
        File.Copy(helperSource, HELPER_PATH, true);
        File.SetUnixFileMode(
            HELPER_PATH,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute
        );

        var tempSudoers = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tempSudoers, CreateSudoersContent(targetUser!));
            File.SetUnixFileMode(tempSudoers, UnixFileMode.UserRead | UnixFileMode.GroupRead);

            var validationResult = RunProcess("visudo", ["-c", "-f", tempSudoers]);
            if (validationResult.ExitCode != 0)
            {
                Console.Error.WriteLine(validationResult.CombinedOutput);
                return 1;
            }

            File.Copy(tempSudoers, SUDOERS_PATH, true);
            File.SetUnixFileMode(SUDOERS_PATH, UnixFileMode.UserRead | UnixFileMode.GroupRead);

            validationResult = RunProcess("visudo", ["-c", "-f", SUDOERS_PATH]);
            if (validationResult.ExitCode != 0)
            {
                Console.Error.WriteLine(validationResult.CombinedOutput);
                return 1;
            }
        }
        finally
        {
            try
            {
                File.Delete(tempSudoers);
            }
            catch (Exception) { /* nom */ }
        }

        Console.WriteLine($"Installed PoE Kompanion logout helper for {targetUser}.");
        return 0;
    }

    public static async Task<LogoutHelperSetupResult> EnsureInstalledForCurrentUserAsync(string installerExecutablePath)
    {
        if (!OperatingSystem.IsLinux()) return LogoutHelperSetupResult.Skipped;
        if (IsPermissionAvailable()) return LogoutHelperSetupResult.Skipped;

        var userName = CurrentUser.GetName();
        if (!IsValidUserName(userName)) return LogoutHelperSetupResult.Failed;

        try
        {
            var process = new Process
            {
                StartInfo = CreateAutoInstallStartInfo(installerExecutablePath, userName),
            };

            process.Start();
            await process.WaitForExitAsync();

            return process.ExitCode == 0 && IsPermissionAvailable()
                ? LogoutHelperSetupResult.Installed
                : LogoutHelperSetupResult.Failed;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to start logout helper setup: {ex.Message}");
            return LogoutHelperSetupResult.Failed;
        }
    }

    public static ProcessStartInfo CreateAutoInstallStartInfo(string installerExecutablePath, string userName)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "pkexec",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        startInfo.ArgumentList.Add(installerExecutablePath);
        startInfo.ArgumentList.Add("--install-logout-helper");
        startInfo.ArgumentList.Add(userName);

        return startInfo;
    }

    public static string GetBundledHelperPath(string? appDir)
    {
        return string.IsNullOrWhiteSpace(appDir)
            ? HELPER_RELATIVE_PATH
            : Path.Combine(appDir, HELPER_RELATIVE_PATH);
    }

    public static bool IsValidUserName(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName) || userName == "root") return false;

        foreach (var c in userName)
        {
            if (char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-') continue;
            return false;
        }

        return true;
    }

    public static string CreateSudoersContent(string userName) =>
        $"# Managed by PoE Kompanion.\n{userName} ALL=(root) NOPASSWD: {HELPER_PATH} *\n";

    private static bool UserExists(string userName)
    {
        var result = RunProcess("/usr/bin/id", ["-u", userName]);
        return result.ExitCode == 0;
    }

    private static bool IsPermissionAvailable()
    {
        if (!File.Exists(HELPER_PATH)) return false;

        var result = RunProcess(
            "sudo",
            ["-n", HELPER_PATH, Environment.ProcessId.ToString(), "127.0.0.1", "9"]
        );

        return result.CombinedOutput.Contains("not a supported Path of Exile process", StringComparison.OrdinalIgnoreCase) ||
               result.CombinedOutput.Contains("Connection is not owned by the supplied Path of Exile process", StringComparison.OrdinalIgnoreCase);
    }

    private static ProcessResult RunProcess(string fileName, string[] arguments)
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

        var process = new Process
        {
            StartInfo = startInfo,
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        return new ProcessResult(process.ExitCode, output, error);
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error)
    {
        public string CombinedOutput => $"{this.Output}{this.Error}";
    }
}

public enum LogoutHelperSetupResult
{
    Skipped,
    Installed,
    Failed,
}
