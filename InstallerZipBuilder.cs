using System.IO.Compression;

namespace AisPublishHelper;

internal static class InstallerZipBuilder
{
    public static string Create(PublishSettings settings)
    {
        if (!Directory.Exists(settings.InstallerResultPath))
        {
            throw new DirectoryNotFoundException($"Installer result folder not found: {settings.InstallerResultPath}");
        }

        foreach (var entry in settings.ZipEntries)
        {
            var source = Path.Combine(settings.InstallerResultPath, entry);
            if (!File.Exists(source) && !Directory.Exists(source))
            {
                throw new FileNotFoundException($"Zip source not found: {source}");
            }
        }

        var zipPath = Path.Combine(settings.InstallerResultPath, settings.ZipFileName);
        var tempPath = zipPath + ".tmp";

        if (File.Exists(tempPath))
        {
            File.Delete(tempPath);
        }

        ConsoleUi.Info($"Creating {settings.ZipFileName} from:");
        foreach (var entry in settings.ZipEntries)
        {
            ConsoleUi.Info("  " + entry);
        }

        using (var zipStream = File.Create(tempPath))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            foreach (var entry in settings.ZipEntries)
            {
                var source = Path.Combine(settings.InstallerResultPath, entry);
                if (Directory.Exists(source))
                {
                    AddDirectory(archive, source, entry);
                }
                else
                {
                    archive.CreateEntryFromFile(source, entry, CompressionLevel.SmallestSize);
                    Console.WriteLine($"  added {entry}");
                }
            }
        }

        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        File.Move(tempPath, zipPath);

        var size = new FileInfo(zipPath).Length;
        Console.WriteLine();
        ConsoleUi.Success($"Created {zipPath} ({size / (1024d * 1024d):0.0} MB).");
        return zipPath;
    }

    private static void AddDirectory(ZipArchive archive, string sourceDir, string entryPrefix)
    {
        var files = Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories);
        foreach (var file in files)
        {
            var relative = Path.GetRelativePath(sourceDir, file);
            var entryName = Path.Combine(entryPrefix, relative).Replace('\\', '/');
            archive.CreateEntryFromFile(file, entryName, CompressionLevel.SmallestSize);
            Console.WriteLine($"  added {entryName}");
        }
    }
}
