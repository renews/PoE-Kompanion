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
using SharpHook.Data;

using Views;

public class App : Application
{
    private ConfigurationModel? config;

    private Process? bgProcess;

    private IGlobalHook? hook;

    private UnixSocketIpc? ipc;

    private bool isShuttingDown;

    private ConfigurationWindow? configWindow;

    private volatile bool isConfigWindowOpen;

    private volatile bool isDisposingHook;

    private int? poeProcessId;

    private ChatCommandMessage? pendingChatCommand;

    private readonly KeyPressTracker keyPressTracker = new();
    private readonly HotkeyCaptureCoordinator hotkeyCaptureCoordinator = new();

    public static App? Instance { get; private set; }

    public int? GetPoEProcessId() => this.poeProcessId;

    public bool TryStartHotkeyCapture(out Task<HotkeyCombo?> captureTask)
    {
        if (this.hook is not null)
        {
            return this.hotkeyCaptureCoordinator.TryStartCapture(out captureTask);
        }

        captureTask = Task.FromResult<HotkeyCombo?>(null);
        return false;
    }

    public void CancelHotkeyCapture() => this.hotkeyCaptureCoordinator.CancelCapture();

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
                await this.SendAutoWeaponSwapConfigAsync();
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

    private async Task SendAutoWeaponSwapConfigAsync()
    {
        try
        {
            if (this.ipc is null || this.config is null) return;

            var autoWeaponSwap = this.config.AutoWeaponSwap;
            var message = new AutoWeaponSwapConfigMessage(
                autoWeaponSwap.Enabled,
                (int)autoWeaponSwap.Key.Key,
                autoWeaponSwap.IntervalMs);

            await this.ipc.SendAsync(message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR sending auto weapon swap config: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
    
    private void StartMainHook()
    {
        if (this.hook is not null) return;

        this.isDisposingHook = false;
        this.ResetKeyboardState();
        var hook = new EventLoopGlobalHook(GlobalHookType.Keyboard);
        this.hook = hook;

        hook.KeyPressed += (_, args) =>
        {
            if (this.isDisposingHook) return;

            var keyCode = args.Data.KeyCode;
            if (!this.keyPressTracker.TryPress(keyCode)) return;

            var isCtrlPressed = this.keyPressTracker.IsControlPressed;
            var isShiftPressed = this.keyPressTracker.IsShiftPressed;
            var isAltPressed = this.keyPressTracker.IsAltPressed;

            if (this.hotkeyCaptureCoordinator.HandleKeyPressed(
                    keyCode,
                    isCtrlPressed,
                    isShiftPressed,
                    isAltPressed)) return;

            if (KeyPressTracker.IsModifierKey(keyCode)) return;

            if (this.isConfigWindowOpen) return;

            _ = this.HandleHotkeyPressedAsync(keyCode, isCtrlPressed, isShiftPressed, isAltPressed);
        };

        hook.KeyReleased += (_, args) =>
        {
            if (this.isDisposingHook) return;

            var keyCode = args.Data.KeyCode;
            this.keyPressTracker.Release(keyCode);

            if (!KeyPressTracker.IsModifierKey(keyCode) || this.isConfigWindowOpen) return;
            if (this.pendingChatCommand is null || !this.keyPressTracker.AreModifiersReleased) return;

            var command = this.pendingChatCommand;
            this.pendingChatCommand = null;
            _ = this.SendChatCommandAsync(command);
        };

        _ = RunMainHookAsync(hook);
    }

    private static async Task RunMainHookAsync(IGlobalHook hook)
    {
        try
        {
            await hook.RunAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Hook crashed: {ex.Message}");
        }
    }

    private async Task HandleHotkeyPressedAsync(KeyCode keyCode, bool ctrl, bool shift, bool alt)
    {
        try
        {
            if (this.config?.LogoutHotkey?.Matches(keyCode, ctrl, shift, alt) == true)
            {
                if (this.ipc is null) return;
                await this.ipc.SendAsync(new ForceLogoutMessage());
            }
            else if (this.config?.OpenSettingsHotkey?.Matches(keyCode, ctrl, shift, alt) == true)
            {
                await this.OpenConfiguration();
            }
            else if (this.config?.AutoWeaponSwap?.ToggleHotkey?.Matches(keyCode, ctrl, shift, alt) == true)
            {
                await this.ToggleAutoWeaponSwapAsync();
            }
            else
            {
                var chatCommandHotkey = this.FindMatchingChatCommandHotkey(keyCode, ctrl, shift, alt);
                if (chatCommandHotkey is not null)
                {
                    await this.SendOrDeferChatCommand(chatCommandHotkey);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling hotkey: {ex.Message}");
        }
    }

    private ChatCommandHotkey? FindMatchingChatCommandHotkey(KeyCode keyCode, bool ctrl, bool shift, bool alt) =>
        this.config is null
            ? null
            : ChatCommandHotkey.FindMatching(
                this.config.GetChatCommandHotkeys(),
                keyCode,
                ctrl,
                shift,
                alt);

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

        try
        {
            await this.ipc.SendAsync(command);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending chat command: {ex.Message}");
        }
    }

    private async Task ToggleAutoWeaponSwapAsync()
    {
        if (this.config is null) return;

        try
        {
            this.config.AutoWeaponSwap.ToggleEnabled();
            await ConfigurationManager.SaveAsync(this.config);
            await this.SendAutoWeaponSwapConfigAsync();

            NotificationManager.SendInfo(this.config.AutoWeaponSwap.Enabled
                ? "Auto weapon swap enabled"
                : "Auto weapon swap disabled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error toggling auto weapon swap: {ex.Message}");
            NotificationManager.SendError("Error toggling auto weapon swap");
        }
    }

    private void ResetKeyboardState()
    {
        this.keyPressTracker.Clear();
        this.pendingChatCommand = null;
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
            this.configWindow.Closed += (_, _) => _ = this.OnConfigurationClosedAsync();
            this.configWindow.Show();
        });
    }

    private async Task OnConfigurationClosedAsync()
    {
        this.configWindow = null;
        this.hotkeyCaptureCoordinator.CancelCapture();
        this.config = await ConfigurationManager.LoadAsync();
        await this.SendAutoWeaponSwapConfigAsync();
        this.isConfigWindowOpen = false;
    }

    private void ExitAction(object? sender, EventArgs e)
    {
        this.isShuttingDown = true;
        this.isDisposingHook = true;
        this.hotkeyCaptureCoordinator.CancelCapture();
        this.ResetKeyboardState();

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
