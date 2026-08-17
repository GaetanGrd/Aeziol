[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+\.\d+$')]
    [string] $Version,

    [string] $InformationalVersion,

    [Parameter(Mandatory = $true)]
    [string] $Publisher,

    [string] $CertificatePath,

    [string] $CertificatePassword
)

$ErrorActionPreference = 'Stop'
$InformationalVersion = if ([string]::IsNullOrWhiteSpace($InformationalVersion)) {
    ($Version.Split('.')[0..2] -join '.')
} else {
    $InformationalVersion
}
if ($InformationalVersion -notmatch '^\d+\.\d+\.\d+(?:-beta\.\d+)?$') {
    throw "Invalid informational version: $InformationalVersion"
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$artifactsRoot = Join-Path $repoRoot 'artifacts'
$buildRoot = Join-Path $artifactsRoot 'build'
$publishRoot = Join-Path $artifactsRoot 'publish'
$layoutRoot = Join-Path $artifactsRoot 'msix-layout'
$assetsRoot = Join-Path $layoutRoot 'Assets'
$packagePath = Join-Path $artifactsRoot "Aeziol-$InformationalVersion-x64.msix"

foreach ($target in @($buildRoot, $publishRoot, $layoutRoot)) {
    $fullTarget = [IO.Path]::GetFullPath($target)
    if (-not $fullTarget.StartsWith([IO.Path]::GetFullPath($artifactsRoot), [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to clean a path outside the artifacts directory: $fullTarget"
    }

    if (Test-Path -LiteralPath $fullTarget) {
        Remove-Item -LiteralPath $fullTarget -Recurse -Force
    }
}

New-Item -ItemType Directory -Path $publishRoot, $layoutRoot, $assetsRoot -Force | Out-Null

$appProject = Join-Path $repoRoot 'src\Aeziol.App\Aeziol.App.csproj'
dotnet restore $appProject `
    -r win-x64 `
    -p:SelfContained=true `
    --artifacts-path $buildRoot
if ($LASTEXITCODE -ne 0) { throw 'self-contained runtime restore failed.' }

dotnet publish $appProject `
    -c Release `
    -r win-x64 `
    --self-contained true `
    --no-restore `
    --artifacts-path $buildRoot `
    -p:DebugType=None `
    -p:DebugSymbols=false `
    -p:Version=$InformationalVersion `
    -p:InformationalVersion=$InformationalVersion `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -o $publishRoot
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }

Copy-Item -Path (Join-Path $publishRoot '*') -Destination $layoutRoot -Recurse -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'AppxManifest.xml') -Destination (Join-Path $layoutRoot 'AppxManifest.xml')

[xml] $manifest = Get-Content -LiteralPath (Join-Path $layoutRoot 'AppxManifest.xml')
$manifest.Package.Identity.Version = $Version
$manifest.Package.Identity.Publisher = $Publisher
$manifest.Save((Join-Path $layoutRoot 'AppxManifest.xml'))

$brandAssetsRoot = Join-Path $PSScriptRoot 'Assets'
$requiredAssets = @('StoreLogo.png', 'Square44x44Logo.png', 'Square150x150Logo.png', 'Wide310x150Logo.png')
foreach ($asset in $requiredAssets) {
    $source = Join-Path $brandAssetsRoot $asset
    if (-not (Test-Path -LiteralPath $source)) {
        throw "Missing generated brand asset: $source. Run tools/generate-brand-assets.py."
    }

    Copy-Item -LiteralPath $source -Destination (Join-Path $assetsRoot $asset) -Force
}

$windowsKits = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'
$makeAppx = Get-ChildItem -LiteralPath $windowsKits -Filter 'makeappx.exe' -Recurse |
    Where-Object { $_.FullName -match '\\x64\\makeappx\.exe$' } |
    Sort-Object FullName -Descending |
    Select-Object -First 1
if (-not $makeAppx) { throw 'makeappx.exe was not found. Install the Windows 11 SDK.' }

& $makeAppx.FullName pack /d $layoutRoot /p $packagePath /o
if ($LASTEXITCODE -ne 0) { throw 'MSIX packaging failed.' }

if ($CertificatePath) {
    $signTool = Get-ChildItem -LiteralPath $windowsKits -Filter 'signtool.exe' -Recurse |
        Where-Object { $_.FullName -match '\\x64\\signtool\.exe$' } |
        Sort-Object FullName -Descending |
        Select-Object -First 1
    if (-not $signTool) { throw 'signtool.exe was not found. Install the Windows 11 SDK.' }
    & $signTool.FullName sign /fd SHA256 /f $CertificatePath /p $CertificatePassword $packagePath
    if ($LASTEXITCODE -ne 0) { throw 'MSIX signing failed.' }
}

Write-Output $packagePath
