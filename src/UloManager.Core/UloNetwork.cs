using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace UloManager.Core;

/// <summary>
/// Local network helpers.
///
/// <para>
/// The camera's identifier is <c>ulo_</c> plus the last four hex digits of its MAC address - what
/// <c>GET /api/v1/accessEverywhere</c> calls <c>trimmedMac</c>. That endpoint only exists from
/// firmware 08.0904 onwards and answers <c>404</c> on 06.0601, so on older cameras the same
/// identifier is worked out from the MAC address the machine already knows from the local network.
/// Verified against a live pair: <c>00-50-c2-bd-ab-12</c> is the camera that reports
/// <c>"trimmedMac": "ab12"</c>.
/// </para>
/// </summary>
public static class UloNetwork
{
    /// <summary>
    /// Builds the <c>ulo_xxxx</c> identifier from the camera's MAC address, or returns null when the
    /// camera is not on the local network or the platform cannot answer.
    /// </summary>
    public static string? TryGetDeviceIdFromMac(string hostOrAddress)
    {
        var mac = TryGetMacAddress(hostOrAddress);
        return mac is null || mac.Length < 2
            ? null
            : $"ulo_{mac[^2]:x2}{mac[^1]:x2}";
    }

    /// <summary>The MAC address of a host on the local network, or null when it cannot be resolved.</summary>
    public static byte[]? TryGetMacAddress(string hostOrAddress)
    {
        var address = ResolveIPv4(hostOrAddress);
        if (address is null)
        {
            return null;
        }

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var destination = BitConverter.ToUInt32(address.GetAddressBytes(), 0);
            var mac = new byte[6];
            var length = (uint)mac.Length;

            return SendARP(destination, 0, mac, ref length) == 0 && length >= 6 ? mac : null;
        }
        catch (Exception)
        {
            // The entry point is missing or the call failed - the identifier is optional.
            return null;
        }
    }

    private static IPAddress? ResolveIPv4(string hostOrAddress)
    {
        if (string.IsNullOrWhiteSpace(hostOrAddress))
        {
            return null;
        }

        var host = hostOrAddress.Trim();
        if (host.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            host.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            host = new Uri(host).Host;
        }

        if (IPAddress.TryParse(host, out var parsed))
        {
            return parsed.AddressFamily == AddressFamily.InterNetwork ? parsed : null;
        }

        try
        {
            return Array.Find(Dns.GetHostAddresses(host), a => a.AddressFamily == AddressFamily.InterNetwork);
        }
        catch (Exception ex) when (ex is SocketException or ArgumentException)
        {
            return null;
        }
    }

    [DllImport("iphlpapi.dll", ExactSpelling = true)]
    private static extern int SendARP(uint destIp, uint srcIp, byte[] macAddr, ref uint physicalAddrLen);
}
