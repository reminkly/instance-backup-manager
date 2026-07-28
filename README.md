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
- Timestamped backups with optional user-facing names and manifests
- Restore using current configured destination paths
- Optional pre-restore safety backups
- Delete one or all completed backups
- Independent retention limits for manual and pre-restore backups
- Keyboard-driven menus with highlighted selection and shortcut keys
- Non-mutating instance, target, and backup validation
- Daily command activity logs
- Protection against unsafe paths, overlapping targets, symbolic links, and junctions
- Configuration and manifest schema validation

## Portable Folder Structure

Place the published executable wherever you want the application and its backups to reside.

```text
InstanceBackupManager.exe
THIRD-PARTY-NOTICES.txt
Logs/
└── instance-backup-manager-2026-07-28.log
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
6. Return to instance selection and select the configured instance.

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

Creates a timestamped backup containing every enabled target. The application prompts for an optional user-facing backup name. Leaving the name blank creates a timestamped name automatically.

Backup names are presentation metadata and do not change the timestamped directory used to store the backup. Names are trimmed, limited to 100 characters, and cannot contain line breaks or other control characters.

Required targets must exist. Missing optional targets are skipped and omitted from the manifest. After a successful manual backup, the configured manual retention limit is applied.

### Restore from backup

Displays validated completed backups from newest to oldest. Each menu entry includes its user-facing name, creation time, backup kind, file count, and byte count. Manifests created before backup names were introduced receive a generated timestamped label.

Restore uses the current `Source` path from `instance.json`. The historical source stored in the manifest is informational and does not override the current configuration.

Matching files are overwritten. Files currently present at the destination but absent from the selected backup remain unchanged.

Before restoration, the application offers to create a pre-restore safety backup. Pressing Enter accepts this safety backup by default. Its generated name identifies the selected backup, such as `Before restoring "Before Palace of Winds"`. Pre-restore retention is applied only after restoration succeeds.

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

### Validate instance

Performs a non-mutating inspection of the loaded instance and reports successful checks, warnings, and errors.

Validation includes:

- Configuration schema and path rules
- Instance enabled state
- Enabled and disabled targets
- Required and optional source availability
- Resolved file and directory types
- Reparse-point safety
- Completed backup discovery and manifest validation
- In-progress backup detection

Validation does not create, overwrite, restore, or delete files.

## Keyboard Navigation

Interactive menus support:

- Up/Down to move the highlighted selection and wrap at either end
- Home/End to jump to the first or last item
- Enter to choose the highlighted item
- Escape to return to the previous menu or cancel a choice
- Displayed number or letter keys as immediate shortcuts

The selected row uses colors derived from the current terminal theme. When console input is redirected for tests or automation, menus retain line-based shortcut input.

Exact-name and `DELETE ALL` prompts remain typed confirmations because they protect destructive operations.

## Terminal Appearance

Instance Backup Manager runs inside the user's selected terminal host. Windows Terminal, the VS Code integrated terminal, PowerShell hosts, and the legacy Windows console each control their own font, font size, color scheme, and related appearance settings. The application cannot reliably select or privately load a bundled font.

For the best experience, configure a readable monospaced font in the terminal profile used to launch the application. A Nerd Font is optional; the application does not require special icon glyphs.

To change the font in Windows Terminal:

1. Open Windows Terminal settings.
2. Select the profile used to run Instance Backup Manager.
3. Open **Appearance**.
4. Select the desired font face and save the profile.

A font file should not be redistributed with the application unless its license explicitly permits redistribution. Users who install a separately obtained font must still select it in their terminal settings.

## Application Logs

Command activity is written to daily text files under the portable application directory:

```text
Logs/
└── instance-backup-manager-YYYY-MM-DD.log
```

Entries include UTC timestamps, severity, command name, instance name, and completion or failure state. Logging is best-effort: inability to create or append a log never prevents the requested operation.

## Backup Manifests

Each completed backup contains a `manifest.json` describing:

- Manifest schema version
- Instance name at creation time
- Optional user-facing backup name
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
- **Decorator:** Logging wraps instance commands without adding logging responsibilities to each command or workflow.
- **Facade:** `ConfigProcessor` provides one entry point for instance discovery, configuration serialization, validation, and runtime-context creation.
- **Repository:** `BackupCatalog` owns discovery and validated loading of completed backups.
- **Strategy:** File and directory targets use separate backup, restore, and clear algorithms selected by target type.
- **Policy utilities:** `FileSystemSafety` centralizes path comparison, containment, overlap, and reparse-point rules. `BackupDisplayNamePolicy` centralizes backup-name generation, normalization, validation, and backward-compatible display.
- **Report model:** Instance validation returns structured findings that the console workflow formats for display.
- **Reusable selector:** Console menus share keyboard navigation and redirected-input behavior without changing command execution.
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

Tests cover configuration processing, backup discovery and maintenance, retention, backup display-name policies, backup and restore behavior, clear safety, target strategies, filesystem safety, validation, command logging, console menus, and command dispatch.

## Development Workflow

The repository uses `develop` for active development and `main` for tested, release-ready code.

With separate local clones:

```text
instance-backup-manager/
├── develop/
└── main/
```

Commit and push active work from `develop`:

```powershell
git add -A
git commit -m "Describe the change"
git push origin develop
```

When changes are ready, open a pull request from `develop` into `main`. After it is merged, update the local main clone:

```powershell
git pull --ff-only origin main
```

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

1. Commit and push the tested changes on `develop`.
2. Merge `develop` into `main`, preferably through a pull request.
3. Pull the updated `main` branch locally.
4. Update `Version`, `FileVersion`, and `InformationalVersion` in `InstanceBackupManager.Console.csproj`.
5. Commit and push the version change on `main`.
6. Confirm the working tree is clean and the current branch is `main`.
7. Confirm GitHub CLI authentication with `gh auth status`.
8. Run the VS Code task **Cut GitHub Release** and enter the version without a leading `v`.

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
