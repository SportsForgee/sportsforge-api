using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.Json;

namespace ForgeInsole.Api.Devices
{
    public record DiscoveredInsole(
        string Host,
        string DeviceId,
        string DeviceName,
        string Foot,
        string Firmware,
        int WsPort,
        bool MpuOk,
        bool RequiresAuth);

    // Finds Forge Insoles on the local network by probing the HTTP identity endpoint the
    // firmware serves on port 80 (GET /device-info).
    //
    // mDNS would be tidier — the firmware advertises forge-insole-l.local — but it does not
    // resolve across a Windows Mobile Hotspot, which is exactly the setup the prototypes run
    // on. A bounded sweep of the local /24 is unglamorous but works everywhere.
    public class InsoleScanner
    {
        private readonly ILogger<InsoleScanner> _logger;

        // Last known address per deviceId. Lets a connect-by-name resolve instantly instead of
        // re-sweeping the subnet, and keeps every address on this side of the API.
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DiscoveredInsole> _lastSeen =
            new(StringComparer.OrdinalIgnoreCase);

        public InsoleScanner(ILogger<InsoleScanner> logger) => _logger = logger;

        public DiscoveredInsole? LastSeen(string deviceId) =>
            _lastSeen.TryGetValue(deviceId, out var d) ? d : null;

        /// Resolves a device id to a live address: cache first, full scan only if the cached
        /// address has gone stale (device rebooted onto a new DHCP lease).
        public async Task<DiscoveredInsole?> ResolveAsync(string deviceId, CancellationToken ct)
        {
            var cached = LastSeen(deviceId);
            if (cached is not null)
            {
                var stillThere = await ProbeAsync(cached.Host, 400, ct);
                if (stillThere is not null &&
                    string.Equals(stillThere.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase))
                    return stillThere;
            }

            var found = await ScanAsync(250, ct);
            return found.FirstOrDefault(d => string.Equals(d.DeviceId, deviceId, StringComparison.OrdinalIgnoreCase));
        }

        /// Local IPv4 /24 subnets worth sweeping, most-likely-first. A hotspot interface
        /// (192.168.137.x by ICS convention) is where a prototype almost always sits.
        public static List<string> LocalSubnets()
        {
            var prefixes = new List<string>();

            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;

                foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                {
                    if (addr.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    var ip = addr.Address.ToString();
                    if (ip.StartsWith("169.254.")) continue;   // link-local, nothing answers there

                    var prefix = ip[..(ip.LastIndexOf('.') + 1)];
                    if (!prefixes.Contains(prefix)) prefixes.Add(prefix);
                }
            }

            // ICS hotspot subnet first — it is the common case for a bench prototype.
            prefixes.Sort((a, b) => b.StartsWith("192.168.137.").CompareTo(a.StartsWith("192.168.137.")));
            return prefixes;
        }

        public async Task<List<DiscoveredInsole>> ScanAsync(int timeoutMs, CancellationToken ct)
        {
            var subnets = LocalSubnets();
            _logger.LogInformation("Scanning for insoles on {Subnets}", string.Join(", ", subnets.Select(s => s + "0/24")));

            var found = new List<DiscoveredInsole>();
            var gate = new SemaphoreSlim(64);   // keep the socket burst civil

            foreach (var prefix in subnets)
            {
                var probes = Enumerable.Range(1, 254).Select(async i =>
                {
                    await gate.WaitAsync(ct);
                    try
                    {
                        var device = await ProbeAsync(prefix + i, timeoutMs, ct);
                        if (device is not null)
                        {
                            lock (found) found.Add(device);
                        }
                    }
                    finally { gate.Release(); }
                });

                await Task.WhenAll(probes);
                // A prototype pair lives on one subnet; once found, don't sweep the rest.
                if (found.Count > 0) break;
            }

            foreach (var d in found) _lastSeen[d.DeviceId] = d;

            _logger.LogInformation("Scan finished, {Count} insole(s) found", found.Count);
            return found.OrderBy(d => d.DeviceName).ToList();
        }

        private static async Task<DiscoveredInsole?> ProbeAsync(string host, int timeoutMs, CancellationToken ct)
        {
            // A TCP pre-check keeps the sweep fast: an unused address fails the connect in
            // milliseconds, whereas an HttpClient GET would sit out the full timeout on each
            // of 254 addresses.
            try
            {
                using var probe = new TcpClient();
                var connect = probe.ConnectAsync(IPAddress.Parse(host), 80, ct).AsTask();
                if (await Task.WhenAny(connect, Task.Delay(timeoutMs, ct)) != connect) return null;
                if (!probe.Connected) return null;
            }
            catch { return null; }

            try
            {
                using var http = new HttpClient { Timeout = TimeSpan.FromMilliseconds(Math.Max(500, timeoutMs * 4)) };
                var json = await http.GetStringAsync($"http://{host}/device-info", ct);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Anything can be listening on :80. Only treat it as an insole if it
                // identifies as one.
                if (!root.TryGetProperty("deviceId", out var idProp)) return null;
                var type = root.TryGetProperty("type", out var t) ? t.GetString() : null;
                if (type is not null && !type.Equals("ESP32", StringComparison.OrdinalIgnoreCase)) return null;

                string Str(string name, string fallback = "") =>
                    root.TryGetProperty(name, out var v) ? v.GetString() ?? fallback : fallback;
                bool Bool(string name, bool fallback) =>
                    root.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
                        ? v.GetBoolean() : fallback;

                var deviceId = idProp.GetString() ?? host;
                return new DiscoveredInsole(
                    Host: host,
                    DeviceId: deviceId,
                    DeviceName: Str("deviceName", deviceId),
                    Foot: Str("foot", "?"),
                    Firmware: Str("fw"),
                    WsPort: root.TryGetProperty("wsPort", out var p) && p.TryGetInt32(out var port) ? port : 81,
                    MpuOk: Bool("mpuOk", true),
                    RequiresAuth: Bool("requiresAuth", true));
            }
            catch
            {
                return null;   // listening, but not a Forge Insole
            }
        }
    }
}
