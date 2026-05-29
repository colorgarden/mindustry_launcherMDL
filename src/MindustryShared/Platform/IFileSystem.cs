namespace MindustryShared;

public interface IFileSystem
{
    string AppDataDir { get; }
    string LauncherDir { get; }
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllText(string path, string content);
    void CreateDirectory(string path);
    bool DirectoryExists(string path);
    string[] GetFiles(string dir, string pattern, bool recurse = false);
    string[] GetDirectories(string dir);
    string Combine(params string[] parts);
    Stream OpenRead(string path);
    Stream OpenWrite(string path);
    string GetFileName(string path);
}
