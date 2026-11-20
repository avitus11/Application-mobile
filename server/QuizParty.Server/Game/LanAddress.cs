using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace QuizParty.Server.Game;

/// <summary>Adresse du PC sur le Wi-Fi de la maison, utilisée pour le QR code.</summary>
public sealed class LanAddress(IConfiguration config)
{
    public string BaseUrl
    {
        get
        {
            // Forçable dans appsettings.json si la détection choisit la mauvaise carte réseau.
            if (config["Quiz:PublicUrl"] is { Length: > 0 } forced) return forced.TrimEnd('/');
            var port = config.GetValue("Quiz:Port", 5000);
            return $"http://{DetectIp()}:{port}";
        }
    }

    public static string DetectIp()
    {
        var candidates = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel))
            .SelectMany(n => n.GetIPProperties().UnicastAddresses
                .Where(a => a.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a.Address))
                .Select(a => (Nic: n, a.Address)))
            .ToList();

        // On privilégie le Wi-Fi / Ethernet avec une passerelle (évite les cartes virtuelles type WSL, Hyper-V).
        var best = candidates.FirstOrDefault(c => c.Nic.GetIPProperties().GatewayAddresses.Any(g =>
                       g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any)))
                   .Address ?? candidates.FirstOrDefault().Address;

        return best?.ToString() ?? "localhost";
    }
}
