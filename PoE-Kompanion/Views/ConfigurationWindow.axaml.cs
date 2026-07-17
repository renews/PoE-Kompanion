namespace PoEKompanion.Views;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using SharpHook.Data;

public partial class ConfigurationWindow : Window, INotifyPropertyChanged
{
    public new event PropertyChangedEventHandler? PropertyChanged;

    private ConfigurationModel currentConfig;
    private InputBlockerOverlay? overlay;
    private CancellationTokenSource? pollCancellation;

    public HotkeyCombo? LogoutHotkey
    {
        get => this.currentConfig.LogoutHotkey;
        set
        {
            if (this.currentConfig.LogoutHotkey == value) return;
            this.currentConfig.LogoutHotkey = value ?? new HotkeyCombo(KeyCode.VcBackQuote);
            this.OnPropertyChanged();
        }
    }

    public HotkeyCombo? OpenSettingsHotkey
    {
        get => this.currentConfig.OpenSettingsHotkey;
        set
        {
            if (this.currentConfig.OpenSettingsHotkey == value) return;
            this.currentConfig.OpenSettingsHotkey = value ?? new HotkeyCombo(KeyCode.VcF10);
            this.OnPropertyChanged();
        }
    }

    public HotkeyCombo? HideoutHotkey
    {
        get => this.currentConfig.HideoutHotkey;
        set
        {
            if (this.currentConfig.HideoutHotkey == value) return;
            this.currentConfig.HideoutHotkey = value ?? new HotkeyCombo(KeyCode.VcF5);
            this.OnPropertyChanged();
        }
    }

    public HotkeyCombo? ExitHotkey
    {
        get => this.currentConfig.ExitHotkey;
        set
        {
            if (this.currentConfig.ExitHotkey == value) return;
            this.currentConfig.ExitHotkey = value ?? new HotkeyCombo(KeyCode.VcSpace, ctrl: true, shift: true);
            this.OnPropertyChanged();
        }
    }

    public HotkeyCombo? AutoWeaponSwapKey
    {
        get => this.currentConfig.AutoWeaponSwap.Key;
        set
        {
            var nextKey = value ?? AutoWeaponSwapSettings.GetDefault().Key;
            if (this.currentConfig.AutoWeaponSwap.Key == nextKey) return;

            this.currentConfig.AutoWeaponSwap.Key = nextKey;
            this.OnPropertyChanged();
        }
    }

    public HotkeyCombo? AutoWeaponSwapToggleHotkey
    {
        get => this.currentConfig.AutoWeaponSwap.ToggleHotkey;
        set
        {
            var nextHotkey = value ?? AutoWeaponSwapSettings.GetDefault().ToggleHotkey;
            if (this.currentConfig.AutoWeaponSwap.ToggleHotkey == nextHotkey) return;

            this.currentConfig.AutoWeaponSwap.ToggleHotkey = nextHotkey;
            this.OnPropertyChanged();
        }
    }

    public double AutoWeaponSwapIntervalMs
    {
        get => this.currentConfig.AutoWeaponSwap.IntervalMs;
        set
        {
            var nextIntervalMs = AutoWeaponSwapSettings.ClampInterval((int)Math.Round(value));
            if (this.currentConfig.AutoWeaponSwap.IntervalMs == nextIntervalMs) return;

            this.currentConfig.AutoWeaponSwap.IntervalMs = nextIntervalMs;
            this.OnPropertyChanged();
            this.OnPropertyChanged(nameof(this.AutoWeaponSwapIntervalLabel));
        }
    }

    public string AutoWeaponSwapIntervalLabel => $"{this.currentConfig.AutoWeaponSwap.IntervalMs} ms";

    public HotkeyCombo? ChatCommand1Hotkey
    {
        get => this.GetChatCommandHotkey(0);
        set => this.SetChatCommandHotkey(0, value);
    }

    public string ChatCommand1Command
    {
        get => this.GetChatCommandCommand(0);
        set => this.SetChatCommandCommand(0, value);
    }

    public HotkeyCombo? ChatCommand2Hotkey
    {
        get => this.GetChatCommandHotkey(1);
        set => this.SetChatCommandHotkey(1, value);
    }

    public string ChatCommand2Command
    {
        get => this.GetChatCommandCommand(1);
        set => this.SetChatCommandCommand(1, value);
    }

    public HotkeyCombo? ChatCommand3Hotkey
    {
        get => this.GetChatCommandHotkey(2);
        set => this.SetChatCommandHotkey(2, value);
    }

    public string ChatCommand3Command
    {
        get => this.GetChatCommandCommand(2);
        set => this.SetChatCommandCommand(2, value);
    }

    public HotkeyCombo? ChatCommand4Hotkey
    {
        get => this.GetChatCommandHotkey(3);
        set => this.SetChatCommandHotkey(3, value);
    }

    public string ChatCommand4Command
    {
        get => this.GetChatCommandCommand(3);
        set => this.SetChatCommandCommand(3, value);
    }

    public HotkeyCombo? ChatCommand5Hotkey
    {
        get => this.GetChatCommandHotkey(4);
        set => this.SetChatCommandHotkey(4, value);
    }

    public string ChatCommand5Command
    {
        get => this.GetChatCommandCommand(4);
        set => this.SetChatCommandCommand(4, value);
    }


    public ConfigurationWindow()
    {
        this.currentConfig = new ConfigurationModel();

        this.InitializeComponent();
        this.DataContext = this;

        _ = this.LoadConfiguration();

        this.Opened += (_, _) =>
        {
            this.Measure(Size.Infinity);
            this.PositionOverPoE();

            EnsureAlwaysOnTop();
            this.ShowOverlayIfGameDetected();
            this.StartPollingForGame();
        };

        this.Closing += (_, _) =>
        {
            App.Instance?.CancelHotkeyCapture();
            this.pollCancellation?.Cancel();
            this.pollCancellation?.Dispose();
            this.overlay?.Close();
            this.overlay = null;
        };
    }

    private void PositionOverPoE()
    {
        var poeProcessId = App.Instance?.GetPoEProcessId();
        if (!poeProcessId.HasValue) return;

        var bounds = WindowManager.GetWindowBounds(poeProcessId.Value);
        if (!bounds.HasValue) return;

        var windowWidth = (int)this.DesiredSize.Width;
        var windowHeight = (int)this.DesiredSize.Height;

        var centerX = bounds.Value.X + (bounds.Value.Width - windowWidth) / 2;
        var centerY = bounds.Value.Y + (bounds.Value.Height - windowHeight) / 2;

        this.Position = new Avalonia.PixelPoint(centerX, centerY);
    }

    private void ShowOverlayIfGameDetected()
    {
        if (this.overlay is not null) return;

        var poeProcessId = App.Instance?.GetPoEProcessId();
        if (!poeProcessId.HasValue) return;

        var overlay = new InputBlockerOverlay(this);
        if (!overlay.PositionOverPoEWindow(poeProcessId.Value)) return;

        this.overlay = overlay;
        this.overlay.Show();

        this.Activate();
    }

    private void StartPollingForGame()
    {
        this.pollCancellation = new CancellationTokenSource();
        var token = this.pollCancellation.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(100, token);
                if (this.overlay is not null) continue;

                var poeProcessId = App.Instance?.GetPoEProcessId();
                if (!poeProcessId.HasValue) continue;

                WindowManager.TrySetAlwaysOnTop(poeProcessId.Value);

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    this.ShowOverlayIfGameDetected();
                    this.PositionOverPoE();
                });

                EnsureAlwaysOnTop();
            }
        }, token);
    }

    public static void EnsureAlwaysOnTop()
    {
        _ = Task.Run(() =>
        {
            var currentPid = Environment.ProcessId;
            WindowManager.TryBringToFront(currentPid);
        });
    }

    private async Task LoadConfiguration()
    {
        this.currentConfig = await ConfigurationManager.LoadAsync();
        this.OnPropertyChanged(nameof(this.LogoutHotkey));
        this.OnPropertyChanged(nameof(this.OpenSettingsHotkey));
        this.OnPropertyChanged(nameof(this.HideoutHotkey));
        this.OnPropertyChanged(nameof(this.ExitHotkey));
        this.OnPropertyChanged(nameof(this.AutoWeaponSwapKey));
        this.OnPropertyChanged(nameof(this.AutoWeaponSwapToggleHotkey));
        this.OnPropertyChanged(nameof(this.AutoWeaponSwapIntervalMs));
        this.OnPropertyChanged(nameof(this.AutoWeaponSwapIntervalLabel));
        this.OnPropertyChanged(nameof(this.ChatCommand1Hotkey));
        this.OnPropertyChanged(nameof(this.ChatCommand1Command));
        this.OnPropertyChanged(nameof(this.ChatCommand2Hotkey));
        this.OnPropertyChanged(nameof(this.ChatCommand2Command));
        this.OnPropertyChanged(nameof(this.ChatCommand3Hotkey));
        this.OnPropertyChanged(nameof(this.ChatCommand3Command));
        this.OnPropertyChanged(nameof(this.ChatCommand4Hotkey));
        this.OnPropertyChanged(nameof(this.ChatCommand4Command));
        this.OnPropertyChanged(nameof(this.ChatCommand5Hotkey));
        this.OnPropertyChanged(nameof(this.ChatCommand5Command));
    }
    
    private async Task SaveConfiguration()
    {
        try
        {
            this.currentConfig.AutoWeaponSwap = AutoWeaponSwapSettings.Normalize(this.currentConfig.AutoWeaponSwap);
            await ConfigurationManager.SaveAsync(this.currentConfig);
            this.Close();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving configuration: {ex.Message}");
            NotificationManager.SendError("Error saving configuration");
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => _ = this.SaveConfiguration();

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        this.Close();
    }

    private HotkeyCombo? GetChatCommandHotkey(int index) => this.currentConfig.ChatCommandHotkeys[index].Hotkey;

    private void SetChatCommandHotkey(int index, HotkeyCombo? value, [CallerMemberName] string? propertyName = null)
    {
        var nextHotkey = value ?? ChatCommandHotkey.GetDefaults()[index].Hotkey;
        if (this.currentConfig.ChatCommandHotkeys[index].Hotkey == nextHotkey) return;

        this.currentConfig.ChatCommandHotkeys[index].Hotkey = nextHotkey;
        this.OnPropertyChanged(propertyName);
    }

    private string GetChatCommandCommand(int index) => this.currentConfig.ChatCommandHotkeys[index].Command;

    private void SetChatCommandCommand(int index, string? value, [CallerMemberName] string? propertyName = null)
    {
        var nextCommand = value ?? string.Empty;
        if (this.currentConfig.ChatCommandHotkeys[index].Command == nextCommand) return;

        this.currentConfig.ChatCommandHotkeys[index].Command = nextCommand;
        this.OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
