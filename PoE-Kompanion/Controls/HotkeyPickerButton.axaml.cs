namespace PoEKompanion.Controls;

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SharpHook.Data;

public partial class HotkeyPickerButton : UserControl, INotifyPropertyChanged
{
    private bool isListening;

    public new event PropertyChangedEventHandler? PropertyChanged;

    public static readonly StyledProperty<HotkeyCombo?> SelectedHotkeyProperty =
        AvaloniaProperty.Register<HotkeyPickerButton, HotkeyCombo?>(
            nameof(SelectedHotkey),
            new HotkeyCombo(KeyCode.VcBackQuote),
            defaultBindingMode: BindingMode.TwoWay);

    public HotkeyCombo? SelectedHotkey
    {
        get => this.GetValue(SelectedHotkeyProperty);
        set => this.SetValue(SelectedHotkeyProperty, value);
    }

    static HotkeyPickerButton()
    {
        SelectedHotkeyProperty.Changed.AddClassHandler<HotkeyPickerButton>((control, args) =>
        {
            control.OnPropertyChanged(nameof(KeyDisplayName));
        });
    }

    public bool IsListening
    {
        get => this.isListening;
        private set
        {
            if (this.isListening == value) return;
            this.isListening = value;
            this.OnPropertyChanged();
            this.UpdateButtonStyle();
        }
    }

    private void UpdateButtonStyle()
    {
        if (this.pickerButton is null) return;

        if (this.isListening)
        {
            this.pickerButton.Classes.Add("listening");
        }
        else
        {
            this.pickerButton.Classes.Remove("listening");
        }
    }

    public string KeyDisplayName => this.SelectedHotkey?.ToString() ?? "None";

    public HotkeyPickerButton()
    {
        this.InitializeComponent();
        this.pickerButton = this.FindControl<Button>("PickerButton") ?? throw new InvalidOperationException("PickerButton not found");
    }

    private Button? pickerButton;

    private void OnButtonClick(object? sender, RoutedEventArgs e) => _ = this.StartListening();

    private async Task StartListening()
    {
        if (this.IsListening) return;

        var app = App.Instance;
        if (app is null || !app.TryStartHotkeyCapture(out var captureTask)) return;

        this.IsListening = true;

        try
        {
            var capturedHotkey = await captureTask;
            if (capturedHotkey is not null)
            {
                await Dispatcher.UIThread.InvokeAsync(() => this.SelectedHotkey = capturedHotkey);
            }
        }
        finally
        {
            await Dispatcher.UIThread.InvokeAsync(() => this.IsListening = false);
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
