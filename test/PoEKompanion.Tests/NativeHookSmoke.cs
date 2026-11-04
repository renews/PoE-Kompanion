namespace PoEKompanion.Tests;

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Data;
using SharpHook.Providers;

internal static class NativeHookSmoke
{
    internal static async Task RunAsync()
    {
        if (!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("Run the native hook smoke test under Xvfb on Linux.");

        UioHookProvider.Instance.SetLinuxMode(LinuxMode.XRecord);
        using var hook = new EventLoopGlobalHook();
        var enabled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pressed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var released = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        hook.HookEnabled += (_, _) => enabled.TrySetResult();
        hook.KeyPressed += (_, args) =>
        {
            if (args.Data.KeyCode == KeyCode.VcF12) pressed.TrySetResult();
        };
        hook.KeyReleased += (_, args) =>
        {
            if (args.Data.KeyCode == KeyCode.VcF12) released.TrySetResult();
        };

        var running = hook.RunAsync(GlobalHookType.Keyboard);
        try
        {
            await enabled.Task.WaitAsync(TimeSpan.FromSeconds(15));
            using var input = Process.Start(new ProcessStartInfo("xdotool", "key F12") { UseShellExecute = false })!;
            await input.WaitForExitAsync();
            if (input.ExitCode != 0) throw new InvalidOperationException("xdotool failed to send F12.");
            await Task.WhenAll(pressed.Task, released.Task).WaitAsync(TimeSpan.FromSeconds(15));
        }
        finally
        {
            hook.Dispose();
            await running.WaitAsync(TimeSpan.FromSeconds(15));
        }

        Console.WriteLine("Native XRecord keyboard press/release smoke test passed.");
    }
}
