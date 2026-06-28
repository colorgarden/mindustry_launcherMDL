using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace mindustry_launcher;

// ========== NVIDIA Profile 管理 — 强制 OpenGL 应用使用独显 ==========
public static class NvidiaProfile
{
    private const uint OK = 0;

    public static bool CreateProfile(string? exePath)
    {
        if (string.IsNullOrWhiteSpace(exePath)) return false;
        exePath = Path.GetFullPath(exePath);
        if (!File.Exists(exePath)) return false;

        try
        {
            if (NativeMethods.NvAPI_Initialize() != OK) return false;

            uint status = NativeMethods.NvAPI_DRS_CreateSession(out var session);
            if (status != OK) return false;

            status = NativeMethods.NvAPI_DRS_LoadSettings(session);
            if (status != OK) { NativeMethods.NvAPI_DRS_DestroySession(session); return false; }

            // 查找或创建
            var app = new NVDRS_APPLICATION { version = 0x4F3C3B80 }; // V4
            string exeName = Path.GetFileName(exePath);
            status = NativeMethods.NvAPI_DRS_FindApplicationByName(session, 0, exeName, out var profile);
            if (status != OK)
            {
                app.appName = exeName;
                app.userFriendlyName = exePath;
                app.launcher = "";
                app.flags = 0;
                app.isPredefined = 1;
                status = NativeMethods.NvAPI_DRS_CreateApplication(session, ref app, out profile);
                if (status != OK) { NativeMethods.NvAPI_DRS_DestroySession(session); return false; }
            }

            // SHIM_RENDERING_MODE: 2 = Dedicated GPU
            var s1 = new NVDRS_SETTING { version = 0x44723F89, settingId = 0x10F9DC81, settingType = 2, settingLocation = 2, currentValue = 2 };
            NativeMethods.NvAPI_DRS_SetSetting(session, profile, ref s1);

            // VERTICAL_SYNC: force off (0x60083C51)
            var s2 = new NVDRS_SETTING { version = 0x44723F89, settingId = 0x10F9DC8E, settingType = 2, settingLocation = 2, currentValue = 0x60083C51 };
            NativeMethods.NvAPI_DRS_SetSetting(session, profile, ref s2);

            // PS_FRAMERATE_LIMITER: 0 = off
            var s3 = new NVDRS_SETTING { version = 0x44723F89, settingId = 0x10835003, settingType = 2, settingLocation = 2, currentValue = 0 };
            NativeMethods.NvAPI_DRS_SetSetting(session, profile, ref s3);

            NativeMethods.NvAPI_DRS_SaveSettings(session);
            NativeMethods.NvAPI_DRS_DestroySession(session);

            Debug.WriteLine($"NVIDIA profile configured for: {exePath}");
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"NVIDIA profile error: {ex.Message}");
            return false;
        }
    }

    private static class NativeMethods
    {
        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface")]
        public static extern IntPtr NvAPI_QueryInterface(uint id);

        [DllImport("nvapi64.dll")]
        public static extern uint NvAPI_Initialize();

        static T? Query<T>(uint id) where T : Delegate
        {
            var ptr = NvAPI_QueryInterface(id);
            return ptr != IntPtr.Zero ? Marshal.GetDelegateForFunctionPointer<T>(ptr) : null;
        }

        static readonly DRS_CreateSession_t? _createSession = Query<DRS_CreateSession_t>(0x8FC05EA6);
        static readonly DRS_LoadSettings_t? _loadSettings = Query<DRS_LoadSettings_t>(0x37584D70);
        static readonly DRS_SaveSettings_t? _saveSettings = Query<DRS_SaveSettings_t>(0xFCBC7EAE);
        static readonly DRS_DestroySession_t? _destroy = Query<DRS_DestroySession_t>(0x0E4C71F0);
        static readonly DRS_FindApp_t? _findApp = Query<DRS_FindApp_t>(0xCCC48F82);
        static readonly DRS_CreateApp_t? _createApp = Query<DRS_CreateApp_t>(0x4347A9DE);
        static readonly DRS_SetSetting_t? _setSetting = Query<DRS_SetSetting_t>(0x577DD258);

        public static uint NvAPI_DRS_CreateSession(out IntPtr s) { s = IntPtr.Zero; var fn = _createSession; return fn != null ? fn(out s) : 0xFFFF; }
        public static uint NvAPI_DRS_LoadSettings(IntPtr s) => _loadSettings?.Invoke(s) ?? 0xFFFF;
        public static uint NvAPI_DRS_SaveSettings(IntPtr s) => _saveSettings?.Invoke(s) ?? 0xFFFF;
        public static uint NvAPI_DRS_DestroySession(IntPtr s) => _destroy?.Invoke(s) ?? 0xFFFF;
        public static uint NvAPI_DRS_FindApplicationByName(IntPtr s, uint flags, string name, out IntPtr p) { p = IntPtr.Zero; var fn = _findApp; return fn != null ? fn(s, flags, name, out p) : 0xFFFF; }
        public static uint NvAPI_DRS_CreateApplication(IntPtr s, ref NVDRS_APPLICATION app, out IntPtr p) { p = IntPtr.Zero; var fn = _createApp; return fn != null ? fn(s, ref app, out p) : 0xFFFF; }
        public static uint NvAPI_DRS_SetSetting(IntPtr s, IntPtr p, ref NVDRS_SETTING st) { var fn = _setSetting; return fn != null ? fn(s, p, ref st) : 0xFFFF; }

        delegate uint DRS_CreateSession_t(out IntPtr s);
        delegate uint DRS_LoadSettings_t(IntPtr s);
        delegate uint DRS_SaveSettings_t(IntPtr s);
        delegate uint DRS_DestroySession_t(IntPtr s);
        delegate uint DRS_FindApp_t(IntPtr s, uint flags, [MarshalAs(UnmanagedType.LPWStr)] string name, out IntPtr p);
        delegate uint DRS_CreateApp_t(IntPtr s, ref NVDRS_APPLICATION app, out IntPtr p);
        delegate uint DRS_SetSetting_t(IntPtr s, IntPtr p, ref NVDRS_SETTING st);
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NVDRS_APPLICATION
    {
        public uint version;
        public uint isPredefined;
        public uint flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)] public string appName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)] public string userFriendlyName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 2048)] public string launcher;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct NVDRS_SETTING
    {
        public uint version;
        public uint settingId;
        public uint settingType;
        public uint settingLocation;
        public uint isCurrentPredefined;
        public uint isPredefinedValid;
        public uint currentValue;
        public uint predefinedValue;
    }
}
