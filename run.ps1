<#
.SYNOPSIS
Build CibMedia, install it on an Android TV device and launch it.

.PARAMETER Configuration
Debug (default) or Release. Judge how the app feels on Release only — Debug inflates UI cost
around 2.5x.

.PARAMETER Device
An adb serial, as printed by `adb devices`. Use this for USB or the emulator.

.PARAMETER Ip
A network address, for a TV over network debugging. Port 5555 is assumed if none is given, and
the script dials the box before it looks for it.

.PARAMETER Log
Follow the app's log after launching, until Ctrl+C.

.EXAMPLE
./run.ps1
Debug onto the only attached device.

.EXAMPLE
./run.ps1 -Configuration Release -Ip 192.168.0.51 -Log

.EXAMPLE
./run.ps1 -Configuration Release -Device emulator-5554
#>

#Requires -Version 5.1

[CmdletBinding(DefaultParameterSetName = 'Default')]
param(
    [ValidateSet('Debug', 'Release')]
    [string] $Configuration = 'Debug',

    [Parameter(ParameterSetName = 'Serial', Mandatory)]
    [string] $Device,

    [Parameter(ParameterSetName = 'Network', Mandatory)]
    [ValidatePattern('^[^\s:]+(:\d+)?$')]
    [string] $Ip,

    [switch] $Log
)

Set-StrictMode -Version 3.0

# Not ErrorActionPreference = Stop: adb writes to stderr on the ordinary "no such device" path,
# which Stop would turn into a terminating error before the exit code can be read.

$Package = 'com.davitigg.cibmedia'
$Project = Join-Path $PSScriptRoot 'src/CibMedia.AndroidTv/CibMedia.AndroidTv.csproj'

function Step([string] $Message)
{
    Write-Host "=== $Message" -ForegroundColor Cyan
}

function Fail([string] $Message)
{
    Write-Host $Message -ForegroundColor Red
    exit 1
}

# Empty unless a target was named, in which case adb picks.
function Resolve-Target
{
    if ($Device)
    {
        return $Device
    }
    if ($Ip)
    {
        if ($Ip -match ':')
        {
            return $Ip
        }
        else
        {
            return "${Ip}:5555"
        }
    }

    return ''
}

# No param block on purpose: an advanced function binds adb's own flags as its common
# parameters, so -p in "shell monkey -p <package>" is rejected as ambiguous against
# -ProgressAction. $args takes them literally.
function Invoke-Adb
{
    if ($Target)
    {
        & adb -s $Target @args
    }
    else
    {
        & adb @args
    }
}

function Connect-Target
{
    if ($Target -match ':')
    {
        & adb connect $Target | Out-Null
    }

    # adb's own words: it distinguishes no device, several, and one that is offline.
    # Stringified per record, because 2>&1 wraps stderr in an ErrorRecord whose default
    # rendering is a positional dump.
    $state = (Invoke-Adb get-state 2>&1 | ForEach-Object { "$_" }) -join ' '
    if ($LASTEXITCODE -ne 0)
    {
        Fail @"
Cannot install: $state
  Attached devices:  adb devices
  Name one with:     -Device <serial>  or  -Ip <address>
  Over the network:  the box needs Developer options > Network debugging left on.
"@
    }
}

# Fast Deployment keeps the assemblies outside the Debug APK, so it is not installable by hand;
# only the Install target pushes both halves.
function Install-Debug
{
    $buildArgs = @($Project, '-c', 'Debug', '-t:Install')
    if ($Target)
    {
        $buildArgs += "-p:AdbTarget=-s $Target"
    }

    dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0)
    {
        Fail 'Build failed.'
    }
}

# Release embeds its assemblies and is installed by hand: the Install target has been seen to
# build and then leave the device on the previous package without saying so.
function Install-Release
{
    dotnet build $Project -c Release
    if ($LASTEXITCODE -ne 0)
    {
        Fail 'Build failed.'
    }

    $apk = Join-Path $PSScriptRoot "src/CibMedia.AndroidTv/bin/Release/net10.0-android/$Package-Signed.apk"
    if (-not (Test-Path $apk))
    {
        Fail "Built, but no APK at $apk"
    }

    Step 'Installing'
    Invoke-Adb install -r $apk
    if ($LASTEXITCODE -ne 0)
    {
        Fail 'Install failed.'
    }
}

# Compare against the clock when a change looks like it did nothing.
function Get-InstalledAt
{
    $line = Invoke-Adb shell dumpsys package $Package |
        Select-String -Pattern 'lastUpdateTime=(.+)' |
        Select-Object -First 1

    if (-not $line)
    {
        return 'unknown'
    }

    return $line.Matches[0].Groups[1].Value.Trim()
}

function Start-App
{
    Invoke-Adb shell am force-stop $Package | Out-Null
    Invoke-Adb logcat -c

    # monkey resolves the leanback launcher intent itself: the Java class behind a .NET Activity
    # is a generated name that changes between builds.
    Invoke-Adb shell monkey -p $Package -c android.intent.category.LEANBACK_LAUNCHER 1 2>&1 |
        Out-Null
}

# A missing adb does not stop the script on its own: the call leaves $LASTEXITCODE unset and
# every check downstream reads a stale value.
if (-not (Get-Command adb -CommandType Application -ErrorAction SilentlyContinue))
{
    Fail @"
adb is not on PATH.
  Add the Android SDK's platform-tools directory to it, then open a new shell:
    $env:LOCALAPPDATA\Android\Sdk\platform-tools
"@
}

$Target = Resolve-Target

Connect-Target

Step "Building $Configuration"
if ($Configuration -eq 'Release')
{
    Install-Release
}
else
{
    Install-Debug
}

Step "Installed $( Get-InstalledAt )"

Step 'Launching'
Start-App

if ($Log)
{
    Step 'Log (Ctrl+C to stop)'
    Invoke-Adb logcat -s CibMedia:V AndroidRuntime:E mono-rt:V MonoDroid:V DOTNET:V
}
else
{
    Step "Running. For a crash:  adb logcat -d | Select-String 'AndroidRuntime|MonoDroid'"
}
