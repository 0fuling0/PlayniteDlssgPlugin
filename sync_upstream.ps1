<#
.SYNOPSIS
    Pulls the latest dlssg_for_sm86 release into the submodule, rolls the plugin
    version forward and records a changelog entry.

.DESCRIPTION
    Run this by hand to catch up with upstream, or let
    .github/workflows/sync-upstream.yml run it on a schedule.

    The script is a no-op when the pinned submodule commit already matches the
    newest upstream release, so it is safe to call repeatedly.

    The newest version is read from the remote's tags with "git ls-remote"
    rather than the GitHub REST API: the API allows only 60 unauthenticated
    requests per hour per IP, which is easy to exhaust, while ls-remote reuses
    the credentials and proxy settings a normal fetch already needs. The API is
    only consulted for the release notes, and the script falls back to the
    submodule's own commit subjects when that call fails.

.PARAMETER UpstreamRepo
    Upstream repository in owner/name form, used for release notes only.

.PARAMETER Bump
    Which version component to roll when the bundled runtime changes. Defaults
    to minor, the convention used so far (1.1 -> 1.2 was a runtime refresh).

.PARAMETER IncludePrerelease
    Also consider tags carrying a suffix, such as 0.4.0-rc1.

.PARAMETER ExtraChangelog
    Additional changelog lines to record alongside the upstream summary.

.PARAMETER NoPush
    Commit and tag locally but do not push.

.PARAMETER DryRun
    Report what would happen without touching the submodule, manifests or git.

.EXAMPLE
    ./sync_upstream.ps1 -DryRun

.EXAMPLE
    ./sync_upstream.ps1 -ExtraChangelog "Added a runtime build selector"
#>
[CmdletBinding()]
param(
    [string]$UpstreamRepo = "sdli1995/dlssg_for_sm86",

    [string]$SubmodulePath = "dlssg_for_sm86",

    [ValidateSet("major", "minor", "patch")]
    [string]$Bump = "minor",

    [switch]$IncludePrerelease,

    [string[]]$ExtraChangelog,

    [switch]$NoPush,

    [switch]$DryRun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Windows PowerShell 5.1 still defaults to TLS 1.0 for Invoke-RestMethod.
if ($PSVersionTable.PSVersion.Major -lt 6) {
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
}

$repoRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$submoduleFullPath = Join-Path $repoRoot $SubmodulePath

function Invoke-Git {
    param(
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$WorkingDirectory = $repoRoot,
        [switch]$AllowFailure
    )

    # git writes progress and notices ("Previous HEAD position was ...") to stderr.
    # Merging that into the output stream while ErrorActionPreference is Stop turns
    # a perfectly successful command into a terminating error, so relax it here.
    $previousPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = & git -C $WorkingDirectory @Arguments 2>&1
        $code = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousPreference
    }

    if ($code -ne 0 -and -not $AllowFailure) {
        throw "git $($Arguments -join ' ') failed in $WorkingDirectory`n$($output -join "`n")"
    }
    return [pscustomobject]@{ ExitCode = $code; Output = @($output) }
}

function Compare-VersionText([string]$Left, [string]$Right) {
    # Numeric comparison, so 0.3.10 sorts above 0.3.9.
    $leftParts = @($Left.Split(".") | ForEach-Object { [int]$_ })
    $rightParts = @($Right.Split(".") | ForEach-Object { [int]$_ })
    $count = [Math]::Max($leftParts.Count, $rightParts.Count)
    for ($index = 0; $index -lt $count; $index++) {
        $leftValue = if ($index -lt $leftParts.Count) { $leftParts[$index] } else { 0 }
        $rightValue = if ($index -lt $rightParts.Count) { $rightParts[$index] } else { 0 }
        if ($leftValue -lt $rightValue) { return -1 }
        if ($leftValue -gt $rightValue) { return 1 }
    }
    return 0
}

function Set-ActionOutput([string]$Name, [string]$Value) {
    # Written as UTF-8 without BOM: Windows PowerShell 5.1's
    # "Out-File -Encoding utf8" adds a BOM when it creates the file, which the
    # GitHub Actions output parser would then read as part of the first name.
    if (-not $env:GITHUB_OUTPUT) { return }
    $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::AppendAllText($env:GITHUB_OUTPUT, "$Name=$Value`n", $utf8NoBom)
}

function ConvertTo-YamlScalar([string]$Text) {
    # Keep the plain style used by the existing entries, but quote anything that
    # YAML could misread as structure (upstream notes contain ":" and "#").
    $leading = '^[\s\-?#\[\]{}&*!|>%@''"]'
    $needsQuoting = $Text -match $leading -or
        $Text -match ":\s" -or
        $Text -match "#" -or
        $Text -match "\s$"

    if (-not $needsQuoting) { return $Text }
    return "'" + ($Text -replace "'", "''") + "'"
}

function Get-UpstreamSummary([string]$Tag, [string]$PreviousTag) {
    # Prefer the GitHub release body, then fall back to the submodule's own
    # commit subjects so the script still works offline or when rate limited.
    $headers = @{
        "User-Agent" = "PlayniteDlssgPlugin-sync"
        "Accept"     = "application/vnd.github+json"
    }
    if ($env:GITHUB_TOKEN) {
        $headers["Authorization"] = "Bearer $env:GITHUB_TOKEN"
    }

    try {
        $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$UpstreamRepo/releases/tags/$Tag" -Headers $headers -UseBasicParsing
        foreach ($rawLine in (([string]$release.body) -split "`r?`n")) {
            $line = ($rawLine -replace "\s+", " ").Trim()
            if ($line -ne "") { return $line }
        }
    }
    catch {
        Write-Warning "GitHub release notes unavailable ($($_.Exception.Message)); using submodule commit messages instead."
    }

    $log = Invoke-Git -Arguments @("log", "--format=%s", "$PreviousTag..$Tag") -WorkingDirectory $submoduleFullPath -AllowFailure
    if ($log.ExitCode -eq 0) {
        foreach ($entry in $log.Output) {
            $line = ([string]$entry -replace "\s+", " ").Trim()
            if ($line -ne "") { return $line }
        }
    }

    return $null
}

# ---------------------------------------------------------------------------
# Read the currently pinned upstream version
# ---------------------------------------------------------------------------

if (-not (Test-Path (Join-Path $submoduleFullPath ".git"))) {
    throw "Submodule not initialised at $submoduleFullPath. Run: git submodule update --init --recursive"
}

$describe = Invoke-Git -Arguments @("describe", "--tags", "--abbrev=0") -WorkingDirectory $submoduleFullPath -AllowFailure
if ($describe.ExitCode -ne 0) {
    throw "Cannot determine the pinned dlssg_for_sm86 version: $($describe.Output -join ' ')"
}
$currentTag = ([string]$describe.Output[0]).Trim()

Write-Host "Pinned upstream version : $currentTag"

# ---------------------------------------------------------------------------
# Find the newest upstream tag straight from the remote
# ---------------------------------------------------------------------------

$lsRemote = Invoke-Git -Arguments @("ls-remote", "--tags", "--refs", "origin") -WorkingDirectory $submoduleFullPath

$tagPattern = if ($IncludePrerelease) { '^(\d+(?:\.\d+)*)(?:[-+].*)?$' } else { '^(\d+(?:\.\d+)*)$' }
$candidates = New-Object System.Collections.Generic.List[object]

foreach ($entry in $lsRemote.Output) {
    $fields = ([string]$entry) -split "\s+"
    if ($fields.Count -lt 2) { continue }

    $tagName = $fields[1] -replace "^refs/tags/", ""
    $tagMatch = [regex]::Match($tagName, $tagPattern)
    if (-not $tagMatch.Success) { continue }

    $candidates.Add([pscustomobject]@{ Tag = $tagName; Version = $tagMatch.Groups[1].Value })
}

if ($candidates.Count -eq 0) {
    throw "No usable release tags found on origin of $UpstreamRepo"
}

$latest = $candidates[0]
foreach ($candidate in $candidates) {
    $comparison = Compare-VersionText $candidate.Version $latest.Version
    if ($comparison -gt 0) {
        $latest = $candidate
    }
    elseif ($comparison -eq 0 -and $candidate.Tag -notmatch "[-+]" -and $latest.Tag -match "[-+]") {
        # Same version: prefer the plain release tag over a suffixed one.
        $latest = $candidate
    }
}

$latestTag = $latest.Tag
Write-Host "Latest upstream version : $latestTag"

if ($currentTag -notmatch "^\d+(\.\d+)*$") {
    throw "Unexpected pinned tag format: '$currentTag'"
}

if ((Compare-VersionText $latest.Version $currentTag) -le 0) {
    Write-Host "Already up to date, nothing to do."
    Set-ActionOutput "updated" "false"
    return
}

# ---------------------------------------------------------------------------
# Build the changelog
# ---------------------------------------------------------------------------

$changelogLines = New-Object System.Collections.Generic.List[string]
$changelogLines.Add((ConvertTo-YamlScalar "Bundled dlssg_for_sm86 $latestTag (was $currentTag)"))

$summary = Get-UpstreamSummary $latestTag $currentTag
if ($summary) {
    if ($summary.Length -gt 300) { $summary = $summary.Substring(0, 297) + "..." }
    $changelogLines.Add((ConvertTo-YamlScalar $summary))
}

foreach ($extra in $ExtraChangelog) {
    if ($extra -and $extra.Trim() -ne "") {
        $changelogLines.Add((ConvertTo-YamlScalar $extra.Trim()))
    }
}

Write-Host ""
Write-Host "Changelog for the new plugin release:"
foreach ($line in $changelogLines) { Write-Host "  - $line" }

if ($DryRun) {
    Write-Host ""
    Write-Host "Dry run: the submodule, manifests and git history were not touched."
    & (Join-Path $repoRoot "bump_version.ps1") -Bump $Bump -Changelog $changelogLines -DryRun
    Set-ActionOutput "updated" "false"
    return
}

# ---------------------------------------------------------------------------
# Move the submodule to the new release
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "Fetching upstream tags..."
Invoke-Git -Arguments @("fetch", "--tags", "--prune", "origin") -WorkingDirectory $submoduleFullPath | Out-Null
Invoke-Git -Arguments @("checkout", "--detach", $latestTag) -WorkingDirectory $submoduleFullPath | Out-Null
Write-Host "Submodule moved to $latestTag"

# ---------------------------------------------------------------------------
# Keep the READMEs in step with the bundled version
# ---------------------------------------------------------------------------

foreach ($readme in @("README.md", "README_EN.md")) {
    $readmePath = Join-Path $repoRoot $readme
    if (-not (Test-Path $readmePath)) { continue }

    $text = [System.IO.File]::ReadAllText($readmePath)
    $updated = [regex]::Replace(
        $text,
        "dlssg_for_sm86\s+$([regex]::Escape($currentTag))",
        "dlssg_for_sm86 $latestTag")

    if ($updated -ne $text) {
        $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
        [System.IO.File]::WriteAllText($readmePath, $updated, $utf8NoBom)
        Write-Host "Updated bundled version in $readme"
    }
}

# ---------------------------------------------------------------------------
# Roll the plugin version
# ---------------------------------------------------------------------------

$bumpOutput = & (Join-Path $repoRoot "bump_version.ps1") -Bump $Bump -Changelog $changelogLines *>&1
$bumpOutput | ForEach-Object { Write-Host $_ }

$versionLine = $bumpOutput | Where-Object { $_ -match "^New version\s+:\s+(\S+)" } | Select-Object -First 1
if (-not $versionLine) {
    throw "Could not read the new version from bump_version.ps1 output."
}
$newVersion = [regex]::Match([string]$versionLine, "^New version\s+:\s+(\S+)").Groups[1].Value
$newTag = "v$newVersion"
Write-Host "Plugin version rolled to $newVersion"

# ---------------------------------------------------------------------------
# Commit, tag, push
# ---------------------------------------------------------------------------

Invoke-Git -Arguments @("add", "--", $SubmodulePath, "manifest", "Properties/AssemblyInfo.cs", "README.md", "README_EN.md") | Out-Null

$staged = Invoke-Git -Arguments @("diff", "--cached", "--name-only")
if ($staged.Output.Count -eq 0) {
    Write-Host "Nothing staged, skipping commit."
    return
}

$message = "Bundled dlssg_for_sm86 $latestTag, version $newVersion"
Invoke-Git -Arguments @("commit", "-m", $message) | Out-Null
Write-Host "Committed: $message"

$existingTag = Invoke-Git -Arguments @("tag", "--list", $newTag)
if ($existingTag.Output.Count -gt 0) {
    throw "Tag $newTag already exists. Delete it or pick another version."
}
Invoke-Git -Arguments @("tag", $newTag) | Out-Null
Write-Host "Tagged $newTag"

if ($NoPush) {
    Write-Host "NoPush set: commit and tag were not pushed."
}
else {
    $branch = (Invoke-Git -Arguments @("rev-parse", "--abbrev-ref", "HEAD")).Output[0]
    Invoke-Git -Arguments @("push", "origin", $branch) | Out-Null
    Invoke-Git -Arguments @("push", "origin", $newTag) | Out-Null
    Write-Host "Pushed $branch and $newTag"
}

Set-ActionOutput "updated" "true"
Set-ActionOutput "version" $newVersion
Set-ActionOutput "tag" $newTag
Set-ActionOutput "upstream" $latestTag
