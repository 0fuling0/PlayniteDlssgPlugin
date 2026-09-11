param(
    [Parameter(Mandatory=$true)]
    [ValidateSet("major","minor","patch")]
    [string]$Bump
)

$manifestPath = "manifest/extension.yaml"
$installerPath = "manifest/installer.yaml"
$addonPath = "manifest/0fuling0_PlayniteDlssgPlugin.yaml"
$assemblyPath = "Properties/AssemblyInfo.cs"

$manifest = Get-Content $manifestPath -Raw
$versionMatch = [regex]::Match($manifest, 'Version:\s*(\d+)\.(\d+)\.(\d+)')
if (-not $versionMatch.Success) {
    throw "Version not found in $manifestPath"
}

$major = [int]$versionMatch.Groups[1].Value
$minor = [int]$versionMatch.Groups[2].Value
$patch = [int]$versionMatch.Groups[3].Value

switch ($Bump) {
    "major" { $major++; $minor = 0; $patch = 0 }
    "minor" { $minor++; $patch = 0 }
    "patch" { $patch++ }
}

$newVersion = "$major.$minor.$patch"
Write-Host "New version: $newVersion"

# Update extension.yaml
$manifest = $manifest -replace 'Version:\s*\d+\.\d+\.\d+', "Version: $newVersion"
Set-Content $manifestPath -Value $manifest -Encoding UTF8

# Update installer.yaml
$installer = Get-Content $installerPath -Raw
$installer = $installer -replace 'Version:\s*\d+\.\d+\.\d+', "Version: $newVersion"
$installer = $installer -replace 'ReleaseDate:\s*\d{4}-\d{2}-\d{2}', "ReleaseDate: $(Get-Date -Format 'yyyy-MM-dd')"
$installer = $installer -replace 'v\d+\.\d+\.\d+/', "v$newVersion/"
Set-Content $installerPath -Value $installer -Encoding UTF8

# Update addon.yaml (no version field, but update installer URL)
$addon = Get-Content $addonPath -Raw
$addon = $addon -replace 'v\d+\.\d+\.\d+/', "v$newVersion/"
Set-Content $addonPath -Value $addon -Encoding UTF8

# Update AssemblyInfo.cs
$assembly = Get-Content $assemblyPath -Raw
$assembly = $assembly -replace 'AssemblyVersion\("\d+\.\d+\.\d+"\)', "AssemblyVersion(`"$newVersion`")"
$assembly = $assembly -replace 'AssemblyFileVersion\("\d+\.\d+\.\d+"\)', "AssemblyFileVersion(`"$newVersion`")"
Set-Content $assemblyPath -Value $assembly -Encoding UTF8

Write-Host "Version bumped to $newVersion"
Write-Host "Run: git add . && git commit -m 'Bump version to $newVersion' && git tag v$newVersion && git push origin master --tags"
