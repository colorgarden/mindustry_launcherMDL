using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace mindustry_launcher;

// ========== URL 代理 ==========
public static class UrlHelper
{
    public static int ProxyIndex { get; set; } = 1;

    public static string Format(string url, bool isApi = false)
    {
        int m = ProxyIndex;
        if (m == 0) return url;
        if (m == 1) return "https://ghfast.top/" + url;
        if (m == 2) return "https://gh-proxy.com/" + url;
        if (m == 3)
        {
            if (isApi) return "https://ghfast.top/" + url;
            if (url.StartsWith("https://github.com")) return url.Replace("https://github.com", "https://kkgithub.com");
            if (url.StartsWith("https://raw.githubusercontent.com")) return url.Replace("https://raw.githubusercontent.com", "https://raw.kkgithub.com");
            return "https://kkgithub.com/" + url;
        }
        if (m == 4 && !isApi && url.StartsWith("https://raw.githubusercontent.com/"))
            return url.Replace("https://raw.githubusercontent.com/", "https://cdn.jsdelivr.net/gh/").Replace("/master/", "@master/").Replace("/main/", "@main/");
        if (m == 5) return "https://gh.llkk.cc/" + url;
        return url;
    }
}

// ========== 平滑滚动 ==========
public static class SmoothScrollHelper
{
    public static readonly DependencyProperty ScrollOffsetProperty =
        DependencyProperty.RegisterAttached("ScrollOffset", typeof(double), typeof(SmoothScrollHelper),
            new PropertyMetadata(0.0, (d, e) =>
            {
                if (d is ScrollViewer sv) sv.ScrollToVerticalOffset((double)e.NewValue);
            }));

    public static void SetScrollOffset(DependencyObject d, double v) => d.SetValue(ScrollOffsetProperty, v);
}

// ========== 硬件信息 ==========
public static class HardwareInfo
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MEMORYSTATUSEX
    {
        public uint dwLength; public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys;
        public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual;
        public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public static int GetTotalPhysicalMemoryMB()
    {
        try
        {
            var mem = new MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            if (GlobalMemoryStatusEx(ref mem)) return (int)(mem.ullTotalPhys / (1024 * 1024));
        }
        catch (Exception ex) { Debug.WriteLine($"GlobalMemoryStatusEx failed: {ex.Message}"); }
        return 16384;
    }
}

// ========== 存档解析 ==========
public static class MsavParser
{
    public static MindustrySaveMetadata Parse(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var reader = new BinaryReader(fs);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MSAV")
            throw new Exception("不是有效的 Mindustry 存档文件！");

        var meta = new MindustrySaveMetadata { Version = "v" + ReadInt32BE(reader).ToString() };
        meta.MapName = ReadJavaUTF(reader);
        meta.Author = ReadJavaUTF(reader);
        meta.Description = ReadJavaUTF(reader);
        meta.Wave = ReadInt32BE(reader).ToString();
        meta.PlayTime = ReadInt64BE(reader).ToString() + " ms";
        return meta;
    }

    private static int ReadInt32BE(BinaryReader r) => BinaryPrimitives.ReadInt32BigEndian(r.ReadBytes(4));
    private static long ReadInt64BE(BinaryReader r) => BinaryPrimitives.ReadInt64BigEndian(r.ReadBytes(8));

    private static string ReadJavaUTF(BinaryReader r)
    {
        ushort len = BinaryPrimitives.ReadUInt16BigEndian(r.ReadBytes(2));
        return len == 0 ? "" : Encoding.UTF8.GetString(r.ReadBytes(len));
    }
}

// ========== Mindustry settings.bin 编辑器 ==========
public class MindustrySettingsEditor
{
    public string ErrorMessage { get; private set; } = "";

    public bool LoadList(string filePath, out List<SettingItem> items)
    {
        items = new();
        ErrorMessage = "";
        if (!File.Exists(filePath)) return false;

        try
        {
            using var ms = new MemoryStream(File.ReadAllBytes(filePath));
            using var r = new BinaryReader(ms);
            int count = ReadInt32BE(r);

            for (int i = 0; i < count; i++)
            {
                var item = new SettingItem { Key = ReadJavaUTF(r), Type = r.ReadByte() };
                switch (item.Type)
                {
                    case 0: item.OriginalValue = r.ReadBoolean(); item.DisplayValue = item.OriginalValue.ToString()!; break;
                    case 1: item.OriginalValue = ReadInt32BE(r); item.DisplayValue = item.OriginalValue.ToString()!; break;
                    case 2: item.OriginalValue = ReadInt64BE(r); item.DisplayValue = item.OriginalValue.ToString()!; break;
                    case 3:
                        byte[] fb = r.ReadBytes(4);
                        if (BitConverter.IsLittleEndian) Array.Reverse(fb);
                        item.OriginalValue = BitConverter.ToSingle(fb, 0);
                        item.DisplayValue = item.OriginalValue.ToString()!;
                        break;
                    case 4: item.OriginalValue = ReadJavaUTF(r); item.DisplayValue = (string)item.OriginalValue; break;
                    case 5:
                        int len = ReadInt32BE(r);
                        item.OriginalValue = r.ReadBytes(len);
                        item.DisplayValue = $"[二进制数据, 长度={len}]";
                        break;
                    default:
                        ErrorMessage = $"未知类型 {item.Type} (Key: {item.Key})";
                        return false;
                }
                items.Add(item);
            }
            return true;
        }
        catch (Exception ex) { ErrorMessage = ex.Message; return false; }
    }

    public void SaveList(string filePath, List<SettingItem> items)
    {
        using var fs = File.Create(filePath);
        using var bw = new BinaryWriter(fs);
        WriteInt32BE(bw, items.Count);

        foreach (var item in items)
        {
            WriteJavaUTF(bw, item.Key);
            bw.Write(item.Type);
            switch (item.Type)
            {
                case 0: bw.Write(bool.Parse(item.DisplayValue)); break;
                case 1: WriteInt32BE(bw, int.Parse(item.DisplayValue)); break;
                case 2: WriteInt64BE(bw, long.Parse(item.DisplayValue)); break;
                case 3:
                    byte[] fb = BitConverter.GetBytes(float.Parse(item.DisplayValue));
                    if (BitConverter.IsLittleEndian) Array.Reverse(fb);
                    bw.Write(fb);
                    break;
                case 4: WriteJavaUTF(bw, item.DisplayValue); break;
                case 5:
                    byte[] bin = (byte[])item.OriginalValue;
                    WriteInt32BE(bw, bin.Length);
                    bw.Write(bin);
                    break;
            }
        }
    }

    static int ReadInt32BE(BinaryReader r) { var b = r.ReadBytes(4); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }
    static void WriteInt32BE(BinaryWriter w, int v) { w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
    static long ReadInt64BE(BinaryReader r) { var b = r.ReadBytes(8); return ((long)b[0] << 56) | ((long)b[1] << 48) | ((long)b[2] << 40) | ((long)b[3] << 32) | ((long)b[4] << 24) | ((long)b[5] << 16) | ((long)b[6] << 8) | b[7]; }
    static void WriteInt64BE(BinaryWriter w, long v) { w.Write((byte)(v >> 56)); w.Write((byte)(v >> 48)); w.Write((byte)(v >> 40)); w.Write((byte)(v >> 32)); w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
    static string ReadJavaUTF(BinaryReader r) { int len = (r.ReadByte() << 8) | r.ReadByte(); return Encoding.UTF8.GetString(r.ReadBytes(len)); }
    static void WriteJavaUTF(BinaryWriter w, string s) { var b = Encoding.UTF8.GetBytes(s); w.Write((byte)(b.Length >> 8)); w.Write((byte)b.Length); w.Write(b); }
}

// ========== Java 扫描 ==========
public static class JavaScanner
{
    private static List<JavaInfo>? _cache;

    public static List<JavaInfo> Scan(string currentConfigPath = "", bool forceRefresh = false)
    {
        if (_cache != null && !forceRefresh) return _cache;

        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string? p)
        {
            if (string.IsNullOrWhiteSpace(p) || p.Contains("\\javapath", StringComparison.OrdinalIgnoreCase)) return;
            try { if (File.Exists(p)) paths.Add(Path.GetFullPath(p)); } catch (Exception ex) { Debug.WriteLine($"Failed to check Java path '{p}': {ex.Message}"); }
        }

        void Walk(string dir, int maxDepth = 2, int depth = 0)
        {
            try { if (depth > maxDepth || !Directory.Exists(dir)) return; } catch (Exception ex) { Debug.WriteLine($"Failed to check directory '{dir}': {ex.Message}"); return; }
            try
            {
                Add(Path.Combine(dir, "bin", "javaw.exe"));
                Add(Path.Combine(dir, "bin", "java.exe"));
                Add(Path.Combine(dir, "javaw.exe"));
                Add(Path.Combine(dir, "java.exe"));
                foreach (var sub in Directory.GetDirectories(dir))
                {
                    var name = Path.GetFileName(sub).ToLower();
                    if (depth == 0 || name.Contains("jre") || name.Contains("jdk") || name.Contains("java") || name.Contains("bin") || name.Contains("runtime") || name.Contains("x64") || name.Contains("x86") || name.Contains("hotspot") || name.Contains("corretto") || name.Contains("zulu") || name.Contains("adopt") || name.StartsWith("jre-") || name.StartsWith("jdk-") || name.Contains("versions"))
                        Walk(sub, maxDepth, depth + 1);
                }
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to enumerate Java directories: {ex.Message}"); }
        }

        Add(currentConfigPath);

        // 注册表
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\JavaSoft");
            if (key != null)
                foreach (var sk in key.GetSubKeyNames())
                    using (var sub = key.OpenSubKey(sk))
                        if (sub != null)
                            foreach (var vk in sub.GetSubKeyNames())
                                using (var vKey = sub.OpenSubKey(vk))
                                {
                                    var home = vKey?.GetValue("JavaHome") as string;
                                    if (!string.IsNullOrEmpty(home)) { Add(Path.Combine(home, "bin", "javaw.exe")); Add(Path.Combine(home, "bin", "java.exe")); }
                                }
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to scan registry for Java: {ex.Message}"); }

        var jh = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrEmpty(jh)) Walk(jh, 1);

        var pe = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrEmpty(pe))
            foreach (var p in pe.Split(Path.PathSeparator))
                try { Add(Path.Combine(p.Trim('"', ' '), "javaw.exe")); } catch (Exception ex) { Debug.WriteLine($"Failed to scan PATH entry for Java: {ex.Message}"); }

        var appData = Environment.GetEnvironmentVariable("APPDATA");
        var localAppData = Environment.GetEnvironmentVariable("LocalAppData");
        var dirs = new List<string?> {
            Environment.GetEnvironmentVariable("ProgramFiles"),
            Environment.GetEnvironmentVariable("ProgramFiles(x86)"),
            localAppData + "\\Programs",
            appData + "\\.minecraft\\runtime",
            appData + "\\.hmcl\\java",
            appData + "\\.minecraft\\versions",
            localAppData + "\\Packages\\Microsoft.4297127D64EC6_8wekyb3d8bbwe\\LocalCache\\Local\\runtime"
        };

        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                var r = d.Name;
                dirs.Add(r + "Java"); dirs.Add(r + "java");
                dirs.Add(r + "Program Files\\Java"); dirs.Add(r + "Program Files (x86)\\Java");
                dirs.Add(r + "MinecraftLauncher\\runtime"); dirs.Add(r + "MCLauncher\\runtime");
                dirs.Add(r + ".minecraft\\runtime"); dirs.Add(r + "MC\\runtime");
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to scan drives for Java: {ex.Message}"); }

        var cwd = AppDomain.CurrentDomain.BaseDirectory;
        dirs.Add(cwd + "runtime"); dirs.Add(cwd + "java"); dirs.Add(cwd + "jre");
        dirs.Add(cwd + ".minecraft\\runtime"); dirs.Add(cwd + "hmcl\\.minecraft\\versions");

        foreach (var d in dirs) { if (!string.IsNullOrWhiteSpace(d)) Walk(d, 3); }

        var results = paths.Select(p => GetVersion(p)).ToList();
        _cache = results.OrderByDescending(j => j.VersionNumber).ToList();
        return _cache;
    }

    private static JavaInfo GetVersion(string path)
    {
        var info = new JavaInfo { Path = path, Version = "未知版本" };
        string type = path.Contains("jre", StringComparison.OrdinalIgnoreCase) ? "JRE" : "JDK";
        string raw = "";

        try
        {
            var home = Directory.GetParent(Path.GetDirectoryName(path)!)!.FullName;
            var release = Path.Combine(home, "release");
            if (File.Exists(release))
                foreach (var line in File.ReadLines(release))
                    if (line.StartsWith("JAVA_VERSION=")) { raw = line[13..].Trim('"', ' ', '\r', '\n'); break; }

            if (string.IsNullOrEmpty(raw))
            {
                var fvi = FileVersionInfo.GetVersionInfo(path);
                var v = fvi.ProductVersion ?? fvi.FileVersion;
                if (!string.IsNullOrEmpty(v)) raw = v.Split(' ')[0];
            }

            if (!string.IsNullOrEmpty(raw))
            {
                var major = raw.StartsWith("1.") ? raw.Split('.')[1] : raw.Split('.')[0];
                if (int.TryParse(major, out int ver)) { info.VersionNumber = ver; info.Version = $"{type} {ver} ({raw})"; }
                else info.Version = $"{type} {raw}";
                return info;
            }

            var m = Regex.Match(home, @"(jre|jdk|java)-?(\d+)", RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[2].Value, out int ver2))
            {
                info.VersionNumber = ver2;
                info.Version = $"{type} {ver2} ({ver2}.0)";
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to get Java version for '{path}': {ex.Message}"); }
        return info;
    }
}
