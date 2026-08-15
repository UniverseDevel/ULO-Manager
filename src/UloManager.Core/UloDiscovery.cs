using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace UloManager.Core;

/// <summary>
/// Discovers ULO cameras on the local network by probing <c>/api/v1/state</c> on every IP
/// in each connected subnet. No credentials are needed — the state endpoint is unauthenticated.
/// </summary>
public static class UloDiscovery
{
    /// <summary>Result of discovering a single ULO camera.</summary>
    public sealed record UloFoundDevice(
        IPAddress Address,
        UloState State,
        UloFirmwareVersion Firmware,
        string? DeviceName,
        string? Hostname,
        TimeSpan ResponseTime)
    {
        /// <summary>Short display label for dropdowns: "192.0.2.10 — ulo.local — "My ULO" (08.0904)"</summary>
        public string DisplayLabel
        {
            get
            {
                var parts = new List<string> { Address.ToString() };
                if (!string.IsNullOrEmpty(Hostname))
                    parts.Add(Hostname);
                if (!string.IsNullOrEmpty(DeviceName))
                    parts.Add($"\"{DeviceName}\"");
                if (Firmware.IsKnown)
                    parts.Add($"fw {Firmware.Raw}");
                return string.Join("  —  ", parts);
            }
        }

        public override string ToString() =>
            $"{Address,-16} " +
            $"{(string.IsNullOrEmpty(Hostname) ? "" : $"({Hostname}) ")}" +
            $"firmware {(Firmware.IsKnown ? Firmware.Raw : "?")} " +
            $"battery {State.BatteryLevel}% " +
            $"{(State.Plugged ? "plugged" : "battery")} " +
            $"{(State.InSetupMode ? "SETUP" : "usage")} " +
            $"{(string.IsNullOrEmpty(DeviceName) ? "" : $"\"{DeviceName}\" ")}" +
            $"({ResponseTime.TotalMilliseconds:F0} ms)";
    }

    /// <summary>
    /// Scans all local /24 subnets for ULO cameras.
    /// </summary>
    /// <param name="progress">Called for each camera found during the scan.</param>
    /// <param name="timeout">TCP + HTTP timeout per host.</param>
    /// <param name="maxParallelism">How many hosts to probe at once.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task<IReadOnlyList<UloFoundDevice>> ScanAsync(
        Action<UloFoundDevice>? progress = null,
        TimeSpan? timeout = null,
        int maxParallelism = 80,
        CancellationToken ct = default)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(2);
        var subnets = GetLocalSubnets();
        var candidates = subnets.SelectMany(ExpandSubnet).Distinct().ToList();

        var found = new List<UloFoundDevice>();
        var lockObj = new object();

        using var semaphore = new SemaphoreSlim(maxParallelism);
        var tasks = candidates.Select(async ip =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var result = await ProbeAsync(ip, effectiveTimeout, ct).ConfigureAwait(false);
                if (result is not null)
                {
                    lock (lockObj)
                    {
                        found.Add(result);
                    }

                    progress?.Invoke(result);
                }
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return found.OrderBy(d => BitConverter.ToUInt32(
            d.Address.GetAddressBytes().Reverse().ToArray(), 0)).ToList();
    }

    /// <summary>
    /// Probes a single IP address for a ULO camera.
    /// Returns null if the host is not a ULO or is unreachable.
    /// </summary>
    public static async Task<UloFoundDevice?> ProbeAsync(
        IPAddress ip,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Quick TCP check first — avoids the HTTP timeout on closed ports.
            using var tcp = new TcpClient();
            var connectTask = tcp.ConnectAsync(ip, 80);
            if (await Task.WhenAny(connectTask, Task.Delay(timeout, ct)).ConfigureAwait(false) != connectTask)
                return null;
            if (connectTask.IsFaulted)
                return null;
            tcp.Close();

            // Hit the unauthenticated state endpoint.
            using var http = new HttpClient { Timeout = timeout };
            var stateJson = await http.GetStringAsync($"http://{ip}/api/v1/state", ct).ConfigureAwait(false);
            var state = JsonSerializer.Deserialize<UloState>(stateJson, UloJson.Options);
            if (state is null || !stateJson.Contains("batteryLevel"))
                return null;

            // Try to get firmware version (requires auth on most endpoints, but config
            // sometimes leaks through; if not, we still report the camera).
            var firmware = new UloFirmwareVersion();
            string? deviceName = null;

            try
            {
                var configJson = await http.GetStringAsync($"http://{ip}/api/v1/config/firmware", ct)
                    .ConfigureAwait(false);
                var fwConfig = JsonSerializer.Deserialize<UloFirmwareConfig>(configJson, UloJson.Options);
                if (fwConfig is not null)
                    firmware = new UloFirmwareVersion(fwConfig.CurrentVersion);
            }
            catch
            {
                // Auth required — that's fine, we still found the camera.
            }

            try
            {
                var deviceJson = await http.GetStringAsync($"http://{ip}/api/v1/config/device", ct)
                    .ConfigureAwait(false);
                var devConfig = JsonSerializer.Deserialize<UloDeviceConfig>(deviceJson, UloJson.Options);
                deviceName = devConfig?.Name;
            }
            catch
            {
                // Auth required.
            }

            // Reverse DNS lookup for a readable hostname.
            string? hostname = null;
            try
            {
                var entry = await Dns.GetHostEntryAsync(ip).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(entry.HostName) &&
                    entry.HostName != ip.ToString())
                {
                    hostname = entry.HostName;
                }
            }
            catch
            {
                // No reverse DNS — that's fine.
            }

            sw.Stop();
            return new UloFoundDevice(ip, state, firmware, deviceName, hostname, sw.Elapsed);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Returns the /24 base addresses for every connected IPv4 interface.</summary>
    private static List<IPAddress> GetLocalSubnets()
    {
        var subnets = new HashSet<string>();
        var result = new List<IPAddress>();

        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up)
                continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var addr in iface.GetIPProperties().UnicastAddresses)
            {
                if (addr.Address.AddressFamily != AddressFamily.InterNetwork)
                    continue;

                var bytes = addr.Address.GetAddressBytes();
                var maskBytes = addr.IPv4Mask.GetAddressBytes();

                // Compute network address.
                var network = new byte[4];
                for (int i = 0; i < 4; i++)
                    network[i] = (byte)(bytes[i] & maskBytes[i]);

                var key = string.Join(".", network);
                if (subnets.Add(key))
                    result.Add(new IPAddress(network));
            }
        }

        return result;
    }

    /// <summary>Expands a /24 network address into all 254 host addresses (skips .0 and .255).</summary>
    private static IEnumerable<IPAddress> ExpandSubnet(IPAddress network)
    {
        var bytes = network.GetAddressBytes();
        for (int i = 1; i <= 254; i++)
        {
            bytes[3] = (byte)i;
            yield return new IPAddress((byte[])bytes.Clone());
        }
    }
}
