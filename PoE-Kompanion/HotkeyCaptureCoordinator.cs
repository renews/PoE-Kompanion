namespace PoEKompanion;

using System.Threading.Tasks;
using SharpHook.Data;

public sealed class HotkeyCaptureCoordinator
{
    private readonly object sync = new();
    private TaskCompletionSource<HotkeyCombo?>? captureCompletion;

    public bool TryStartCapture(out Task<HotkeyCombo?> captureTask)
    {
        lock (this.sync)
        {
            if (this.captureCompletion is not null)
            {
                captureTask = Task.FromResult<HotkeyCombo?>(null);
                return false;
            }

            this.captureCompletion = new TaskCompletionSource<HotkeyCombo?>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            captureTask = this.captureCompletion.Task;
            return true;
        }
    }

    public bool HandleKeyPressed(KeyCode keyCode, bool ctrl, bool shift, bool alt)
    {
        TaskCompletionSource<HotkeyCombo?>? completion;
        HotkeyCombo? capturedHotkey;

        lock (this.sync)
        {
            if (this.captureCompletion is null) return false;
            if (KeyPressTracker.IsModifierKey(keyCode)) return true;

            completion = this.captureCompletion;
            this.captureCompletion = null;
            capturedHotkey = keyCode == KeyCode.VcEscape
                ? null
                : new HotkeyCombo(keyCode, ctrl, shift, alt);
        }

        completion.TrySetResult(capturedHotkey);
        return true;
    }

    public void CancelCapture()
    {
        TaskCompletionSource<HotkeyCombo?>? completion;

        lock (this.sync)
        {
            completion = this.captureCompletion;
            this.captureCompletion = null;
        }

        completion?.TrySetResult(null);
    }
}
