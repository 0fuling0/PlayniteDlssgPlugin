<#
.SYNOPSIS
    Rolls the PlayniteDlssgPlugin version forward and records the matching
    package entry in the installer manifest.

.DESCRIPTION
    Replaces an earlier version of this script that had three defects:

      * it only matched three part versions, but the manifests store "1.2",
        so it threw "Version not found" straight away;
      * its unanchored 'Version:' pattern also matched inside
        'RequiredApiVersion: 6.0.0' and rewrote it to the plugin version;
      * its unanchored 'ReleaseDate:' pattern reset the release date of every
        historical package to today instead of only the new one.

    The manifest version keeps the component count used by
    manifest/extension.yaml, so "1.2" bumps to "1.3", while
    Properties/AssemblyInfo.cs always receives three components ("1.3.0").

    The script only inserts a new package entry; existing entries are left
    untouched.

.PARAMETER Bump
    Component to increment when -Version is not supplied. Defaults to minor,
    which is the convention for "the bundled runtime was refreshed".

.PARAMETER Version
    Explicit target version, for example 1.4. Overrides -Bump.

.PARAMETER Changelog
    Changelog lines for the new package entry.

.PARAMETER ChangelogFile
    File to read changelog lines from, one per line. Ignored when -Changelog
    is supplied.

.PARAMETER RequiredApiVersion
    RequiredApiVersion written into the new package entry.

.PARAMETER DryRun
    Report what would change without writing anything.

.EXAMPLE
    ./bump_version.ps1 -Bump minor -Changelog "Bundled dlssg_for_sm86 0.3.5"

.EXAMPLE
    ./bump_version.ps1 -Version 2.0 -DryRun
#>
[CmdletBinding()]
param(
    [ValidateSet("major", "minor", "patch")]
    [string]$Bump = "minor",

    [string]$Version,

    [string[]]$Changelog,

    [string]$ChangelogFile,

    [string]$RequiredApiVersion = "6.0.0",

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$extensionPath = Join-Path $repoRoot "manifest/extension.yaml"
$installerPath = Join-Path $repoRoot "manifest/installer.yaml"
$assemblyPath = Join-Path $repoRoot "Properties/AssemblyInfo.cs"

$newline = "`n"

function Read-TextFile([string]$Path) {
    return [System.IO.File]::ReadAllText($Path)
}

function Write-TextFile([string]$Path, [string]$Content) {
    # UTF-8 without BOM and LF endings, matching .gitattributes (*.yaml text eol=lf).
    # Set-Content -Encoding UTF8 would add a BOM on Windows PowerShell 5.1.
    $normalized = $Content -replace "`r`n", "`n"
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $normalized, $utf8NoBom)
}

function Get-VersionComponents([string]$Text) {
    # Returns a plain array: a List[int] would be unrolled by the PowerShell
    # pipeline on return, so later .Add() calls would fail.
    $values = @()
    foreach ($piece in $Text.Split(".")) {
        $values += [int]$piece
    }
    return $values
}

# ---------------------------------------------------------------------------
# Work out the new version
# ---------------------------------------------------------------------------

$extension = Read-TextFile $extensionPath

# Anchored to the start of a line so it cannot match inside RequiredApiVersion.
$versionPattern = "(?m)^Version:[ \t]*(\d+(?:\.\d+)*)[ \t\r]*$"
$versionMatch = [regex]::Match($extension, $versionPattern)
if (-not $versionMatch.Success) {
    throw "No 'Version:' line found in $extensionPath"
}

$currentVersion = $versionMatch.Groups[1].Value
$componentCount = $currentVersion.Split(".").Count

if ($Version) {
    if ($Version -notmatch "^\d+(\.\d+)*$") {
        throw "-Version must look like 1.3 or 1.3.0 (got '$Version')"
    }
    $next = Get-VersionComponents $Version
}
else {
    $current = Get-VersionComponents $currentVersion
    $major = $current[0]
    $minor = if ($current.Count -gt 1) { $current[1] } else { 0 }
    $patch = if ($current.Count -gt 2) { $current[2] } else { 0 }

    switch ($Bump) {
        "major" {
            $major = $major + 1
            $minor = 0
            $patch = 0
        }
        "minor" {
            $minor = $minor + 1
            $patch = 0
        }
        "patch" {
            $patch = $patch + 1
        }
    }

    $next = @($major, $minor, $patch)
}

while ($next.Count -lt 3) { $next += 0 }

# Manifest keeps the existing component count; AssemblyInfo always gets three.
$take = [Math]::Min($componentCount, $next.Count)
$manifestVersion = ($next[0..($take - 1)] -join ".")
$fullVersion = ($next[0..2] -join ".")

if ($manifestVersion -eq $currentVersion) {
    throw "Version would not change ($currentVersion). Use -Version to force a value."
}

Write-Host "Current version : $currentVersion"
Write-Host "New version     : $manifestVersion (assembly $fullVersion)"

# ---------------------------------------------------------------------------
# Changelog lines
# ---------------------------------------------------------------------------

$changelogLines = @()
if ($Changelog) {
    $changelogLines = $Changelog
}
elseif ($ChangelogFile) {
    if (-not (Test-Path $ChangelogFile)) {
        throw "Changelog file not found: $ChangelogFile"
    }
    $changelogLines = Get-Content $ChangelogFile |
        ForEach-Object { $_.Trim() } |
        Where-Object { $_ -ne "" }
}

if ($changelogLines.Count -eq 0) {
    $changelogLines = @("Maintenance release")
    Write-Warning "No changelog supplied, using a placeholder line."
}

# ---------------------------------------------------------------------------
# manifest/installer.yaml - prepend a package entry, leave the rest alone
# ---------------------------------------------------------------------------

$installer = Read-TextFile $installerPath

$addonIdMatch = [regex]::Match($installer, "AddonId:[ \t]*'?([^'\r\n]+)'?")
if (-not $addonIdMatch.Success) {
    throw "No AddonId found in $installerPath"
}
$addonId = $addonIdMatch.Groups[1].Value.Trim()

$repoUrlMatch = [regex]::Match($installer, "(?m)^\s*PackageUrl:\s*'(https://github\.com/[^/'\s]+/[^/'\s]+)/releases/download/")
if (-not $repoUrlMatch.Success) {
    throw "No GitHub releases PackageUrl found in $installerPath"
}
$repoUrl = $repoUrlMatch.Groups[1].Value

if ($installer -match "(?m)^\s*-\s*Version:[ \t]*$([regex]::Escape($manifestVersion))[ \t\r]*$") {
    throw "manifest/installer.yaml already contains a package for version $manifestVersion"
}

$pextName = "{0}_{1}.pext" -f $addonId, ($manifestVersion -replace "\.", "_")
$packageUrl = "$repoUrl/releases/download/v$manifestVersion/$pextName"
$releaseDate = Get-Date -Format "yyyy-MM-dd"

$block = New-Object System.Text.StringBuilder
[void]$block.Append("  - Version: $manifestVersion$newline")
[void]$block.Append("    RequiredApiVersion: $RequiredApiVersion$newline")
[void]$block.Append("    ReleaseDate: $releaseDate$newline")
[void]$block.Append("    PackageUrl: '$packageUrl'$newline")
[void]$block.Append("    Changelog:$newline")
foreach ($line in $changelogLines) {
    [void]$block.Append("      - $line$newline")
}

$packagesMatch = [regex]::Match($installer, "(?m)^Packages:[ \t\r]*$")
if (-not $packagesMatch.Success) {
    throw "No 'Packages:' line found in $installerPath"
}

$insertAt = $packagesMatch.Index + $packagesMatch.Length
if ($insertAt -lt $installer.Length -and $installer[$insertAt] -eq "`r") { $insertAt++ }
if ($insertAt -lt $installer.Length -and $installer[$insertAt] -eq "`n") { $insertAt++ }

$newInstaller = $installer.Insert($insertAt, $block.ToString())

# ---------------------------------------------------------------------------
# manifest/extension.yaml
# ---------------------------------------------------------------------------

$versionGroup = $versionMatch.Groups[1]
$newExtension = $extension.Remove($versionGroup.Index, $versionGroup.Length).
    Insert($versionGroup.Index, $manifestVersion)

# ---------------------------------------------------------------------------
# Properties/AssemblyInfo.cs
# ---------------------------------------------------------------------------

$assembly = Read-TextFile $assemblyPath
$newAssembly = [regex]::Replace(
    $assembly,
    'AssemblyVersion\("\d+(?:\.\d+)*"\)',
    "AssemblyVersion(`"$fullVersion`")")
$newAssembly = [regex]::Replace(
    $newAssembly,
    'AssemblyFileVersion\("\d+(?:\.\d+)*"\)',
    "AssemblyFileVersion(`"$fullVersion`")")

# ---------------------------------------------------------------------------
# Write
# ---------------------------------------------------------------------------

if ($DryRun) {
    Write-Host ""
    Write-Host "--- manifest/installer.yaml (new entry) ---"
    Write-Host $block.ToString().TrimEnd()
    Write-Host ""
    Write-Host "Dry run: nothing written."
}
else {
    Write-TextFile $extensionPath $newExtension
    Write-TextFile $installerPath $newInstaller
    Write-TextFile $assemblyPath $newAssembly

    Write-Host "Updated manifest/extension.yaml, manifest/installer.yaml, Properties/AssemblyInfo.cs"
    Write-Host ""
    Write-Host "Next steps:"
    Write-Host "  git add . && git commit -m 'Bump version to $manifestVersion'"
    Write-Host "  git tag v$manifestVersion && git push origin HEAD --tags"
}

# Surface the result to GitHub Actions when running inside a workflow.
if ($env:GITHUB_OUTPUT) {
    "version=$manifestVersion" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
    "tag=v$manifestVersion" | Out-File -FilePath $env:GITHUB_OUTPUT -Append -Encoding utf8
}
