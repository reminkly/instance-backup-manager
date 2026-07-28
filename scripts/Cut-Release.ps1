<#
.SYNOPSIS
Tests, publishes, packages, tags, and creates a draft GitHub release.

.PARAMETER Version
The release version without the leading "v", such as 0.1.0.
#>

param
(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+$')]
    [string] $Version
)

$ErrorActionPreference = 'Stop'

#region Helper Functions

<#
.SYNOPSIS
Runs an external command and stops the release when the command fails.
#>
function Invoke-CheckedCommand
{
    param
    (
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(Mandatory = $true)]
        [string[]] $ArgumentList
    )

    & $FilePath @ArgumentList

    if ($LASTEXITCODE -ne 0)
    {
        throw "Command '$FilePath' failed with exit code $LASTEXITCODE."
    }
}

#endregion

#region Release Paths

$repositoryPath = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryPath 'instance-backup-manager.slnx'
$projectPath = Join-Path $repositoryPath 'InstanceBackupManager.Console\InstanceBackupManager.Console.csproj'
$noticePath = Join-Path $repositoryPath 'InstanceBackupManager.Console\THIRD-PARTY-NOTICES.txt'
$licensePath = Join-Path $repositoryPath 'LICENSE'
$readmePath = Join-Path $repositoryPath 'README.md'

$tagName = "v$Version"
$releaseName = "InstanceBackupManager-$Version-win-x64"
$releaseRoot = Join-Path $repositoryPath "artifacts\$tagName"
$publishDirectory = Join-Path $releaseRoot 'publish'
$packageDirectory = Join-Path $releaseRoot $releaseName
$archivePath = Join-Path $releaseRoot "$releaseName.zip"

#endregion

#region Prerequisite Validation

Set-Location $repositoryPath

if (-not (Get-Command 'git' -ErrorAction SilentlyContinue))
{
    throw 'Git is not installed or is not available through PATH.'
}

if (-not (Get-Command 'gh' -ErrorAction SilentlyContinue))
{
    throw 'GitHub CLI is not installed or is not available through PATH.'
}

Invoke-CheckedCommand `
    -FilePath 'gh' `
    -ArgumentList @('auth', 'status')

$workingTreeChanges = git status --porcelain

if ($LASTEXITCODE -ne 0)
{
    throw 'The Git working-tree status could not be determined.'
}

if ($workingTreeChanges)
{
    throw 'The working tree contains uncommitted changes. Commit or discard them before cutting a release.'
}

$currentBranch = git branch --show-current

if ($LASTEXITCODE -ne 0)
{
    throw 'The current Git branch could not be determined.'
}

if ($currentBranch -ne 'main')
{
    throw "Releases must be cut from the main branch. Current branch: '$currentBranch'."
}

$projectText = Get-Content `
    -LiteralPath $projectPath `
    -Raw

$escapedVersion = [Regex]::Escape($Version)

if ($projectText -notmatch "<Version>$escapedVersion</Version>")
{
    throw "The project version does not match release version '$Version'. Update the project file before continuing."
}

$existingLocalTag = git tag --list $tagName

if ($LASTEXITCODE -ne 0)
{
    throw 'Local Git tags could not be inspected.'
}

if ($existingLocalTag)
{
    throw "Local tag '$tagName' already exists."
}

$existingRemoteTag = git ls-remote --tags origin "refs/tags/$tagName"

if ($LASTEXITCODE -ne 0)
{
    throw 'Remote Git tags could not be inspected.'
}

if ($existingRemoteTag)
{
    throw "Remote tag '$tagName' already exists."
}

if (Test-Path -LiteralPath $releaseRoot)
{
    throw "Release artifact directory already exists: '$releaseRoot'."
}

foreach ($requiredPath in @($solutionPath, $projectPath, $noticePath, $licensePath, $readmePath))
{
    if (-not (Test-Path -LiteralPath $requiredPath))
    {
        throw "Required release file was not found: '$requiredPath'."
    }
}

#endregion

#region Test and Publish

Write-Host
Write-Host "Testing release $tagName..."

Invoke-CheckedCommand `
    -FilePath 'dotnet' `
    -ArgumentList @(
        'test',
        $solutionPath,
        '--configuration',
        'Release'
    )

Write-Host
Write-Host "Publishing release $tagName..."

New-Item `
    -ItemType Directory `
    -Path $publishDirectory `
    -Force |
    Out-Null

Invoke-CheckedCommand `
    -FilePath 'dotnet' `
    -ArgumentList @(
        'publish',
        $projectPath,
        '--configuration',
        'Release',
        '--runtime',
        'win-x64',
        '--self-contained',
        'true',
        '-p:PublishSingleFile=true',
        '--output',
        $publishDirectory
    )

#endregion

#region Package

$publishedExecutablePath = Join-Path $publishDirectory 'InstanceBackupManager.exe'
$publishedNoticePath = Join-Path $publishDirectory 'THIRD-PARTY-NOTICES.txt'

if (-not (Test-Path -LiteralPath $publishedExecutablePath))
{
    throw "Published executable was not found: '$publishedExecutablePath'."
}

if (-not (Test-Path -LiteralPath $publishedNoticePath))
{
    throw "Published third-party notice was not found: '$publishedNoticePath'."
}

New-Item `
    -ItemType Directory `
    -Path $packageDirectory `
    -Force |
    Out-Null

Copy-Item `
    -LiteralPath $publishedExecutablePath `
    -Destination $packageDirectory

Copy-Item `
    -LiteralPath $publishedNoticePath `
    -Destination $packageDirectory

Copy-Item `
    -LiteralPath $licensePath `
    -Destination $packageDirectory

Copy-Item `
    -LiteralPath $readmePath `
    -Destination $packageDirectory

Compress-Archive `
    -Path (Join-Path $packageDirectory '*') `
    -DestinationPath $archivePath

Write-Host
Write-Host "Release archive created:"
Write-Host $archivePath

#endregion

#region Tag and Release

Write-Host
Write-Host "Creating tag $tagName..."

Invoke-CheckedCommand `
    -FilePath 'git' `
    -ArgumentList @(
        'tag',
        '-a',
        $tagName,
        '-m',
        "Instance Backup Manager $tagName"
    )

Invoke-CheckedCommand `
    -FilePath 'git' `
    -ArgumentList @(
        'push',
        'origin',
        $tagName
    )

Write-Host
Write-Host "Creating draft GitHub release..."

Invoke-CheckedCommand `
    -FilePath 'gh' `
    -ArgumentList @(
        'release',
        'create',
        $tagName,
        $archivePath,
        '--title',
        "Instance Backup Manager $tagName",
        '--generate-notes',
        '--verify-tag',
        '--draft'
    )

Write-Host
Write-Host "Draft release created successfully."

Invoke-CheckedCommand `
    -FilePath 'gh' `
    -ArgumentList @(
        'release',
        'view',
        $tagName,
        '--web'
    )

#endregion