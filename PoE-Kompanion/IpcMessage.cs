namespace PoEKompanion;

using System.Collections.Generic;

using MessagePack;

[MessagePackObject]
[Union(0, typeof(ForceLogoutMessage))]
[Union(1, typeof(NotificationMessage))]
[Union(2, typeof(SetAlwaysOnTopMessage))]
[Union(3, typeof(PoEHookedMessage))]
[Union(4, typeof(PoEUnhookedMessage))]
[Union(5, typeof(ChatCommandMessage))]
[Union(6, typeof(KeyboardLayoutMapMessage))]
[Union(7, typeof(BackgroundReadyMessage))]
[Union(8, typeof(ModifierStateMessage))]
[Union(9, typeof(AutoWeaponSwapConfigMessage))]
public abstract record IpcMessage;

[MessagePackObject]
public sealed record ForceLogoutMessage : IpcMessage;

[MessagePackObject]
public sealed record ChatCommandMessage(
    [property: Key(0)] string Command,
    [property: Key(1)] int MinimumSubmitDelayMs = 0
) : IpcMessage;

[MessagePackObject]
public sealed record NotificationMessage(
    [property: Key(0)] string Message,
    [property: Key(1)] bool IsError,
    [property: Key(2)] string Title = "PoE Kompanion"
) : IpcMessage;

[MessagePackObject]
public sealed record SetAlwaysOnTopMessage(
    [property: Key(0)] int ProcessId
) : IpcMessage;


[MessagePackObject]
public sealed record PoEHookedMessage(
    [property: Key(0)] int ProcessId
) : IpcMessage;

[MessagePackObject]
public sealed record PoEUnhookedMessage : IpcMessage;

[MessagePackObject]
public sealed record KeyboardLayoutMapMessage(
    [property: Key(0)] CharKeyMapping[] LayoutMap
) : IpcMessage;

[MessagePackObject]
public sealed record CharKeyMapping(
    [property: Key(0)] char Character,
    [property: Key(1)] int Keycode,
    [property: Key(2)] bool Shift
);

[MessagePackObject]
public sealed record KeycodMapping(
    [property: Key(0)] int Keycode,
    [property: Key(1)] bool Shift
);

[MessagePackObject]
public sealed record BackgroundReadyMessage : IpcMessage;

[MessagePackObject]
public sealed record ModifierStateMessage(
    [property: Key(0)] bool AllModifiersReleased
) : IpcMessage;

[MessagePackObject]
public sealed record AutoWeaponSwapConfigMessage(
    [property: Key(0)] bool Enabled,
    [property: Key(1)] int KeyCode,
    [property: Key(2)] int BaseIntervalMs
) : IpcMessage;
