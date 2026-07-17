namespace PoEKompanion;

using System.Collections.Generic;
using SharpHook.Data;

public sealed class KeyPressTracker
{
    private readonly object sync = new();
    private readonly HashSet<KeyCode> pressedKeys = [];

    public bool IsControlPressed => this.IsEitherPressed(KeyCode.VcLeftControl, KeyCode.VcRightControl);

    public bool IsShiftPressed => this.IsEitherPressed(KeyCode.VcLeftShift, KeyCode.VcRightShift);

    public bool IsAltPressed => this.IsEitherPressed(KeyCode.VcLeftAlt, KeyCode.VcRightAlt);

    public bool AreModifiersReleased
    {
        get
        {
            lock (this.sync)
            {
                return !this.pressedKeys.Contains(KeyCode.VcLeftControl) &&
                       !this.pressedKeys.Contains(KeyCode.VcRightControl) &&
                       !this.pressedKeys.Contains(KeyCode.VcLeftShift) &&
                       !this.pressedKeys.Contains(KeyCode.VcRightShift) &&
                       !this.pressedKeys.Contains(KeyCode.VcLeftAlt) &&
                       !this.pressedKeys.Contains(KeyCode.VcRightAlt);
            }
        }
    }

    public bool TryPress(KeyCode keyCode)
    {
        lock (this.sync)
        {
            return this.pressedKeys.Add(keyCode);
        }
    }

    public void Release(KeyCode keyCode)
    {
        lock (this.sync)
        {
            this.pressedKeys.Remove(keyCode);
        }
    }

    public void Clear()
    {
        lock (this.sync)
        {
            this.pressedKeys.Clear();
        }
    }

    public static bool IsModifierKey(KeyCode keyCode) =>
        keyCode is KeyCode.VcLeftControl or KeyCode.VcRightControl or
                   KeyCode.VcLeftShift or KeyCode.VcRightShift or
                   KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
                   KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    private bool IsEitherPressed(KeyCode leftKeyCode, KeyCode rightKeyCode)
    {
        lock (this.sync)
        {
            return this.pressedKeys.Contains(leftKeyCode) || this.pressedKeys.Contains(rightKeyCode);
        }
    }
}
