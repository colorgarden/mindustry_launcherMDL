using Android.App;
using Android.Runtime;
using Android.Util;

namespace MindustryLauncher;

[Application]
public class MainApplication : MauiApplication
{
    public MainApplication(IntPtr handle, JniHandleOwnership ownership)
        : base(handle, ownership)
    {
    }

    protected override MauiApp CreateMauiApp()
    {
        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            var ex = e.Exception;
            Log.Error("MDL_CRASH", $"Unhandled: {ex?.GetType()}: {ex?.Message}\n{ex?.StackTrace}");
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            var ex = e.ExceptionObject as Exception;
            Log.Error("MDL_CRASH", $"Domain unhandled: {ex?.GetType()}: {ex?.Message}\n{ex?.StackTrace}");
        };

        return MauiProgram.CreateMauiApp();
    }
}
