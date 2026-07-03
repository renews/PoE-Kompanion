namespace PoEKompanion;

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;

using SharpHook;

using Views;

public class App : Application
{
    private ConfigurationModel? config;

    private Process? bgProcess;

    private IGlobalHook? hook;

    private UnixSocketIpc? ipc;

    private bool isShuttingDown;

    private ConfigurationWindow? configWindow;

    private bool isConfigWindowOpen;

    private volatile bool isDisposingHook;

    private int? poeProcessId;

    private bool isCtrlPressed;
    private bool isShiftPressed;
    private bool isAltPressed;

    private ChatCommandMessage? pendingChatCommand;

    public static App? Instance { get; private set; }

    public int? GetPoEProcessId() => this.poeProcessId;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Instance = this;
    }

    public override void OnFrameworkInitializationCompleted()
    {
#if DEBUG
        this.AttachDevTools();
#endif

        if (this.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            desktop.MainWindow = null;

            var path = Program.GetExecutablePath();

            var args = desktop.Args;
            for (var i = 0; i < args?.Length; ++i)
            {
                if (args[i] == "--bg-path=" && i < args.Length - 2)
                {
                    path = args[i + 1].Trim();
                }
            }

            _ = this.InitAsync(path);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private async Task InitAsync(string backgroundProcessPath)
    {
        this.config = await ConfigurationManager.LoadAsync();

        await this.EnsureLogoutHelperInstalledAsync();
        this.StartBackgroundProcess(backgroundProcessPath);
        this.ipc = await UnixSocketIpc.CreateServerAsync();
        this.StartMainHook();
        _ = this.ListenForIpcMessagesAsync();
    }

    private async Task EnsureLogoutHelperInstalledAsync()
    {
        var result = await LogoutHelperInstaller.EnsureInstalledForCurrentUserAsync(Program.GetInstallerExecutablePath());
        if (result == LogoutHelperSetupResult.Installed)
        {
            NotificationManager.SendInfo("Logout helper installed");
        }
        else if (result == LogoutHelperSetupResult.Failed)
        {
            NotificationManager.SendError("Logout helper setup failed", "Logout will not work until the helper is installed.");
        }
    }

    private async Task ListenForIpcMessagesAsync()
    {
        if (this.ipc is null) return;

        while (true)
        {
            var message = await this.ipc.ReceiveAsync();

            if (message is NotificationMessage notification)
            {
                if (notification.IsError)
                {
                    NotificationManager.SendError(notification.Title, notification.Message);
                }
                else
                {
                    NotificationManager.SendInfo(notification.Title, notification.Message);
                }
            }
            else if (message is PoEHookedMessage poeHookedMessage)
            {
                this.poeProcessId = poeHookedMessage.ProcessId;
                NotificationManager.SendInfo("Path of Exile process detected and hooked!");
            }
            else if (message is PoEUnhookedMessage)
            {
                this.poeProcessId = null;
                NotificationManager.SendInfo("Path of Exile closed");
            }
            else if (message is SetAlwaysOnTopMessage setAlwaysOnTop)
            {
                _ = Task.Run(() => WindowManager.TrySetAlwaysOnTop(setAlwaysOnTop.ProcessId));
            }
            else if (message is BackgroundReadyMessage)
            {
                await this.SendKeyboardLayoutMapAsync();
            }
        }
    }

    private async Task SendKeyboardLayoutMapAsync()
    {
        try
        {
            if (this.ipc is null) return;

            var layoutMap = KeyboardLayoutHelper.BuildLayoutMap();
            var layoutArray = layoutMap.Select(kvp => new CharKeyMapping(kvp.Key, kvp.Value.Keycode, kvp.Value.Shift)).ToArray();
            var message = new KeyboardLayoutMapMessage(layoutArray);

            await this.ipc.SendAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR sending layout map: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    private void StartMainHook()
    {
        if (this.hook is not null) return;

        this.isDisposingHook = false;
        this.hook = new SimpleGlobalHook();

        this.hook.KeyPressed += async (_, args) =>
        {
            if (this.isDisposingHook) return;

            var keyCode = args.Data.KeyCode;

            if (IsModifierKey(keyCode))
            {
                this.UpdateModifierState(keyCode, true);
                return;
            }

            if (this.isConfigWindowOpen) return;

            if (this.config?.LogoutHotkey?.Matches(keyCode, this.isCtrlPressed, this.isShiftPressed, this.isAltPressed) == true)
            {
                // Fire immediately for logout (doesn't send text)
                if (this.ipc is null) return;
                await this.ipc.SendAsync(new ForceLogoutMessage());
            }
            else if (this.config?.OpenSettingsHotkey?.Matches(keyCode, this.isCtrlPressed, this.isShiftPressed, this.isAltPressed) == true)
            {
                // Fire immediately for settings (doesn't send text)
                await this.OpenConfiguration();
            }
            else
            {
                var chatCommandHotkey = this.FindMatchingChatCommandHotkey(keyCode);
                if (chatCommandHotkey is not null)
                {
                    await this.SendOrDeferChatCommand(chatCommandHotkey);
                }
            }
        };

        this.hook.KeyReleased += async (_, args) =>
        {
            if (this.isDisposingHook) return;

            var keyCode = args.Data.KeyCode;

            if (IsModifierKey(keyCode))
            {
                this.UpdateModifierState(keyCode, false);

                if (this.isConfigWindowOpen) return;

                // Check if all modifiers are now released and we have a pending hotkey
                if (this.pendingChatCommand is not null &&
                    !this.isCtrlPressed && !this.isShiftPressed && !this.isAltPressed)
                {
                    var command = this.pendingChatCommand;
                    this.pendingChatCommand = null;

                    await this.SendChatCommandAsync(command);
                }
            }
        };

        _ = Task.Run(() =>
        {
            try
            {
                this.hook.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hook crashed: {ex.Message}");
            }
        });
    }

    private ChatCommandHotkey? FindMatchingChatCommandHotkey(SharpHook.Data.KeyCode keyCode) =>
        this.config is null
            ? null
            : ChatCommandHotkey.FindMatching(
                this.config.GetChatCommandHotkeys(),
                keyCode,
                this.isCtrlPressed,
                this.isShiftPressed,
                this.isAltPressed);

    private async Task SendOrDeferChatCommand(ChatCommandHotkey chatCommandHotkey)
    {
        if (string.IsNullOrWhiteSpace(chatCommandHotkey.Command)) return;

        var message = new ChatCommandMessage(chatCommandHotkey.Command, chatCommandHotkey.MinimumSubmitDelayMs);

        if (chatCommandHotkey.Hotkey.HasModifiers())
        {
            this.pendingChatCommand = message;
            return;
        }

        await this.SendChatCommandAsync(message);
    }

    private async Task SendChatCommandAsync(ChatCommandMessage command)
    {
        if (this.ipc is null) return;

        await this.ipc.SendAsync(command);
    }

    public async Task StopMainHookAsync()
    {
        if (this.hook is null) return;

        this.isDisposingHook = true;

        // Give any in-flight events a moment to check the flag
        await Task.Delay(50);

        var hookToDispose = this.hook;
        this.hook = null;

        try
        {
            await Task.Run(() => hookToDispose.Dispose());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error disposing hook: {ex.Message}");
        }
    }

    private static bool IsModifierKey(SharpHook.Data.KeyCode keyCode) =>
        keyCode is SharpHook.Data.KeyCode.VcLeftControl or SharpHook.Data.KeyCode.VcRightControl or
                   SharpHook.Data.KeyCode.VcLeftShift or SharpHook.Data.KeyCode.VcRightShift or
                   SharpHook.Data.KeyCode.VcLeftAlt or SharpHook.Data.KeyCode.VcRightAlt or
                   SharpHook.Data.KeyCode.VcLeftMeta or SharpHook.Data.KeyCode.VcRightMeta;

    private void UpdateModifierState(SharpHook.Data.KeyCode keyCode, bool isPressed)
    {
        switch (keyCode)
        {
            case SharpHook.Data.KeyCode.VcLeftControl:
            case SharpHook.Data.KeyCode.VcRightControl:
                this.isCtrlPressed = isPressed;
                break;
            case SharpHook.Data.KeyCode.VcLeftShift:
            case SharpHook.Data.KeyCode.VcRightShift:
                this.isShiftPressed = isPressed;
                break;
            case SharpHook.Data.KeyCode.VcLeftAlt:
            case SharpHook.Data.KeyCode.VcRightAlt:
                this.isAltPressed = isPressed;
                break;
        }
    }

    private void StartBackgroundProcess(string path)
    {
        var currentPid = Environment.ProcessId;

        try
        {
            this.bgProcess = new Process
            {
                StartInfo = BackgroundProcessStartInfoFactory.Create(path, currentPid),
            };
            this.bgProcess.Start();

            _ = Task.Run(() =>
            {
                this.bgProcess.WaitForExit();

                if (this.isShuttingDown) return;

                if (this.bgProcess.ExitCode != 0)
                {
                    NotifyInitializationError();
                }
                else
                {
                    NotifyInitializationSuccess();
                }
            });
        }
        catch (Exception)
        {
            if (this.isShuttingDown) return;
            NotifyInitializationError();
        }
    }

    private static void NotifyInitializationSuccess()
    {
        NotificationManager.SendInfo("initialized successfully!");
    }
    
    private static void NotifyInitializationError()
    {
        NotificationManager.SendError("Failed to initialize; application will exit");
        Environment.Exit(1);
    }

    private void ConfigureAction(object? sender, EventArgs e) => _ = this.OpenConfiguration();
    
    private async Task OpenConfiguration()
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (this.configWindow is not null)
            {
                this.configWindow.Activate();
                ConfigurationWindow.EnsureAlwaysOnTop();
                return;
            }

            this.isConfigWindowOpen = true;
            this.configWindow = new ConfigurationWindow();
            this.configWindow.Closed += async (_, _) =>
            {
                this.configWindow = null;
                this.isConfigWindowOpen = false;
                this.config = await ConfigurationManager.LoadAsync();
                this.StartMainHook();
            };
            this.configWindow.Show();
        });
    }

    private void ExitAction(object? sender, EventArgs e)
    {
        this.isShuttingDown = true;

        this.hook?.Dispose();
        this.hook = null;

        try
        {
            this.bgProcess?.Kill(true);
            this.bgProcess?.Close();
        } catch (Exception) { /* nom */ }

        Environment.Exit(0);
    }
}
