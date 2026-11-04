[CmdletBinding()]
param(
    [string]$RepositoryUrl = 'https://github.com/aeilkema/NIR-Rebuild.git',
    [string]$Branch = 'codex/nir5000-modern',
    [string]$InstallPath = (Get-Location).Path,
    [switch]$SkipBuild,
    [switch]$Launch,
    [switch]$PushChanges,
    [string]$CommitMessage = 'Update NIR-Rebuild modern 64-bit application'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Invoke-Git {
    param([Parameter(Mandatory)][string[]]$Arguments)
    & git @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed with exit code ${LASTEXITCODE}: git $($Arguments -join ' ')"
    }
}

function Require-Command {
    param([Parameter(Mandatory)][string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Required command '$Name' was not found. Install it and run this script again."
    }
}

$Root = [IO.Path]::GetFullPath($InstallPath)
New-Item -ItemType Directory -Path $Root -Force | Out-Null
Set-Location $Root

Require-Command 'git'
if (-not $SkipBuild) { Require-Command 'dotnet' }

if (-not (Test-Path (Join-Path $Root '.git'))) {
    $temporaryClone = Join-Path ([IO.Path]::GetTempPath()) ("nir-rebuild-clone-" + [guid]::NewGuid().ToString('N'))
    try {
        Write-Host "Cloning $RepositoryUrl ($Branch)..." -ForegroundColor Cyan
        Invoke-Git @('clone', '--branch', $Branch, '--single-branch', $RepositoryUrl, $temporaryClone)
        Get-ChildItem -LiteralPath $temporaryClone -Force |
            Where-Object { $_.Name -ne '.git' } |
            Copy-Item -Destination $Root -Recurse -Force
    }
    finally {
        if (Test-Path $temporaryClone) {
            Remove-Item -LiteralPath $temporaryClone -Recurse -Force
        }
    }
}
else {
    Write-Host "Updating existing checkout..." -ForegroundColor Cyan
    Invoke-Git @('-C', $Root, 'fetch', 'origin', $Branch)
    $localBranch = (& git -C $Root branch --show-current).Trim()
    if ($localBranch -ne $Branch) {
        # The bootstrap script is often copied into an existing checkout before
        # the first run. Git must not reject the branch switch merely because
        # that untracked copy would be replaced by the tracked version.
        $bootstrapPath = Join-Path $Root 'Install-NirRebuild.ps1'
        $bootstrapBackup = $null
        if (Test-Path -LiteralPath $bootstrapPath) {
            $trackedBootstrap = (& git -C $Root ls-files --error-unmatch -- Install-NirRebuild.ps1 2>$null)
            if ($LASTEXITCODE -ne 0) {
                $bootstrapBackup = Join-Path ([IO.Path]::GetTempPath()) ("nir-rebuild-bootstrap-" + [guid]::NewGuid().ToString('N') + '.ps1')
                Copy-Item -LiteralPath $bootstrapPath -Destination $bootstrapBackup -Force
                Remove-Item -LiteralPath $bootstrapPath -Force
            }
        }
        try {
            Invoke-Git @('-C', $Root, 'checkout', '-B', $Branch, "origin/$Branch")
        }
        catch {
            if ($bootstrapBackup -and (Test-Path -LiteralPath $bootstrapBackup)) {
                Copy-Item -LiteralPath $bootstrapBackup -Destination $bootstrapPath -Force
            }
            throw
        }
        finally {
            if ($bootstrapBackup -and (Test-Path -LiteralPath $bootstrapBackup)) {
                Remove-Item -LiteralPath $bootstrapBackup -Force
            }
        }
    }
    Invoke-Git @('-C', $Root, 'pull', '--ff-only', 'origin', $Branch)
}

if (-not $SkipBuild) {
    Write-Host 'Building Nir5000.sln...' -ForegroundColor Cyan
    Invoke-Git @('-C', $Root, 'status', '--short')
    $nugetConfig = Join-Path $Root 'NuGet.Config'
    if (-not (Test-Path -LiteralPath $nugetConfig)) {
        throw "NuGet.Config is missing from $Root. Re-run the bootstrap script so the current branch is checked out."
    }

    $sdkList = (& dotnet --list-sdks 2>$null) -join [Environment]::NewLine
    if ([string]::IsNullOrWhiteSpace($sdkList)) {
        throw 'No .NET SDK was found. Install the .NET 8 SDK or a newer SDK that supports targeting net8.0.'
    }

    Write-Host 'Restoring NuGet packages from nuget.org...' -ForegroundColor Cyan
    & dotnet restore (Join-Path $Root 'Nir5000.sln') --configfile $nugetConfig --force
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed. Check internet/proxy access to https://api.nuget.org/v3/index.json and install the .NET 8 targeting packs. Exit code: ${LASTEXITCODE}"
    }

    & dotnet build (Join-Path $Root 'Nir5000.sln') -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE" }

    $publishRoot = Join-Path $Root 'publish'
    $cliPublish = Join-Path $publishRoot 'cli-win-x64'
    $appPublish = Join-Path $publishRoot 'app-win-x64'
    New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null

    Write-Host 'Publishing self-contained x64 CLI...' -ForegroundColor Cyan
    & dotnet publish (Join-Path $Root 'src\Nir5000.Cli\Nir5000.Cli.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $cliPublish --no-restore
    if ($LASTEXITCODE -ne 0) { throw "CLI publish failed with exit code $LASTEXITCODE" }

    Write-Host 'Publishing self-contained x64 GUI...' -ForegroundColor Cyan
    & dotnet publish (Join-Path $Root 'src\Nir5000.App\Nir5000.App.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $appPublish --no-restore
    if ($LASTEXITCODE -ne 0) { throw "GUI publish failed with exit code $LASTEXITCODE" }

    Write-Host 'Running offline regression self-test...' -ForegroundColor Cyan
    $selfTest = Join-Path $env:TEMP 'nir5000-selftest-win'
    & dotnet publish (Join-Path $Root 'src\Nir5000.Cli\Nir5000.Cli.csproj') -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o $selfTest --no-restore | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Self-test publish failed with exit code $LASTEXITCODE" }
    & (Join-Path $selfTest 'Nir5000.Cli.exe') --self-test
    if ($LASTEXITCODE -ne 0) { throw "Offline self-test failed with exit code $LASTEXITCODE" }

    Write-Host "GUI: $appPublish\Nir5000.App.exe" -ForegroundColor Green
    Write-Host "CLI: $cliPublish\Nir5000.Cli.exe" -ForegroundColor Green

    if ($Launch) {
        Start-Process -FilePath (Join-Path $appPublish 'Nir5000.App.exe') -WorkingDirectory $appPublish
    }
}

if ($PushChanges) {
    Write-Host "Pushing local changes to $Branch..." -ForegroundColor Cyan
    Invoke-Git @('-C', $Root, 'add', '--all')
    & git -C $Root diff --cached --quiet
    if ($LASTEXITCODE -eq 1) {
        Invoke-Git @('-C', $Root, 'commit', '-m', $CommitMessage)
        Invoke-Git @('-C', $Root, 'push', '--set-upstream', 'origin', $Branch)
    }
    elseif ($LASTEXITCODE -eq 0) {
        Write-Host 'No changes to push.' -ForegroundColor Yellow
    }
    else {
        throw "Unable to inspect staged changes."
    }
}

Write-Host 'NIR-Rebuild setup completed.' -ForegroundColor Green
