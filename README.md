# AisPublishHelper

Console tool that publishes a new AIS AppServer version in one guided run. Each step prints what it will do, waits for Enter, then reports success or failure. Type `q` and Enter to abort. A failed step stops the rest.

## What it does

1. **Build AIS** — MSBuild `ais8_net45_pg.sln` with **Debug | Any CPU** (this product is published as Debug on purpose). The `TaxPayer` project is skipped (NuGet package downgrade). NuGet restore is not forced; packages are expected to already be restored from Visual Studio.
2. **Build installer** — MSBuild `Ais7Install\AisInstall.sln` the same way.
3. **Copy AppServer binaries** — copies only `.dll` and `.exe` from `abdm-web\Result\AppServer` (including subfolders) to the updater SMB share, replacing files on conflict. Missing remote folders fail immediately; new folders are not created.
4. **Pack and upload installer zip** — builds `dosc.zip` in `Ais7Install\Result\ru-ru` from `abdmServerInstallWix.msi`, `abdmClientInstallWix.msi`, `Docs_x0012`, and `Distrib_0x9956`, then replaces `/home/abdmsa/dosc.zip` on the portal over SFTP.

## Requirements

- Visual Studio 2022 (MSBuild) and .NET 8 SDK
- Writable SMB share on the updater (not RDP and not the `C$` admin share)
- SSH/SFTP access to the portal Linux host
- `appsettings.local.json` with passwords (this file is gitignored)

## Setup

```text
copy AisPublishHelper\appsettings.local.json.example AisPublishHelper\appsettings.local.json
```

Fill in passwords. Paths, hosts, share name, and build options live in `AisPublishHelper\appsettings.json`.

Current updater copy target:

- Share: `\\10.10.57.232\updates$`
- Share local root: `C:\inetpub\abdm_portal\master\updates`
- Destination folder: `...\updates\gaz21test\00000001`

If the share name changes, ask an admin for `net share` on the updater and put the **share name** (first column) into `Updater.Share`. NTFS write access inside an RDP session is not the same as an SMB share.

## Run

Open `AisPublishHelper.sln` in Visual Studio and run it, or:

```powershell
dotnet run --project AisPublishHelper\AisPublishHelper.csproj
```

## Notes

- Step 3 uses a Windows file share (`updates$`). RDP credentials do not imply access to `C$`.
- `ExcludedProjects` can list more solution projects to skip.
- Set `"RestorePackages": true` only if you need a full NuGet restore (some projects in AIS have broken or private-feed restore).
