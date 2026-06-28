using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Buffers.Binary;

namespace mindustry_launcher
{

    public partial class MainWindow : Window
    {


        private void RefreshSavesBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. 获取 settings.bin 的绝对路径
                string binPath = GetSettingsBinPath();
                if (string.IsNullOrEmpty(binPath))
                {
                    ShowDialog(L.Get("dialog.info"), L.Get("status.select_version_first"));
                    return;
                }

                // 2. 拿到 data 文件夹，再拼上 saves
                string dataDir = Path.GetDirectoryName(binPath);
                string savesDir = Path.Combine(dataDir, "saves");

                // 3. 检查目录是否存在
                if (!Directory.Exists(savesDir))
                {
                    ShowDialog(L.Get("dialog.info"), L.T("status.save_dir_missing", savesDir));
                    return;
                }

                var saveList = new List<MindustrySaveMetadata>();
                string[] msavFiles = Directory.GetFiles(savesDir, "*.msav");

                // 4. 解析并组合列表
                foreach (var file in msavFiles)
                {
                    try
                    {
                        var meta = ParseMindustrySave(file);
                        FileInfo fi = new FileInfo(file);

                        // 只用 PlayTime 字段显示最后修改时间
                        meta.PlayTime = fi.LastWriteTime.ToString("yyyy/MM/dd HH:mm");

                        // 删掉了把 KB 塞进 Wave 的弱智操作，保持原汁原味的 "-" 或 "高压隐藏"

                        saveList.Add(meta);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Failed to parse save file {file}: {ex.Message}");
                    }
                }

                // 5. 按照修改时间倒序排列并绑定给前台
                SavesListView.ItemsSource = saveList.OrderByDescending(x => x.PlayTime).ToList();
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("status.saves_refresh_error", ex.Message), DialogIcon.Error);
            }
        }
        // 核心深度解析逻辑
        public MindustrySaveMetadata ParseMindustrySave(string path)
        {
            string fileName = Path.GetFileName(path);
            string nameNoExt = Path.GetFileNameWithoutExtension(path);

            var meta = new MindustrySaveMetadata { MapName = nameNoExt };

            // 1. 战役模式判断 (sector-星球-区块)
            if (fileName.StartsWith("sector-") || int.TryParse(nameNoExt.Replace("-backup", ""), out _))
            {
                string processedName = nameNoExt;
                if (fileName.StartsWith("sector-"))
                {
                    string[] parts = nameNoExt.Split('-');
                    if (parts.Length >= 3)
                    {
                        string planet = char.ToUpper(parts[1][0]) + parts[1].Substring(1);
                        processedName = L.T("save.region_format", planet, parts[2]);
                    }
                }
                else if (nameNoExt.Replace("-backup", "") == "0")
                {
                    processedName = L.Get("model.zero_region");
                }

                meta.MapName = processedName + (nameNoExt.EndsWith("backup") ? L.Get("save.is_backup") : "");
                meta.Author = L.Get("save.author.official");
                meta.Wave = L.Get("save.wave.compressed");
                return meta;
            }

            // 2. 自定义/沙盒存档尝试读取 MSAV 头
            try
            {
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new BinaryReader(fs))
                {
                    if (fs.Length >= 8)
                    {
                        byte[] header = reader.ReadBytes(4);
                        if (Encoding.ASCII.GetString(header) == "MSAV")
                        {
                            byte[] verBytes = reader.ReadBytes(4);
                            Array.Reverse(verBytes);
                            meta.Version = "v" + BitConverter.ToInt32(verBytes, 0).ToString();
                            meta.Author = L.Get("save.author.local");
                            meta.Wave = L.Get("save.wave.click_view");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to parse save metadata: {ex.Message}");
                meta.Author = L.Get("save.author.corrupt");
            }

            return meta;
        }
        private void ImportVersionBtn_Click(object sender, RoutedEventArgs e)
        {
            // 1. 检查是否设置了版本存储目录
            if (_configService.GetConfig().ManagedFolders == null || _configService.GetConfig().ManagedFolders.Count == 0)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("status.no_managed_folder"));
                return;
            }

            // 2. 呼出资源管理器选择 jar 文件
            var openFileDialog = new OpenFileDialog
            {
                Filter = $"{L.Get("dialog.jar_filter")} (*.jar)|*.jar",
                Title = L.Get("dialog.select_jar_title")
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string sourceFilePath = openFileDialog.FileName;

                // 3. 弹窗提示输入版本名称
                string versionName = PromptForVersionName();
                if (string.IsNullOrWhiteSpace(versionName)) return; // 用户取消或未输入

                // 4. 校验文件夹名称的合法性
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    if (versionName.Contains(c))
                    {
                        ShowDialog(L.Get("dialog.error"), L.Get("dialog.import_illegal_chars"), DialogIcon.Error);
                        return;
                    }
                }

                // 5. 准备目标路径
                string targetBaseDir = _configService.GetConfig().ManagedFolders[0]; // 默认导入到第一个管理的目录
                string targetDir = Path.Combine(targetBaseDir, "Versions", versionName);

                if (Directory.Exists(targetDir))
                {
                    ShowDialog(L.Get("dialog.info"), L.Get("dialog.import_exists"));
                    return;
                }

                // 6. 执行创建和复制
                try
                {
                    Directory.CreateDirectory(targetDir);
                    // 核心要求：将任意名字的 jar 复制过去并重命名为 Mindustry.jar
                    File.Copy(sourceFilePath, Path.Combine(targetDir, "Mindustry.jar"), true);

                    ShowDialog(L.Get("dialog.success"), L.T("dialog.import_success", versionName), DialogIcon.Info);

                    // 👇 刷新主 UI 和版本列表，这里的 UpdateMainUI() 是你代码中自带的方法
                    UpdateMainUI();
                }
                catch (Exception ex)
                {
                    ShowDialog(L.Get("dialog.error"), L.T("dialog.import_error", ex.Message), DialogIcon.Error);
                }
            }
        }

        // ==========================================
        // 纯代码生成的迷你输入框 (无需新建 XAML)
        // ==========================================
        private string PromptForVersionName()
        {
            Window prompt = new Window()
            {
                Width = 350,
                Height = 180,
                Title = L.Get("dialog.import_prompt_title"),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, // 模态窗口，跟随主窗口
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            StackPanel panel = new StackPanel() { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock() { Text = L.Get("dialog.import_prompt_msg"), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });

            TextBox inputBox = new TextBox() { Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5, 0, 5, 0) };
            panel.Children.Add(inputBox);

            Button confirmBtn = new Button()
            {
                Content = L.Get("dialog.import_prompt_confirm"),
                Width = 90,
                Height = 32,
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(19, 114, 206)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };

            // 绑定点击事件和回车键事件
            confirmBtn.Click += (s, e) => { prompt.DialogResult = true; prompt.Close(); };
            inputBox.KeyDown += (s, e) => { if (e.Key == Key.Enter) { prompt.DialogResult = true; prompt.Close(); } };

            panel.Children.Add(confirmBtn);
            prompt.Content = panel;

            // 打开弹窗时自动聚焦输入框
            prompt.Loaded += (s, e) => { inputBox.Focus(); };

            if (prompt.ShowDialog() == true)
            {
                return inputBox.Text.Trim();
            }
            return string.Empty;
        }

        // ==========================================
        // 1. 服务引用与 UI 状态
        // ==========================================
        private readonly ConfigService _configService;
        private readonly VersionManagementService _versionService;
        private readonly GameLaunchService _launchService;
        private readonly RemoteDownloadService _downloadService;
        private readonly ModService _modService;
        private readonly SchematicService _schematicService;
        private readonly MultiplayerService _multiplayerService;
        private readonly HttpClient _httpClient;

        private bool _isDownloading = false;
        private string _currentDetailUrl = "";

        // 通用弹窗
        private TaskCompletionSource<MsgResult>? _dialogTcs;

        private ICollectionView? _settingsView;

        // ==========================================
        // 3. 窗口初始化
        // ==========================================
        public MainWindow()
        {
            _configService = App.Services.GetRequiredService<ConfigService>();
            _versionService = App.Services.GetRequiredService<VersionManagementService>();
            _launchService = App.Services.GetRequiredService<GameLaunchService>();
            _downloadService = App.Services.GetRequiredService<RemoteDownloadService>();
            _modService = App.Services.GetRequiredService<ModService>();
            _schematicService = App.Services.GetRequiredService<SchematicService>();
            _multiplayerService = App.Services.GetRequiredService<MultiplayerService>();
            _httpClient = App.Services.GetRequiredService<HttpClient>();
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _configService.LoadConfig();

            // 语言初始化
            if (string.IsNullOrEmpty(_configService.GetConfig().Language))
            {
                _configService.GetConfig().Language = L.AutoDetect();
                _configService.SaveConfig();
            }
            L.LoadLanguage(_configService.GetConfig().Language);
            L.LanguageChanged += () => Dispatcher.Invoke(RefreshAllUI);

            GlobalJavaComboBox.Text = _configService.GetConfig().GlobalJavaPath;
            PlayerNameBox.Text = _configService.GetConfig().PlayerNickname;
            int maxRam = (HardwareInfo.GetTotalPhysicalMemoryMB() / 512) * 512;
            GlobalRamSlider.Maximum = maxRam;
            VSettingsRamSlider.Maximum = maxRam;
            _configService.GetConfig().GlobalRamMB = Math.Min(_configService.GetConfig().GlobalRamMB, maxRam);
            GlobalRamSlider.Value = _configService.GetConfig().GlobalRamMB;
            GlobalAutoRamCheck.IsChecked = _configService.GetConfig().GlobalUseAutoRam;
            UrlHelper.ProxyIndex = _configService.GetConfig().ProxyNodeIndex;
            ProxyNodeBox.SelectedIndex = _configService.GetConfig().ProxyNodeIndex;

            if (!string.IsNullOrEmpty(_configService.GetConfig().LastSelectedInstancePath) && File.Exists(Path.Combine(_configService.GetConfig().LastSelectedInstancePath, "Mindustry.jar")))
            {
                _versionService.CurrentInstance = new GameInstanceInfo { Name = Path.GetFileName(_configService.GetConfig().LastSelectedInstancePath), FullPath = _configService.GetConfig().LastSelectedInstancePath };
            }

            UpdateMainUI();
            RbOfficial.IsChecked = true;
            MainTabControl.SelectedIndex = -1; SwitchTab(0);

            try
            {
                if (_configService.GetConfig().WindowWidth > 0 && _configService.GetConfig().WindowHeight > 0)
                {
                    Width = _configService.GetConfig().WindowWidth;
                    Height = _configService.GetConfig().WindowHeight;
                }
                if (_configService.GetConfig().WindowLeft >= 0 && _configService.GetConfig().WindowTop >= 0)
                {
                    Left = _configService.GetConfig().WindowLeft;
                    Top = _configService.GetConfig().WindowTop;
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to restore window state: {ex.Message}"); }

            RefreshAllUI();

            Task.Run(() =>
            {
                var javas = JavaScanner.Scan();
                Dispatcher.InvokeAsync(() =>
                {
                    GlobalJavaComboBox.ItemsSource = javas;
                    VSettingsJavaComboBox.ItemsSource = javas;
                });
            });
            LoadGameIconAsync();
            _isInitialized = true;
        }

        // ==========================================
        // 4. 内存滑块控制
        // ==========================================
        private static int CalculateSmartRam()
        {
            return GameLaunchService.CalculateSmartRam();
        }

        private void GlobalAutoRamCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (GlobalRamSlider == null || GlobalRamText == null) return;
            bool isAuto = GlobalAutoRamCheck.IsChecked ?? false;
            GlobalRamSlider.IsEnabled = !isAuto;
            _configService.GetConfig().GlobalUseAutoRam = isAuto;
            if (isAuto)
            {
                int autoRam = CalculateSmartRam();
                GlobalRamSlider.Value = autoRam;
                GlobalRamText.Text = L.T("settings.auto_ram_text", autoRam);
            }
            else
            {
                GlobalRamText.Text = $"{(int)GlobalRamSlider.Value} MB";
            }
        }

        private void GlobalRamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (GlobalRamText != null && GlobalAutoRamCheck.IsChecked == false)
                GlobalRamText.Text = $"{(int)e.NewValue} MB";
        }

        private void VersionAutoRamCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (VSettingsRamSlider == null || VSettingsRamText == null) return;
            bool isAuto = VersionAutoRamCheck.IsChecked ?? false;
            VSettingsRamSlider.IsEnabled = !isAuto;
            _versionService.CurrentVersionConfig.UseAutoRam = isAuto;
            if (isAuto)
            {
                int targetRam = _configService.GetConfig().GlobalUseAutoRam ? CalculateSmartRam() : _configService.GetConfig().GlobalRamMB;
                VSettingsRamSlider.Value = targetRam;
                VSettingsRamText.Text = L.T("vsettings.follow_auto_text", targetRam);
            }
            else
            {
                VSettingsRamText.Text = $"{(int)VSettingsRamSlider.Value} MB";
            }
        }

        private void VSettingsRamSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VSettingsRamText != null && VersionAutoRamCheck.IsChecked == false)
                VSettingsRamText.Text = $"{(int)e.NewValue} MB";
        }

        // ==========================================
        // 5. 联机大厅：UDP 广播探测雷达逻辑
        // ==========================================
        private void StartUdpDiscovery(string broadcastIp, string nickname)
        {
            _multiplayerService.MyBroadcastIp = broadcastIp;
            _multiplayerService.MyNickname = nickname;
            _multiplayerService.OnlinePlayers.Clear();
            RoomPlayersListBox.ItemsSource = _multiplayerService.OnlinePlayers;
            _multiplayerService.DiscoveryCts = new CancellationTokenSource();
            Task.Run(() => DiscoveryListenLoop(_multiplayerService.DiscoveryCts.Token));

            _multiplayerService.DiscoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _multiplayerService.DiscoveryTimer.Tick += (s, e) =>
            {
                try
                {
                    using var sender = new UdpClient();
                    sender.EnableBroadcast = true;
                    byte[] data = Encoding.UTF8.GetBytes($"MDL_WHO|{_multiplayerService.MyNickname}");
                    sender.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(_multiplayerService.MyBroadcastIp), 6568));
                }
                catch (Exception ex) { Debug.WriteLine($"UDP broadcast failed: {ex.Message}"); }

                for (int i = _multiplayerService.OnlinePlayers.Count - 1; i >= 0; i--)
                {
                    if ((DateTime.Now - _multiplayerService.OnlinePlayers[i].LastSeen).TotalSeconds > 6)
                        _multiplayerService.OnlinePlayers.RemoveAt(i);
                }
            };
            _multiplayerService.DiscoveryTimer.Start();
        }

        private void DiscoveryListenLoop(CancellationToken token)
        {
            try
            {
                _multiplayerService.DiscoveryListener = new UdpClient();
                _multiplayerService.DiscoveryListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _multiplayerService.DiscoveryListener.Client.Bind(new IPEndPoint(IPAddress.Any, 6568));
                while (!token.IsCancellationRequested)
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = _multiplayerService.DiscoveryListener.Receive(ref remoteEP);
                    string msg = Encoding.UTF8.GetString(bytes);
                    string ip = remoteEP.Address.ToString();

                    // 核心修复：改用 InvokeAsync 防止底层网络线程与界面线程发生死锁
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (msg.StartsWith("MDL_WHO|"))
                        {
                            string name = msg.Substring(8);
                            var existing = _multiplayerService.OnlinePlayers.FirstOrDefault(p => p.IP == ip);
                            if (existing != null)
                            {
                                existing.LastSeen = DateTime.Now;
                                int idx = _multiplayerService.OnlinePlayers.IndexOf(existing);
                                _multiplayerService.OnlinePlayers[idx] = new RoomPlayerInfo { IP = ip, Name = name, LastSeen = DateTime.Now };
                            }
                            else
                            {
                                _multiplayerService.OnlinePlayers.Add(new RoomPlayerInfo { IP = ip, Name = name, LastSeen = DateTime.Now });
                            }
                        }
                        else if (msg.StartsWith("MDL_BYE|"))
                        {
                            var existing = _multiplayerService.OnlinePlayers.FirstOrDefault(p => p.IP == ip);
                            if (existing != null)
                                _multiplayerService.OnlinePlayers.Remove(existing);
                        }
                    });
                }
            }
            catch (Exception ex) { Debug.WriteLine($"UDP discovery error: {ex.Message}"); }
        }

        private void StopUdpDiscovery()
        {
            try
            {
                if (!string.IsNullOrEmpty(_multiplayerService.MyBroadcastIp) && !string.IsNullOrEmpty(_multiplayerService.MyNickname))
                {
                    using var sender = new UdpClient(); sender.EnableBroadcast = true;
                    byte[] data = Encoding.UTF8.GetBytes($"MDL_BYE|{_multiplayerService.MyNickname}");
                    sender.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(_multiplayerService.MyBroadcastIp), 6568));
                }
            }
            catch (Exception ex) { Debug.WriteLine($"UDP bye send failed: {ex.Message}"); }
            _multiplayerService.DiscoveryCts?.Cancel();
            _multiplayerService.DiscoveryTimer?.Stop();
            try { _multiplayerService.DiscoveryListener?.Close(); } catch (Exception ex) { Debug.WriteLine($"Failed to close UDP listener: {ex.Message}"); }
            Dispatcher.InvokeAsync(() => _multiplayerService.OnlinePlayers.Clear());
        }

        // ==========================================
        // 6. 联机大厅：创建、加入与进程管理
        // ==========================================
        private void CreateRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_multiplayerService.EasyTierProcess != null && !_multiplayerService.EasyTierProcess.HasExited)
            {
                StopUdpDiscovery();
                KillEasyTierProcess();
                return;
            }

            string exe = GetEasyTierExePath();
            if (string.IsNullOrEmpty(exe))
                return;

            string myName = PlayerNameBox.Text.Trim();
            if (string.IsNullOrEmpty(myName))
            {
                ShowDialog(L.Get("dialog.info"), L.Get("multiplayer.name_required"));
                return;
            }

            string roomCode = new Random().Next(100000, 999999).ToString();
            EasyTierRoomBox.Text = roomCode;
            var (myIp, broadcastIp) = MultiplayerService.ComputeRoomIps(roomCode, true);
            string args = $"-e \"{EasyTierServerBox.Text}\" --network-name \"mdl_room_{roomCode}\" --network-secret \"mdl_pwd_{roomCode}\" --ipv4 {myIp}/24";

            string fw1 = "netsh advfirewall firewall add rule name=\"MDL_TCP\" dir=in action=allow protocol=TCP localport=6567,6568 >nul 2>&1";
            string fw2 = "netsh advfirewall firewall add rule name=\"MDL_UDP\" dir=in action=allow protocol=UDP localport=6567,6568 >nul 2>&1";
            StartEasyTierProcess($"/k \"{fw1} & {fw2} & \"{exe}\" {args}\"", roomCode, true, myIp, myIp, broadcastIp, myName);
        }

        private void JoinRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_multiplayerService.EasyTierProcess != null && !_multiplayerService.EasyTierProcess.HasExited)
            {
                StopUdpDiscovery();
                KillEasyTierProcess();
                return;
            }

            string exe = GetEasyTierExePath();
            string roomCode = EasyTierRoomBox.Text.Trim();
            string myName = PlayerNameBox.Text.Trim();

            if (string.IsNullOrEmpty(roomCode) || string.IsNullOrEmpty(myName))
            {
                ShowDialog(L.Get("dialog.info"), L.Get("multiplayer.room_and_name_required"));
                return;
            }

            if (roomCode.Length != 6 || !int.TryParse(roomCode, out _))
            {
                ShowDialog(L.Get("dialog.info"), L.Get("multiplayer.invalid_room"));
                return;
            }

            var (myIp, broadcastIp) = MultiplayerService.ComputeRoomIps(roomCode, false);
            string hostIp = $"10.{roomCode.Substring(0, 2)}.{roomCode.Substring(2, 2)}.1";
            string args = $"-e \"{EasyTierServerBox.Text}\" --network-name \"mdl_room_{roomCode}\" --network-secret \"mdl_pwd_{roomCode}\" --ipv4 {myIp}/24";

            string fw1 = "netsh advfirewall firewall add rule name=\"MDL_TCP\" dir=in action=allow protocol=TCP localport=6567,6568 >nul 2>&1";
            string fw2 = "netsh advfirewall firewall add rule name=\"MDL_UDP\" dir=in action=allow protocol=UDP localport=6567,6568 >nul 2>&1";

            StartEasyTierProcess($"/c \"{fw1} & {fw2} & \"{exe}\" {args}\"", roomCode, false, myIp, hostIp, broadcastIp, myName);
        }
        private void StartEasyTierProcess(string cmdArgs, string roomCode, bool isHost, string myIp, string hostIp, string brIp, string myName)
        {
            try
            {
                _multiplayerService.EasyTierProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (_multiplayerService.EasyTierProcess != null)
                {
                    MyVirtualIpBox.Text = myIp; StartUdpDiscovery(brIp, myName);

                    // 创建一个大红色的画刷
                    var redBrush = new SolidColorBrush(Color.FromRgb(232, 17, 35));

                    if (isHost)
                    {
                        CreateRoomBtn.Content = L.Get("multiplayer.disband");
                        CreateRoomBtn.Background = redBrush; // 直接在代码里染红，绝对有效！
                        CreateRoomBtn.Tag = "Locked";        // 上锁，掐断 XAML 的悬停变绿功能
                        JoinRoomBtn.IsEnabled = false;       // 触发灰化
                    }
                    else
                    {
                        JoinRoomBtn.Content = L.Get("multiplayer.exit");
                        JoinRoomBtn.Background = redBrush;   // 直接在代码里染红，绝对有效！
                        JoinRoomBtn.Tag = "Locked";          // 上锁，掐断 XAML 的悬停变蓝功能
                        CreateRoomBtn.IsEnabled = false;     // 触发灰化
                    }

                    _multiplayerService.EasyTierProcess.EnableRaisingEvents = true;
                    _multiplayerService.EasyTierProcess.Exited += (s, ev) => Dispatcher.InvokeAsync(() =>
                    {
                        _multiplayerService.EasyTierProcess = null;
                        StopUdpDiscovery();
                        MyVirtualIpBox.Text = L.Get("multiplayer.not_connected");

                        CreateRoomBtn.Content = L.Get("multiplayer.create");
                        CreateRoomBtn.ClearValue(Button.BackgroundProperty);
                        CreateRoomBtn.Tag = null;

                        JoinRoomBtn.Content = L.Get("multiplayer.join");
                        JoinRoomBtn.ClearValue(Button.BackgroundProperty);
                        JoinRoomBtn.Tag = null;

                        CreateRoomBtn.IsEnabled = true;
                        JoinRoomBtn.IsEnabled = true;
                    });
                }
            }
            catch { ShowDialog(L.Get("dialog.error"), L.Get("multiplayer.startup_error"), DialogIcon.Error); }
        }
        // --- 修改部分 ---
        private void KillEasyTierProcess()
        {
            _multiplayerService.KillEasyTierProcess();
        }

        // ==========================================
        // 往下是你原有的图标加载、游戏启动等方法，保持不变
        private async void LoadGameIconAsync()
        {
            string cd = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
            Directory.CreateDirectory(cd);
            string p = Path.Combine(cd, "icon_64.png");
            if (!File.Exists(p))
            {
                try
                {
                    var bytes = await _httpClient.GetByteArrayAsync(UrlHelper.Format("https://raw.githubusercontent.com/Anuken/Mindustry/master/core/assets/icons/icon_64.png", false));
                    await File.WriteAllBytesAsync(p, bytes);
                }
                catch { return; }
            }
            if (File.Exists(p))
            {
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(p);
                bmp.EndInit();
                bmp.Freeze();
                MainGameIcon.Source = bmp;
                SettingsGameIcon.Source = bmp;
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            StopUdpDiscovery();
            KillEasyTierProcess(); // 彻底清理底层进程

            _configService.GetConfig().PlayerNickname = PlayerNameBox.Text;
            _configService.GetConfig().GlobalJavaPath = GlobalJavaComboBox.Text;
            _configService.GetConfig().GlobalRamMB = (int)GlobalRamSlider.Value;
            if (WindowState == WindowState.Normal)
            {
                _configService.GetConfig().WindowWidth = Width;
                _configService.GetConfig().WindowHeight = Height;
                _configService.GetConfig().WindowLeft = Left;
                _configService.GetConfig().WindowTop = Top;
            }
            _configService.SaveConfig();
        }

        private void ProxyNodeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProxyNodeBox.SelectedIndex != -1)
            {
                _configService.GetConfig().ProxyNodeIndex = ProxyNodeBox.SelectedIndex;
                UrlHelper.ProxyIndex = ProxyNodeBox.SelectedIndex;
                _configService.SaveConfig();
            }
        }

        private void ToggleDownloadState(bool isD)
        {
            _isDownloading = isD;
            RemoteVersionListBox.IsEnabled = !isD;
            ModBrowserListBox.IsEnabled = !isD;
            SchematicBrowserListBox.IsEnabled = !isD;
            if (RbSchemMinRi2 != null) RbSchemMinRi2.IsEnabled = !isD;
            if (RbSchemDesignIt != null) RbSchemDesignIt.IsEnabled = !isD;
            if (RbSchemDesignIt != null) RbSchemDesignIt.IsEnabled = !isD;
        }

        private static readonly SolidColorBrush NavInactiveFg = Brushes.White;
        private static readonly SolidColorBrush NavActiveFg = new(Color.FromRgb(19, 114, 206));
        private static readonly SolidColorBrush NavActiveBg = Brushes.White;

        private void ResetNavButton(Button btn)
        {
            btn.Foreground = NavInactiveFg;
            btn.Background = Brushes.Transparent;
            btn.BorderBrush = Brushes.Transparent;
            btn.BorderThickness = new Thickness(0);
        }

        private void ActivateNavButton(Button btn)
        {
            btn.Foreground = NavActiveFg;
            btn.Background = NavActiveBg;
            btn.BorderBrush = Brushes.Transparent;
            btn.BorderThickness = new Thickness(0);
        }

        private void SwitchTab(int idx)
        {
            MainTabControl.SelectedIndex = idx;

            ResetNavButton(NavLaunchBtn);
            ResetNavButton(NavDownloadBtn);
            ResetNavButton(NavMultiplayerBtn);
            ResetNavButton(NavSettingsBtn);
            ResetNavButton(NavMoreBtn);

            if (idx == 0)
                ActivateNavButton(NavLaunchBtn);
            else if (idx == 1 || idx == 2 || idx == 3)
                ActivateNavButton(NavDownloadBtn);
            else if (idx == 4)
                ActivateNavButton(NavMultiplayerBtn);
            else if (idx == 5)
                ActivateNavButton(NavSettingsBtn);
            else if (idx == 6)
                ActivateNavButton(NavMoreBtn);

            // 子标签栏：仅下载页(idx=1-3) 显示
            SubTabBar.Visibility = (idx >= 1 && idx <= 3) ? Visibility.Visible : Visibility.Collapsed;

            // 设置默认子标签
            _suppressSubTabEvent = true;
            if (idx == 1)
                SubTabDownloadSource.IsChecked = true;
            _suppressSubTabEvent = false;

        }

        private void AnimateFade(FrameworkElement ele, bool isShow)
        {
            if (isShow)
                ele.Visibility = Visibility.Visible;

            DoubleAnimation op = new DoubleAnimation
            {
                From = isShow ? 0 : 1,
                To = isShow ? 1 : 0,
                Duration = TimeSpan.FromSeconds(0.25)
            };

            DoubleAnimation tr = new DoubleAnimation
            {
                From = isShow ? 20 : 0,
                To = isShow ? 0 : 20,
                Duration = TimeSpan.FromSeconds(0.25),
                EasingFunction = new CircleEase { EasingMode = EasingMode.EaseOut }
            };

            if (!isShow)
                op.Completed += (s, e) => ele.Visibility = Visibility.Collapsed;

            ele.BeginAnimation(OpacityProperty, op);
            ele.RenderTransform.BeginAnimation(TranslateTransform.YProperty, tr);
        }

        private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.Source is TabControl tc)
            {
                var h = (FrameworkElement)tc.Template.FindName("PART_SelectedContentHost", tc);
                if (h != null)
                {
                    Storyboard? sb = h.Resources["FadeIn"] as Storyboard;
                    sb?.Begin(h);
                }
            }
        }

        private void NavLaunch_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(0);
            ShowLaunchSubPanel(10);
        }

        private void NavDownload_Click(object sender, RoutedEventArgs e)
        {
            SubTabDownloadSource.IsChecked = true;
            SwitchTab(1);
        }

        // 统一的子标签处理（启动页 + 下载页）
        private async void SubTab_Checked(object sender, RoutedEventArgs e)
        {
            if (_suppressSubTabEvent || !_isInitialized) return;
            if (sender is not RadioButton rb || rb.Tag is not string tagStr || !int.TryParse(tagStr, out int idx))
                return;

            if (idx >= 1 && idx <= 3)
            {
                // 下载页子标签
                SwitchTab(idx);
                if (idx == 2 && _modService.AllOnlineMods.Count == 0)
                    await FetchModRegistryAsync();
                else if (idx == 3 && _schematicService.AllOnlineSchematics.Count == 0)
                {
                    string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", $"{_schematicService.CurrentRepo.Replace("/", "_")}.zip");
                    if (File.Exists(cachePath))
                        _ = FetchSchematicsAsync(false);
                }
            }
        }

        private void ShowLaunchSubPanel(int subIdx)
        {
            FrameworkElement targetPanel = subIdx switch
            {
                11 => LaunchVersionSelectPanel,
                12 => LaunchVersionSettingsPanel,
                _ => LaunchOverviewPanel
            };

            if (targetPanel.Visibility == Visibility.Visible) return;

            // 淡出当前可见面板
            foreach (var p in new FrameworkElement[] { LaunchOverviewPanel, LaunchVersionSelectPanel, LaunchVersionSettingsPanel })
            {
                if (p.Visibility == Visibility.Visible)
                    AnimateFade(p, false);
            }

            // 淡入目标面板
            AnimateFade(targetPanel, true);

            // 标题栏返回按钮：仅在子面板中显示，同时隐藏 MDL 文字避免重叠
            LaunchBackBtn.Visibility = (subIdx != 10) ? Visibility.Visible : Visibility.Collapsed;
            TitleBarMDL.Visibility = (subIdx != 10) ? Visibility.Collapsed : Visibility.Visible;

            if (subIdx == 11)
                OpenVersionSelect_Click(null!, null!);
            else if (subIdx == 12)
                OpenVersionSettings_Click(null!, null!);
        }

        private void LaunchSubPanelBack_Click(object sender, RoutedEventArgs e)
        {
            ShowLaunchSubPanel(10);
        }

        // 新增：联机按钮点击 (传入 false 走缓存)
        private void NavMultiplayer_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(4);
            _ = CheckAndDownloadEasyTierAsync(false);
        }

        private void NavSettings_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(5);
        }

        private void NavMore_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(6);
        }

        // ==========================================
        // 联机组件 (EasyTier) 自动下载、缓存与事件
        // ==========================================

        // 追踪后台 EasyTier 进程的变量


        // 核心增强：智能寻找解压后的 exe (无视里面嵌套了多少层文件夹)
        private static string GetEasyTierExePath()
        {
            return MultiplayerService.GetEasyTierExePath();
        }

        // 手动点击下载按钮 (传入 true 强制无视缓存重新下载)
        private void BtnDownloadEasyTier_Click(object sender, RoutedEventArgs e)
        {
            _ = CheckAndDownloadEasyTierAsync(true);
        }

        private async Task CheckAndDownloadEasyTierAsync(bool forceDownload)
        {
            string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "EasyTier");

            // 缓存拦截：如果不强制下载，且通过智能扫描找到了 exe，直接秒进就绪！绝对不重下！
            if (!forceDownload && !string.IsNullOrEmpty(GetEasyTierExePath()))
            {
                EasyTierStatusText.Text = L.Get("multiplayer.cached_ready");
                return;
            }

            EasyTierStatusText.Text = L.Get("easytier.connecting_github");
            EasyTierProgressBar.Visibility = Visibility.Visible;
            EasyTierProgressBar.Value = 0;

            try
            {
                var rel = await _multiplayerService.FetchLatestEasyTierReleaseAsync();
                var asset = rel?.Assets?.FirstOrDefault(a => a.Name.Contains("windows-x86_64") && a.Name.EndsWith(".zip"));
                if (asset == null) { EasyTierStatusText.Text = L.Get("easytier.no_windows_version"); return; }

                System.IO.Directory.CreateDirectory(dir);
                string zipPath = System.IO.Path.Combine(dir, asset.Name);

                EasyTierStatusText.Text = L.Get("easytier.downloading");
                await _downloadService.DownloadFileAsync(UrlHelper.Format(asset.BrowserDownloadUrl), zipPath, new Progress<double>(p => EasyTierProgressBar.Value = p));

                EasyTierStatusText.Text = L.Get("easytier.extracting");
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, dir, true);
                System.IO.File.Delete(zipPath); // 下完清理垃圾

                EasyTierStatusText.Text = L.Get("easytier.installed");
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                EasyTierStatusText.Text = L.Get("easytier.download_error");
            }
            catch (Exception ex)
            {
                EasyTierStatusText.Text = L.T("easytier.generic_error", ex.Message);
            }
            finally
            {
                EasyTierProgressBar.Visibility = Visibility.Collapsed;
            }
        }

        private void GenerateRoom_Click(object sender, RoutedEventArgs e)
        {
            Random rnd = new Random();
            EasyTierRoomBox.Text = rnd.Next(100000, 999999).ToString();
        }

        // ==========================================
        // 核心：基于房号系统的 EasyTier 连接逻辑 (终极完美版)
        // 包含：动态IP分配、双向防火墙静默穿透、进程树强制断开
        // ==========================================

        private void CloseOverlays_Click(object sender, RoutedEventArgs e)
        {
            // 保存版本设置（如果当前在版本设置子面板中）
            if (LaunchVersionSettingsPanel.Visibility == Visibility.Visible && _versionService.CurrentInstance != null)
                SaveVersionConfig(_versionService.CurrentInstance.FullPath);

            if (ReleaseNotesOverlay.Visibility == Visibility.Visible)
                AnimateFade(ReleaseNotesOverlay, false);

            if (SchematicInstallOverlay.Visibility == Visibility.Visible)
                AnimateFade(SchematicInstallOverlay, false);

            // 切回概览
            ShowLaunchSubPanel(10);

            UpdateMainUI();
        }

        private void CloseReleaseNotes_Click(object sender, RoutedEventArgs e)
        {
            AnimateFade(ReleaseNotesOverlay, false);
        }

        // ==========================================
        // 通用弹窗 (替代 MessageBox)
        // ==========================================
        private void ConfigureDialogIcon(DialogIcon icon)
        {
            switch (icon)
            {
                case DialogIcon.Info:
                    DialogIconText.Text = "ℹ";
                    DialogIconBorder.Background = new SolidColorBrush(Color.FromRgb(19, 114, 206));
                    DialogIconText.Foreground = Brushes.White;
                    break;
                case DialogIcon.Warning:
                    DialogIconText.Text = "⚠";
                    DialogIconBorder.Background = new SolidColorBrush(Color.FromRgb(255, 152, 0));
                    DialogIconText.Foreground = Brushes.White;
                    break;
                case DialogIcon.Error:
                    DialogIconText.Text = "✕";
                    DialogIconBorder.Background = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    DialogIconText.Foreground = Brushes.White;
                    break;
                case DialogIcon.Question:
                    DialogIconText.Text = "?";
                    DialogIconBorder.Background = new SolidColorBrush(Color.FromRgb(19, 114, 206));
                    DialogIconText.Foreground = Brushes.White;
                    break;
            }
        }

        public void ShowDialog(string title, string message, DialogIcon icon = DialogIcon.Info)
        {
            ConfigureDialogIcon(icon);
            DialogTitleText.Text = title;
            DialogBodyText.Text = message;
            DialogOkBtn.Visibility = Visibility.Visible;
            DialogYesBtn.Visibility = Visibility.Collapsed;
            DialogNoBtn.Visibility = Visibility.Collapsed;
            DialogCancelBtn.Visibility = Visibility.Collapsed;
            AnimateFade(DialogOverlay, true);
        }

        public Task<MsgResult> ShowDialogAsync(string title, string message, DialogIcon icon, bool showCancel = false)
        {
            _dialogTcs = new TaskCompletionSource<MsgResult>();
            ConfigureDialogIcon(icon);
            DialogTitleText.Text = title;
            DialogBodyText.Text = message;
            DialogOkBtn.Visibility = Visibility.Collapsed;
            DialogYesBtn.Visibility = Visibility.Visible;
            DialogNoBtn.Visibility = Visibility.Visible;
            DialogCancelBtn.Visibility = showCancel ? Visibility.Visible : Visibility.Collapsed;
            AnimateFade(DialogOverlay, true);
            return _dialogTcs.Task;
        }

        private void DialogOk_Click(object sender, RoutedEventArgs e)
        {
            AnimateFade(DialogOverlay, false);
        }

        private void DialogYes_Click(object sender, RoutedEventArgs e)
        {
            AnimateFade(DialogOverlay, false);
            _dialogTcs?.TrySetResult(MsgResult.Yes);
        }

        private void DialogNo_Click(object sender, RoutedEventArgs e)
        {
            AnimateFade(DialogOverlay, false);
            _dialogTcs?.TrySetResult(MsgResult.No);
        }

        private void DialogCancel_Click(object sender, RoutedEventArgs e)
        {
            AnimateFade(DialogOverlay, false);
            _dialogTcs?.TrySetResult(MsgResult.Cancel);
        }

        private void UpdateMainUI()
        {
            if (_versionService.CurrentInstance == null)
            {
                CurrentLaunchVersionText.Text = L.Get("launch.no_version_hint");
                LaunchBtn.IsEnabled = false;
            }
            else
            {
                if (_launchService.IsInstanceRunning(_versionService.CurrentInstance.FullPath))
                {
                    CurrentLaunchVersionText.Text = L.Get("launch.running");
                    LaunchBtn.IsEnabled = false;
                }
                else
                {
                    CurrentLaunchVersionText.Text = _versionService.CurrentInstance.Name;
                    LaunchBtn.IsEnabled = true;
                }
            }
        }

        private void RefreshAllUI()
        {
            this.Title = L.Get("app.title");
            RefreshNavigationUI();
            RefreshLaunchUI();
            RefreshDownloadUI();
            RefreshModBrowserUI();
            RefreshSchematicUI();
            RefreshMultiplayerUI();
            RefreshSettingsUI();
            RefreshVersionSelectUI();
            RefreshMoreUI();
            RefreshDialogButtons();
            RefreshSaveDataUI();
            RefreshDataGridHeaders();
            RefreshDataTemplateListBoxes();
            UpdateMainUI();
        }

        private void RefreshDataTemplateListBoxes()
        {
            RebindListBox(ModBrowserListBox);
            RebindListBox(SchematicBrowserListBox);
            RebindListBox(RemoteVersionListBox);
            RebindListBox(InstanceListBox);
            RebindListBox(ModListBox);
            RebindListBox(LocalSchematicListBox);
        }

        private static void RebindListBox(ListBox lb)
        {
            if (lb.ItemsSource != null)
            {
                var src = lb.ItemsSource;
                lb.ItemsSource = null;
                lb.ItemsSource = src;
            }
        }

        private static void SetNavText(Button btn, string text)
        {
            if (btn.Content is StackPanel sp)
            {
                for (int i = sp.Children.Count - 1; i >= 0; i--)
                {
                    if (sp.Children[i] is TextBlock tb) { tb.Text = text; break; }
                }
            }
        }

        private void RefreshNavigationUI()
        {
            SetNavText(NavLaunchBtn, L.Get("nav.launch"));
            SetNavText(NavDownloadBtn, L.Get("nav.download"));
            SetNavText(NavMultiplayerBtn, L.Get("nav.multiplayer"));
            SetNavText(NavSettingsBtn, L.Get("nav.settings"));
            SetNavText(NavMoreBtn, L.Get("nav.more"));
        }

        private void RefreshLaunchUI()
        {
            GameNameText.Text = "Mindustry";
            LaunchGameText.Text = L.Get("launch.start_game");
            VersionSelectBtn.Content = L.Get("launch.version_select");
            VersionSettingsBtn.Content = L.Get("launch.version_settings");
            WelcomeText.Text = L.Get("launch.welcome");
        }

        private void RefreshDownloadUI()
        {
            SubTabDownloadSource.Content = L.Get("download.source");
            SubTabModBrowser.Content = L.Get("nav.mods");
            SubTabSchematics.Content = L.Get("nav.schematics");
            DownloadSourceLabel.Text = L.Get("download.source");
            RbOfficial.Content = L.Get("download.official");
            RbX.Content = L.Get("download.x_client");
            RbFoo.Content = L.Get("download.foo_client");
            DownloadSourceTitle.Text = L.T("download.source_with_name",
                RbOfficial.IsChecked == true ? L.Get("download.official")
                : RbX.IsChecked == true ? L.Get("download.x_client")
                : L.Get("download.foo_client"));
            RemoteVersionLoadingText.Text = L.Get("download.fetching");
        }

        private void RefreshModBrowserUI()
        {
            ModBrowserTitle.Text = L.Get("mods.browser_title");
            ModBrowserLoadingText.Text = L.Get("mods.fetching");
            ModSearchLabel.Text = L.Get("search.label");
            ModBrowserRefreshBtn.Content = L.Get("mods.refresh");
            ConfirmModInstallBtn.Content = L.Get("mods.install_start");
        }

        private void RefreshSchematicUI()
        {
            SchematicSourceLabel.Text = L.Get("nav.schematics");
            RbSchemMinRi2.Content = L.Get("schematics.source_minri");
            RbSchemDesignIt.Content = L.Get("schematics.source_designit");
            SchematicBrowserLoadingText.Text = L.Get("schematics.parsing_status");
            SchematicSearchLabel.Text = L.Get("search.label");
            SchematicRefreshBtn.Content = L.Get("schematics.force_refresh");
            FetchSchematicBtn.Content = L.Get("schematics.fetch");
            ConfirmSchematicInstallBtn.Content = L.Get("schematics.instant_install");
        }

        private void RefreshMultiplayerUI()
        {
            MultiplayerTitle.Text = L.Get("multiplayer.title");
            NicknameLabel.Text = L.Get("multiplayer.nickname");
            PlayerNameBox.Text = _configService.GetConfig().PlayerNickname;
            VirtualIpLabel.Text = L.Get("multiplayer.virtual_ip");
            JoinLobbyHeader.Text = L.Get("multiplayer.join_title");
            CreateLobbyHeader.Text = L.Get("multiplayer.create_title");
            RoomPlayersTitle.Text = L.Get("multiplayer.players_title");
            RoomPlayersHint.Text = L.Get("multiplayer.players_hint");
            BtnDownloadEasyTier.Content = L.Get("multiplayer.redownload");
            EasyTierRoomBox.ApplyTemplate();
            if (EasyTierRoomBox.Template.FindName("WaterMark", EasyTierRoomBox) is TextBlock wm)
                wm.Text = L.Get("multiplayer.room_placeholder");
            if (_multiplayerService.EasyTierProcess == null || _multiplayerService.EasyTierProcess.HasExited)
            {
                MyVirtualIpBox.Text = L.Get("multiplayer.not_connected");
                CreateRoomBtn.Content = L.Get("multiplayer.create");
                JoinRoomBtn.Content = L.Get("multiplayer.join");
                EasyTierStatusText.Text = L.Get("multiplayer.ready");
            }
        }

        private void RefreshSettingsUI()
        {
            SettingsTitle.Text = L.Get("settings.global_title");
            SettingsProxyLabel.Text = L.Get("settings.proxy_label");
            var proxyIdx = ProxyNodeBox.SelectedIndex;
            ProxyNodeBox.Items.Clear();
            ProxyNodeBox.Items.Add(L.Get("settings.proxy_0"));
            ProxyNodeBox.Items.Add(L.Get("settings.proxy_1"));
            ProxyNodeBox.Items.Add(L.Get("settings.proxy_2"));
            ProxyNodeBox.Items.Add(L.Get("settings.proxy_3"));
            ProxyNodeBox.Items.Add(L.Get("settings.proxy_4"));
            ProxyNodeBox.Items.Add(L.Get("settings.proxy_5"));
            ProxyNodeBox.SelectedIndex = proxyIdx >= 0 ? proxyIdx : 1;

            SettingsJavaPathLabel.Text = L.Get("settings.java_path_label");
            SettingsBrowseBtn.Content = L.Get("settings.browse");
            ScanGlobalJavaBtn.Content = L.Get("settings.rescan");
            SettingsRamLabel.Text = L.Get("settings.java_ram_label");
            GlobalAutoRamCheck.Content = L.Get("settings.auto_ram");

            SettingsLanguageLabel.Text = L.Get("settings.language_label");
            _suppressLangEvent = true;
            LanguageComboBox.Items.Clear();
            LanguageComboBox.Items.Add(new ComboBoxItem { Tag = "auto", Content = L.Get("settings.language_auto") });
            LanguageComboBox.Items.Add(new ComboBoxItem { Tag = "zh-CN", Content = L.Get("settings.language_zh") });
            LanguageComboBox.Items.Add(new ComboBoxItem { Tag = "en-US", Content = L.Get("settings.language_en") });
            LanguageComboBox.SelectedIndex = string.IsNullOrEmpty(_configService.GetConfig().Language) ? 0
                : _configService.GetConfig().Language == "zh-CN" ? 1
                : _configService.GetConfig().Language == "en-US" ? 2 : 0;
            _suppressLangEvent = false;

            if (!_suppressAutoRamEvent)
            {
                bool isAuto = GlobalAutoRamCheck.IsChecked ?? false;
                GlobalRamText.Text = isAuto ? L.T("settings.auto_ram_text", (int)GlobalRamSlider.Value) : $"{(int)GlobalRamSlider.Value} MB";
            }
        }

        private void RefreshVersionSelectUI()
        {
            VersionSelectFolderLabel.Text = L.Get("version.folder_list");
            ImportVersionBtn.Content = L.Get("version.import_jar");
            AddFolderBtn.Content = L.Get("version.import_folder");
        }

        private void RefreshMoreUI()
        {
            MoreHeader.Text = L.Get("more.title");
            CommunityResourcesHeader.Text = L.Get("more.community");
            WikiButtonText.Text = L.Get("more.wiki");
            WikiDescriptionText.Text = L.Get("more.wiki_desc");
            SupportHeader.Text = L.Get("more.support");
            SupportDescriptionText.Text = L.Get("more.buy_prompt");
        }

        private void RefreshDialogButtons()
        {
            DialogOkBtn.Content = L.Get("dialog.ok");
            DialogYesBtn.Content = L.Get("dialog.yes");
            DialogNoBtn.Content = L.Get("dialog.no");
            DialogCancelBtn.Content = L.Get("dialog.cancel");
            ModInstallCancelBtn.Content = L.Get("dialog.cancel");
            SchematicInstallCancelBtn.Content = L.Get("dialog.cancel");
            ReleaseNotesCloseBtn.Content = L.Get("release_notes.close");
        }

        private void RefreshDataGridHeaders()
        {
            // DataGrid column headers set dynamically in XAML, handled when needed
        }

        private void RefreshVersionSettingsUI()
        {
            if (_versionService.CurrentInstance != null)
                VSettingsTitle.Text = L.Get("vsettings.title");
            else
                VSettingsTitle.Text = L.Get("vsettings.title");

            VSidebarOverviewBtn.Content = L.Get("vsettings.overview");
            VSidebarConfigBtn.Content = L.Get("vsettings.config");
            VSidebarModBtn.Content = L.Get("vsettings.mod_manage");
            VSidebarSchematicBtn.Content = L.Get("vsettings.schematic_manage");
            VSidebarSaveDataBtn.Content = L.Get("vsettings.save_data");
            VSidebarOpenFolderBtn.Content = L.Get("vsettings.open_folder");
            OpenGameFolderItem.Header = L.Get("vsettings.open_game_folder");
            OpenDataFolderItem.Header = L.Get("vsettings.open_data_folder");

            VSettingsOverviewTitle.Text = L.Get("vsettings.version_overview");
            VSettingsInstanceTypeLabel.Text = L.Get("vsettings.mindustry_instance");
            VSettingsPathLabel.Text = L.Get("vsettings.physical_path");
            StartRenameBtn.Content = L.Get("vsettings.rename");
            ConfirmRenameBtn.Content = L.Get("vsettings.confirm");
            CancelRenameBtn.Content = L.Get("vsettings.cancel");

            VSettingsConfigTitle.Text = L.Get("vsettings.startup_options");
            VSettingsIsolationLabel.Text = L.Get("vsettings.isolation_label");
            var isoIdx = VSettingsIsolationBox.SelectedIndex;
            VSettingsIsolationBox.Items.Clear();
            VSettingsIsolationBox.Items.Add(L.Get("vsettings.isolation_on"));
            VSettingsIsolationBox.Items.Add(L.Get("vsettings.isolation_off"));
            VSettingsIsolationBox.SelectedIndex = isoIdx >= 0 ? isoIdx : 0;

            VSettingsJavaLabel.Text = L.Get("vsettings.custom_java");
            VSettingsBrowseBtn.Content = L.Get("settings.browse");
            ScanVersionJavaBtn.Content = L.Get("settings.rescan");
            VSettingsInstanceRamLabel.Text = L.Get("vsettings.instance_ram");
            VersionAutoRamCheck.Content = L.Get("vsettings.follow_auto");
            VSettingsJvmLabel.Text = L.Get("vsettings.custom_jvm");

            LocalModTitle.Text = L.Get("mods.local_title");
            LocalModRefreshBtn.Content = L.Get("mods.refresh");
            NoModText.Text = L.Get("mods.no_mods");

            LocalSchematicTitle.Text = L.Get("schematics.local_title");
            LocalSchematicRefreshBtn.Content = L.Get("mods.refresh");
            NoSchematicText.Text = L.Get("schematics.no_local");
        }

        private void RefreshSaveDataUI()
        {
            SaveDataTitle.Text = L.Get("saves.title");
            SaveDataSectionTitle.Text = L.Get("saves.section_title");
            SaveSettingsBtn.Content = L.Get("saves.save_changes");
            RescueSaveDataBtn.Content = L.Get("saves.rebuild_index");
            ParseSaveDataBtn.Content = L.Get("saves.read_edit");
            RefreshSavesBtn.Content = L.Get("saves.refresh_parse");
            SettingsSearchLabel.Text = L.Get("search.label");
        }

        private bool _suppressAutoRamEvent = false;
        private bool _suppressLangEvent = false;
        private bool _suppressSubTabEvent = false;
        private bool _isInitialized = false;
        private void LanguageComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_suppressLangEvent) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem item && item.Tag is string tag)
            {
                string target = tag;
                if (tag == "auto")
                    target = L.AutoDetect();

                if (target != L.CurrentLang)
                {
                    _configService.GetConfig().Language = tag == "auto" ? "" : target;
                    _configService.SaveConfig();
                    L.LoadLanguage(target);
                }
            }
        }
        private List<GameInstanceInfo> GetAllInstalledInstances()
        {
            return _versionService.GetAllInstalledInstances();
        }
        private void OpenVersionSelect_Click(object sender, RoutedEventArgs e)
        {
            FolderListBox.ItemsSource = null;
            FolderListBox.ItemsSource = _configService.GetConfig().ManagedFolders;
            if (_configService.GetConfig().ManagedFolders.Count > 0) FolderListBox.SelectedIndex = 0;
            ShowLaunchSubPanel(11);
        }

        private void AddNewFolder_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFolderDialog();
            if (d.ShowDialog() == true)
            {
                if (!_configService.GetConfig().ManagedFolders.Contains(d.FolderName))
                {
                    _configService.GetConfig().ManagedFolders.Add(d.FolderName);
                    _configService.SaveConfig();
                    OpenVersionSelect_Click(null!, null!);
                }
            }
        }

        private async void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string fp)
            {
                if (await ShowDialogAsync(L.Get("dialog.remove_folder_title"), L.T("dialog.remove_folder_msg", fp), DialogIcon.Question) == MsgResult.Yes)
                {
                    _configService.GetConfig().ManagedFolders.Remove(fp);
                    _configService.SaveConfig();
                    OpenVersionSelect_Click(null!, null!);
                }
            }
        }

        private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderListBox.SelectedItem is string p && Directory.Exists(p))
            {
                InstanceListBox.ItemsSource = VersionManagementService.GetInstancesInFolder(p);
            }
            else
            {
                InstanceListBox.ItemsSource = null;
            }
        }

        private void InstanceListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (InstanceListBox.SelectedItem is GameInstanceInfo info)
            {
                _versionService.CurrentInstance = info;
                _configService.GetConfig().LastSelectedInstancePath = info.FullPath;
                _configService.SaveConfig();
                CloseOverlays_Click(null!, null!);
            }
        }

        private async void DeleteInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameInstanceInfo info)
            {
                if (await ShowDialogAsync(L.Get("dialog.delete_version_title"), L.T("dialog.delete_version_msg", info.FullPath), DialogIcon.Warning) == MsgResult.Yes)
                {
                    try
                    {
                        Directory.Delete(info.FullPath, true);
                        if (_versionService.CurrentInstance != null && _versionService.CurrentInstance.FullPath == info.FullPath)
                        {
                            _versionService.CurrentInstance = null;
                            _configService.GetConfig().LastSelectedInstancePath = "";
                            _configService.SaveConfig();
                            UpdateMainUI();
                        }
                        FolderListBox_SelectionChanged(null!, null!);
                    }
                    catch (Exception ex)
                    {
                        ShowDialog(L.Get("dialog.error"), L.T("dialog.delete_error", ex.Message), DialogIcon.Error);
                    }
                }
            }
        }

        private void OpenVersionSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_versionService.CurrentInstance == null)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("status.select_instance_first"));
                return;
            }
            LoadVersionConfig(_versionService.CurrentInstance.FullPath);
            VSettingsTitle.Text = L.Get("vsettings.title");
            VSettingsIsolationBox.SelectedIndex = _versionService.CurrentVersionConfig.UseIsolation ? 0 : 1;
            VSettingsJavaComboBox.Text = _versionService.CurrentVersionConfig.CustomJavaPath;
            VSettingsJvmArgsBox.Text = _versionService.CurrentVersionConfig.CustomJvmArgs;
            VersionAutoRamCheck.IsChecked = _versionService.CurrentVersionConfig.UseAutoRam;
            VSettingsRamSlider.Value = Math.Min(_versionService.CurrentVersionConfig.CustomRamMB, GlobalRamSlider.Maximum);
            CancelRename_Click(null!, null!);
            VSidebarConfig_Click(null!, null!);
            ShowLaunchSubPanel(12);
        }

        private void ResetSidebarStyles()
        {
            var d = new SolidColorBrush(Color.FromRgb(51, 51, 51));
            VSidebarOverviewBtn.Foreground = d;
            VSidebarConfigBtn.Foreground = d;
            VSidebarModBtn.Foreground = d;
            VSidebarSchematicBtn.Foreground = d;
            VSidebarSaveDataBtn.Foreground = d;
            VSidebarOverviewBtn.FontWeight = FontWeights.Normal;
            VSidebarConfigBtn.FontWeight = FontWeights.Normal;
            VSidebarModBtn.FontWeight = FontWeights.Normal;
            VSidebarSchematicBtn.FontWeight = FontWeights.Normal;
            VSidebarSaveDataBtn.FontWeight = FontWeights.Normal;
            VSettingsOverviewPanel.Visibility = Visibility.Collapsed;
            VSettingsConfigPanel.Visibility = Visibility.Collapsed;
            VSettingsModPanel.Visibility = Visibility.Collapsed;
            VSettingsSchematicPanel.Visibility = Visibility.Collapsed;
            VSettingsSaveDataPanel.Visibility = Visibility.Collapsed;
        }

        private void VSidebarOverview_Click(object sender, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            VSettingsOverviewPanel.Visibility = Visibility.Visible;
            VSidebarOverviewBtn.Foreground = Brushes.DodgerBlue;
            VSidebarOverviewBtn.FontWeight = FontWeights.Bold;
            if (_versionService.CurrentInstance != null)
            {
                OverviewVersionName.Text = _versionService.CurrentInstance.Name;
                OverviewVersionPath.Text = _versionService.CurrentInstance.FullPath;
            }
        }

        private void VSidebarConfig_Click(object sender, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            VSettingsConfigPanel.Visibility = Visibility.Visible;
            VSidebarConfigBtn.Foreground = Brushes.DodgerBlue;
            VSidebarConfigBtn.FontWeight = FontWeights.Bold;
        }

        private void VSidebarMod_Click(object sender, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            VSettingsModPanel.Visibility = Visibility.Visible;
            VSidebarModBtn.Foreground = Brushes.DodgerBlue;
            VSidebarModBtn.FontWeight = FontWeights.Bold;
            ScanMods();
        }

        private void VSidebarSchematic_Click(object sender, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            VSettingsSchematicPanel.Visibility = Visibility.Visible;
            VSidebarSchematicBtn.Foreground = Brushes.DodgerBlue;
            VSidebarSchematicBtn.FontWeight = FontWeights.Bold;
            ScanSchematics();
        }

        private void VSidebarSaveData_Click(object sender, RoutedEventArgs e)
        {
            ResetSidebarStyles();
            VSettingsSaveDataPanel.Visibility = Visibility.Visible;
            VSidebarSaveDataBtn.Foreground = Brushes.DodgerBlue;
            VSidebarSaveDataBtn.FontWeight = FontWeights.Bold;
            ScanSaveDataStatus();
        }

        private void VSidebarOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.ContextMenu != null)
            {
                btn.ContextMenu.PlacementTarget = btn;
                btn.ContextMenu.IsOpen = true;
            }
        }

        private void OpenGameFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_versionService.CurrentInstance != null)
                Process.Start("explorer.exe", _versionService.CurrentInstance.FullPath);
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_versionService.CurrentInstance == null) return;
            LoadVersionConfig(_versionService.CurrentInstance.FullPath);
            string data = _versionService.CurrentVersionConfig.UseIsolation ? Path.Combine(_versionService.CurrentInstance.FullPath, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
            Directory.CreateDirectory(data);
            Process.Start("explorer.exe", data);
        }

        private void BrowseVersionJava_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter = "Java|java.exe;javaw.exe" };
            if (d.ShowDialog() == true)
                VSettingsJavaComboBox.Text = d.FileName;
        }

        private void StartRename_Click(object sender, RoutedEventArgs e)
        {
            if (_versionService.CurrentInstance == null) return;
            RenameTextBox.Text = _versionService.CurrentInstance.Name;
            StartRenameBtn.Visibility = Visibility.Collapsed;
            RenamePanel.Visibility = Visibility.Visible;
        }

        private void CancelRename_Click(object sender, RoutedEventArgs e)
        {
            StartRenameBtn.Visibility = Visibility.Visible;
            RenamePanel.Visibility = Visibility.Collapsed;
        }

        private void ConfirmRename_Click(object sender, RoutedEventArgs e)
        {
            if (_versionService.CurrentInstance == null) return;
            string nn = RenameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(nn) || nn.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("dialog.rename_invalid"));
                return;
            }

            if (nn == _versionService.CurrentInstance.Name)
            {
                CancelRename_Click(null!, null!);
                return;
            }

            try
            {
                string op = _versionService.CurrentInstance.FullPath;
                string np = Path.Combine(Directory.GetParent(op)!.FullName, nn);
                if (Directory.Exists(np))
                {
                    ShowDialog(L.Get("dialog.info"), L.Get("dialog.rename_exists"));
                    return;
                }
                Directory.Move(op, np);
                _versionService.CurrentInstance.Name = nn;
                _versionService.CurrentInstance.FullPath = np;
                if (_configService.GetConfig().LastSelectedInstancePath == op)
                {
                    _configService.GetConfig().LastSelectedInstancePath = np;
                    _configService.SaveConfig();
                }
                OverviewVersionName.Text = nn;
                OverviewVersionPath.Text = np;
                VSettingsTitle.Text = L.Get("vsettings.title");
                UpdateMainUI();
                FolderListBox_SelectionChanged(null!, null!);
                CancelRename_Click(null!, null!);
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("dialog.rename_error", ex.Message), DialogIcon.Error);
            }
        }
        private void ScanMods()
        {
            string modsDir = ModService.GetModsDir(_versionService.CurrentInstance, _versionService.CurrentVersionConfig);
            if (string.IsNullOrEmpty(modsDir))
            {
                ModListBox.ItemsSource = null;
                NoModText.Visibility = Visibility.Visible;
                return;
            }

            var list = ModService.ScanMods(modsDir);
            ModListBox.ItemsSource = list;
            NoModText.Visibility = list.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private string StripColors(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return Regex.Replace(input, @"\[.*?\]", "");
        }

        private void RefreshMods_Click(object sender, RoutedEventArgs e)
        {
            ScanMods();
        }

        private async void DeleteMod_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string p)
            {
                if (await ShowDialogAsync(L.Get("dialog.mdl"), L.Get("dialog.confirm_delete"), DialogIcon.Warning) == MsgResult.Yes)
                {
                    try
                    {
                        File.Delete(p);
                        Dispatcher.InvokeAsync(() => ScanMods());
                    }
                    catch (Exception ex)
                    {
                        ShowDialog(L.Get("dialog.error"), L.T("dialog.delete_error", ex.Message), DialogIcon.Error);
                    }
                }
            }
        }

        private void ScanSchematics()
        {
            string schematicsDir = SchematicService.GetSchematicsDir(_versionService.CurrentInstance, _versionService.CurrentVersionConfig);
            if (string.IsNullOrEmpty(schematicsDir))
            {
                LocalSchematicListBox.ItemsSource = null;
                NoSchematicText.Visibility = Visibility.Visible;
                return;
            }

            var files = SchematicService.ScanLocalSchematics(schematicsDir);
            LocalSchematicListBox.ItemsSource = files;
            NoSchematicText.Visibility = files.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void RefreshSchematics_Click(object sender, RoutedEventArgs e)
        {
            ScanSchematics();
        }

        private async void DeleteSchematic_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button b && b.Tag is string p)
            {
                if (await ShowDialogAsync(L.Get("dialog.mdl"), L.Get("dialog.confirm_delete_schematic"), DialogIcon.Warning) == MsgResult.Yes)
                {
                    try
                    {
                        File.Delete(p);
                        Dispatcher.InvokeAsync(() => ScanSchematics());
                    }
                    catch (Exception ex)
                    {
                        ShowDialog(L.Get("dialog.error"), L.T("dialog.delete_error", ex.Message), DialogIcon.Error);
                    }
                }
            }
        }

        // ===============================================
        // 核心增强：带 DataGrid 虚拟化的动态解析与编辑
        // ===============================================
        private string GetSettingsBinPath()
        {
            if (_versionService.CurrentInstance == null) return "";
            bool isIso = VSettingsIsolationBox.SelectedIndex == 0;
            string d = isIso ? Path.Combine(_versionService.CurrentInstance.FullPath, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
            return Path.Combine(d, "settings.bin");
        }

        private async void ScanSaveDataStatus()
        {
            string binPath = GetSettingsBinPath();
            if (string.IsNullOrEmpty(binPath)) return;
            if (!File.Exists(binPath))
            {
                SaveDataStatusText.Text = L.Get("saves.file_not_found");
                SaveDataStatusText.Foreground = Brushes.Gray;
                return;
            }
            SaveDataStatusText.Text = "...";
            SaveDataStatusText.Foreground = Brushes.Gray;
            var editor = new MindustrySettingsEditor();
            bool isHealthy = false;
            int count = 0;
            string errMsg = "";
            await Task.Run(() => { isHealthy = editor.LoadList(binPath, out var lst); count = lst.Count; errMsg = editor.ErrorMessage; });
            if (isHealthy)
            {
                SaveDataStatusText.Text = L.T("saves.parse_perfect", count);
                SaveDataStatusText.Foreground = Brushes.Green;
            }
            else
            {
                SaveDataStatusText.Text = L.T("saves.partial_damage", errMsg);
                SaveDataStatusText.Foreground = Brushes.Crimson;
            }
        }

        private async void ParseSaveData_Click(object sender, RoutedEventArgs e)
        {
            string binPath = GetSettingsBinPath();
            if (!File.Exists(binPath))
            {
                ShowDialog(L.Get("dialog.info"), L.Get("saves.no_settings_bin"));
                return;
            }
            var editor = new MindustrySettingsEditor();
            List<SettingItem>? lst = null;
            bool isHealthy = false;
            await Task.Run(() => { isHealthy = editor.LoadList(binPath, out var items); lst = items; });
            if (lst!.Count == 0 && !isHealthy)
            {
                ShowDialog(L.Get("dialog.error"), L.T("saves.parse_critical", editor.ErrorMessage), DialogIcon.Error);
                return;
            }
            _settingsView = CollectionViewSource.GetDefaultView(lst);
            UpdateSettingsFilter();
            SettingsDataGrid.ItemsSource = _settingsView;
            if (!isHealthy)
            {
                ShowDialog(L.Get("dialog.warning"), L.T("saves.parse_warning", editor.ErrorMessage), DialogIcon.Warning);
            }
        }

        private void SettingsSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateSettingsFilter();
        }

        private void SettingsCategoryBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSettingsFilter();
        }

        private void UpdateSettingsFilter()
        {
            if (_settingsView == null) return;
            string q = SettingsSearchBox?.Text.ToLower() ?? "";
            int category = SettingsCategoryBox?.SelectedIndex ?? 0;
            _settingsView.Filter = o =>
            {
                if (o is SettingItem si)
                {
                    bool matchSearch = string.IsNullOrEmpty(q) || si.Key.ToLower().Contains(q) || (!si.IsBinary && si.DisplayValue.ToLower().Contains(q));
                    if (!matchSearch) return false;
                    bool isTechTree = si.Key.Contains("req-") || si.Key.Contains("-unlocked") || si.Key.Contains("sector-") || si.Key.StartsWith("save-");
                    if (category == 1) return !isTechTree;
                    if (category == 2) return isTechTree;
                    return true;
                }
                return false;
            };
        }

        private void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_settingsView == null) return;
            var list = _settingsView.SourceCollection.Cast<SettingItem>().ToList();
            var editor = new MindustrySettingsEditor();
            try
            {
                string p = GetSettingsBinPath();
                string bak = p + ".bak";
                File.Copy(p, bak, true);
                editor.SaveList(p, list);
                ShowDialog(L.Get("dialog.mdl"), L.Get("saves.save_success"), DialogIcon.Info);
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("saves.save_error", ex.Message), DialogIcon.Error);
            }
        }

        private async void RescueSaveData_Click(object sender, RoutedEventArgs e)
        {
            string binPath = GetSettingsBinPath();
            if (!File.Exists(binPath)) return;
            if (await ShowDialogAsync(L.Get("dialog.warning"), L.Get("saves.rebuild_prompt"), DialogIcon.Warning) == MsgResult.Yes)
            {
                try
                {
                    File.Move(binPath, binPath + $".bak_{DateTime.Now:MMddHHmm}");
                    string bak = Path.Combine(Path.GetDirectoryName(binPath)!, "settings.backup");
                    if (File.Exists(bak)) File.Delete(bak);
                    ShowDialog(L.Get("dialog.info"), L.Get("saves.rebuild_sent"));
                    ScanSaveDataStatus();
                    SettingsDataGrid.ItemsSource = null;
                }
                catch (Exception ex)
                {
                    ShowDialog(L.Get("dialog.error"), L.T("saves.rebuild_error", ex.Message), DialogIcon.Error);
                }
            }
        }

        private void LaunchGame_Click(object sender, RoutedEventArgs e)
        {
            if (_versionService.CurrentInstance == null)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("status.select_version_first"));
                return;
            }

            string instancePath = _versionService.CurrentInstance.FullPath;

            if (_launchService.IsInstanceRunning(instancePath))
            {
                ShowDialog(L.Get("dialog.info"), L.Get("status.already_running"), DialogIcon.Warning);
                return;
            }

            string jar = Path.Combine(instancePath, "Mindustry.jar");
            if (!File.Exists(jar))
            {
                ShowDialog(L.Get("dialog.error"), L.Get("status.core_missing"), DialogIcon.Error);
                return;
            }

            LoadVersionConfig(instancePath);
            string data = _launchService.GetDataDir(instancePath);
            if (_versionService.CurrentVersionConfig.UseIsolation) Directory.CreateDirectory(data);

            try
            {
                var pInfo = _launchService.BuildLaunchProcessInfo(instancePath, jar);
                Process? p = Process.Start(pInfo);
                if (p == null)
                    return;

                _launchService.MarkInstanceRunning(instancePath);
                UpdateMainUI();

                string errorLog = "";
                _ = Task.Run(async () =>
                {
                    while (!p.HasExited)
                    {
                        string? line = await p.StandardError.ReadLineAsync();
                        if (line != null)
                            errorLog += line + "\n";
                    }
                });

                p.EnableRaisingEvents = true;
                p.Exited += (s, ev) => Dispatcher.InvokeAsync(() =>
                {
                    // 进程结束，把该路径从运行列表中移除，并刷新 UI
                    _launchService.MarkInstanceStopped(instancePath);
                    UpdateMainUI();

                    if (p.ExitCode != 0) AnalyzeCrash(errorLog);
                });
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("status.startup_fail", ex.Message), DialogIcon.Error);
            }
        }

        private void AnalyzeCrash(string log)
        {
            var (title, report) = GameLaunchService.AnalyzeCrashLog(log);
            ReleaseNotesTitle.Text = title;
            ReleaseNotesTitle.Foreground = Brushes.Crimson;
            ReleaseNotesText.Text = report;
            OpenRepoBtn.Visibility = Visibility.Collapsed;
            ExportCrashBtn.Visibility = Visibility.Visible;
            AnimateFade(ReleaseNotesOverlay, true);
        }

        private void ExportCrash_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Title = L.Get("dialog.export_report_title"), Filter = L.Get("dialog.export_report_filter"), FileName = $"MDL_CrashReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, ReleaseNotesText.Text);
                    ShowDialog(L.Get("dialog.mdl"), L.Get("dialog.export_success"), DialogIcon.Info);
                }
                catch (Exception ex)
                {
                    ShowDialog(L.Get("dialog.error"), L.T("dialog.export_error", ex.Message), DialogIcon.Error);
                }
            }
        }
        private async void RefreshModBrowser_Click(object sender, RoutedEventArgs e)
        {
            await FetchModRegistryAsync();
        }
        private async Task FetchModRegistryAsync()
        {
            ModBrowserLoadingText.Visibility = Visibility.Visible;
            ModBrowserListBox.Visibility = Visibility.Collapsed;

            try
            {
                var list = await _modService.FetchModRegistryAsync();
                ModBrowserListBox.ItemsSource = list;
                ModBrowserListBox.Visibility = Visibility.Visible;
                ModBrowserLoadingText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                ModBrowserLoadingText.Text = L.T("mods.fetch_error", ex.InnerException?.Message ?? ex.Message);
            }
        }
        private void ModSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string k = ModSearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(k))
            {
                ModBrowserListBox.ItemsSource = _modService.AllOnlineMods;
            }
            else
            {
                ModBrowserListBox.ItemsSource = _modService.AllOnlineMods.Where(m =>
                    m.Name.ToLower().Contains(k)
                    || m.Author.ToLower().Contains(k)
                    || (m.Description != null && m.Description.ToLower().Contains(k))
                ).ToList();
            }
        }
        private void ModItem_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is ModRegistryEntry mod)
            {
                ReleaseNotesTitle.Text = L.T("mods.detail_title", mod.Name);
                ReleaseNotesTitle.Foreground = Brushes.Black;
                ReleaseNotesText.Text = L.T("mods.detail_format", mod.Author, mod.Repo, mod.Description);
                _currentDetailUrl = $"https://github.com/{mod.Repo}";
                OpenRepoBtn.Visibility = Visibility.Visible;
                ExportCrashBtn.Visibility = Visibility.Collapsed;
                AnimateFade(ReleaseNotesOverlay, true);
            }
        }
        private async void InstallModFromBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                ShowDialog(L.Get("dialog.mdl"), L.Get("status.busy_downloading"), DialogIcon.Warning);
                return;
            }

            if (sender is Button b && b.Tag is ModRegistryEntry mod)
            {
                var all = GetAllInstalledInstances();
                if (all.Count == 0)
                {
                    ShowDialog(L.Get("dialog.info"), L.Get("mods.no_instances"));
                    return;
                }

                _modService.SelectedModToInstall = mod;
                ModInstallTitle.Text = L.T("mods.install_title_with_name", mod.Name);
                AllInstancesListBox.ItemsSource = all;

                if (_versionService.CurrentInstance != null)
                {
                    var m = all.FirstOrDefault(i => i.FullPath == _versionService.CurrentInstance.FullPath);
                    if (m != null)
                        AllInstancesListBox.SelectedItem = m;
                }

                ModVersionComboBox.ItemsSource = null;
                ModInstallProgressPanel.Visibility = Visibility.Visible;
                ModInstallStatusText.Text = L.Get("mods.install_preparing");
                AllInstancesListBox.IsEnabled = false;
                ModVersionComboBox.IsEnabled = false;
                ConfirmModInstallBtn.IsEnabled = false;

                AnimateFade(ModInstallOverlay, true);

                try
                {
                    var rels = await _modService.FetchModReleasesAsync(mod.Repo);
                    if (rels != null)
                    {
                        if (rels.Count == 0)
                        {
                            ShowDialog(L.Get("dialog.mdl"), L.Get("mods.no_releases"), DialogIcon.Info);
                            AnimateFade(ModInstallOverlay, false);
                            return;
                        }
                        ModVersionComboBox.ItemsSource = rels;
                        ModVersionComboBox.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    ShowDialog(L.Get("dialog.error"), L.T("mods.fetch_version_error", ex.InnerException?.Message ?? ex.Message), DialogIcon.Error);
                    AnimateFade(ModInstallOverlay, false);
                    return;
                }
                finally
                {
                    ModInstallProgressPanel.Visibility = Visibility.Collapsed;
                    AllInstancesListBox.IsEnabled = true;
                    ModVersionComboBox.IsEnabled = true;
                    ValidateModInstallForm();
                }
            }
        }
        private void ModInstallForm_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ValidateModInstallForm();
        }

        private void ValidateModInstallForm()
        {
            ConfirmModInstallBtn.IsEnabled = AllInstancesListBox.SelectedItem != null
                && ModVersionComboBox.SelectedItem != null;
        }

        private void CancelModInstall_Click(object sender, RoutedEventArgs e)
        {
            AnimateFade(ModInstallOverlay, false);
        }
        private async void ConfirmModInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_modService.SelectedModToInstall == null
                || AllInstancesListBox.SelectedItem is not GameInstanceInfo target
                || ModVersionComboBox.SelectedItem is not GitHubRelease rel)
                return;

            ConfirmModInstallBtn.IsEnabled = false;
            ModInstallProgressPanel.Visibility = Visibility.Visible;
            ToggleDownloadState(true);

            try
            {
                var asset = rel.Assets?.FirstOrDefault(a =>
                    a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                    || a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase));

                string url, file;
                if (asset != null)
                {
                    url = UrlHelper.Format(asset.BrowserDownloadUrl);
                    file = asset.Name;
                }
                else
                {
                    if (await ShowDialogAsync(L.Get("dialog.mdl"), L.Get("mods.no_asset"), DialogIcon.Question) == MsgResult.Yes)
                    {
                        url = UrlHelper.Format($"https://github.com/{_modService.SelectedModToInstall.Repo}/archive/refs/tags/{rel.TagName}.zip");
                        file = $"{string.Join("_", _modService.SelectedModToInstall.Name.Split(Path.GetInvalidFileNameChars()))}_{rel.TagName}_source.zip";
                    }
                    else
                    {
                        ModInstallProgressPanel.Visibility = Visibility.Collapsed;
                        return;
                    }
                }

                LoadVersionConfig(target.FullPath);
                string modsDir = Path.Combine(
                    _versionService.CurrentVersionConfig.UseIsolation
                        ? Path.Combine(target.FullPath, "data")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry"),
                    "mods");
                Directory.CreateDirectory(modsDir);

                var prog = new Progress<double>(p =>
                {
                    ModInstallProgressBar.Value = p;
                    ModInstallStatusText.Text = L.T("mods.install_downloading", p);
                });
                await _downloadService.DownloadFileAsync(url, Path.Combine(modsDir, file), prog);
                ShowDialog(L.Get("dialog.mdl"), L.Get("mods.install_success"), DialogIcon.Info);
                AnimateFade(ModInstallOverlay, false);
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("mods.install_error", ex.InnerException?.Message ?? ex.Message), DialogIcon.Error);
            }
            finally
            {
                ConfirmModInstallBtn.IsEnabled = true;
                ToggleDownloadState(false);
            }
        }

        private void SchematicSource_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                var parts = tag.Split('|');
                if (parts.Length == 2)
                {
                    _schematicService.CurrentRepo = parts[0];
                    _schematicService.CurrentBranch = parts[1];
                    if (SchematicSearchBox != null)
                        SchematicSearchBox.Text = "";
                    if (SchematicBrowserListBox != null)
                        SchematicBrowserListBox.Visibility = Visibility.Collapsed;
                    _schematicService.AllOnlineSchematics.Clear();
                    string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", $"{_schematicService.CurrentRepo.Replace("/", "_")}.zip");
                    if (File.Exists(cachePath))
                        _ = FetchSchematicsAsync(false);
                    else if (FetchSchematicBtn != null)
                        FetchSchematicBtn.Visibility = Visibility.Visible;
                }
            }
        }

        private async void FetchSchematic_Click(object sender, RoutedEventArgs e)
        {
            await FetchSchematicsAsync(false);
        }

        private async void RefreshSchematicBrowser_Click(object sender, RoutedEventArgs e)
        {
            await FetchSchematicsAsync(true);
        }
        private async Task FetchSchematicsAsync(bool forceRefresh)
        {
            _schematicService.FetchCts?.Cancel();
            _schematicService.FetchCts?.Dispose();
            _schematicService.FetchCts = new CancellationTokenSource();
            var token = _schematicService.FetchCts.Token;
            await _schematicService.FetchLock.WaitAsync();

            try
            {
                if (token.IsCancellationRequested) return;

                if (FetchSchematicBtn != null)
                    FetchSchematicBtn.Visibility = Visibility.Collapsed;

                if (SchematicBrowserLoadingText != null)
                    SchematicBrowserLoadingText.Visibility = Visibility.Visible;

                if (SchematicBrowserListBox != null)
                    SchematicBrowserListBox.Visibility = Visibility.Collapsed;

                ToggleDownloadState(true);

                string zipPath = _schematicService.GetCacheZipPath();

                if (forceRefresh || !File.Exists(zipPath))
                {
                    SchematicBrowserLoadingText!.Text = L.Get("schematics.fetching_zip");
                    await _schematicService.DownloadRepoZipAsync(zipPath, token);
                }

                if (token.IsCancellationRequested) return;

                SchematicBrowserLoadingText!.Text = L.Get("schematics.parsing");

                var newList = await Task.Run(() =>
                    SchematicService.ParseSchematicsFromZip(zipPath, token), token);

                if (token.IsCancellationRequested) return;

                _schematicService.AllOnlineSchematics = newList;
                if (SchematicBrowserListBox != null)
                {
                    SchematicBrowserListBox.ItemsSource = null;
                    SchematicBrowserListBox.ItemsSource = _schematicService.AllOnlineSchematics;
                    SchematicBrowserListBox.Visibility = Visibility.Visible;
                }

                if (SchematicBrowserLoadingText != null)
                    SchematicBrowserLoadingText.Visibility = Visibility.Collapsed;
            }
            catch (TaskCanceledException) { Debug.WriteLine("Schematic fetch cancelled."); }
            catch (OperationCanceledException) { Debug.WriteLine("Schematic fetch cancelled."); }
            catch (Exception ex)
            {
                if (SchematicBrowserLoadingText != null && !token.IsCancellationRequested)
                    SchematicBrowserLoadingText.Text = L.T("schematics.fetch_error", ex.InnerException?.Message ?? ex.Message);
                if (FetchSchematicBtn != null)
                    FetchSchematicBtn.Visibility = Visibility.Visible;
            }
            finally
            {
                _schematicService.FetchLock.Release();
                if (!token.IsCancellationRequested)
                    ToggleDownloadState(false);
            }
        }
        private void SchematicSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string k = SchematicSearchBox.Text.ToLower();
            SchematicBrowserListBox.ItemsSource = string.IsNullOrWhiteSpace(k)
                ? _schematicService.AllOnlineSchematics
                : _schematicService.AllOnlineSchematics.Where(s =>
                    s.UI_Name.ToLower().Contains(k) || s.UI_Description.ToLower().Contains(k)).ToList();
        }
        private void InstallSchematicFromBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                ShowDialog(L.Get("dialog.mdl"), L.Get("status.busy_downloading"), DialogIcon.Warning);
                return;
            }

            if (sender is Button b && b.Tag is SchematicEntry schematic)
            {
                var all = GetAllInstalledInstances();
                if (all.Count == 0)
                {
                    ShowDialog(L.Get("dialog.info"), L.Get("status.no_instances"));
                    return;
                }

                _schematicService.SelectedSchematicToInstall = schematic;
                SchematicInstallTitle.Text = L.T("schematics.install_title_with_name", schematic.UI_Name);
                SchematicInstancesListBox.ItemsSource = all;

                if (_versionService.CurrentInstance != null)
                {
                    var m = all.FirstOrDefault(i => i.FullPath == _versionService.CurrentInstance.FullPath);
                    if (m != null)
                        SchematicInstancesListBox.SelectedItem = m;
                }

                SchematicInstancesListBox.IsEnabled = true;
                ConfirmSchematicInstallBtn.IsEnabled = SchematicInstancesListBox.SelectedItem != null;
                AnimateFade(SchematicInstallOverlay, true);
            }
        }
        private void SchematicInstallForm_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ConfirmSchematicInstallBtn.IsEnabled = SchematicInstancesListBox.SelectedItem != null;
        }

        private void CancelSchematicInstall_Click(object sender, RoutedEventArgs e)
        {
            AnimateFade(SchematicInstallOverlay, false);
        }
        private void ConfirmSchematicInstall_Click(object sender, RoutedEventArgs e)
        {
            if (_schematicService.SelectedSchematicToInstall == null || SchematicInstancesListBox.SelectedItem is not GameInstanceInfo target)
                return;

            try
            {
                LoadVersionConfig(target.FullPath);

                string schematicDir = Path.Combine(
                    _versionService.CurrentVersionConfig.UseIsolation
                        ? Path.Combine(target.FullPath, "data")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry"),
                    "schematics");
                Directory.CreateDirectory(schematicDir);

                string targetFile = Path.Combine(schematicDir, _schematicService.SelectedSchematicToInstall.FileName);
                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
                string zipPath = Path.Combine(cacheDir, $"{_schematicService.CurrentRepo.Replace("/", "_")}.zip");

                using var fs = File.OpenRead(zipPath);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
                var entry = zip.GetEntry(_schematicService.SelectedSchematicToInstall.ZipEntryFullName);
                if (entry != null)
                {
                    entry.ExtractToFile(targetFile, true);
                    ShowDialog(L.Get("dialog.mdl"), L.Get("schematics.install_success"), DialogIcon.Info);
                }

                AnimateFade(SchematicInstallOverlay, false);
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("schematics.install_error", ex.Message), DialogIcon.Error);
            }
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            this.DragMove();
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void OpenWiki_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://mdtwiki.top/") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("status.cannot_open_link", ex.Message), DialogIcon.Error);
            }
        }

        private void OpenItch_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://anuke.itch.io/mindustry") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("status.cannot_open_link", ex.Message), DialogIcon.Error);
            }
        }

        private void OpenSteam_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://store.steampowered.com/app/1127400/Mindustry") { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("status.cannot_open_link", ex.Message), DialogIcon.Error);
            }
        }

        private async void DownloadSource_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string repo)
            {
                _downloadService.CurrentDownloadRepo = repo;
                if (DownloadSourceTitle != null)
                {
                    string sourceName = rb == RbOfficial ? L.Get("download.official")
                        : rb == RbX ? L.Get("download.x_client")
                        : L.Get("download.foo_client");
                    DownloadSourceTitle.Text = L.T("download.source_with_name", sourceName);
                }
                await FetchRemoteVersionsAsync();
            }
        }
        private async Task FetchRemoteVersionsAsync()
        {
            if (RemoteVersionLoadingText != null)
            {
                RemoteVersionLoadingText.Text = L.Get("download.fetching_short");
                RemoteVersionLoadingText.Visibility = Visibility.Visible;
            }

            if (RemoteVersionListBox != null)
                RemoteVersionListBox.Visibility = Visibility.Collapsed;

            try
            {
                var list = await _downloadService.FetchFilteredReleasesAsync();

                if (RemoteVersionListBox != null)
                {
                    RemoteVersionListBox.ItemsSource = list;
                    RemoteVersionListBox.Visibility = Visibility.Visible;
                }

                if (RemoteVersionLoadingText != null)
                    RemoteVersionLoadingText.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                if (RemoteVersionLoadingText != null)
                    RemoteVersionLoadingText.Text = L.T("download.fetch_timeout", ex.InnerException?.Message ?? ex.Message);
            }
        }
        private void RemoteVersion_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is GitHubRelease rel)
            {
                ReleaseNotesTitle.Text = L.T("release_notes.version_title", rel.TagName);
                ReleaseNotesTitle.Foreground = Brushes.Black;
                ReleaseNotesText.Text = string.IsNullOrWhiteSpace(rel.Body)
                    ? L.Get("mods.blank_description")
                    : rel.Body;
                _currentDetailUrl = $"https://github.com/{_downloadService.CurrentDownloadRepo}/releases/tag/{rel.TagName}";
                OpenRepoBtn.Visibility = Visibility.Visible;
                ExportCrashBtn.Visibility = Visibility.Collapsed;
                AnimateFade(ReleaseNotesOverlay, true);
            }
        }

        private void OpenRepo_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentDetailUrl))
            {
                try
                {
                    Process.Start(new ProcessStartInfo(_currentDetailUrl) { UseShellExecute = true });
                }
                catch (Exception ex)
                {
                    ShowDialog(L.Get("dialog.error"), L.T("status.cannot_open_link", ex.Message), DialogIcon.Error);
                }
            }
        }
        private async void DownloadVersion_Click(object sender, RoutedEventArgs e)
        {
            if (_configService.GetConfig().ManagedFolders.Count == 0)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("status.import_folder_first"));
                return;
            }

            var rel = (sender as Button)?.Tag as GitHubRelease;
            if (rel == null)
                return;

            if (_isDownloading)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("download.busy"));
                return;
            }

            var candidates = RemoteDownloadService.FilterClientAssets(rel);

            if (candidates.Count == 0)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("download.no_client"));
                return;
            }

            GitHubAsset? asset = null;
            if (_downloadService.CurrentDownloadRepo.Contains("antigrief", StringComparison.OrdinalIgnoreCase))
            {
                var audio = RemoteDownloadService.SelectFooAudioAsset(candidates);
                var standard = RemoteDownloadService.SelectBestAsset(candidates, _downloadService.CurrentDownloadRepo);

                if (audio != null && standard != null)
                {
                    var r = await ShowDialogAsync(L.Get("download.foo_voice_title"), L.Get("download.foo_voice_msg"), DialogIcon.Question, showCancel: true);
                    if (r == MsgResult.Yes)
                        asset = audio;
                    else if (r == MsgResult.No)
                        asset = standard;
                    else
                        return;
                }
            }

            asset ??= RemoteDownloadService.SelectBestAsset(candidates, _downloadService.CurrentDownloadRepo);

            if (asset == null)
            {
                ShowDialog(L.Get("dialog.info"), L.Get("download.cannot_determine"));
                return;
            }

            string folder = _downloadService.GetDownloadFolderName(rel.TagName);

            Directory.CreateDirectory(folder);
            DownloadPanel.Visibility = Visibility.Visible;
            ToggleDownloadState(true);
            try
            {
                var prog = new Progress<double>(p =>
                {
                    DownloadProgressBar.Value = p;
                    StatusText.Text = L.T("mods.install_downloading", p);
                });
                await _downloadService.DownloadFileAsync(UrlHelper.Format(asset.BrowserDownloadUrl), Path.Combine(folder, "Mindustry.jar"), prog);
                ShowDialog(L.Get("dialog.success"), L.Get("download.success"), DialogIcon.Info);
                StatusText.Text = L.Get("download.success");
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("download.fail", ex.InnerException?.Message ?? ex.Message), DialogIcon.Error);
            }
            finally
            {
                await Task.Delay(2000);
                DownloadPanel.Visibility = Visibility.Collapsed;
                ToggleDownloadState(false);
            }
        }
        private async void AutoScanGlobalJava_Click(object sender, RoutedEventArgs e)
            => await AutoScanJava(GlobalJavaComboBox, ScanGlobalJavaBtn);

        private async void AutoScanVersionJava_Click(object sender, RoutedEventArgs e)
            => await AutoScanJava(VSettingsJavaComboBox, ScanVersionJavaBtn);

        private async Task AutoScanJava(ComboBox comboBox, Button scanBtn)
        {
            try
            {
                scanBtn.Content = L.Get("settings.scanning");
                scanBtn.IsEnabled = false;
                string currentPath = comboBox.Text;
                var javas = await Task.Run(() => JavaScanner.Scan(currentPath, false));
                comboBox.ItemsSource = javas;
                if (javas.Count > 0)
                    comboBox.Text = javas[0].Path;
                else
                    ShowDialog(L.Get("dialog.mdl"), L.Get("settings.no_java"), DialogIcon.Info);
            }
            catch (Exception ex)
            {
                ShowDialog(L.Get("dialog.error"), L.T("settings.scan_error", ex.Message), DialogIcon.Error);
            }
            finally
            {
                scanBtn.Content = L.Get("settings.rescan");
                scanBtn.IsEnabled = true;
            }
        }

        private void BrowseGlobalJavaBtn_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter = "Java|java.exe;javaw.exe" };
            if (d.ShowDialog() == true)
                GlobalJavaComboBox.Text = d.FileName;
        }
        private void LoadVersionConfig(string p)
        {
            _versionService.LoadVersionConfig(p);
        }

        private void SaveVersionConfig(string p)
        {
            _versionService.CurrentVersionConfig.CustomJavaPath = VSettingsJavaComboBox.Text;
            _versionService.CurrentVersionConfig.CustomJvmArgs = VSettingsJvmArgsBox.Text;
            _versionService.CurrentVersionConfig.UseIsolation = VSettingsIsolationBox.SelectedIndex == 0;
            if (VSettingsRamSlider != null)
                _versionService.CurrentVersionConfig.CustomRamMB = (int)VSettingsRamSlider.Value;

            VersionManagementService.SaveVersionConfigToFile(p, _versionService.CurrentVersionConfig);
        }

    }
}
