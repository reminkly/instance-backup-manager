# Instance Backup Manager

Instance Backup Manager is a portable console application for backing up, restoring, clearing, and managing files associated with emulators, ROM hacks, games, documents, and other configurable applications.

Each managed item is represented by an instance directory containing an `instance.json` configuration file and a `backups` directory.

## Features

- Portable, self-contained Windows application
- Multiple independently configured instances
- Individual file and complete-directory targets
- Multiple targets per instance
- Optional and required source paths
- Per-target enable and clear settings
- Timestamped backups with manifests
- Restore using current configured destination paths
- Optional pre-restore safety backups
- Delete one or all completed backups
- Independent retention limits for manual and pre-restore backups
- Protection against unsafe paths, overlapping targets, symbolic links, and junctions
- Configuration and manifest schema validation

## Portable Folder Structure

Place the published executable wherever you want the application and its backups to reside.

```text
InstanceBackupManager.exe
THIRD-PARTY-NOTICES.txt
Instances/
├── BizHawk - Minish Cap/
│   ├── instance.json
│   └── backups/
│       ├── 2026-07-28_13-21-39/
│       │   ├── manifest.json
│       │   └── saves/
│       └── 2026-07-28_13-25-38/
│           ├── manifest.json
│           └── saves/
└── Documents/
    ├── instance.json
    └── backups/
```

The `Instances` directory is created beside the executable. Each immediate subdirectory represents one independently configurable instance.

## Creating an Instance

1. Create a subdirectory inside the application’s `Instances` directory.
2. Start Instance Backup Manager.
3. Select the unconfigured instance.
4. The application creates a skeleton `instance.json`.
5. Update the configuration file.
6. Restart the application and select the configured instance.

A complete example is available at [`examples/instance.example.json`](examples/instance.example.json).

## Example Configuration

```json
{
  "SchemaVersion": 1,
  "Name": "Example Emulator",
  "Enabled": true,
  "Retention": {
    "ManualBackupsToKeep": 10,
    "PreRestoreBackupsToKeep": 5
  },
  "Targets": [
    {
      "Id": "save-ram",
      "Name": "Save RAM",
      "Enabled": true,
      "Required": true,
      "AllowClear": true,
      "Source": "C:\\Path\\To\\Game.SaveRAM",
      "Type": "file",
      "BackupPath": "saves/Game.SaveRAM"
    },
    {
      "Id": "mods",
      "Name": "Mods",
      "Enabled": true,
      "Required": false,
      "AllowClear": false,
      "Source": "C:\\Path\\To\\Mods",
      "Type": "directory",
      "BackupPath": "mods"
    }
  ]
}
```

Configuration property names are case-insensitive. JSON comments and trailing commas are accepted.

## Instance Configuration

### Instance properties

| Property | Type | Description |
|---|---|---|
| `SchemaVersion` | Integer | Configuration schema version. The current supported version is `1`. |
| `Name` | String | User-facing instance name shown by the application. |
| `Enabled` | Boolean | Determines whether the instance can participate in operations. |
| `Retention` | Object or `null` | Optional per-kind retention settings. Missing or `null` means unlimited retention. |
| `Targets` | Array | Files and directories managed by the instance. |

### Retention properties

| Property | Type | Description |
|---|---|---|
| `ManualBackupsToKeep` | Integer or `null` | Maximum manual backups retained. `null` means unlimited. |
| `PreRestoreBackupsToKeep` | Integer or `null` | Maximum pre-restore backups retained. `null` means unlimited. |

Configured limits must be at least `1`. Manual and pre-restore limits are applied independently.

Manual retention runs after a successful manual backup. Pre-restore retention runs only after the selected backup has been restored successfully.

### Target properties

| Property | Type | Description |
|---|---|---|
| `Id` | String | Stable, unique, machine-readable identifier. IDs are case-insensitive. |
| `Name` | String | User-facing target name. |
| `Enabled` | Boolean | Determines whether the target participates in operations. |
| `Required` | Boolean | When `true`, backup creation fails if the source does not exist. When `false`, a missing source is skipped. |
| `AllowClear` | Boolean | Explicitly permits the target to participate in Clear operations. |
| `Source` | String | Source file or directory. Environment variables are expanded. Relative paths are resolved from the instance directory. |
| `Type` | String | Either `file` or `directory`. |
| `BackupPath` | String | Relative location used to store the target inside each backup. |

Target IDs and backup paths must be unique and non-overlapping within an instance.

## Operations

### Back up now

Creates a timestamped backup containing every enabled target.

Required targets must exist. Missing optional targets are skipped and omitted from the manifest. After a successful manual backup, the configured manual retention limit is applied.

### Restore from backup

Displays validated completed backups from newest to oldest. Each backup is labeled as either `Manual` or `Pre-restore`.

Restore uses the current `Source` path from `instance.json`. The historical source stored in the manifest is informational and does not override the current configuration.

Matching files are overwritten. Files currently present at the destination but absent from the selected backup remain unchanged.

Before restoration, the application offers to create a pre-restore safety backup. Pressing Enter accepts this safety backup by default. Pre-restore retention is applied only after restoration succeeds.

### Clear instance data

Clear is shown only when at least one target has both:

```json
"Enabled": true,
"AllowClear": true
```

For file targets, the configured file is deleted. For directory targets, all contents are deleted while the configured root directory is preserved.

The application displays every affected path and requires the exact instance name before continuing. Clear does not automatically create a backup.

### Manage backups

Backup management supports:

- Delete one completed backup
- Delete all validated completed backups

Deleting one backup requires confirmation. Deleting all backups requires both the exact instance name and the phrase `DELETE ALL`.

In-progress and unrelated directories are not deleted.

## Backup Manifests

Each completed backup contains a `manifest.json` describing:

- Manifest schema version
- Instance name at creation time
- Backup directory name
- Backup kind
- UTC creation time
- Included targets
- Historical source paths
- Stored payload paths
- Target types
- File counts
- Byte counts

A manifest describes what was captured. Restoration always uses the current configured source paths.

## Safety Protections

Instance Backup Manager rejects or protects against:

- Filesystem roots used as Clear targets
- Instance directories used as Clear targets
- Source paths overlapping the backups directory
- Backup paths escaping through parent traversal
- Duplicate target IDs
- Overlapping target backup paths
- Overlapping Clear targets
- Unsupported target or backup kinds
- Symbolic links, junctions, and other reparse points
- Malformed or unsupported manifests
- Partial bulk deletion when validation fails

All destructive deletion plans are validated before any selected directory is removed. Backups are still recommended before using Restore or Clear against important data.

## Architecture

The solution is divided into three projects:

```text
InstanceBackupManager.Console
InstanceBackupManager.Processing
InstanceBackupManager.Tests
```

The implementation uses several focused design patterns:

- **Command:** Instance-menu actions implement a common command contract, allowing the menu to display and dispatch available operations without depending directly on every workflow.
- **Facade:** `ConfigProcessor` provides one entry point for instance discovery, configuration serialization, validation, and runtime-context creation.
- **Repository:** `BackupCatalog` owns discovery and validated loading of completed backups.
- **Strategy:** File and directory targets use separate backup, restore, and clear algorithms selected by target type.
- **Policy utility:** `FileSystemSafety` centralizes path comparison, containment, overlap, and reparse-point rules.
- **Composition root:** `Program` creates and connects the application’s processors, workflows, commands, and menus.

The processing project contains the filesystem and configuration behavior. The console project is responsible for user interaction and workflow coordination.

## Building

Requirements:

- .NET 10 SDK

Build the complete solution:

```powershell
dotnet build .\instance-backup-manager.slnx
```

## Testing

Run the complete test suite:

```powershell
dotnet test .\instance-backup-manager.slnx
```

Tests cover configuration processing, backup discovery and maintenance, retention, backup and restore behavior, clear safety, target strategies, filesystem safety, and console command dispatch.

## Publishing

Publish a self-contained, single-file Windows x64 application:

```powershell
dotnet publish .\InstanceBackupManager.Console\InstanceBackupManager.Console.csproj `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    --output .\publish
```

The VS Code task **Publish Portable Windows App** performs the same operation.

The published directory should contain:

```text
InstanceBackupManager.exe
THIRD-PARTY-NOTICES.txt
```

Create the `Instances` directory beside the executable, or start the application and allow it to create the directory.

## Cutting a Release

Before cutting a release:

1. Update `Version`, `FileVersion`, and `InformationalVersion` in `InstanceBackupManager.Console.csproj`.
2. Commit and push all changes.
3. Confirm the working tree is clean and the current branch is `main`.
4. Confirm GitHub CLI authentication with `gh auth status`.
5. Run the VS Code task **Cut GitHub Release** and enter the version without a leading `v`.

The release script:

- Runs the tests in Release configuration
- Publishes the self-contained Windows x64 application
- Packages the executable, notices, license, and README
- Creates and pushes an annotated Git tag
- Creates a draft GitHub release with generated release notes
- Opens the draft release for final review

## License

Instance Backup Manager is licensed under the [MIT License](LICENSE).

The application icon is derived from Tabler Icons and is covered separately by [`InstanceBackupManager.Console/THIRD-PARTY-NOTICES.txt`](InstanceBackupManager.Console/THIRD-PARTY-NOTICES.txt).
