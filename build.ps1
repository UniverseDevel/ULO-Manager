<#
    Builds the ULO Manager solution, and can diagnose why an IDE fails to build it.

    Background: SDK-style projects need an MSBuild that carries the .NET SDK resolver AND is new
    enough for the installed SDK (.NET 9.0.3xx requires MSBuild 17.12 or newer). Several MSBuild
    copies usually exist side by side on a developer machine and not all of them qualify.

    Usage:
        .\build.ps1                    # build Release
        .\build.ps1 -Configuration Debug -Run
        .\build.ps1 -Diagnose          # explain the build environment and how to set up the IDE
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [switch]$Run,

    [switch]$Diagnose
)

$ErrorActionPreference = 'Stop'

function Find-Dotnet {
    $candidates = @(
        (Join-Path $env:USERPROFILE '.dotnet\dotnet.exe'),
        'C:\Program Files\dotnet\dotnet.exe'
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            if (& $candidate --list-sdks 2>$null) {
                return $candidate
            }
        }
    }

    $command = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($command) {
        return $command.Source
    }

    throw 'No .NET SDK found. Install the .NET 9 SDK from https://dotnet.microsoft.com/download'
}

function Get-MsBuildCandidates {
    $paths = @(
        (Join-Path $env:LOCALAPPDATA 'JetBrains\BuildTools\MSBuild\Current\Bin\MSBuild.exe'),
        'C:\Program Files\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Professional\MSBuild\Current\Bin\MSBuild.exe',
        'C:\Program Files\Microsoft Visual Studio\2022\Enterprise\MSBuild\Current\Bin\MSBuild.exe'
    )

    foreach ($drive in @('C:\', 'D:\', 'W:\')) {
        foreach ($rider in Get-ChildItem $drive -Directory -Filter 'Rider*' -ErrorAction SilentlyContinue) {
            $paths += (Join-Path $rider.FullName 'tools\MSBuild\Current\Bin\MSBuild.exe')
        }
    }

    $toolboxApps = Join-Path $env:LOCALAPPDATA 'JetBrains\Toolbox\apps'
    if (Test-Path $toolboxApps) {
        foreach ($found in Get-ChildItem $toolboxApps -Recurse -Filter 'MSBuild.exe' -ErrorAction SilentlyContinue) {
            $paths += $found.FullName
        }
    }

    $result = @()
    foreach ($path in ($paths | Select-Object -Unique)) {
        if (-not (Test-Path $path)) { continue }

        $version = ((Get-Item $path).VersionInfo.ProductVersion -replace '\+.*', '')
        $hasResolver = Test-Path (Join-Path (Split-Path $path) 'SdkResolvers\Microsoft.DotNet.MSBuildSdkResolver')

        $parsed = $null
        [void][version]::TryParse((($version -split '-')[0]), [ref]$parsed)

        $result += [pscustomobject]@{
            Path        = $path
            Version     = $version
            HasResolver = $hasResolver
            Usable      = $hasResolver -and $parsed -and $parsed -ge [version]'17.12'
        }
    }

    return $result
}

if ($Diagnose) {
    Write-Host 'ULO Manager - build environment' -ForegroundColor Cyan
    Write-Host ''

    $dotnet = Find-Dotnet
    Write-Host "dotnet   : $dotnet"
    Write-Host 'SDKs     :'
    & $dotnet --list-sdks | ForEach-Object { Write-Host "           $_" }
    Push-Location $PSScriptRoot
    try { Write-Host "Selected : $(& $dotnet --version) (per global.json)" } finally { Pop-Location }

    Write-Host ''
    Write-Host 'MSBuild copies found:'

    $candidates = Get-MsBuildCandidates
    foreach ($candidate in $candidates) {
        $note = if ($candidate.Usable) { 'OK - can build this solution' }
                elseif (-not $candidate.HasResolver) { 'no .NET SDK resolver - cannot build SDK-style projects' }
                else { 'too old for .NET 9 (needs MSBuild 17.12+)' }

        $colour = if ($candidate.Usable) { 'Green' } else { 'Yellow' }
        Write-Host ("  {0,-10} {1}" -f $candidate.Version, $candidate.Path) -ForegroundColor $colour
        Write-Host ("             {0}" -f $note) -ForegroundColor $colour
    }

    $best = $candidates | Where-Object Usable | Select-Object -First 1
    Write-Host ''

    if ($best) {
        Write-Host 'Rider: Settings > Build, Execution, Deployment > Toolset and Build' -ForegroundColor Cyan
        Write-Host "  Use MSBuild version      : $($best.Path)"
        Write-Host "  .NET CLI executable path : $dotnet"
        Write-Host '  then File > Reload All Projects'
    }
    else {
        Write-Host 'No usable MSBuild found - install the .NET 9 SDK machine-wide, or build with this script.' -ForegroundColor Yellow
    }

    Write-Host ''
    Write-Host 'Rider stores this per solution in <solution>.sln.DotSettings.user, so set it for'
    Write-Host 'each solution you open. UloManager.sln needs a 17.12+ MSBuild.'
    return
}

$dotnet = Find-Dotnet
Write-Host "Using SDK: $dotnet" -ForegroundColor Cyan

$solution = Join-Path $PSScriptRoot 'UloManager.sln'
& $dotnet build $solution -c $Configuration --nologo
if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

$gui = Join-Path $PSScriptRoot "src\UloManager.Gui\bin\$Configuration\net9.0-windows\UloManager.exe"
$cli = Join-Path $PSScriptRoot "src\UloManager.Cli\bin\$Configuration\net9.0\ulo.exe"

Write-Host ''
Write-Host 'Build finished:' -ForegroundColor Green
Write-Host "  GUI : $gui"
Write-Host "  CLI : $cli"

if ($Run) {
    Start-Process $gui
}
