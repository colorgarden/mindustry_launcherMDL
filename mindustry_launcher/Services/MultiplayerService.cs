using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Windows.Threading;

namespace mindustry_launcher
{
    public class MultiplayerService
    {
        private readonly HttpClient _http;

        public MultiplayerService(HttpClient http)
        {
            _http = http;
        }

        public Process? EasyTierProcess { get; set; }
        public ObservableCollection<RoomPlayerInfo> OnlinePlayers { get; } = new();
        public UdpClient? DiscoveryListener { get; set; }
        public CancellationTokenSource? DiscoveryCts { get; set; }
        public DispatcherTimer? DiscoveryTimer { get; set; }
        public string MyBroadcastIp { get; set; } = "";
        public string MyNickname { get; set; } = "";

        public static string GetEasyTierExePath()
        {
            string dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "EasyTier");
            if (Directory.Exists(dir))
            {
                var files = Directory.GetFiles(dir, "easytier-core.exe", SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }
            return "";
        }

        public static (string myIp, string subnet) ComputeRoomIps(string roomCode, bool isHost)
        {
            string sub1 = roomCode.Substring(0, 2);
            string sub2 = roomCode.Substring(2, 2);
            string myIp = isHost
                ? $"10.{sub1}.{sub2}.1"
                : $"10.{sub1}.{sub2}.{new Random().Next(2, 254)}";
            string subnet = $"10.{sub1}.{sub2}.255";
            return (myIp, subnet);
        }

        public void KillEasyTierProcess()
        {
            if (EasyTierProcess == null || EasyTierProcess.HasExited)
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {EasyTierProcess.Id} /T /F",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to kill EasyTier process: {ex.Message}"); }
        }

        public async Task<GitHubRelease?> FetchLatestEasyTierReleaseAsync()
        {
            return await _http.GetFromJsonAsync<GitHubRelease>("https://api.github.com/repos/EasyTier/EasyTier/releases/latest");
        }
    }
}
