using Renci.SshNet;

namespace AisPublishHelper;

internal static class SftpUploader
{
    public static void UploadZip(PublishSettings settings, string localZipPath)
    {
        if (!File.Exists(localZipPath))
        {
            throw new FileNotFoundException($"Zip file not found: {localZipPath}");
        }

        var ssh = settings.PortalSsh;
        ConsoleUi.Info($"Connecting over SSH/SFTP to {ssh.Host}:{ssh.Port} as {ssh.User}...");

        using var client = new SftpClient(ssh.Host, ssh.Port, ssh.User, ssh.Password);
        client.HostKeyReceived += (_, e) =>
        {
            e.CanTrust = true;
        };

        client.Connect();

        try
        {
            var remoteDir = PosixDirectory(settings.SftpRemoteZipPath);
            if (!client.Exists(remoteDir))
            {
                throw new DirectoryNotFoundException(
                    $"Remote portal folder does not exist (failing fast): {remoteDir}");
            }

            var fileName = Path.GetFileName(localZipPath);
            var length = new FileInfo(localZipPath).Length;
            ConsoleUi.Info($"Uploading {fileName} ({length / (1024d * 1024d):0.0} MB) to {settings.SftpRemoteZipPath} (replace on conflict)...");

            using var stream = File.OpenRead(localZipPath);
            var lastPercent = -1;
            client.UploadFile(stream, settings.SftpRemoteZipPath, canOverride: true, uploaded =>
            {
                var percent = length == 0 ? 100 : (int)(uploaded * 100 / (ulong)length);
                if (percent != lastPercent && percent % 5 == 0)
                {
                    lastPercent = percent;
                    Console.Write($"\r  upload {percent,3}%");
                }
            });

            Console.WriteLine();
            ConsoleUi.Success($"Replaced remote file {settings.SftpRemoteZipPath}.");
        }
        finally
        {
            if (client.IsConnected)
            {
                client.Disconnect();
            }
        }
    }

    private static string PosixDirectory(string remoteFilePath)
    {
        var slash = remoteFilePath.LastIndexOf('/');
        if (slash <= 0)
        {
            return "/";
        }

        return remoteFilePath[..slash];
    }
}
