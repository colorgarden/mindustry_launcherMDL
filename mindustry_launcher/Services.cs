using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    private static bool _initialized;

    public static void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(OnPreviewMouseWheel),
            handledEventsToo: true);
    }

    private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv && sv.ComputedVerticalScrollBarVisibility == Visibility.Visible)
        {
            e.Handled = true;
            sv.BeginAnimation(ScrollOffsetProperty, null);
            SetScrollOffset(sv, sv.VerticalOffset);
            double target = Math.Max(0, Math.Min(sv.ScrollableHeight, sv.VerticalOffset - (e.Delta * 1.2)));
            var anim = new System.Windows.Media.Animation.DoubleAnimation(
                target, TimeSpan.FromMilliseconds(350))
            { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
            sv.BeginAnimation(ScrollOffsetProperty, anim);
        }
    }
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
            throw new Exception(L.Get("save.invalid_format"));

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

// ========== UBJSON 读写 (Mindustry type=5 专用) ==========
public static class UbjsonReader
{
    public static JsonElement Read(byte[] data)
    {
        using var ms = new MemoryStream(data);
        using var doc = JsonDocument.Parse(ReadValue(ms).ToString()!);
        return doc.RootElement.Clone();
    }

    private static object? ReadValue(MemoryStream ms)
    {
        byte marker = (byte)ms.ReadByte();
        return marker switch
        {
            (byte)'T' => true,
            (byte)'F' => false,
            (byte)'Z' => null,
            (byte)'i' => (int)(sbyte)ReadRawByte(ms),
            (byte)'U' => (int)ReadRawByte(ms),
            (byte)'I' => (int)BinaryPrimitives.ReadInt16BigEndian(ReadN(ms, 2)),
            (byte)'l' => BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4)),
            (byte)'L' => BinaryPrimitives.ReadInt64BigEndian(ReadN(ms, 8)),
            (byte)'d' => ReadFloat32BE(ms),
            (byte)'D' => ReadFloat64BE(ms),
            (byte)'h' => ReadStringAfterH(ms),
            (byte)'s' => ReadUtfString(ms, ReadRawByte(ms)),
            (byte)'S' => ReadUtfString(ms, BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4))),
            (byte)'B' => (int)ReadRawByte(ms),
            (byte)'C' => ((char)ReadRawByte(ms)).ToString(),
            (byte)'[' => ReadArray(ms),
            (byte)'{' => ReadObject(ms),
            _ => null
        };
    }

    private static List<object?> ReadArray(MemoryStream ms)
    {
        // 优化数组: [$type#count
        if (ms.Position < ms.Length && PeekByte(ms) == '$')
        {
            ms.ReadByte(); // consume '$'
            byte type = ReadRawByte(ms);
            ms.ReadByte(); // consume '#'
            int count = BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4));
            var list = new List<object?>(count);
            for (int i = 0; i < count; i++) list.Add(ReadOptimizedValue(ms, type));
            if (ms.Position < ms.Length && PeekByte(ms) == ']') ms.ReadByte();
            return list;
        }
        // 标准数组
        var lst = new List<object?>();
        while (ms.Position < ms.Length && PeekByte(ms) != ']') lst.Add(ReadValue(ms));
        if (ms.Position < ms.Length) ms.ReadByte();
        return lst;
    }

    private static Dictionary<string, object?> ReadObject(MemoryStream ms)
    {
        // 优化对象: {$keyType#count [key...] [value...]
        if (ms.Position < ms.Length && PeekByte(ms) == '$')
        {
            ms.ReadByte(); // consume '$'
            byte keyType = ReadRawByte(ms);
            ms.ReadByte(); // consume '#'
            int count = BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4));
            // 按类型读 keys
            var keys = new string[count];
            for (int i = 0; i < count; i++) keys[i] = ReadStringByType(ms, keyType);
            // values 各自带类型标记
            var dict = new Dictionary<string, object?>(count);
            for (int i = 0; i < count; i++) dict[keys[i]] = ReadValue(ms);
            if (ms.Position < ms.Length && PeekByte(ms) == '}') ms.ReadByte();
            return dict;
        }
        // 标准对象
        var d = new Dictionary<string, object?>();
        while (ms.Position < ms.Length && PeekByte(ms) != '}')
        {
            string key = ReadUtfString(ms, BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4)));
            d[key] = ReadValue(ms);
        }
        if (ms.Position < ms.Length) ms.ReadByte();
        return d;
    }

    static object? ReadOptimizedValue(MemoryStream ms, byte type)
    {
        return type switch
        {
            (byte)'T' => true, (byte)'F' => false, (byte)'Z' => null,
            (byte)'i' => (int)(sbyte)ReadRawByte(ms),
            (byte)'U' => (int)ReadRawByte(ms),
            (byte)'I' => (int)BinaryPrimitives.ReadInt16BigEndian(ReadN(ms, 2)),
            (byte)'l' => BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4)),
            (byte)'L' => BinaryPrimitives.ReadInt64BigEndian(ReadN(ms, 8)),
            (byte)'d' => ReadFloat32BE(ms),
            (byte)'D' => ReadFloat64BE(ms),
            (byte)'B' => (int)ReadRawByte(ms),
            (byte)'C' => ((char)ReadRawByte(ms)).ToString(),
            (byte)'s' => ReadUtfString(ms, ReadRawByte(ms)),
            (byte)'S' => ReadUtfString(ms, BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4))),
            _ => null
        };
    }

    static string ReadStringByType(MemoryStream ms, byte type)
    {
        int len = type == 'S' ? BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4)) : ReadRawByte(ms);
        return ReadUtfString(ms, len);
    }

    static byte PeekByte(MemoryStream ms) { byte b = (byte)ms.ReadByte(); ms.Position--; return b; }
    static byte ReadRawByte(MemoryStream ms) => (byte)ms.ReadByte();
    static byte[] ReadN(MemoryStream ms, int n) { var buf = new byte[n]; ms.Read(buf, 0, n); return buf; }
    static string ReadUtfString(MemoryStream ms, int len) { var buf = new byte[len]; ms.Read(buf, 0, len); return Encoding.UTF8.GetString(buf); }
    // h 后跟一个字符串类型标记（s 或 S），再跟字符串内容
    static string ReadStringAfterH(MemoryStream ms)
    {
        byte s = ReadRawByte(ms);
        int len = s == 'S' ? BinaryPrimitives.ReadInt32BigEndian(ReadN(ms, 4)) : ReadRawByte(ms);
        return ReadUtfString(ms, len);
    }
    static float ReadFloat32BE(MemoryStream ms) { var b = ReadN(ms, 4); if (BitConverter.IsLittleEndian) Array.Reverse(b); return BitConverter.ToSingle(b, 0); }
    static double ReadFloat64BE(MemoryStream ms) { var b = ReadN(ms, 8); if (BitConverter.IsLittleEndian) Array.Reverse(b); return BitConverter.ToDouble(b, 0); }
}

public static class UbjsonWriter
{
    public static byte[] Write(JsonElement el)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        WriteElement(w, el);
        w.Flush();
        return ms.ToArray();
    }

    private static void WriteElement(BinaryWriter w, JsonElement el)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.True: w.Write((byte)'T'); break;
            case JsonValueKind.False: w.Write((byte)'F'); break;
            case JsonValueKind.Null: w.Write((byte)'Z'); break;
            case JsonValueKind.Number:
                if (el.TryGetInt64(out long lv))
                {
                    if (lv >= sbyte.MinValue && lv <= sbyte.MaxValue) { w.Write((byte)'i'); w.Write((sbyte)lv); }
                    else if (lv >= short.MinValue && lv <= short.MaxValue) { w.Write((byte)'I'); WriteInt16BE(w, (short)lv); }
                    else if (lv >= int.MinValue && lv <= int.MaxValue) { w.Write((byte)'l'); WriteInt32BE(w, (int)lv); }
                    else { w.Write((byte)'L'); WriteInt64BE(w, lv); }
                }
                else
                {
                    double dv = el.GetDouble();
                    float fv = (float)dv;
                    if (Math.Abs(fv - dv) < 1e-7) { w.Write((byte)'d'); WriteFloat32BE(w, fv); }
                    else { w.Write((byte)'D'); WriteFloat64BE(w, dv); }
                }
                break;
            case JsonValueKind.String:
                WriteUtfString(w, el.GetString()!);
                break;
            case JsonValueKind.Array:
                w.Write((byte)'[');
                foreach (var item in el.EnumerateArray()) WriteElement(w, item);
                w.Write((byte)']');
                break;
            case JsonValueKind.Object:
                w.Write((byte)'{');
                foreach (var prop in el.EnumerateObject())
                {
                    WriteUtfString(w, prop.Name);
                    WriteElement(w, prop.Value);
                }
                w.Write((byte)'}');
                break;
        }
    }

    static void WriteUtfString(BinaryWriter w, string s)
    {
        var bytes = Encoding.UTF8.GetBytes(s);
        w.Write((byte)'S');
        WriteInt32BE(w, bytes.Length);
        w.Write(bytes);
    }
    static void WriteInt16BE(BinaryWriter w, short v) { w.Write((byte)(v >> 8)); w.Write((byte)v); }
    static void WriteInt32BE(BinaryWriter w, int v) { w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
    static void WriteInt64BE(BinaryWriter w, long v) { w.Write((byte)(v >> 56)); w.Write((byte)(v >> 48)); w.Write((byte)(v >> 40)); w.Write((byte)(v >> 32)); w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
    static void WriteFloat32BE(BinaryWriter w, float v) { var b = BitConverter.GetBytes(v); if (BitConverter.IsLittleEndian) Array.Reverse(b); w.Write(b); }
    static void WriteFloat64BE(BinaryWriter w, double v) { var b = BitConverter.GetBytes(v); if (BitConverter.IsLittleEndian) Array.Reverse(b); w.Write(b); }
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
            byte[] raw = File.ReadAllBytes(filePath);

            // zlib 解压（Mindustry 偶尔压缩 settings.bin，magic byte 0x78）
            if (raw.Length >= 2 && raw[0] == 0x78 && (raw[1] == 0x01 || raw[1] == 0x5E || raw[1] == 0x9C || raw[1] == 0xDA))
            {
                try
                {
                    using var compressed = new MemoryStream(raw, 2, raw.Length - 2);
                    using var deflate = new DeflateStream(compressed, CompressionMode.Decompress);
                    using var decompressed = new MemoryStream();
                    deflate.CopyTo(decompressed);
                    raw = decompressed.ToArray();
                }
                catch { /* 解压失败，按原始字节解析 */ }
            }

            return ParseEntries(raw, items);
        }
        catch (Exception ex) { ErrorMessage = ex.Message; return false; }
    }

    private bool ParseEntries(byte[] raw, List<SettingItem> items)
    {
        try
        {
            using var ms = new MemoryStream(raw);
            using var r = new BinaryReader(ms);
            int count = ReadInt32BE(r);
            if (count < 0 || count > 100_000)
            {
                ErrorMessage = $"Invalid entry count: {count}";
                return false;
            }

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
                        byte[] bytes = r.ReadBytes(len);
                        item.OriginalValue = bytes;
                        try
                        {
                            var json = UbjsonReader.Read(bytes);
                            item.DisplayValue = JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true });
                        }
                        catch
                        {
                            item.DisplayValue = $"[{len} bytes]";
                        }
                        break;
                    default:
                        ErrorMessage = L.T("settings_bin.unknown_type", item.Type, item.Key);
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
                    try
                    {
                        using var doc = JsonDocument.Parse(item.DisplayValue);
                        byte[] ubjson = UbjsonWriter.Write(doc.RootElement);
                        WriteInt32BE(bw, ubjson.Length);
                        bw.Write(ubjson);
                    }
                    catch
                    {
                        // JSON 解析失败，回写原始字节（未修改或格式错误时的安全兜底）
                        byte[] bin = (byte[])item.OriginalValue;
                        WriteInt32BE(bw, bin.Length);
                        bw.Write(bin);
                    }
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
	        if (_cache != null && !forceRefresh)
	        {
	            // 缓存命中时，确保当前配置的路径也在列表里
	            if (!string.IsNullOrEmpty(currentConfigPath) && File.Exists(currentConfigPath))
	            {
	                var hit = _cache.FirstOrDefault(j => string.Equals(j.Path, currentConfigPath, StringComparison.OrdinalIgnoreCase));
	                if (hit == null)
	                {
	                    var extra = GetVersion(currentConfigPath);
	                    return _cache.Append(extra).OrderByDescending(j => j.VersionNumber).ToList();
	                }
	            }
	            return _cache;
	        }

	        // 用家目录去重，每个安装只出一条（首选 javaw.exe）
	        var homes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
	        var binDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

	        string? ResolveExe(string home)
	        {
	            var jw = Path.Combine(home, "bin", "javaw.exe"); if (File.Exists(jw)) return jw;
	            var j  = Path.Combine(home, "bin", "java.exe");  if (File.Exists(j))  return j;
	            jw = Path.Combine(home, "javaw.exe");            if (File.Exists(jw)) return jw;
	            j  = Path.Combine(home, "java.exe");             if (File.Exists(j))  return j;
	            return null;
	        }

	        void AddHome(string? h)
	        {
	            if (string.IsNullOrWhiteSpace(h)) return;
	            try
	            {
	                var p = Path.GetFullPath(h);
	                if (Path.GetFileName(p).Equals("bin", StringComparison.OrdinalIgnoreCase))
	                    p = Directory.GetParent(p)?.FullName ?? p;
	                if (!p.Contains("javapath", StringComparison.OrdinalIgnoreCase) && Directory.Exists(p))
	                    homes.Add(p);
	            }
	            catch (Exception ex) { Debug.WriteLine($"AddHome: {ex.Message}"); }
	        }

	        void AddBinDir(string? d)
	        {
	            if (string.IsNullOrWhiteSpace(d)) return;
	            try { if (Directory.Exists(d)) binDirs.Add(Path.GetFullPath(d)); }
	            catch (Exception ex) { Debug.WriteLine($"AddBinDir: {ex.Message}"); }
	        }

	        void Walk(string dir, int maxDepth = 2, int depth = 0)
	        {
	            try { if (depth > maxDepth || !Directory.Exists(dir)) return; } catch { return; }
	            try
	            {
	                var bin = Path.Combine(dir, "bin");
	                if (Directory.Exists(bin) &&
	                    (File.Exists(Path.Combine(bin, "javaw.exe")) || File.Exists(Path.Combine(bin, "java.exe"))))
	                    AddHome(dir);
	                if (File.Exists(Path.Combine(dir, "javaw.exe")) || File.Exists(Path.Combine(dir, "java.exe")))
	                    AddHome(dir);
	                foreach (var sub in Directory.GetDirectories(dir))
	                {
	                    var name = Path.GetFileName(sub).ToLower();
	                    if (depth == 0 || name.Contains("jre") || name.Contains("jdk") || name.Contains("java") ||
	                        name.Contains("bin") || name.Contains("runtime") || name.Contains("x64") ||
	                        name.Contains("x86") || name.Contains("hotspot") || name.Contains("corretto") ||
	                        name.Contains("zulu") || name.Contains("adopt") || name.StartsWith("jre-") ||
	                        name.StartsWith("jdk-") || name.Contains("versions") || name.Contains("graalvm") ||
	                        name.Contains("temurin") || name.Contains("liberica") || name.Contains("semeru") ||
	                        name.Contains("sapmachine") || name.Contains("dragonwell") || name.Contains("kona") ||
	                        name.Contains("bisheng") || name.Contains("mandrel"))
	                        Walk(sub, maxDepth, depth + 1);
	                }
	            }
	            catch (Exception ex) { Debug.WriteLine($"Walk: {ex.Message}"); }
	        }

	        // ==== 注册表（所有主流 JDK 厂商） ====
	        void AddRegJavaHome(string keyPath, string? valName = "JavaHome")
	        {
	            try
	            {
	                using var kr = Registry.LocalMachine.OpenSubKey(keyPath);
	                if (kr == null) return;
	                foreach (var sk in kr.GetSubKeyNames())
	                {
	                    using var sub = kr.OpenSubKey(sk);
	                    if (sub == null) continue;
	                    var home = sub.GetValue(valName ?? "") as string;
	                    if (!string.IsNullOrEmpty(home)) AddHome(home);
	                    foreach (var vk in sub.GetSubKeyNames())
	                    {
	                        using var vKey = sub.OpenSubKey(vk);
	                        home = vKey?.GetValue(valName ?? "JavaHome") as string;
	                        if (!string.IsNullOrEmpty(home)) AddHome(home);
	                    }
	                }
	            }
	            catch (Exception ex) { Debug.WriteLine($"Reg {keyPath}: {ex.Message}"); }
	        }
	        foreach (var rk in new[] {
	            @"SOFTWARE\JavaSoft\JDK",
	            @"SOFTWARE\JavaSoft\Java Runtime Environment",
	            @"SOFTWARE\JavaSoft\Java Development Kit",
	            @"SOFTWARE\Eclipse Adoptium\JDK",
	            @"SOFTWARE\Eclipse Foundation\JDK",
	            @"SOFTWARE\Microsoft\JDK",
	            @"SOFTWARE\Azul Systems\Zulu",
	            @"SOFTWARE\Azul Systems\Zulu\JDK",
	            @"SOFTWARE\BellSoft\Liberica",
	            @"SOFTWARE\BellSoft\Liberica\JDK",
	            @"SOFTWARE\Amazon\Corretto\JDK",
	            @"SOFTWARE\Red Hat\OpenJDK",
	            @"SOFTWARE\Semeru\JDK",
	            @"SOFTWARE\GraalVM",
	        }) AddRegJavaHome(rk);

	        if (!string.IsNullOrEmpty(currentConfigPath))
	            AddHome(Path.GetDirectoryName(Path.GetDirectoryName(currentConfigPath)));

	        // ==== 环境变量 ====
	        var jh = Environment.GetEnvironmentVariable("JAVA_HOME");
	        if (!string.IsNullOrEmpty(jh)) AddHome(jh);

	        var pe = Environment.GetEnvironmentVariable("PATH");
	        if (!string.IsNullOrEmpty(pe))
	            foreach (var p in pe.Split(Path.PathSeparator))
	            {
	                var clean = p.Trim('"', ' ');
	                if (File.Exists(Path.Combine(clean, "java.exe")) || File.Exists(Path.Combine(clean, "javaw.exe")))
	                    AddBinDir(clean);
	                if (Directory.Exists(Path.Combine(clean, "bin")))
	                    AddHome(clean);
	            }

	        // ==== 文件系统扫描 ====
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
	        catch (Exception ex) { Debug.WriteLine($"Drive scan: {ex.Message}"); }

	        var cwd = AppDomain.CurrentDomain.BaseDirectory;
	        dirs.Add(cwd + "runtime"); dirs.Add(cwd + "java"); dirs.Add(cwd + "jre");
	        dirs.Add(cwd + ".minecraft\\runtime"); dirs.Add(cwd + "hmcl\\.minecraft\\versions");

	        foreach (var d in dirs) { if (!string.IsNullOrWhiteSpace(d)) Walk(d, 3); }

	        // ==== 汇总 ====
	        var results = new List<JavaInfo>();
	        foreach (var home in homes)
	        {
	            var exe = ResolveExe(home);
	            if (exe != null) results.Add(GetVersion(exe));
	        }
	        foreach (var bin in binDirs)
	        {
	            var jw = Path.Combine(bin, "javaw.exe");
	            var j  = Path.Combine(bin, "java.exe");
	            var exe = File.Exists(jw) ? jw : (File.Exists(j) ? j : null);
	            if (exe != null) results.Add(GetVersion(exe));
	        }

	        _cache = results.OrderByDescending(j => j.VersionNumber).ToList();
	        return _cache;
	    }

	private static JavaInfo GetVersion(string path)
    {
        var info = new JavaInfo { Path = path, Version = L.Get("java.unknown_version") };
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
