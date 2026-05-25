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
                    ShowDialog("提示", "请先选择版本！");
                    return;
                }

                // 2. 拿到 data 文件夹，再拼上 saves
                string dataDir = Path.GetDirectoryName(binPath);
                string savesDir = Path.Combine(dataDir, "saves");

                // 3. 检查目录是否存在
                if (!Directory.Exists(savesDir))
                {
                    ShowDialog("提示", $"存档目录不存在：\n{savesDir}");
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
                    catch { }
                }

                // 5. 按照修改时间倒序排列并绑定给前台
                SavesListView.ItemsSource = saveList.OrderByDescending(x => x.PlayTime).ToList();
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"刷新失败: {ex.Message}", DialogIcon.Error);
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
                        processedName = $"{planet} - 区块 {parts[2]}";
                    }
                }
                else if (nameNoExt.Replace("-backup", "") == "0")
                {
                    processedName = "零号地区 (Ground Zero)";
                }

                meta.MapName = processedName + (nameNoExt.EndsWith("backup") ? " (备份)" : "");
                meta.Author = "官方战役";
                meta.Wave = "高压压缩";
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
                            meta.Author = "本地玩家";
                            meta.Wave = "点击查看";
                        }
                    }
                }
            }
            catch
            {
                meta.Author = "损坏/未知";
            }

            return meta;
        }
        private void ImportVersionBtn_Click(object sender, RoutedEventArgs e)
        {
            // 1. 检查是否设置了版本存储目录
            if (_config.ManagedFolders == null || _config.ManagedFolders.Count == 0)
            {
                ShowDialog("提示", "请先在设置中添加一个游戏存储目录！");
                return;
            }

            // 2. 呼出资源管理器选择 jar 文件
            var openFileDialog = new OpenFileDialog
            {
                Filter = "Mindustry Jar 文件 (*.jar)|*.jar",
                Title = "选择要导入的 Mindustry.jar 文件"
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
                        ShowDialog("错误", "版本名称包含非法字符（如 \\ / : * ? \" < > |），请换一个名称！", DialogIcon.Error);
                        return;
                    }
                }

                // 5. 准备目标路径
                string targetBaseDir = _config.ManagedFolders[0]; // 默认导入到第一个管理的目录
                string targetDir = Path.Combine(targetBaseDir, "Versions", versionName);

                if (Directory.Exists(targetDir))
                {
                    ShowDialog("提示", "该版本名称已存在，请换一个名字！");
                    return;
                }

                // 6. 执行创建和复制
                try
                {
                    Directory.CreateDirectory(targetDir);
                    // 核心要求：将任意名字的 jar 复制过去并重命名为 Mindustry.jar
                    File.Copy(sourceFilePath, Path.Combine(targetDir, "Mindustry.jar"), true);

                    ShowDialog("成功", $"成功导入版本：{versionName}", DialogIcon.Info);

                    // 👇 刷新主 UI 和版本列表，这里的 UpdateMainUI() 是你代码中自带的方法
                    UpdateMainUI();
                }
                catch (Exception ex)
                {
                    ShowDialog("错误", $"导入失败: {ex.Message}", DialogIcon.Error);
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
                Title = "输入版本名称",
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this, // 模态窗口，跟随主窗口
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245))
            };

            StackPanel panel = new StackPanel() { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock() { Text = "请为导入的版本起一个专属名称：", FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) });

            TextBox inputBox = new TextBox() { Height = 30, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(5, 0, 5, 0) };
            panel.Children.Add(inputBox);

            Button confirmBtn = new Button()
            {
                Content = "确定导入",
                Width = 90,
                Height = 32,
                Margin = new Thickness(0, 15, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Right,
                Cursor = Cursors.Hand,
                Background = new SolidColorBrush(Color.FromRgb(33, 150, 243)),
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
        // 1. 全局配置与基础变量
        // ==========================================
        private static readonly string ConfigFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launcher_config.json");
        private AppConfig _config = new AppConfig();
        private GameInstanceInfo? _currentInstance;
        private HashSet<string> _runningInstancePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private VersionConfig _currentVersionConfig = new VersionConfig();
        private readonly HttpClient _http;

        private string _currentDownloadRepo = "Anuken/Mindustry";
        private List<ModRegistryEntry> _allOnlineMods = new List<ModRegistryEntry>();
        private ModRegistryEntry? _selectedModToInstall;

        private string _currentSchematicRepo = "MinRi2/schematics-archives";
        private string _currentSchematicBranch = "master";
        private List<SchematicEntry> _allOnlineSchematics = new List<SchematicEntry>();
        private SchematicEntry? _selectedSchematicToInstall;

        private CancellationTokenSource? _schematicFetchCts;
        private readonly SemaphoreSlim _schematicFetchLock = new SemaphoreSlim(1, 1);
        private bool _isDownloading = false;
        private string _currentDetailUrl = "";

        // 通用弹窗
        private TaskCompletionSource<MsgResult>? _dialogTcs;
        public enum MsgResult { Ok, Yes, No, Cancel }
        public enum DialogIcon { Info, Warning, Error, Question }

        private ICollectionView? _settingsView;

        // ==========================================
        // 2. 联机大厅：底层变量 (雷达 + 进程)
        // ==========================================
        private Process? _easyTierProcess = null;
        private ObservableCollection<RoomPlayerInfo> _onlinePlayers = new ObservableCollection<RoomPlayerInfo>();
        private UdpClient? _discoveryListener;
        private CancellationTokenSource? _discoveryCts;
        private DispatcherTimer? _discoveryTimer;
        private string _myBroadcastIp = "";
        private string _myNickname = "";

        // ==========================================
        // 3. 窗口初始化
        // ==========================================
        public MainWindow()
        {
            var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (m, c, ch, e) => true };
            _http = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(45) };
            _http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            LoadConfig();
            GlobalJavaComboBox.Text = _config.GlobalJavaPath;
            PlayerNameBox.Text = _config.PlayerNickname;
            int maxRam = (HardwareInfo.GetTotalPhysicalMemoryMB() / 512) * 512;
            GlobalRamSlider.Maximum = maxRam;
            VSettingsRamSlider.Maximum = maxRam;
            _config.GlobalRamMB = Math.Min(_config.GlobalRamMB, maxRam);
            GlobalRamSlider.Value = _config.GlobalRamMB;
            GlobalAutoRamCheck.IsChecked = _config.GlobalUseAutoRam;
            UrlHelper.ProxyIndex = _config.ProxyNodeIndex;
            ProxyNodeBox.SelectedIndex = _config.ProxyNodeIndex;

            if (!string.IsNullOrEmpty(_config.LastSelectedInstancePath) && File.Exists(Path.Combine(_config.LastSelectedInstancePath, "Mindustry.jar")))
            {
                _currentInstance = new GameInstanceInfo { Name = Path.GetFileName(_config.LastSelectedInstancePath), FullPath = _config.LastSelectedInstancePath };
            }

            UpdateMainUI();
            RbOfficial.IsChecked = true;
            MainTabControl.SelectedIndex = -1; SwitchTab(0);

            try
            {
                if (_config.WindowWidth > 0 && _config.WindowHeight > 0)
                {
                    Width = _config.WindowWidth;
                    Height = _config.WindowHeight;
                }
                if (_config.WindowLeft >= 0 && _config.WindowTop >= 0)
                {
                    Left = _config.WindowLeft;
                    Top = _config.WindowTop;
                }
            }
            catch { }

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
        }

        // ==========================================
        // 4. 内存滑块控制
        // ==========================================
        private int CalculateSmartRam()
        {
            int raw = (HardwareInfo.GetTotalPhysicalMemoryMB() - 2048) / 2;
            int clamped = Math.Clamp(raw, 1024, 8192);
            return (clamped / 512) * 512;
        }

        private void GlobalAutoRamCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (GlobalRamSlider == null || GlobalRamText == null) return;
            bool isAuto = GlobalAutoRamCheck.IsChecked ?? false;
            GlobalRamSlider.IsEnabled = !isAuto;
            _config.GlobalUseAutoRam = isAuto;
            if (isAuto)
            {
                int autoRam = CalculateSmartRam();
                GlobalRamSlider.Value = autoRam;
                GlobalRamText.Text = $"{autoRam} MB (自动)";
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
            _currentVersionConfig.UseAutoRam = isAuto;
            if (isAuto)
            {
                int targetRam = _config.GlobalUseAutoRam ? CalculateSmartRam() : _config.GlobalRamMB;
                VSettingsRamSlider.Value = targetRam;
                VSettingsRamText.Text = $"{targetRam} MB (跟随全局)";
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
            _myBroadcastIp = broadcastIp;
            _myNickname = nickname;
            _onlinePlayers.Clear();
            RoomPlayersListBox.ItemsSource = _onlinePlayers;
            _discoveryCts = new CancellationTokenSource();
            Task.Run(() => DiscoveryListenLoop(_discoveryCts.Token));

            _discoveryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _discoveryTimer.Tick += (s, e) =>
            {
                try
                {
                    using var sender = new UdpClient();
                    sender.EnableBroadcast = true;
                    byte[] data = Encoding.UTF8.GetBytes($"MDL_WHO|{_myNickname}");
                    sender.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(_myBroadcastIp), 6568));
                }
                catch { }

                for (int i = _onlinePlayers.Count - 1; i >= 0; i--)
                {
                    if ((DateTime.Now - _onlinePlayers[i].LastSeen).TotalSeconds > 6)
                        _onlinePlayers.RemoveAt(i);
                }
            };
            _discoveryTimer.Start();
        }

        private void DiscoveryListenLoop(CancellationToken token)
        {
            try
            {
                _discoveryListener = new UdpClient();
                _discoveryListener.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _discoveryListener.Client.Bind(new IPEndPoint(IPAddress.Any, 6568));
                while (!token.IsCancellationRequested)
                {
                    IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = _discoveryListener.Receive(ref remoteEP);
                    string msg = Encoding.UTF8.GetString(bytes);
                    string ip = remoteEP.Address.ToString();

                    // 核心修复：改用 InvokeAsync 防止底层网络线程与界面线程发生死锁
                    Dispatcher.InvokeAsync(() =>
                    {
                        if (msg.StartsWith("MDL_WHO|"))
                        {
                            string name = msg.Substring(8);
                            var existing = _onlinePlayers.FirstOrDefault(p => p.IP == ip);
                            if (existing != null)
                            {
                                existing.LastSeen = DateTime.Now;
                                int idx = _onlinePlayers.IndexOf(existing);
                                _onlinePlayers[idx] = new RoomPlayerInfo { IP = ip, Name = name, LastSeen = DateTime.Now };
                            }
                            else
                            {
                                _onlinePlayers.Add(new RoomPlayerInfo { IP = ip, Name = name, LastSeen = DateTime.Now });
                            }
                        }
                        else if (msg.StartsWith("MDL_BYE|"))
                        {
                            var existing = _onlinePlayers.FirstOrDefault(p => p.IP == ip);
                            if (existing != null)
                                _onlinePlayers.Remove(existing);
                        }
                    });
                }
            }
            catch { }
        }

        private void StopUdpDiscovery()
        {
            try
            {
                if (!string.IsNullOrEmpty(_myBroadcastIp) && !string.IsNullOrEmpty(_myNickname))
                {
                    using var sender = new UdpClient(); sender.EnableBroadcast = true;
                    byte[] data = Encoding.UTF8.GetBytes($"MDL_BYE|{_myNickname}");
                    sender.Send(data, data.Length, new IPEndPoint(IPAddress.Parse(_myBroadcastIp), 6568));
                }
            }
            catch { }
            _discoveryCts?.Cancel();
            _discoveryTimer?.Stop();
            try { _discoveryListener?.Close(); } catch { }
            Dispatcher.InvokeAsync(() => _onlinePlayers.Clear());
        }

        // ==========================================
        // 6. 联机大厅：创建、加入与进程管理
        // ==========================================
        private void CreateRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_easyTierProcess != null && !_easyTierProcess.HasExited)
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
                ShowDialog("提示", "起个名字吧！");
                return;
            }

            string roomCode = new Random().Next(100000, 999999).ToString();
            EasyTierRoomBox.Text = roomCode;
            string sub1 = roomCode.Substring(0, 2);
            string sub2 = roomCode.Substring(2, 2);
            string myIp = $"10.{sub1}.{sub2}.1";
            string args = $"-e \"{EasyTierServerBox.Text}\" --network-name \"mdl_room_{roomCode}\" --network-secret \"mdl_pwd_{roomCode}\" --ipv4 {myIp}/24";

            // 核心修复：分离 TCP 和 UDP 防火墙指令，完美执行不报错
            string fw1 = "netsh advfirewall firewall add rule name=\"MDL_TCP\" dir=in action=allow protocol=TCP localport=6567,6568 >nul 2>&1";
            string fw2 = "netsh advfirewall firewall add rule name=\"MDL_UDP\" dir=in action=allow protocol=UDP localport=6567,6568 >nul 2>&1";
            StartEasyTierProcess($"/k \"{fw1} & {fw2} & \"{exe}\" {args}\"", roomCode, true, myIp, myIp, $"10.{sub1}.{sub2}.255", myName);
        }

        private void JoinRoomBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_easyTierProcess != null && !_easyTierProcess.HasExited)
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
                ShowDialog("提示", "房号和名字都得写！");
                return;
            }

            if (roomCode.Length != 6 || !int.TryParse(roomCode, out _))
            {
                ShowDialog("提示", "无效的房号！房间号必须是 6 位纯数字。");
                return;
            }

            string sub1 = roomCode.Substring(0, 2);
            string sub2 = roomCode.Substring(2, 2);
            string myIp = $"10.{sub1}.{sub2}.{new Random().Next(2, 254)}";
            string args = $"-e \"{EasyTierServerBox.Text}\" --network-name \"mdl_room_{roomCode}\" --network-secret \"mdl_pwd_{roomCode}\" --ipv4 {myIp}/24";

            string fw1 = "netsh advfirewall firewall add rule name=\"MDL_TCP\" dir=in action=allow protocol=TCP localport=6567,6568 >nul 2>&1";
            string fw2 = "netsh advfirewall firewall add rule name=\"MDL_UDP\" dir=in action=allow protocol=UDP localport=6567,6568 >nul 2>&1";

            StartEasyTierProcess($"/c \"{fw1} & {fw2} & \"{exe}\" {args}\"", roomCode, false, myIp, $"10.{sub1}.{sub2}.1", $"10.{sub1}.{sub2}.255", myName);
        }
        private void StartEasyTierProcess(string cmdArgs, string roomCode, bool isHost, string myIp, string hostIp, string brIp, string myName)
        {
            try
            {
                _easyTierProcess = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = cmdArgs,
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (_easyTierProcess != null)
                {
                    MyVirtualIpBox.Text = myIp; StartUdpDiscovery(brIp, myName);

                    // 创建一个大红色的画刷
                    var redBrush = new SolidColorBrush(Color.FromRgb(232, 17, 35));

                    if (isHost)
                    {
                        CreateRoomBtn.Content = "⏹ 解散大厅";
                        CreateRoomBtn.Background = redBrush; // 直接在代码里染红，绝对有效！
                        CreateRoomBtn.Tag = "Locked";        // 上锁，掐断 XAML 的悬停变绿功能
                        JoinRoomBtn.IsEnabled = false;       // 触发灰化
                    }
                    else
                    {
                        JoinRoomBtn.Content = "⏹ 退出大厅";
                        JoinRoomBtn.Background = redBrush;   // 直接在代码里染红，绝对有效！
                        JoinRoomBtn.Tag = "Locked";          // 上锁，掐断 XAML 的悬停变蓝功能
                        CreateRoomBtn.IsEnabled = false;     // 触发灰化
                    }

                    _easyTierProcess.EnableRaisingEvents = true;
                    _easyTierProcess.Exited += (s, ev) => Dispatcher.InvokeAsync(() =>
                    {
                        _easyTierProcess = null;
                        StopUdpDiscovery();
                        MyVirtualIpBox.Text = "尚未连接";

                        CreateRoomBtn.Content = "创 建 大 厅";
                        CreateRoomBtn.ClearValue(Button.BackgroundProperty);
                        CreateRoomBtn.Tag = null;

                        JoinRoomBtn.Content = "加 入";
                        JoinRoomBtn.ClearValue(Button.BackgroundProperty);
                        JoinRoomBtn.Tag = null;

                        CreateRoomBtn.IsEnabled = true;
                        JoinRoomBtn.IsEnabled = true;
                    });
                }
            }
            catch { ShowDialog("错误", "启动失败，请检查网络或权限。", DialogIcon.Error); }
        }
        // --- 修改部分 ---
        private void KillEasyTierProcess()
        {
            if (_easyTierProcess == null || _easyTierProcess.HasExited)
                return;

            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "taskkill",
                    Arguments = $"/PID {_easyTierProcess.Id} /T /F",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });
            }
            catch { }
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
                    var bytes = await _http.GetByteArrayAsync(UrlHelper.Format("https://raw.githubusercontent.com/Anuken/Mindustry/master/core/assets/icons/icon_64.png", false));
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

            _config.PlayerNickname = PlayerNameBox.Text;
            _config.GlobalJavaPath = GlobalJavaComboBox.Text;
            _config.GlobalRamMB = (int)GlobalRamSlider.Value;
            if (WindowState == WindowState.Normal)
            {
                _config.WindowWidth = Width;
                _config.WindowHeight = Height;
                _config.WindowLeft = Left;
                _config.WindowTop = Top;
            }
            SaveConfig();
        }

        private void ProxyNodeBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ProxyNodeBox.SelectedIndex != -1)
            {
                _config.ProxyNodeIndex = ProxyNodeBox.SelectedIndex;
                UrlHelper.ProxyIndex = ProxyNodeBox.SelectedIndex;
                SaveConfig();
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
        }

        private void SwitchTab(int idx)
        {
            MainTabControl.SelectedIndex = idx;
            var d = new SolidColorBrush(Color.FromRgb(85, 85, 85));
            var a = new SolidColorBrush(Color.FromRgb(33, 150, 243));

            // 把所有导航按钮都恢复默认颜色 (加入了 NavMultiplayerBtn)
            NavLaunchBtn.Foreground = d;
            NavDownloadBtn.Foreground = d;
            NavModBrowserBtn.Foreground = d;
            NavSchematicsBtn.Foreground = d;
            NavMultiplayerBtn.Foreground = d;
            NavSettingsBtn.Foreground = d;
            NavMoreBtn.Foreground = d;

            if (idx == 0)
                NavLaunchBtn.Foreground = a;
            else if (idx == 1)
                NavDownloadBtn.Foreground = a;
            else if (idx == 2)
                NavModBrowserBtn.Foreground = a;
            else if (idx == 3)
                NavSchematicsBtn.Foreground = a;
            else if (idx == 4)
                NavMultiplayerBtn.Foreground = a;
            else if (idx == 5)
                NavSettingsBtn.Foreground = a;
            else if (idx == 6)
                NavMoreBtn.Foreground = a;
        }

        private void SmoothScroll_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is DependencyObject d)
            {
                ScrollViewer? sv = FindVisualChild<ScrollViewer>(d);
                if (sv != null)
                {
                    e.Handled = true;
                    sv.BeginAnimation(SmoothScrollHelper.ScrollOffsetProperty, null);
                    SmoothScrollHelper.SetScrollOffset(sv, sv.VerticalOffset);
                    double t = Math.Max(0, Math.Min(sv.ScrollableHeight, sv.VerticalOffset - (e.Delta * 1.2)));
                    sv.BeginAnimation(SmoothScrollHelper.ScrollOffsetProperty, new DoubleAnimation(t, TimeSpan.FromMilliseconds(350)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
                }
            }
        }

        private T? FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject c = VisualTreeHelper.GetChild(obj, i);
                if (c is T t) return t;
                T? res = FindVisualChild<T>(c);
                if (res != null) return res;
            }
            return null;
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
        }

        private void NavDownload_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(1);
        }

        private async void NavModBrowser_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(2);
            if (_allOnlineMods.Count == 0)
                await FetchModRegistryAsync();
        }

        private void NavSchematics_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(3);
            if (_allOnlineSchematics.Count == 0)
            {
                string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", $"{_currentSchematicRepo.Replace("/", "_")}.zip");
                if (File.Exists(cachePath))
                    _ = FetchSchematicsAsync(false);
            }
        }

        // 新增：联机按钮点击 (传入 false 走缓存)
        private void NavMultiplayer_Click(object sender, RoutedEventArgs e)
        {
            SwitchTab(4);
            _ = CheckAndDownloadEasyTierAsync(false);
        }

        // 注意：这两个的索引更新了！
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
        private string GetEasyTierExePath()
        {
            string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "EasyTier");
            if (System.IO.Directory.Exists(dir))
            {
                // 开启 AllDirectories，穿透所有子文件夹寻找客户端
                var files = System.IO.Directory.GetFiles(dir, "easytier-core.exe", System.IO.SearchOption.AllDirectories);
                if (files.Length > 0) return files[0];
            }
            return "";
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
                EasyTierStatusText.Text = "EasyTier 组件已缓存就绪，可以开始联机";
                return;
            }

            EasyTierStatusText.Text = "正在连接 GitHub 获取最新 EasyTier 版本...";
            EasyTierProgressBar.Visibility = Visibility.Visible;
            EasyTierProgressBar.Value = 0;

            try
            {
                var rel = await _http.GetFromJsonAsync<GitHubRelease>("https://api.github.com/repos/EasyTier/EasyTier/releases/latest");
                var asset = rel?.Assets?.FirstOrDefault(a => a.Name.Contains("windows-x86_64") && a.Name.EndsWith(".zip"));
                if (asset == null) { EasyTierStatusText.Text = "未找到适用的 Windows 版本。"; return; }

                System.IO.Directory.CreateDirectory(dir);
                string zipPath = System.IO.Path.Combine(dir, asset.Name);

                EasyTierStatusText.Text = "正在使用加速节点下载底层组件...";
                await DownloadFileAsync(UrlHelper.Format(asset.BrowserDownloadUrl), zipPath, new Progress<double>(p => EasyTierProgressBar.Value = p));

                EasyTierStatusText.Text = "正在解压安装...";
                System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, dir, true);
                System.IO.File.Delete(zipPath); // 下完清理垃圾

                EasyTierStatusText.Text = "EasyTier 安装并缓存成功！准备就绪。";
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Forbidden)
            {
                EasyTierStatusText.Text = "下载失败: 节点拒绝访问(403)，请去设置页切换下载节点！";
            }
            catch (Exception ex)
            {
                EasyTierStatusText.Text = "下载失败: " + ex.Message;
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
            if (VersionSettingsOverlay.Visibility == Visibility.Visible && _currentInstance != null)
            {
                SaveVersionConfig(_currentInstance.FullPath);
                AnimateFade(VersionSettingsOverlay, false);
            }

            if (VersionSelectOverlay.Visibility == Visibility.Visible)
                AnimateFade(VersionSelectOverlay, false);

            if (ReleaseNotesOverlay.Visibility == Visibility.Visible)
                AnimateFade(ReleaseNotesOverlay, false);

            if (SchematicInstallOverlay.Visibility == Visibility.Visible)
                AnimateFade(SchematicInstallOverlay, false);

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
                    DialogIconBorder.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
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
                    DialogIconBorder.Background = new SolidColorBrush(Color.FromRgb(33, 150, 243));
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
            if (_currentInstance == null)
            {
                CurrentLaunchVersionText.Text = "未选择版本，请点击下方选择";
                LaunchBtn.IsEnabled = false;
            }
            else
            {
                // 核心修复：如果当前选中的实例正在运行，按钮变灰；否则允许启动
                if (_runningInstancePaths.Contains(_currentInstance.FullPath))
                {
                    CurrentLaunchVersionText.Text = "该实例正在运行中...";
                    LaunchBtn.IsEnabled = false;
                }
                else
                {
                    CurrentLaunchVersionText.Text = _currentInstance.Name;
                    LaunchBtn.IsEnabled = true;
                }
            }
        }
        private List<GameInstanceInfo> GetAllInstalledInstances()
        {
            var all = new List<GameInstanceInfo>();
            foreach (var root in _config.ManagedFolders)
            {
                if (!Directory.Exists(root)) continue;
                string vDir = Path.Combine(root, "Versions");
                if (Directory.Exists(vDir))
                {
                    foreach (var d in Directory.GetDirectories(vDir))
                    {
                        if (File.Exists(Path.Combine(d, "Mindustry.jar")))
                        {
                            all.Add(new GameInstanceInfo { Name = Path.GetFileName(d), FullPath = d });
                        }
                    }
                }
            }
            return all;
        }
        private void OpenVersionSelect_Click(object sender, RoutedEventArgs e)
        {
            FolderListBox.ItemsSource = null;
            FolderListBox.ItemsSource = _config.ManagedFolders;
            if (_config.ManagedFolders.Count > 0) FolderListBox.SelectedIndex = 0;
            AnimateFade(VersionSelectOverlay, true);
        }

        private void AddNewFolder_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFolderDialog();
            if (d.ShowDialog() == true)
            {
                if (!_config.ManagedFolders.Contains(d.FolderName))
                {
                    _config.ManagedFolders.Add(d.FolderName);
                    SaveConfig();
                    OpenVersionSelect_Click(null!, null!);
                }
            }
        }

        private async void RemoveFolder_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string fp)
            {
                if (await ShowDialogAsync("移除列表项", $"仅从启动器列表中移除此文件夹（不影响本地文件）。\n确定移除吗？\n{fp}", DialogIcon.Question) == MsgResult.Yes)
                {
                    _config.ManagedFolders.Remove(fp);
                    SaveConfig();
                    OpenVersionSelect_Click(null!, null!);
                }
            }
        }

        private void FolderListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FolderListBox.SelectedItem is string p && Directory.Exists(p))
            {
                var list = new List<GameInstanceInfo>();
                string vDir = Path.Combine(p, "Versions");
                if (Directory.Exists(vDir))
                {
                    foreach (var d in Directory.GetDirectories(vDir))
                    {
                        if (File.Exists(Path.Combine(d, "Mindustry.jar")))
                        {
                            list.Add(new GameInstanceInfo { Name = Path.GetFileName(d), FullPath = d });
                        }
                    }
                }
                InstanceListBox.ItemsSource = list;
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
                _currentInstance = info;
                _config.LastSelectedInstancePath = info.FullPath;
                SaveConfig();
                CloseOverlays_Click(null!, null!);
            }
        }

        private async void DeleteInstance_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is GameInstanceInfo info)
            {
                if (await ShowDialogAsync("删除版本", $"警告：确定要彻底删除该版本吗？此操作不可逆！\n{info.FullPath}", DialogIcon.Warning) == MsgResult.Yes)
                {
                    try
                    {
                        Directory.Delete(info.FullPath, true);
                        if (_currentInstance != null && _currentInstance.FullPath == info.FullPath)
                        {
                            _currentInstance = null;
                            _config.LastSelectedInstancePath = "";
                            SaveConfig();
                            UpdateMainUI();
                        }
                        FolderListBox_SelectionChanged(null!, null!);
                    }
                    catch (Exception ex)
                    {
                        ShowDialog("错误", $"删除失败: {ex.Message}", DialogIcon.Error);
                    }
                }
            }
        }

        private void OpenVersionSettings_Click(object sender, RoutedEventArgs e)
        {
            if (_currentInstance == null)
            {
                ShowDialog("提示", "请先选择一个版本！");
                return;
            }
            LoadVersionConfig(_currentInstance.FullPath);
            VSettingsTitle.Text = $"版本设置 - {_currentInstance.Name}";
            VSettingsIsolationBox.SelectedIndex = _currentVersionConfig.UseIsolation ? 0 : 1;
            VSettingsJavaComboBox.Text = _currentVersionConfig.CustomJavaPath;
            VSettingsJvmArgsBox.Text = _currentVersionConfig.CustomJvmArgs;
            VersionAutoRamCheck.IsChecked = _currentVersionConfig.UseAutoRam;
            VSettingsRamSlider.Value = Math.Min(_currentVersionConfig.CustomRamMB, GlobalRamSlider.Maximum);
            CancelRename_Click(null!, null!);
            VSidebarConfig_Click(null!, null!);
            AnimateFade(VersionSettingsOverlay, true);
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
            if (_currentInstance != null)
            {
                OverviewVersionName.Text = _currentInstance.Name;
                OverviewVersionPath.Text = _currentInstance.FullPath;
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
            if (_currentInstance != null)
                Process.Start("explorer.exe", _currentInstance.FullPath);
        }

        private void OpenDataFolder_Click(object sender, RoutedEventArgs e)
        {
            if (_currentInstance == null) return;
            LoadVersionConfig(_currentInstance.FullPath);
            string data = _currentVersionConfig.UseIsolation ? Path.Combine(_currentInstance.FullPath, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
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
            if (_currentInstance == null) return;
            RenameTextBox.Text = _currentInstance.Name;
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
            if (_currentInstance == null) return;
            string nn = RenameTextBox.Text.Trim();
            if (string.IsNullOrEmpty(nn) || nn.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ShowDialog("提示", "名称无效！");
                return;
            }

            if (nn == _currentInstance.Name)
            {
                CancelRename_Click(null!, null!);
                return;
            }

            try
            {
                string op = _currentInstance.FullPath;
                string np = Path.Combine(Directory.GetParent(op)!.FullName, nn);
                if (Directory.Exists(np))
                {
                    ShowDialog("提示", "已存在同名版本！");
                    return;
                }
                Directory.Move(op, np);
                _currentInstance.Name = nn;
                _currentInstance.FullPath = np;
                if (_config.LastSelectedInstancePath == op)
                {
                    _config.LastSelectedInstancePath = np;
                    SaveConfig();
                }
                OverviewVersionName.Text = nn;
                OverviewVersionPath.Text = np;
                VSettingsTitle.Text = $"版本设置 - {nn}";
                UpdateMainUI();
                FolderListBox_SelectionChanged(null!, null!);
                CancelRename_Click(null!, null!);
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"重命名失败: {ex.Message}", DialogIcon.Error);
            }
        }
        private void ScanMods()
        {
            if (_currentInstance == null) return;
            string data = _currentVersionConfig.UseIsolation ? Path.Combine(_currentInstance.FullPath, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
            string mDir = Path.Combine(data, "mods");
            if (!Directory.Exists(mDir))
            {
                ModListBox.ItemsSource = null;
                NoModText.Visibility = Visibility.Visible;
                return;
            }

            var files = new DirectoryInfo(mDir).GetFiles()
                .Where(f => f.Extension.Equals(".jar", StringComparison.OrdinalIgnoreCase) || f.Extension.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var list = new List<ModInfo>();
            foreach (var f in files)
            {
                var info = new ModInfo { FileName = f.Name, FullPath = f.FullName, FileSize = $"{(f.Length / 1024.0):F2} KB" };
                ParseModArchive(info);
                list.Add(info);
            }
            ModListBox.ItemsSource = list;
            NoModText.Visibility = list.Any() ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ParseModArchive(ModInfo info)
        {
            try
            {
                using var stream = File.OpenRead(info.FullPath);
                using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
                var iconEntry = zip.Entries.FirstOrDefault(e => e.Name.Equals("icon.png", StringComparison.OrdinalIgnoreCase));
                if (iconEntry != null)
                {
                    using var iconStream = iconEntry.Open();
                    using var ms = new MemoryStream();
                    iconStream.CopyTo(ms);
                    ms.Position = 0;
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.StreamSource = ms;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    info.IconImage = bitmap;
                }
                var metaEntry = zip.Entries.FirstOrDefault(e => e.Name.Equals("mod.json", StringComparison.OrdinalIgnoreCase) || e.Name.Equals("mod.hjson", StringComparison.OrdinalIgnoreCase));
                if (metaEntry != null)
                {
                    using var metaStream = metaEntry.Open();
                    using var reader = new StreamReader(metaStream);
                    string content = reader.ReadToEnd();
                    try
                    {
                        var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                        using var doc = JsonDocument.Parse(content, options);
                        var root = doc.RootElement;
                        string GetJsonString(string key) => root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String ? prop.GetString() ?? "" : "";
                        string name = GetJsonString("displayName");
                        if (string.IsNullOrEmpty(name)) name = GetJsonString("name");
                        info.DisplayName = StripColors(name);
                        info.Author = StripColors(GetJsonString("author"));
                        info.Description = StripColors(GetJsonString("description"));
                        info.Version = StripColors(GetJsonString("version"));
                    }
                    catch
                    {
                        info.DisplayName = StripColors(ExtractHjsonValue(content, "displayName") ?? ExtractHjsonValue(content, "name") ?? "");
                        info.Author = StripColors(ExtractHjsonValue(content, "author") ?? "");
                        string desc = ExtractHjsonValue(content, "description") ?? "";
                        info.Description = StripColors(desc).Replace("\\n", "\n");
                        info.Version = StripColors(ExtractHjsonValue(content, "version") ?? "");
                    }
                }
            }
            catch { }
        }

        private string? ExtractHjsonValue(string content, string key)
        {
            var match = Regex.Match(content, $@"""?{key}""?\s*:\s*([^""\r\n]+|""([^""]*)"")", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string val = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[1].Value.Trim();
                return val.TrimEnd(',').Trim();
            }
            return null;
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
                if (await ShowDialogAsync("MDL", "确定删除吗？", DialogIcon.Warning) == MsgResult.Yes)
                {
                    try
                    {
                        File.Delete(p);
                        Dispatcher.InvokeAsync(() => ScanMods());
                    }
                    catch (Exception ex)
                    {
                        ShowDialog("错误", $"删除失败: {ex.Message}", DialogIcon.Error);
                    }
                }
            }
        }

        private void ScanSchematics()
        {
            if (_currentInstance == null) return;
            string data = _currentVersionConfig.UseIsolation ? Path.Combine(_currentInstance.FullPath, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
            string sDir = Path.Combine(data, "schematics");
            if (!Directory.Exists(sDir))
            {
                LocalSchematicListBox.ItemsSource = null;
                NoSchematicText.Visibility = Visibility.Visible;
                return;
            }
            var files = new DirectoryInfo(sDir).GetFiles("*.msch", SearchOption.TopDirectoryOnly)
                .Select(f => new ModInfo { FileName = f.Name, FullPath = f.FullName, FileSize = $"{(f.Length / 1024.0):F2} KB" })
                .ToList();
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
                if (await ShowDialogAsync("MDL", "确定删除该蓝图吗？", DialogIcon.Warning) == MsgResult.Yes)
                {
                    try
                    {
                        File.Delete(p);
                        Dispatcher.InvokeAsync(() => ScanSchematics());
                    }
                    catch (Exception ex)
                    {
                        ShowDialog("错误", $"删除失败: {ex.Message}", DialogIcon.Error);
                    }
                }
            }
        }

        // ===============================================
        // 核心增强：带 DataGrid 虚拟化的动态解析与编辑
        // ===============================================
        private string GetSettingsBinPath()
        {
            if (_currentInstance == null) return "";
            bool isIso = VSettingsIsolationBox.SelectedIndex == 0;
            string d = isIso ? Path.Combine(_currentInstance.FullPath, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
            return Path.Combine(d, "settings.bin");
        }

        private void ScanSaveDataStatus()
        {
            string binPath = GetSettingsBinPath();
            if (string.IsNullOrEmpty(binPath)) return;
            if (!File.Exists(binPath))
            {
                SaveDataStatusText.Text = "文件未找到 (从未启动过游戏，或被移动)";
                SaveDataStatusText.Foreground = Brushes.Gray;
                return;
            }
            var editor = new MindustrySettingsEditor();
            bool isHealthy = editor.LoadList(binPath, out var lst);
            if (isHealthy)
            {
                SaveDataStatusText.Text = $"解析完美！包含 {lst.Count} 个游戏配置项\n(注：地图存档位于 saves 文件夹)";
                SaveDataStatusText.Foreground = Brushes.Green;
            }
            else
            {
                SaveDataStatusText.Text = $"部分损坏！({editor.ErrorMessage})";
                SaveDataStatusText.Foreground = Brushes.Crimson;
            }
        }

        private void ParseSaveData_Click(object sender, RoutedEventArgs e)
        {
            string binPath = GetSettingsBinPath();
            if (!File.Exists(binPath))
            {
                ShowDialog("提示", "找不到 settings.bin！");
                return;
            }
            var editor = new MindustrySettingsEditor();
            bool isHealthy = editor.LoadList(binPath, out var lst);
            if (lst.Count == 0 && !isHealthy)
            {
                ShowDialog("错误", $"解析严重失败:\n{editor.ErrorMessage}", DialogIcon.Error);
                return;
            }
            _settingsView = CollectionViewSource.GetDefaultView(lst);
            UpdateSettingsFilter();
            SettingsDataGrid.ItemsSource = _settingsView;
            if (!isHealthy)
            {
                ShowDialog("警告", $"解析中途遇到异常，尾部数据可能已丢失。\n原因: {editor.ErrorMessage}", DialogIcon.Warning);
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
                ShowDialog("MDL", "保存成功！已自动备份原文件为 .bak", DialogIcon.Info);
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"保存失败: {ex.Message}", DialogIcon.Error);
            }
        }

        private async void RescueSaveData_Click(object sender, RoutedEventArgs e)
        {
            string binPath = GetSettingsBinPath();
            if (!File.Exists(binPath)) return;
            if (await ShowDialogAsync("重建索引", "此操作会将 settings.bin 隔离，让游戏下次启动时重建全新索引！\n\n是否继续？", DialogIcon.Warning) == MsgResult.Yes)
            {
                try
                {
                    File.Move(binPath, binPath + $".bak_{DateTime.Now:MMddHHmm}");
                    string bak = Path.Combine(Path.GetDirectoryName(binPath)!, "settings.backup");
                    if (File.Exists(bak)) File.Delete(bak);
                    ShowDialog("提示", "重建指令已下达！请启动游戏恢复存档。");
                    ScanSaveDataStatus();
                    SettingsDataGrid.ItemsSource = null;
                }
                catch (Exception ex)
                {
                    ShowDialog("错误", $"操作失败: {ex.Message}", DialogIcon.Error);
                }
            }
        }

        private void LaunchGame_Click(object sender, RoutedEventArgs e)
        {
            if (_currentInstance == null)
            {
                ShowDialog("提示", "请先选择版本！");
                return;
            }

            string instancePath = _currentInstance.FullPath;

            if (_runningInstancePaths.Contains(instancePath))
            {
                ShowDialog("提示", "该实例已经正在运行中！\n请不要重复启动同一个版本。", DialogIcon.Warning);
                return;
            }

            string jar = Path.Combine(instancePath, "Mindustry.jar");
            if (!File.Exists(jar))
            {
                ShowDialog("错误", "核心缺失！", DialogIcon.Error);
                return;
            }

            LoadVersionConfig(instancePath);
            string data = _currentVersionConfig.UseIsolation ? Path.Combine(instancePath, "data") : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry");
            if (_currentVersionConfig.UseIsolation) Directory.CreateDirectory(data);

            string exe = string.IsNullOrWhiteSpace(_currentVersionConfig.CustomJavaPath) ? _config.GlobalJavaPath : _currentVersionConfig.CustomJavaPath;
            if (string.IsNullOrWhiteSpace(exe)) exe = "java";

            int finalRam = _currentVersionConfig.UseAutoRam ? CalculateSmartRam() : _currentVersionConfig.CustomRamMB;
            string memArg = $"-Xmx{finalRam}m ";
            string jvmArgs = string.IsNullOrWhiteSpace(_currentVersionConfig.CustomJvmArgs) ? "" : _currentVersionConfig.CustomJvmArgs + " ";

            try
            {
                var pInfo = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = $"{memArg}{jvmArgs}-jar \"{jar}\"",
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    WorkingDirectory = instancePath
                };
                if (_currentVersionConfig.UseIsolation)
                {
                    pInfo.EnvironmentVariables["MINDUSTRY_DATA_DIR"] = data;
                }

                Process? p = Process.Start(pInfo);
                if (p == null)
                    return;

                _runningInstancePaths.Add(instancePath);
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
                p.Exited += (s, ev) => Dispatcher.Invoke(() =>
                {
                    // 进程结束，把该路径从运行列表中移除，并刷新 UI
                    _runningInstancePaths.Remove(instancePath);
                    UpdateMainUI();

                    if (p.ExitCode != 0) AnalyzeCrash(errorLog);
                });
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"启动失败: {ex.Message}", DialogIcon.Error);
            }
        }

        private void AnalyzeCrash(string log)
        {
            string advice = "【未知错误】建议检查最近安装的 Mod 是否冲突。";
            if (string.IsNullOrWhiteSpace(log))
            {
                log = "未捕获到具体错误信息，可能是系统强制终止了游戏。";
            }
            else if (log.Contains("OutOfMemoryError"))
                advice = "【内存爆炸】检测到分配内存不足。当前模组较多，请在设置中关闭\"自动分配\"并将内存滑块向右拉大。";
            else if (log.Contains("UnsupportedClassVersionError"))
                advice = "【Java版本过旧】请安装并切换到 Java 17 或更高版本。";
            else if (log.Contains("MixinTransformationException") || log.Contains("MixinApplyError"))
                advice = "【模组冲突】检测到多个 Mod 尝试修改同一核心代码。请尝试逐个禁用最近安装的 Mod。";
            else if (log.Contains("NoSuchMethodError") || log.Contains("ClassNotFoundException"))
                advice = "【版本不兼容】某些模组不支持当前的游戏核心版本。";

            ReleaseNotesTitle.Text = "🚀 游戏不幸坠毁！";
            ReleaseNotesTitle.Foreground = Brushes.Crimson;
            ReleaseNotesText.Text = $"{advice}\n\n--- 原始报错诊断 (底部) ---\n{(log.Length > 800 ? log.Substring(log.Length - 800) : log)}";
            OpenRepoBtn.Visibility = Visibility.Collapsed;
            ExportCrashBtn.Visibility = Visibility.Visible;
            AnimateFade(ReleaseNotesOverlay, true);
        }

        private void ExportCrash_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Title = "导出错误报告", Filter = "文本文件 (*.txt)|*.txt", FileName = $"MDL_CrashReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt" };
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dialog.FileName, ReleaseNotesText.Text);
                    ShowDialog("MDL", "错误报告导出成功！", DialogIcon.Info);
                }
                catch (Exception ex)
                {
                    ShowDialog("错误", $"导出失败: {ex.Message}", DialogIcon.Error);
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
                string url = UrlHelper.Format("https://raw.githubusercontent.com/Anuken/MindustryMods/master/mods.json", false);
                var list = await _http.GetFromJsonAsync<List<ModRegistryEntry>>(url);
                if (list != null)
                {
                    _allOnlineMods = list.OrderByDescending(m => m.Stars).ToList();
                    ModBrowserListBox.ItemsSource = _allOnlineMods;
                    ModBrowserListBox.Visibility = Visibility.Visible;
                    ModBrowserLoadingText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                ModBrowserLoadingText.Text = $"拉取列表失败: {ex.InnerException?.Message ?? ex.Message}";
            }
        }
        private void ModSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string k = ModSearchBox.Text.ToLower();
            if (string.IsNullOrWhiteSpace(k))
            {
                ModBrowserListBox.ItemsSource = _allOnlineMods;
            }
            else
            {
                ModBrowserListBox.ItemsSource = _allOnlineMods.Where(m =>
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
                ReleaseNotesTitle.Text = $"{mod.Name} - 模组详情";
                ReleaseNotesTitle.Foreground = Brushes.Black;
                ReleaseNotesText.Text = $"作者: {mod.Author}\n仓库: {mod.Repo}\n描述: \n{mod.Description}";
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
                ShowDialog("MDL", "当前有任务正在下载中，请稍后操作！", DialogIcon.Warning);
                return;
            }

            if (sender is Button b && b.Tag is ModRegistryEntry mod)
            {
                var all = GetAllInstalledInstances();
                if (all.Count == 0)
                {
                    ShowDialog("提示", "请先导入游戏！");
                    return;
                }

                _selectedModToInstall = mod;
                ModInstallTitle.Text = $"安装模组 - {mod.Name}";
                AllInstancesListBox.ItemsSource = all;

                if (_currentInstance != null)
                {
                    var m = all.FirstOrDefault(i => i.FullPath == _currentInstance.FullPath);
                    if (m != null)
                        AllInstancesListBox.SelectedItem = m;
                }

                ModVersionComboBox.ItemsSource = null;
                ModInstallProgressPanel.Visibility = Visibility.Visible;
                ModInstallStatusText.Text = "获取版本中...";
                AllInstancesListBox.IsEnabled = false;
                ModVersionComboBox.IsEnabled = false;
                ConfirmModInstallBtn.IsEnabled = false;

                AnimateFade(ModInstallOverlay, true);

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                    string apiUrl = UrlHelper.Format($"https://api.github.com/repos/{mod.Repo}/releases", true);
                    var rels = await _http.GetFromJsonAsync<List<GitHubRelease>>(apiUrl, cts.Token);
                    if (rels != null)
                    {
                        if (rels.Count == 0)
                        {
                            ShowDialog("MDL", "该模组没有任何发布版本，无法下载。", DialogIcon.Info);
                            AnimateFade(ModInstallOverlay, false);
                            return;
                        }
                        ModVersionComboBox.ItemsSource = rels;
                        ModVersionComboBox.SelectedIndex = 0;
                    }
                }
                catch (Exception ex)
                {
                    ShowDialog("错误", $"获取版本网络错误: {ex.InnerException?.Message ?? ex.Message}", DialogIcon.Error);
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
            if (_selectedModToInstall == null
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
                    if (await ShowDialogAsync("MDL", "该版本无编译好的附件，是否下载源码 ZIP？", DialogIcon.Question) == MsgResult.Yes)
                    {
                        url = UrlHelper.Format($"https://github.com/{_selectedModToInstall.Repo}/archive/refs/tags/{rel.TagName}.zip");
                        file = $"{string.Join("_", _selectedModToInstall.Name.Split(Path.GetInvalidFileNameChars()))}_{rel.TagName}_source.zip";
                    }
                    else
                    {
                        ModInstallProgressPanel.Visibility = Visibility.Collapsed;
                        return;
                    }
                }

                LoadVersionConfig(target.FullPath);
                string modsDir = Path.Combine(
                    _currentVersionConfig.UseIsolation
                        ? Path.Combine(target.FullPath, "data")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry"),
                    "mods");
                Directory.CreateDirectory(modsDir);

                var prog = new Progress<double>(p =>
                {
                    ModInstallProgressBar.Value = p;
                    ModInstallStatusText.Text = $"下载中 {p:F1}%";
                });
                await DownloadFileAsync(url, Path.Combine(modsDir, file), prog);
                ShowDialog("MDL", "模组安装成功！", DialogIcon.Info);
                AnimateFade(ModInstallOverlay, false);
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"安装失败: {ex.InnerException?.Message ?? ex.Message}", DialogIcon.Error);
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
                    _currentSchematicRepo = parts[0];
                    _currentSchematicBranch = parts[1];
                    if (SchematicSourceTitle != null)
                        SchematicSourceTitle.Text = $"社区蓝图库 - {rb.Content}";
                    if (SchematicSearchBox != null)
                        SchematicSearchBox.Text = "";
                    if (SchematicBrowserListBox != null)
                        SchematicBrowserListBox.Visibility = Visibility.Collapsed;
                    _allOnlineSchematics.Clear();
                    string cachePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache", $"{_currentSchematicRepo.Replace("/", "_")}.zip");
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
            _schematicFetchCts?.Cancel();
            _schematicFetchCts = new CancellationTokenSource();
            var token = _schematicFetchCts.Token;
            await _schematicFetchLock.WaitAsync();

            try
            {
                if (token.IsCancellationRequested) return;

                if (FetchSchematicBtn != null)
                    FetchSchematicBtn.Visibility = Visibility.Collapsed;

                if (SchematicBrowserLoadingText != null)
                {
                    SchematicBrowserLoadingText.Visibility = Visibility.Visible;
                }

                if (SchematicBrowserListBox != null)
                    SchematicBrowserListBox.Visibility = Visibility.Collapsed;

                ToggleDownloadState(true);

                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
                Directory.CreateDirectory(cacheDir);
                string zipPath = Path.Combine(cacheDir, $"{_currentSchematicRepo.Replace("/", "_")}.zip");

                if (forceRefresh || !File.Exists(zipPath))
                {
                    SchematicBrowserLoadingText!.Text = "正在解析并下载仓库 ZIP，首次拉取可能需要几十秒，请耐心等待...";
                    string zipUrl = UrlHelper.Format($"https://github.com/{_currentSchematicRepo}/archive/refs/heads/{_currentSchematicBranch}.zip");
                    using var resp = await _http.GetAsync(zipUrl, HttpCompletionOption.ResponseHeadersRead, token);
                    using var fs = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None);
                    await resp.Content.CopyToAsync(fs);
                }

                if (token.IsCancellationRequested) return;

                SchematicBrowserLoadingText!.Text = "正在本地原生解析二进制蓝图元数据...";
                var newList = new List<SchematicEntry>();

                await Task.Run(() =>
                {
                    using var zip = ZipFile.OpenRead(zipPath);
                    foreach (var entry in zip.Entries)
                    {
                        if (token.IsCancellationRequested) return;
                        if (entry.Name.EndsWith(".msch", StringComparison.OrdinalIgnoreCase))
                        {
                            using var es = entry.Open();
                            using var ms = new MemoryStream();
                            es.CopyTo(ms);
                            string desc = "";
                            string? realName = ParseMschName(ms.ToArray(), out desc);
                            newList.Add(new SchematicEntry(realName ?? "", desc, entry.Name, entry.FullName));
                        }
                    }
                }, token);

                if (token.IsCancellationRequested) return;

                _allOnlineSchematics = newList;
                if (SchematicBrowserListBox != null)
                {
                    SchematicBrowserListBox.ItemsSource = null;
                    SchematicBrowserListBox.ItemsSource = _allOnlineSchematics;
                    SchematicBrowserListBox.Visibility = Visibility.Visible;
                }

                if (SchematicBrowserLoadingText != null)
                    SchematicBrowserLoadingText.Visibility = Visibility.Collapsed;
            }
            catch (TaskCanceledException) { }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                if (SchematicBrowserLoadingText != null && !token.IsCancellationRequested)
                    SchematicBrowserLoadingText.Text = $"拉取蓝图列表失败: {ex.InnerException?.Message ?? ex.Message}\n请前往\"设置\"切换节点后，重试拉取。";
                if (FetchSchematicBtn != null)
                    FetchSchematicBtn.Visibility = Visibility.Visible;
            }
            finally
            {
                _schematicFetchLock.Release();
                if (!token.IsCancellationRequested)
                {
                    ToggleDownloadState(false);
                }
            }
        }
        private string? ParseMschName(byte[] mschBytes, out string description)
        {
            description = "";
            try
            {
                using var ms = new MemoryStream(mschBytes);
                using var reader = new BinaryReader(ms);

                if (reader.ReadByte() != 'm' || reader.ReadByte() != 's'
                    || reader.ReadByte() != 'c' || reader.ReadByte() != 'h')
                    return null;

                reader.ReadByte();
                ms.Seek(2, SeekOrigin.Current);

                using var deflate = new DeflateStream(ms, CompressionMode.Decompress);
                using var deflatedMs = new MemoryStream();
                deflate.CopyTo(deflatedMs);
                deflatedMs.Position = 0;

                using var dataReader = new BinaryReader(deflatedMs);

                short ReadShort()
                {
                    return (short)((dataReader.ReadByte() << 8) | dataReader.ReadByte());
                }

                string ReadString()
                {
                    short len = ReadShort();
                    return Encoding.UTF8.GetString(dataReader.ReadBytes(len));
                }

                ReadShort();
                ReadShort();
                byte tagsCount = dataReader.ReadByte();
                string? foundName = null;

                for (int i = 0; i < tagsCount; i++)
                {
                    string key = ReadString();
                    string val = ReadString();
                    if (key == "name") foundName = StripColors(val);
                    if (key == "description") description = StripColors(val);
                }

                return foundName;
            }
            catch
            {
                return null;
            }
        }
        private void SchematicSearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string k = SchematicSearchBox.Text.ToLower();
            SchematicBrowserListBox.ItemsSource = string.IsNullOrWhiteSpace(k)
                ? _allOnlineSchematics
                : _allOnlineSchematics.Where(s =>
                    s.UI_Name.ToLower().Contains(k) || s.UI_Description.ToLower().Contains(k)).ToList();
        }
        private void InstallSchematicFromBrowser_Click(object sender, RoutedEventArgs e)
        {
            if (_isDownloading)
            {
                ShowDialog("MDL", "当前有任务正在下载中，请稍后操作！", DialogIcon.Warning);
                return;
            }

            if (sender is Button b && b.Tag is SchematicEntry schematic)
            {
                var all = GetAllInstalledInstances();
                if (all.Count == 0)
                {
                    ShowDialog("提示", "请先导入游戏实例！");
                    return;
                }

                _selectedSchematicToInstall = schematic;
                SchematicInstallTitle.Text = $"安装蓝图 - {schematic.UI_Name}";
                SchematicInstancesListBox.ItemsSource = all;

                if (_currentInstance != null)
                {
                    var m = all.FirstOrDefault(i => i.FullPath == _currentInstance.FullPath);
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
            if (_selectedSchematicToInstall == null || SchematicInstancesListBox.SelectedItem is not GameInstanceInfo target)
                return;

            try
            {
                LoadVersionConfig(target.FullPath);

                string schematicDir = Path.Combine(
                    _currentVersionConfig.UseIsolation
                        ? Path.Combine(target.FullPath, "data")
                        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Mindustry"),
                    "schematics");
                Directory.CreateDirectory(schematicDir);

                string targetFile = Path.Combine(schematicDir, _selectedSchematicToInstall.FileName);
                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Cache");
                string zipPath = Path.Combine(cacheDir, $"{_currentSchematicRepo.Replace("/", "_")}.zip");

                using var fs = File.OpenRead(zipPath);
                using var zip = new ZipArchive(fs, ZipArchiveMode.Read);
                var entry = zip.GetEntry(_selectedSchematicToInstall.ZipEntryFullName);
                if (entry != null)
                {
                    entry.ExtractToFile(targetFile, true);
                    ShowDialog("MDL", "蓝图从缓存解压并安装成功！", DialogIcon.Info);
                }

                AnimateFade(SchematicInstallOverlay, false);
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"安装失败: {ex.Message}", DialogIcon.Error);
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
                ShowDialog("错误", $"无法打开链接: {ex.Message}", DialogIcon.Error);
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
                ShowDialog("错误", $"无法打开链接: {ex.Message}", DialogIcon.Error);
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
                ShowDialog("错误", $"无法打开链接: {ex.Message}", DialogIcon.Error);
            }
        }

        private async void DownloadSource_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string repo)
            {
                _currentDownloadRepo = repo;
                if (DownloadSourceTitle != null)
                    DownloadSourceTitle.Text = $"获取新版本 - {rb.Content}";
                await FetchRemoteVersionsAsync();
            }
        }
        private async Task FetchRemoteVersionsAsync()
        {
            if (RemoteVersionLoadingText != null)
            {
                RemoteVersionLoadingText.Text = "正在拉取列表...";
                RemoteVersionLoadingText.Visibility = Visibility.Visible;
            }

            if (RemoteVersionListBox != null)
                RemoteVersionListBox.Visibility = Visibility.Collapsed;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                string apiUrl = UrlHelper.Format($"https://api.github.com/repos/{_currentDownloadRepo}/releases", true);
                var rels = await _http.GetFromJsonAsync<List<GitHubRelease>>(apiUrl, cts.Token);

                if (rels != null)
                {
                    var list = rels.Where(r =>
                        r.Assets != null && r.Assets.Any(a =>
                            a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                            && !a.Name.Contains("server", StringComparison.OrdinalIgnoreCase)
                            && !a.Name.Contains("android", StringComparison.OrdinalIgnoreCase)
                            && !a.Name.Contains("dependencies", StringComparison.OrdinalIgnoreCase)
                            && !a.Name.Contains("javadoc", StringComparison.OrdinalIgnoreCase)
                            && !a.Name.Contains("sources", StringComparison.OrdinalIgnoreCase))
                    ).ToList();

                    if (RemoteVersionListBox != null)
                    {
                        RemoteVersionListBox.ItemsSource = list;
                        RemoteVersionListBox.Visibility = Visibility.Visible;
                    }

                    if (RemoteVersionLoadingText != null)
                        RemoteVersionLoadingText.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                if (RemoteVersionLoadingText != null)
                    RemoteVersionLoadingText.Text = $"拉取超时，请稍后刷新: {ex.InnerException?.Message ?? ex.Message}";
            }
        }
        private void RemoteVersion_RightClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement f && f.DataContext is GitHubRelease rel)
            {
                ReleaseNotesTitle.Text = $"{rel.TagName} - 更新日志";
                ReleaseNotesTitle.Foreground = Brushes.Black;
                ReleaseNotesText.Text = string.IsNullOrWhiteSpace(rel.Body)
                    ? "作者很懒，没有留下任何说明..."
                    : rel.Body;
                _currentDetailUrl = $"https://github.com/{_currentDownloadRepo}/releases/tag/{rel.TagName}";
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
                    ShowDialog("错误", $"无法打开链接: {ex.Message}", DialogIcon.Error);
                }
            }
        }
        private async void DownloadVersion_Click(object sender, RoutedEventArgs e)
        {
            if (_config.ManagedFolders.Count == 0)
            {
                ShowDialog("提示", "请先导入文件夹！");
                return;
            }

            var rel = (sender as Button)?.Tag as GitHubRelease;
            if (rel == null)
                return;

            if (_isDownloading)
            {
                ShowDialog("提示", "当前有下载任务，请稍后完成！");
                return;
            }

            var candidates = rel.Assets?.Where(a =>
                a.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("server", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("android", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("dependencies", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("javadoc", StringComparison.OrdinalIgnoreCase)
                && !a.Name.Contains("sources", StringComparison.OrdinalIgnoreCase)
            ).ToList();

            if (candidates == null || candidates.Count == 0)
            {
                ShowDialog("提示", "无适用的客户端文件。");
                return;
            }

            GitHubAsset? asset = null;
            if (_currentDownloadRepo.Contains("antigrief", StringComparison.OrdinalIgnoreCase))
            {
                var audio = candidates.FirstOrDefault(a =>
                    a.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)
                    || a.Name.Contains("voice", StringComparison.OrdinalIgnoreCase));

                var standard = candidates.FirstOrDefault(a =>
                    (a.Name.Contains("desktop", StringComparison.OrdinalIgnoreCase)
                     || a.Name.Contains("client", StringComparison.OrdinalIgnoreCase))
                    && !a.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)
                    && !a.Name.Contains("voice", StringComparison.OrdinalIgnoreCase));

                if (standard == null)
                    standard = candidates.FirstOrDefault(a =>
                        !a.Name.Contains("audio", StringComparison.OrdinalIgnoreCase)
                        && !a.Name.Contains("voice", StringComparison.OrdinalIgnoreCase));

                if (audio != null && standard != null)
                {
                    var r = await ShowDialogAsync("版本选择", "检测到 Foo 端带语音版。\n▶ 是: 下载语音版\n▶ 否: 下载标准版", DialogIcon.Question, showCancel: true);
                    if (r == MsgResult.Yes)
                        asset = audio;
                    else if (r == MsgResult.No)
                        asset = standard;
                    else
                        return;
                }
            }

            if (asset == null)
            {
                asset = candidates.FirstOrDefault(a => a.Name.Equals("Mindustry.jar", StringComparison.OrdinalIgnoreCase));
                if (asset == null)
                {
                    asset = candidates.FirstOrDefault(a =>
                        a.Name.Contains("desktop", StringComparison.OrdinalIgnoreCase)
                        || a.Name.Contains("Desktop")
                        || a.Name.Contains("client", StringComparison.OrdinalIgnoreCase)
                        || a.Name.Contains("windows", StringComparison.OrdinalIgnoreCase));

                    if (asset == null)
                    {
                        var nonModAssets = candidates.Where(a =>
                            !a.Name.Contains("mod", StringComparison.OrdinalIgnoreCase)
                            && !a.Name.Contains("addon", StringComparison.OrdinalIgnoreCase)
                            && !a.Name.Contains("plugin", StringComparison.OrdinalIgnoreCase)
                        ).ToList();
                        asset = nonModAssets.Count > 0 ? nonModAssets[0] : candidates[0];
                    }
                }
            }

            if (asset == null)
            {
                ShowDialog("提示", "无法确定需要下载的文件，数据可能异常。");
                return;
            }

            string folder = Path.Combine(_config.ManagedFolders[0], "Versions",
                rel.TagName + (_currentDownloadRepo.Contains("TinyLake") ? "-X端"
                    : (_currentDownloadRepo.Contains("antigrief") ? "-Foo端" : "")));

            int c = 1;
            string baseF = folder;
            while (Directory.Exists(folder))
            {
                folder = $"{baseF}-{c++}";
            }

            Directory.CreateDirectory(folder);
            DownloadPanel.Visibility = Visibility.Visible;
            ToggleDownloadState(true);
            try
            {
                var prog = new Progress<double>(p =>
                {
                    DownloadProgressBar.Value = p;
                    StatusText.Text = $"下载中 {p:F1}%";
                });
                await DownloadFileAsync(UrlHelper.Format(asset.BrowserDownloadUrl), Path.Combine(folder, "Mindustry.jar"), prog);
                ShowDialog("成功", "下载成功！", DialogIcon.Info);
                StatusText.Text = "下载成功！";
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"下载失败: {ex.InnerException?.Message ?? ex.Message}", DialogIcon.Error);
            }
            finally
            {
                await Task.Delay(2000);
                DownloadPanel.Visibility = Visibility.Collapsed;
                ToggleDownloadState(false);
            }
        }
        private async Task DownloadFileAsync(string url, string p, IProgress<double> prog)
        {
            using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            var total = resp.Content.Headers.ContentLength ?? -1L;
            using var rs = await resp.Content.ReadAsStreamAsync();
            using var ws = File.Open(p, FileMode.Create);
            var buf = new byte[8192];
            long read = 0;
            int r;
            while ((r = await rs.ReadAsync(buf, 0, buf.Length)) != 0)
            {
                await ws.WriteAsync(buf, 0, r);
                read += r;
                if (total != -1)
                    prog.Report((double)read / total * 100);
            }
        }

        private async void AutoScanGlobalJava_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ScanGlobalJavaBtn.Content = "扫描中...";
                ScanGlobalJavaBtn.IsEnabled = false;
                string currentPath = GlobalJavaComboBox.Text;
                var javas = await Task.Run(() => JavaScanner.Scan(currentPath, true));
                GlobalJavaComboBox.ItemsSource = javas;
                if (javas.Count > 0)
                    GlobalJavaComboBox.Text = javas[0].Path;
                else
                    ShowDialog("MDL", "未在电脑中找到 Java。请手动浏览选择 javaw.exe", DialogIcon.Info);
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"扫描出错: {ex.Message}", DialogIcon.Error);
            }
            finally
            {
                ScanGlobalJavaBtn.Content = "重新扫描";
                ScanGlobalJavaBtn.IsEnabled = true;
            }
        }

        private async void AutoScanVersionJava_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ScanVersionJavaBtn.Content = "扫描中...";
                ScanVersionJavaBtn.IsEnabled = false;
                string currentPath = VSettingsJavaComboBox.Text;
                var javas = await Task.Run(() => JavaScanner.Scan(currentPath, true));
                VSettingsJavaComboBox.ItemsSource = javas;
                if (javas.Count > 0)
                    VSettingsJavaComboBox.Text = javas[0].Path;
                else
                    ShowDialog("MDL", "未在电脑中找到 Java。请手动浏览选择 javaw.exe", DialogIcon.Info);
            }
            catch (Exception ex)
            {
                ShowDialog("错误", $"扫描出错: {ex.Message}", DialogIcon.Error);
            }
            finally
            {
                ScanVersionJavaBtn.Content = "重新扫描";
                ScanVersionJavaBtn.IsEnabled = true;
            }
        }

        private void BrowseGlobalJavaBtn_Click(object sender, RoutedEventArgs e)
        {
            var d = new OpenFileDialog { Filter = "Java|java.exe;javaw.exe" };
            if (d.ShowDialog() == true)
                GlobalJavaComboBox.Text = d.FileName;
        }
        private void LoadConfig()
        {
            if (File.Exists(ConfigFilePath))
            {
                try
                {
                    _config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFilePath)) ?? new AppConfig();
                }
                catch { }
            }
        }

        private void SaveConfig()
        {
            try { File.WriteAllText(ConfigFilePath, JsonSerializer.Serialize(_config)); } catch { }
        }

        private void LoadVersionConfig(string p)
        {
            string cp = Path.Combine(p, "mdl_instance_config.json");
            if (File.Exists(cp))
            {
                try
                {
                    _currentVersionConfig = JsonSerializer.Deserialize<VersionConfig>(File.ReadAllText(cp)) ?? new VersionConfig();
                }
                catch
                {
                    _currentVersionConfig = new VersionConfig();
                }
            }
            else
            {
                _currentVersionConfig = new VersionConfig();
                _currentVersionConfig.CustomRamMB = _config.GlobalRamMB;
            }
        }

        private void SaveVersionConfig(string p)
        {
            _currentVersionConfig.CustomJavaPath = VSettingsJavaComboBox.Text;
            _currentVersionConfig.CustomJvmArgs = VSettingsJvmArgsBox.Text;
            _currentVersionConfig.UseIsolation = VSettingsIsolationBox.SelectedIndex == 0;
            if (VSettingsRamSlider != null)
                _currentVersionConfig.CustomRamMB = (int)VSettingsRamSlider.Value;

            string cp = Path.Combine(p, "mdl_instance_config.json");
            try
            {
                File.WriteAllText(cp, JsonSerializer.Serialize(_currentVersionConfig));
            }
            catch { }
        }

    }
}
