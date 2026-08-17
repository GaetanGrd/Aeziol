[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern('^\d+\.\d+\.\d+-beta\.\d+$')]
    [string] $Version,

    [switch] $Publish
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $revision = [int]([regex]::Match($Version, '-beta\.(\d+)$').Groups[1].Value)
    if ($revision -lt 1 -or $revision -gt 65534) {
        throw 'The beta revision must be between 1 and 65534.'
    }

    & git rev-parse --verify HEAD *> $null
    if ($LASTEXITCODE -ne 0) {
        throw 'The repository has no commit yet. Create the initial commit before publishing a beta.'
    }

    $branch = (& git branch --show-current).Trim()
    if ($LASTEXITCODE -ne 0 -or $branch -ne 'main') {
        throw "Beta publication is allowed only from main; current branch: '$branch'."
    }

    $changes = @(& git status --porcelain=v1)
    if ($LASTEXITCODE -ne 0) {
        throw 'Unable to inspect the Git working tree.'
    }

    if ($changes.Count -gt 0) {
        throw "The working tree is not clean. Commit or preserve every change before publishing.`n$($changes -join [Environment]::NewLine)"
    }

    $validationRoot = Join-Path $repoRoot '.aez-local\publish-validation'
    Write-Host "Validating Aeziol $Version in $validationRoot..."
    & dotnet restore Aeziol.slnx --artifacts-path $validationRoot
    if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }

    & dotnet build Aeziol.slnx -c Release --no-restore --artifacts-path $validationRoot
    if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }

    $testExecutables = @(Get-ChildItem `
        -LiteralPath (Join-Path $validationRoot 'bin\Aeziol.Tests') `
        -Filter 'Aeziol.Tests.exe' `
        -File `
        -Recurse)
    if ($testExecutables.Count -ne 1) {
        throw "Expected exactly one isolated test runner, found $($testExecutables.Count)."
    }

    & $testExecutables[0].FullName --timeout 60s
    if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }

    $tag = "v$Version"
    if (-not $Publish) {
        Write-Host "Validation succeeded. Nothing was pushed."
        Write-Host "Publish with: .\packaging\publish-beta.ps1 -Version $Version -Publish"
        return
    }

    & git push origin main
    if ($LASTEXITCODE -ne 0) { throw 'Unable to push main.' }

    $head = (& git rev-parse HEAD).Trim()
    $existingTagOutput = & git rev-list -n 1 $tag 2>$null
    $existingTag = if ($null -eq $existingTagOutput) { '' } else { ([string]$existingTagOutput).Trim() }
    if ($existingTag) {
        if ($existingTag -ne $head) {
            throw "Tag $tag already exists on another commit."
        }
    }
    else {
        & git tag -a $tag -m "Aeziol $Version"
        if ($LASTEXITCODE -ne 0) { throw "Unable to create tag $tag." }
    }

    & git push origin $tag
    if ($LASTEXITCODE -ne 0) {
        throw "Unable to push $tag. The local tag was preserved so the command can be retried safely."
    }

    Write-Host "Published $tag. GitHub Actions is building the package and creating a draft prerelease."
    Write-Host "https://github.com/GaetanGrd/Aeziol/actions"
}
finally {
    Pop-Location
}
