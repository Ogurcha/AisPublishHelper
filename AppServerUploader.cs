namespace AisPublishHelper;

internal static class AppServerUploader
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".dll",
        ".exe"
    };

    public static void Upload(PublishSettings settings)
    {
        if (!Directory.Exists(settings.AppServerLocalPath))
        {
            throw new DirectoryNotFoundException($"Local AppServer folder not found: {settings.AppServerLocalPath}");
        }

        var shareRoot = GetShareRoot(settings);
        var remoteRoot = GetRemoteUnc(settings);

        ConsoleUi.Info($"Connecting to {shareRoot} as {settings.Updater.User}...");
        SmbDiagnostics.PrintHostLookup(settings.Updater.Host);

        using var ipc = WindowsShareSession.ForIpc(settings.Updater.Host);
        var ipcResult = ipc.TryConnect(settings.Updater.User, settings.Updater.Password);
        if (ipcResult is 0 or 85)
        {
            ConsoleUi.Info($"Authenticated to \\\\{settings.Updater.Host}\\IPC$.");
        }
        else
        {
            ConsoleUi.Info($"IPC$ connect: {new System.ComponentModel.Win32Exception(ipcResult).Message} ({ipcResult}).");
        }

        var shares = SmbDiagnostics.ListShares(settings.Updater.Host);
        if (shares.Count > 0)
        {
            ConsoleUi.Info("Shares visible to this account:");
            foreach (var name in shares)
            {
                ConsoleUi.Info("  " + name);
            }

            if (!shares.Contains(settings.Updater.Share, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Share '{settings.Updater.Share}' is not in that list. Put the exact name from the list (or from `net share` on the server) into Updater.Share in appsettings.json.");
            }
        }

        using var share = new WindowsShareSession(shareRoot);
        share.Connect(settings.Updater.User, settings.Updater.Password);

        if (!Directory.Exists(remoteRoot))
        {
            throw new DirectoryNotFoundException(
                $"Remote update folder does not exist (failing fast): {remoteRoot}");
        }

        ConsoleUi.Info($"Remote folder found: {remoteRoot}");

        var files = Directory.EnumerateFiles(settings.AppServerLocalPath, "*.*", SearchOption.AllDirectories)
            .Where(path => AllowedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            throw new InvalidOperationException(
                $"No .dll or .exe files found under {settings.AppServerLocalPath}.");
        }

        ConsoleUi.Info($"Copying {files.Count} .dll/.exe file(s), replacing on conflict. Subfolders will not be created.");
        Console.WriteLine();

        var copied = 0;
        long bytes = 0;
        foreach (var source in files)
        {
            var relative = Path.GetRelativePath(settings.AppServerLocalPath, source);
            var destination = Path.Combine(remoteRoot, relative);
            if (destination.Contains("\\UpdateClient\\"))
            {
                destination = destination.Replace("\\gaz21\\00000001", "");
            }
            var destinationDir = Path.GetDirectoryName(destination)
                ?? throw new InvalidOperationException($"Could not determine destination folder for {relative}.");

            if (!Directory.Exists(destinationDir))
            {
                throw new DirectoryNotFoundException(
                    $"Remote folder does not exist (failing fast): {destinationDir}{Environment.NewLine}" +
                    $"Needed for local file: {relative}");
            }

            ClearReadOnly(destination);
            File.Copy(source, destination, overwrite: true);
            copied++;
            bytes += new FileInfo(source).Length;
            Console.WriteLine($"  [{copied}/{files.Count}] {relative}");
        }

        Console.WriteLine();
        ConsoleUi.Success($"Uploaded {copied} file(s) ({FormatBytes(bytes)}) to the updater server.");
    }

    public static string GetShareRoot(PublishSettings settings)
    {
        return $@"\\{settings.Updater.Host}\{settings.Updater.Share}";
    }

    public static string GetRemoteUnc(PublishSettings settings)
    {
        var shareRoot = GetShareRoot(settings);
        var shareLocal = Path.GetFullPath(settings.Updater.ShareLocalPath);
        var remoteLocal = Path.GetFullPath(settings.UpdaterRemoteDirectory);
        var relative = Path.GetRelativePath(shareLocal, remoteLocal);
        if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException(
                $"Remote directory '{settings.UpdaterRemoteDirectory}' is not under share local path '{settings.Updater.ShareLocalPath}'.");
        }

        return Path.Combine(shareRoot, relative);
    }

    private static void ClearReadOnly(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if (attributes.HasFlag(FileAttributes.ReadOnly))
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }

    private static string FormatBytes(long bytes)
    {
        const double kb = 1024d;
        if (bytes < kb)
        {
            return $"{bytes} B";
        }

        if (bytes < kb * kb)
        {
            return $"{bytes / kb:0.0} KB";
        }

        return $"{bytes / (kb * kb):0.0} MB";
    }
}
