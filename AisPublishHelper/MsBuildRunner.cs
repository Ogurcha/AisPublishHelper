using System.Diagnostics;
using System.Text;

namespace AisPublishHelper;

internal static class MsBuildRunner
{
    public static bool Build(string solutionPath, PublishSettings settings, string? msBuildPathOverride)
    {
        if (!File.Exists(solutionPath))
        {
            ConsoleUi.Fail($"Solution not found: {solutionPath}");
            return false;
        }

        string msbuild;
        try
        {
            msbuild = ResolveMsBuild(msBuildPathOverride);
        }
        catch (Exception ex)
        {
            ConsoleUi.Fail(ex.Message);
            return false;
        }

        ConsoleUi.Info($"MSBuild: {msbuild}");
        ConsoleUi.Info(settings.RestorePackages
            ? "Starting restore + build..."
            : "Starting build (NuGet restore skipped; same as a Visual Studio Build when packages are already restored)...");

        string? filterPath = null;
        try
        {
            filterPath = SolutionFilterBuilder.CreateIfNeeded(solutionPath, settings.ExcludedProjects);
        }
        catch (Exception ex)
        {
            ConsoleUi.Fail("Failed to create solution filter: " + ex.Message);
            return false;
        }

        var buildPath = filterPath ?? solutionPath;
        Console.WriteLine();

        try
        {
            return RunMsBuild(msbuild, buildPath, solutionPath, settings);
        }
        finally
        {
            if (filterPath is not null)
            {
                try
                {
                    File.Delete(filterPath);
                }
                catch
                {
                    // Ignore cleanup failures for the temporary solution filter.
                }
            }
        }
    }

    private static bool RunMsBuild(string msbuild, string buildPath, string solutionPath, PublishSettings settings)
    {
        // Do not force /restore by default. A full solution restore is not what Visual Studio
        // "Build" does, and it was failing on Ais8WebPortalCore's NuGet.Config.
        var restoreArgs = settings.RestorePackages
            ? "/restore"
            : "/p:RestoreDuringBuild=false /p:Restore=false";

        var arguments =
            $"\"{buildPath}\" /t:Build {restoreArgs} " +
            $"/p:Configuration=\"{settings.BuildConfiguration}\" /p:Platform=\"{settings.BuildPlatform}\" " +
            "/m /v:minimal /nologo /nr:false";

        var psi = new ProcessStartInfo
        {
            FileName = msbuild,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(solutionPath) ?? Environment.CurrentDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = new Process { StartInfo = psi };
        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Console.WriteLine(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                Console.Error.WriteLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        process.WaitForExit();

        Console.WriteLine();
        if (process.ExitCode == 0)
        {
            ConsoleUi.Success($"Build succeeded: {Path.GetFileName(solutionPath)}");
            return true;
        }

        ConsoleUi.Fail($"Build failed: {Path.GetFileName(solutionPath)} (exit code {process.ExitCode}).");
        return false;
    }

    private static string ResolveMsBuild(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            if (!File.Exists(overridePath))
            {
                throw new FileNotFoundException($"MSBuildPath from settings was not found: {overridePath}");
            }

            return overridePath;
        }

        var vswhere = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            @"Microsoft Visual Studio\Installer\vswhere.exe");

        if (File.Exists(vswhere))
        {
            var psi = new ProcessStartInfo
            {
                FileName = vswhere,
                Arguments = "-latest -requires Microsoft.Component.MSBuild -find MSBuild\\**\\Bin\\MSBuild.exe",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            var output = process?.StandardOutput.ReadToEnd().Trim();
            process?.WaitForExit();
            var first = output?.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(first) && File.Exists(first))
            {
                return first;
            }
        }

        var fallbacks = new[]
        {
            @"C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe",
            @"C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"
        };

        var found = fallbacks.FirstOrDefault(File.Exists);
        if (found is null)
        {
            throw new FileNotFoundException("MSBuild.exe was not found. Install Visual Studio or set MsBuildPath in appsettings.json.");
        }

        return found;
    }
}
