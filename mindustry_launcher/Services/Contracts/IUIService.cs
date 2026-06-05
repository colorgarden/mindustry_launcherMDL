using System.Windows.Threading;

namespace mindustry_launcher
{
    public interface IUIService
    {
        void ShowDialog(string title, string message, DialogIcon icon = DialogIcon.Info);
        Task<MsgResult> ShowDialogAsync(string title, string message, DialogIcon icon, bool showCancel = false);
        void ShowOverlay(string overlayName);
        void HideOverlay(string overlayName);
        void SetDownloadState(bool isDownloading);
        bool IsDownloading { get; }
        Dispatcher Dispatcher { get; }
    }

    public enum MsgResult { Ok, Yes, No, Cancel }
    public enum DialogIcon { Info, Warning, Error, Question }
}
