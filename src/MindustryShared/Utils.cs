using System.Buffers.Binary;
using System.Text;

namespace MindustryShared;

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

// ========== 存档解析 ==========
public static class MsavParser
{
    public static MindustrySaveMetadata Parse(string filePath)
    {
        using var fs = File.OpenRead(filePath);
        using var reader = new BinaryReader(fs);
        if (Encoding.ASCII.GetString(reader.ReadBytes(4)) != "MSAV")
            throw new Exception("Not a valid Mindustry save file!");

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
                        item.DisplayValue = $"[Binary data, length={len}]";
                        break;
                    default:
                        ErrorMessage = $"Unknown type {item.Type} (Key: {item.Key})";
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

    private static int ReadInt32BE(BinaryReader r) { var b = r.ReadBytes(4); return (b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]; }
    private static void WriteInt32BE(BinaryWriter w, int v) { w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
    private static long ReadInt64BE(BinaryReader r) { var b = r.ReadBytes(8); return ((long)b[0] << 56) | ((long)b[1] << 48) | ((long)b[2] << 40) | ((long)b[3] << 32) | ((long)b[4] << 24) | ((long)b[5] << 16) | ((long)b[6] << 8) | b[7]; }
    private static void WriteInt64BE(BinaryWriter w, long v) { w.Write((byte)(v >> 56)); w.Write((byte)(v >> 48)); w.Write((byte)(v >> 40)); w.Write((byte)(v >> 32)); w.Write((byte)(v >> 24)); w.Write((byte)(v >> 16)); w.Write((byte)(v >> 8)); w.Write((byte)v); }
    private static string ReadJavaUTF(BinaryReader r) { int len = (r.ReadByte() << 8) | r.ReadByte(); return Encoding.UTF8.GetString(r.ReadBytes(len)); }
    private static void WriteJavaUTF(BinaryWriter w, string s) { var b = Encoding.UTF8.GetBytes(s); w.Write((byte)(b.Length >> 8)); w.Write((byte)b.Length); w.Write(b); }
}
