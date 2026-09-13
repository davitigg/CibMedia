<#
.SYNOPSIS
Cut a signed release: bump the version, build, publish the apk and point manifest.json at it.

.DESCRIPTION
Every box compares the versionCode in manifest.json against its own, so a release is only an
update once this has run and manifest.json is on main. The apk is uploaded to a GitHub release
and manifest.json stays in the repo, which is why changing playback config later is a commit
rather than another release.

.PARAMETER VersionName
What the update prompt shows, for example 2.1. The versionCode beside it is bumped by one.

.PARAMETER Notes
The line the update prompt shows under the title.

.PARAMETER WhatIf
Build and write manifest.json, but publish nothing.

.EXAMPLE
./release.ps1 -VersionName 2.1 -Notes 'Playback config now travels without an update.'
#>

#Requires -Version 5.1

[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)]
    [string] $VersionName,

    [string] $Notes = ''
)

$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot

$project = 'src/CibMedia.AndroidTv/CibMedia.AndroidTv.csproj'
$manifestPath = 'manifest.json'
$repository = 'davitigg/CibMedia'

# Without the signing key every box refuses the update, and the build would quietly use the
# machine's debug key instead.
if (-not (Test-Path 'keystore/signing.props')) {
    throw 'keystore/signing.props is missing: a release built without it cannot install over an installed app.'
}

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'The GitHub CLI (gh) is required to publish a release.'
}

$xml = [xml](Get-Content $project)
$properties = $xml.Project.PropertyGroup | Where-Object { $_.ApplicationVersion } | Select-Object -First 1
$versionCode = [int] $properties.ApplicationVersion + 1
$tag = "v$VersionName"

Write-Host "=== $tag, versionCode $versionCode"

$properties.ApplicationVersion = "$versionCode"
$properties.ApplicationDisplayVersion = $VersionName
$xml.Save((Resolve-Path $project))

dotnet build $project -c Release -t:SignAndroidPackage
if ($LASTEXITCODE -ne 0) { throw 'The release build failed.' }

$built = 'src/CibMedia.AndroidTv/bin/Release/net10.0-android/com.davitigg.cibmedia-Signed.apk'
$apk = "artifacts/cibmedia-$VersionName.apk"

# -WhatIf:$false on both: the apk is what the hash and the size below describe, so a dry run that
# skipped the copy could not write the manifest it exists to show.
New-Item -ItemType Directory -Force artifacts -WhatIf:$false | Out-Null
Copy-Item $built $apk -Force -WhatIf:$false

# Not Get-FileHash: it ships in Microsoft.PowerShell.Utility and is missing on at least one box
# this is cut from, which failed the release after the build with the version already bumped.
$sha = [System.Security.Cryptography.SHA256]::Create()
try { $digest = $sha.ComputeHash([System.IO.File]::ReadAllBytes((Resolve-Path $apk))) }
finally { $sha.Dispose() }

$sha256 = [BitConverter]::ToString($digest).Replace('-', '').ToLowerInvariant()

# Only the six fields a release decides are rewritten, and everything from the playback block on
# is carried across as the characters it already is. Reading the document in and writing it back
# out reformats the whole of it — ConvertTo-Json indents nothing like the file is kept in — and
# playback is what a release must not touch at all: it carries the config already on main, and
# never reverts it to whatever this working tree happens to hold.
$raw = Get-Content $manifestPath -Raw
$playback = $raw.IndexOf('"playback"')
if ($playback -lt 0) { throw "$manifestPath names no playback block, so it is not a manifest." }

$newline = if ($raw.Contains("`r`n")) { "`r`n" } else { "`n" }

# ConvertTo-Json on a lone string is what quotes and escapes it; notes is free text and the url
# carries slashes.
$fields = @(
    "    ""versionCode"": $versionCode,"
    "    ""versionName"": $(ConvertTo-Json $VersionName),"
    "    ""apkUrl"": $(ConvertTo-Json "https://github.com/$repository/releases/download/$tag/$(Split-Path $apk -Leaf)"),"
    "    ""sha256"": $(ConvertTo-Json $sha256),"
    "    ""sizeBytes"": $((Get-Item $apk).Length),"
    "    ""notes"": $(ConvertTo-Json $Notes),"
)

$manifest = "{$newline" + ($fields -join $newline) + "$newline    " + $raw.Substring($playback)

Set-Content $manifestPath $manifest -Encoding utf8 -NoNewline -WhatIf:$false

Write-Host "=== $apk"
Write-Host "=== sha256 $sha256"

if ($PSCmdlet.ShouldProcess($tag, 'publish the release and push manifest.json')) {
    # Pushed before the release is cut: gh tags whatever the remote head is, so a release created
    # first is tagged against the commit before the one it was built from.
    git add $project $manifestPath
    git commit -m "Release $VersionName"
    git push
    if ($LASTEXITCODE -ne 0) { throw 'Pushing the release commit failed.' }

    gh release create $tag $apk --repo $repository --title $tag --notes $Notes
    if ($LASTEXITCODE -ne 0) { throw 'Publishing the release failed: manifest.json now names an apk that is not there.' }

    Write-Host '=== Published. Boxes are offered it on their next launch.'
}
else {
    Write-Host '=== Nothing published. manifest.json and the csproj were still updated.'
}
