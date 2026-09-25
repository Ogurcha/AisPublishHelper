using System.Text;

namespace AisPublishHelper;

internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.Title = "AIS Publish Helper";
        ConsoleUi.Header();

        PublishSettings settings;
        try
        {
            settings = PublishSettings.Load();
        }
        catch (Exception ex)
        {
            ConsoleUi.Fail(ex.Message);
            WaitBeforeExit();
            return 1;
        }

        try
        {
            if (!RunWorkflow(settings))
            {
                WaitBeforeExit();
                return 1;
            }
        }
        catch (Exception ex)
        {
            ConsoleUi.Fail(ex.Message);
            WaitBeforeExit();
            return 1;
        }

        ConsoleUi.Separator();
        ConsoleUi.Success("All steps completed.");
        WaitBeforeExit();
        return 0;
    }

    private static bool RunWorkflow(PublishSettings settings)
    {
        var confirmStep1 = ConsoleUi.ConfirmStep(
                "Step 1 of 4: Build the main AIS solution",
                "Going to build:",
                settings.MainSolution,
                $"Configuration: {settings.BuildConfiguration}",
                $"Platform: {settings.BuildPlatform}",
                settings.ExcludedProjects.Count == 0
                    ? "Excluded projects: none"
                    : "Excluded projects: " + string.Join(", ", settings.ExcludedProjects));
        if (confirmStep1 == ConfirmStatusEnum.Abort)
        {
            return false;
        }

        if (confirmStep1 == ConfirmStatusEnum.Go)
        {
            if (!MsBuildRunner.Build(settings.MainSolution, settings, settings.MsBuildPath))
            {
                return false;
            }
        }

        var confirmStep2 = ConsoleUi.ConfirmStep(
                "Step 2 of 4: Build the installer solution",
                "Going to build:",
                settings.InstallerSolution,
                $"Configuration: {settings.BuildConfiguration}",
                $"Platform: {settings.BuildPlatform}");
        if (confirmStep2 == ConfirmStatusEnum.Abort)
        {
            return false;
        }

        if (confirmStep2 == ConfirmStatusEnum.Go)
        {
            if (!MsBuildRunner.Build(settings.InstallerSolution, settings, settings.MsBuildPath))
            {
                return false;
            }
        }

        var confirmStep3 = ConsoleUi.ConfirmStep(
                "Step 3 of 4: Upload AppServer .dll and .exe files to the updater server",
                "Local source:",
                settings.AppServerLocalPath,
                "Remote destination (Windows file share):",
                AppServerUploader.GetRemoteUnc(settings),
                "Only .dll and .exe files are copied. Existing files are replaced. Missing remote folders fail fast.");
        if (confirmStep3 == ConfirmStatusEnum.Abort)
        {
            return false;
        }

        if (confirmStep3 == ConfirmStatusEnum.Go)
        {
            AppServerUploader.Upload(settings);
        }

        var confirmStep4 = ConsoleUi.ConfirmStep(
                "Step 4 of 4: Pack installer files and upload dosc.zip to the web portal",
                "Local source:",
                settings.InstallerResultPath,
                "Zip name: " + settings.ZipFileName,
                "Contents: " + string.Join(", ", settings.ZipEntries),
                $"SSH upload to {settings.PortalSsh.Host}:{settings.PortalSsh.Port} as {settings.PortalSsh.User}",
                "Remote file: " + settings.SftpRemoteZipPath);

        if (confirmStep4 == ConfirmStatusEnum.Abort)
        {
            return false;
        }

        if (confirmStep4 == ConfirmStatusEnum.Go)
        {
            var zipPath = InstallerZipBuilder.Create(settings);
            SftpUploader.UploadZip(settings, zipPath);
        }
        
        return true;
    }

    private static void WaitBeforeExit()
    {
        Console.Write("Press Enter to exit...");
        Console.ReadLine();
    }
}
