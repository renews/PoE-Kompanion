namespace PoEKompanion;

using System;
using System.Buffers;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using MessagePack;

public sealed class UnixSocketIpc : IDisposable
{
    private readonly Socket socket;
    private readonly string? localPath;
    private readonly UnixDomainSocketEndPoint? remoteEndPoint;

    private UnixSocketIpc(Socket socket, string? localPath, UnixDomainSocketEndPoint? remoteEndPoint)
    {
        this.socket = socket;
        this.localPath = localPath;
        this.remoteEndPoint = remoteEndPoint;
    }

    public static string GetServerSocketPath(string? runtimeDirectory = null, string? userName = null) =>
        Path.Combine(GetSocketDirectory(runtimeDirectory, userName), "server.sock");

    public static string GetClientSocketPath(string? runtimeDirectory = null, string? userName = null) =>
        Path.Combine(GetSocketDirectory(runtimeDirectory, userName), "client.sock");

    public static async Task<UnixSocketIpc> CreateServerAsync()
    {
        var serverSocketPath = GetServerSocketPath();
        var clientSocketPath = GetClientSocketPath();
        EnsureSocketDirectory(serverSocketPath);

        if (File.Exists(serverSocketPath))
        {
            File.Delete(serverSocketPath);
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(serverSocketPath));

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var serverSocketInfo = new FileInfo(serverSocketPath);
            serverSocketInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        while (!File.Exists(clientSocketPath))
        {
            await Task.Yield();
        }

        var clientEndPoint = new UnixDomainSocketEndPoint(clientSocketPath);
        return new UnixSocketIpc(socket, serverSocketPath, clientEndPoint);
    }

    public static async Task<UnixSocketIpc> CreateClientAsync()
    {
        var serverSocketPath = GetServerSocketPath();
        var clientSocketPath = GetClientSocketPath();
        EnsureSocketDirectory(clientSocketPath);

        if (File.Exists(clientSocketPath))
        {
            File.Delete(clientSocketPath);
        }

        var socket = new Socket(AddressFamily.Unix, SocketType.Dgram, ProtocolType.Unspecified);
        socket.Bind(new UnixDomainSocketEndPoint(clientSocketPath));

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var clientSocketInfo = new FileInfo(clientSocketPath);
            clientSocketInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        while (!File.Exists(serverSocketPath))
        {
            await Task.Yield();
        }

        var serverEndPoint = new UnixDomainSocketEndPoint(serverSocketPath);
        return new UnixSocketIpc(socket, clientSocketPath, serverEndPoint);
    }

    public async Task SendAsync(IpcMessage message, CancellationToken cancellationToken = default)
    {
        if (this.remoteEndPoint is null) return;

        var buffer = MessagePackSerializer.Serialize(message, cancellationToken: cancellationToken);
        Console.WriteLine($"Sending IPC message: {message.GetType().Name} ({buffer.Length} bytes)");

        if (buffer.Length > 8192)
        {
            Console.WriteLine($"WARNING: Message size {buffer.Length} bytes exceeds typical datagram limits!");
        }

        await this.socket.SendToAsync(buffer, SocketFlags.None, this.remoteEndPoint, cancellationToken);
    }

    public async Task<IpcMessage?> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[16384];
        var received = await this.socket.ReceiveAsync(buffer, SocketFlags.None, cancellationToken);

        if (received == 0) return null;

        Console.WriteLine($"Received {received} bytes via IPC");
        return MessagePackSerializer.Deserialize<IpcMessage>(buffer.AsMemory(0, received), cancellationToken: cancellationToken);
    }

    public void Dispose()
    {
        this.socket.Dispose();

        if (this.localPath is not null && File.Exists(this.localPath))
        {
            File.Delete(this.localPath);
        }
    }

    private static string GetSocketDirectory(string? runtimeDirectory = null, string? userName = null)
    {
        runtimeDirectory ??= Environment.GetEnvironmentVariable("XDG_RUNTIME_DIR");
        if (!string.IsNullOrWhiteSpace(runtimeDirectory))
        {
            return Path.Combine(runtimeDirectory, "poe-kompanion");
        }

        userName ??= CurrentUser.GetName();
        if (string.IsNullOrWhiteSpace(userName))
        {
            userName = CurrentUser.GetEffectiveUserIdSegment();
        }

        return Path.Combine(Path.GetTempPath(), $"poe-kompanion-{userName}");
    }

    private static void EnsureSocketDirectory(string socketPath)
    {
        var directory = Path.GetDirectoryName(socketPath);
        if (directory is null) return;

        Directory.CreateDirectory(directory);
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            // ReSharper disable once UseObjectOrCollectionInitializer
            var directoryInfo = new DirectoryInfo(directory);
            directoryInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        }
    }
}
