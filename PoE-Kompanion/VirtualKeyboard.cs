namespace PoEKompanion;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

internal sealed class VirtualKeyboard : IDisposable
{
    private const int UINPUT_MAX_NAME_SIZE = 80;
    private const uint UI_SET_EVBIT = 0x40045564;
    private const uint UI_SET_KEYBIT = 0x40045565;
    private const uint UI_DEV_SETUP = 0x405C5503;
    private const uint UI_DEV_CREATE = 0x5501;
    private const uint UI_DEV_DESTROY = 0x5502;
    private const int EV_KEY = 0x01;
    private const int EV_SYN = 0x00;
    private const int SYN_REPORT = 0;
    private const int KEY_LEFTSHIFT = 42;
    private const int KEY_ENTER = 28;

    [DllImport("libc", SetLastError = true)]
    private static extern int open(string pathname, int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int close(int fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, int value);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(int fd, uint request, ref UinputSetup setup);

    [DllImport("libc", SetLastError = true)]
    private static extern int write(int fd, byte[] buffer, int count);

    [DllImport("libc", SetLastError = true)]
    private static extern IntPtr strerror(int errnum);

    [StructLayout(LayoutKind.Sequential)]
    private struct InputEvent
    {
        public long TimeSec;
        public long TimeUsec;
        public ushort Type;
        public ushort Code;
        public int Value;
    }

    [StructLayout(LayoutKind.Sequential)]
    private unsafe struct UinputSetup
    {
        public UinputId Id;
        public fixed byte Name[UINPUT_MAX_NAME_SIZE];
        public uint FfEffectsMax;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct UinputId
    {
        public ushort BusType;
        public ushort Vendor;
        public ushort Product;
        public ushort Version;
    }

    private int virtualKeyboardFd = -1;
    private Dictionary<char, KeycodMapping>? layoutMap;

    public bool IsInitialized => this.virtualKeyboardFd >= 0;

    public void Initialize()
    {
        if (this.virtualKeyboardFd >= 0) return;

        const int O_WRONLY = 1;
        const int O_NONBLOCK = 0x800;

        this.virtualKeyboardFd = open("/dev/uinput", O_WRONLY | O_NONBLOCK);

        if (this.virtualKeyboardFd < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"Failed to open /dev/uinput: {errMsg} (errno={errno})");
            return;
        }

        ioctl(this.virtualKeyboardFd, UI_SET_EVBIT, EV_KEY);
        ioctl(this.virtualKeyboardFd, UI_SET_EVBIT, EV_SYN);

        for (var i = 0; i < 256; i++)
        {
            ioctl(this.virtualKeyboardFd, UI_SET_KEYBIT, i);
        }

        var nameStr = "PoE Kompanion Virtual Keyboard";
        var nameBytes = System.Text.Encoding.ASCII.GetBytes(nameStr);

        UinputSetup setup;
        setup.Id.BusType = 0x03;
        setup.Id.Vendor = 0x1234;
        setup.Id.Product = 0x5678;
        setup.Id.Version = 1;
        setup.FfEffectsMax = 0;

        unsafe
        {
            for (var i = 0; i < UINPUT_MAX_NAME_SIZE; i++)
            {
                setup.Name[i] = i < nameBytes.Length ? nameBytes[i] : (byte)0;
            }
        }

        var ret = ioctl(this.virtualKeyboardFd, UI_DEV_SETUP, ref setup);

        if (ret < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"UI_DEV_SETUP failed: {errMsg} (errno={errno})");
        }

        ret = ioctl(this.virtualKeyboardFd, UI_DEV_CREATE, 0);

        if (ret < 0)
        {
            var errno = Marshal.GetLastWin32Error();
            var errMsg = Marshal.PtrToStringAnsi(strerror(errno));
            Console.WriteLine($"UI_DEV_CREATE failed: {errMsg} (errno={errno})");
            close(this.virtualKeyboardFd);
            this.virtualKeyboardFd = -1;
            return;
        }

        Console.WriteLine("Virtual keyboard created successfully");
    }

    public void SetLayoutMap(CharKeyMapping[] layoutArray)
    {
        this.layoutMap = layoutArray.ToDictionary(
            m => m.Character,
            m => new KeycodMapping(m.Keycode, m.Shift)
        );
        Console.WriteLine($"Received layout map with {this.layoutMap.Count} character mappings");

        if (this.layoutMap.Count == 0)
        {
            Console.WriteLine("WARNING: Received empty layout map! Character input will not work.");
        }
    }

    public async Task SendChatCommandAsync(string command, int minimumSubmitDelayMs = 0)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        if (!this.IsInitialized)
        {
            Console.WriteLine("Virtual keyboard not available");
            return;
        }

        try
        {
            var startedAt = Stopwatch.StartNew();

            this.SendKey(KEY_ENTER, true);
            this.SendKey(KEY_ENTER, false);

            await Task.Delay(10);

            foreach (var c in command)
            {
                this.SendChar(c);
                await Task.Delay(1);
            }

            await Task.Delay(1);
            var remainingDelayMs = minimumSubmitDelayMs - (int)startedAt.ElapsedMilliseconds;
            if (remainingDelayMs > 0)
            {
                await Task.Delay(remainingDelayMs);
            }

            this.SendKey(KEY_ENTER, true);
            this.SendKey(KEY_ENTER, false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error sending chat command: {ex.Message}");
        }
    }

    private void SendKey(int keyCode, bool press)
    {
        var evt = new InputEvent
        {
            TimeSec = 0,
            TimeUsec = 0,
            Type = EV_KEY,
            Code = (ushort)keyCode,
            Value = press ? 1 : 0
        };

        var evtSize = Marshal.SizeOf<InputEvent>();
        var buffer = new InputEvent[1];
        buffer[0] = evt;

        var handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        var evtBytes = new byte[evtSize];
        Marshal.Copy(handle.AddrOfPinnedObject(), evtBytes, 0, evtSize);
        handle.Free();

        var ret = write(this.virtualKeyboardFd, evtBytes, evtBytes.Length);

        if (ret < 0)
        {
            Console.WriteLine($"Failed to write key event (keycode={keyCode}, press={press})");
        }

        var synEvt = new InputEvent
        {
            TimeSec = 0,
            TimeUsec = 0,
            Type = EV_SYN,
            Code = SYN_REPORT,
            Value = 0
        };

        buffer[0] = synEvt;
        handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        var synBytes = new byte[evtSize];
        Marshal.Copy(handle.AddrOfPinnedObject(), synBytes, 0, evtSize);
        handle.Free();

        write(this.virtualKeyboardFd, synBytes, synBytes.Length);
    }

    private void SendChar(char c)
    {
        var map = this.GetLayoutMap();

        if (!map.TryGetValue(c, out var mapping))
        {
            Console.WriteLine($"Warning: No keycode mapping for character '{c}'");
            return;
        }

        var keyCode = mapping.Keycode;
        var needsShift = mapping.Shift;

        if (needsShift)
        {
            this.SendKey(KEY_LEFTSHIFT, true);
        }

        this.SendKey(keyCode, true);
        this.SendKey(keyCode, false);

        if (needsShift)
        {
            this.SendKey(KEY_LEFTSHIFT, false);
        }
    }

    private Dictionary<char, KeycodMapping> GetLayoutMap()
    {
        if (this.layoutMap != null)
        {
            if (this.layoutMap.Count == 0)
            {
                Console.WriteLine("ERROR: Layout map is empty! This likely means xmodmap failed in the foreground process.");
            }
            return this.layoutMap;
        }

        Console.WriteLine("ERROR: Layout map not yet received from foreground process");
        return new Dictionary<char, KeycodMapping>();
    }

    public void Dispose()
    {
        if (this.virtualKeyboardFd >= 0)
        {
            ioctl(this.virtualKeyboardFd, UI_DEV_DESTROY, 0);
            close(this.virtualKeyboardFd);
            this.virtualKeyboardFd = -1;
        }
    }
}
