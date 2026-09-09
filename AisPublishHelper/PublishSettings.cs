using Microsoft.Extensions.Configuration;

namespace AisPublishHelper;

public sealed class PublishSettings
{
    public string? MsBuildPath { get; set; }
    public string MainSolution { get; set; } = "";
    public string InstallerSolution { get; set; } = "";
    public string BuildConfiguration { get; set; } = "Debug";
    public string BuildPlatform { get; set; } = "Any CPU";
    public bool RestorePackages { get; set; }
    public List<string> ExcludedProjects { get; set; } = [];
    public string AppServerLocalPath { get; set; } = "";
    public string UpdaterRemoteDirectory { get; set; } = "";
    public string InstallerResultPath { get; set; } = "";
    public string ZipFileName { get; set; } = "dosc.zip";
    public List<string> ZipEntries { get; set; } = [];
    public string SftpRemoteZipPath { get; set; } = "";
    public WindowsShareSettings Updater { get; set; } = new();
    public SshSettings PortalSsh { get; set; } = new();

    public static PublishSettings Load()
    {
        var basePath = AppContext.BaseDirectory;
        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);

        var settings = builder.Build().Get<PublishSettings>()
            ?? throw new InvalidOperationException("Failed to load appsettings.json.");

        settings.Validate();
        return settings;
    }

    public void Validate()
    {
        Require(MainSolution, nameof(MainSolution));
        Require(InstallerSolution, nameof(InstallerSolution));
        Require(AppServerLocalPath, nameof(AppServerLocalPath));
        Require(UpdaterRemoteDirectory, nameof(UpdaterRemoteDirectory));
        Require(InstallerResultPath, nameof(InstallerResultPath));
        Require(ZipFileName, nameof(ZipFileName));
        Require(SftpRemoteZipPath, nameof(SftpRemoteZipPath));
        Require(Updater.Host, "Updater.Host");
        Require(Updater.Share, "Updater.Share");
        Require(Updater.ShareLocalPath, "Updater.ShareLocalPath");
        Require(Updater.User, "Updater.User");
        Require(PortalSsh.Host, "PortalSsh.Host");
        Require(PortalSsh.User, "PortalSsh.User");

        if (ZipEntries.Count == 0)
        {
            throw new InvalidOperationException("ZipEntries must contain at least one file or folder.");
        }

        if (string.IsNullOrWhiteSpace(Updater.Password) || string.IsNullOrWhiteSpace(PortalSsh.Password))
        {
            throw new InvalidOperationException(
                "Credentials are missing. Copy appsettings.local.json.example to appsettings.local.json and fill in passwords.");
        }
    }

    private static void Require(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Setting '{name}' is required.");
        }
    }
}

public sealed class WindowsShareSettings
{
    public string Host { get; set; } = "";
    public string Share { get; set; } = "master$";
    public string ShareLocalPath { get; set; } = "";
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed class SshSettings
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 22;
    public string User { get; set; } = "";
    public string Password { get; set; } = "";
}
