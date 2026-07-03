namespace PoEKompanion.Views;

using System;
using Avalonia.Controls;
using Avalonia.Input;

public partial class InputBlockerOverlay : Window
{
    private readonly Window? parentWindow;

    public InputBlockerOverlay()
    {
        this.InitializeComponent();

        this.Opened += (_, _) =>
        {
            _ = System.Threading.Tasks.Task.Run(() =>
            {
                var currentPid = Environment.ProcessId;
                WindowManager.SetWindowSkipTaskbar(currentPid, "Input Blocker");
            });
        };
    }

    public InputBlockerOverlay(Window parentWindow) : this()
    {
        this.parentWindow = parentWindow;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        this.parentWindow?.Activate();
        e.Handled = true;
    }

    public bool PositionOverPoEWindow(int poeProcessId)
    {
        var bounds = WindowManager.GetWindowBounds(poeProcessId);
        if (bounds is null || bounds.Value.Width <= 0 || bounds.Value.Height <= 0) return false;

        var value = bounds.Value;
        this.Position = new Avalonia.PixelPoint(value.X, value.Y);
        this.Width = value.Width;
        this.Height = value.Height;

        return true;
    }
}
