using System.Net.NetworkInformation;

namespace UloManager.Core;

/// <summary>How several hosts are combined into a single answer.</summary>
public enum UloAvailabilityRule
{
    /// <summary>Every host must answer.</summary>
    All,

    /// <summary>At least one host must answer.</summary>
    Any,
}

public sealed record UloAvailabilityResult(
    IReadOnlyDictionary<string, bool> Hosts,
    UloAvailabilityRule Rule)
{
    public bool IsAvailable => Rule == UloAvailabilityRule.All
        ? Hosts.Count > 0 && Hosts.Values.All(up => up)
        : Hosts.Values.Any(up => up);

    public override string ToString()
        => string.Join(", ", Hosts.Select(h => $"{h.Key}={(h.Value ? "up" : "down")}")) +
           $" ({Rule.ToString().ToLowerInvariant()} -> {(IsAvailable ? "available" : "not available")})";
}

/// <summary>
/// Presence checking, as used by the original controller to follow people around the house: ping a
/// set of devices (typically phones) and pick the camera mode from whether they answer. The camera
/// tends to forget its mode after an unattended reboot, so re-applying it on a schedule is useful
/// even when nothing has changed.
/// </summary>
public sealed class UloAvailabilityService
{
    private readonly UloDevice _device;

    public UloAvailabilityService(UloDevice device) => _device = device;

    /// <summary>How long to wait for each ping.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);

    /// <summary>How many times an unreachable host is retried before it counts as down.</summary>
    public int Attempts { get; set; } = 2;

    /// <summary>Pings one host.</summary>
    public async Task<bool> IsHostUpAsync(string host, CancellationToken ct = default)
    {
        for (var attempt = 0; attempt < Math.Max(1, Attempts); attempt++)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(host, (int)Timeout.TotalMilliseconds).ConfigureAwait(false);

                if (reply.Status == IPStatus.Success)
                {
                    return true;
                }
            }
            catch (Exception ex) when (ex is PingException or ArgumentException)
            {
                // Unresolvable or unreachable - counts as down.
                return false;
            }
        }

        return false;
    }

    /// <summary>Pings every host and combines the answers with the given rule.</summary>
    public async Task<UloAvailabilityResult> CheckAsync(
        IEnumerable<string> hosts,
        UloAvailabilityRule rule = UloAvailabilityRule.Any,
        CancellationToken ct = default)
    {
        var results = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

        foreach (var host in hosts.Where(h => !string.IsNullOrWhiteSpace(h)))
        {
            results[host] = await IsHostUpAsync(host.Trim(), ct).ConfigureAwait(false);
        }

        return new UloAvailabilityResult(results, rule);
    }

    /// <summary>
    /// Checks the hosts and applies <paramref name="whenAvailable"/> or <paramref name="whenUnavailable"/>.
    /// Returns the mode that is now set.
    /// </summary>
    public async Task<UloMode> ApplyModeAsync(
        IEnumerable<string> hosts,
        UloMode whenAvailable,
        UloMode whenUnavailable,
        UloAvailabilityRule rule = UloAvailabilityRule.Any,
        bool force = false,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        var availability = await CheckAsync(hosts, rule, ct).ConfigureAwait(false);
        var wanted = availability.IsAvailable ? whenAvailable : whenUnavailable;

        progress?.Report(availability.ToString());

        var current = await _device.GetModeAsync(ct).ConfigureAwait(false);

        if (current == wanted && !force)
        {
            progress?.Report($"Camera is already in {wanted.ToApiValue()} mode.");
            return current;
        }

        await _device.SetModeAsync(wanted, ct).ConfigureAwait(false);
        progress?.Report($"Camera switched from {current.ToApiValue()} to {wanted.ToApiValue()}.");
        return wanted;
    }
}
