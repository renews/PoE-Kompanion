namespace PoEKompanion;

using System;
using System.Net;

public static class ProcNetTcpAddressParser
{
    public static string HexToIp(string hex)
    {
        return hex.Length == 32 ? HexToIpV6(hex) : HexToIpV4(hex);
    }

    public static bool IsRemoteAddress(string address)
    {
        if (!IPAddress.TryParse(address, out var ipAddress)) return true;

        return !IPAddress.IsLoopback(ipAddress) &&
               !ipAddress.Equals(IPAddress.Any) &&
               !ipAddress.Equals(IPAddress.IPv6Any);
    }

    private static string HexToIpV4(string hex)
    {
        var bytes = new byte[4];
        for (var i = 0; i < 4; ++i)
        {
            bytes[3 - i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }

        return new IPAddress(bytes).ToString();
    }

    private static string HexToIpV6(string hex)
    {
        var bytes = new byte[16];
        for (var wordIndex = 0; wordIndex < 4; ++wordIndex)
        {
            var wordOffset = wordIndex * 8;
            for (var byteIndex = 0; byteIndex < 4; ++byteIndex)
            {
                var sourceOffset = wordOffset + (3 - byteIndex) * 2;
                bytes[wordIndex * 4 + byteIndex] = Convert.ToByte(hex.Substring(sourceOffset, 2), 16);
            }
        }

        return new IPAddress(bytes).ToString();
    }
}
