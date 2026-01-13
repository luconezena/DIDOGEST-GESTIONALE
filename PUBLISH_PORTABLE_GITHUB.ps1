[CmdletBinding()]
param(
    # Repository in the form "owner/repo".
    [Parameter(Mandatory = $true)]
    [string]$Repo,

    # Tag to use for the release (e.g. v1.0.2). If omitted, it is inferred from CHANGELOG.md.
    [string]$Tag,

    # Release title. If omitted, it is inferred from CHANGELOG.md.
    [string]$Title,

    # Create as a draft release.
    [switch]$Draft,

    # Create as a pre-release.
    [switch]$Prerelease,

    # Skip running MAKE_PORTABLE.ps1 (assumes portable ZIP already exists).
    [switch]$SkipBuild,

    # Path of the portable zip produced by MAKE_PORTABLE.ps1.
    [string]$ZipPath = (Join-Path $PSScriptRoot 'portable\dist\DidoGest-Portable.zip')
)

$ErrorActionPreference = 'Stop'

function Get-LatestVersionFromChangelog {
    param([Parameter(Mandatory = $true)][string]$ChangelogPath)

    if (-not (Test-Path $ChangelogPath)) {
        throw "Changelog non trovato: $ChangelogPath"
    }

    $content = Get-Content -LiteralPath $ChangelogPath -Raw
    $m = [regex]::Match($content, "(?m)^## \[(?<ver>[^\]]+)\]\s*-\s*.*$")
    if (-not $m.Success) {
        throw "Impossibile determinare la versione da CHANGELOG.md (header '## [x.y.z] - ...')"
    }

    return $m.Groups['ver'].Value.Trim()
}

function Extract-ChangelogSection {
    param(
        [Parameter(Mandatory = $true)][string]$ChangelogPath,
        [Parameter(Mandatory = $true)][string]$Version
    )

    $lines = Get-Content -LiteralPath $ChangelogPath
    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "^## \[$([regex]::Escape($Version))\]\b") {
            $start = $i
            break
        }
    }

    if ($start -lt 0) {
        return $null
    }

    $end = $lines.Count
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match '^## \[') {
            $end = $i
            break
        }
    }

    # Drop the header line itself; keep the section body.
    $body = $lines[($start + 1)..($end - 1)]
    return ($body -join "`r`n").Trim()
}

function Get-AppDescriptionFromReadme {
    param([Parameter(Mandatory = $true)][string]$ReadmePath)

    if (-not (Test-Path $ReadmePath)) {
        return $null
    }

    $lines = Get-Content -LiteralPath $ReadmePath

    # Heuristic: take the first meaningful paragraph after the initial headings.
    $started = $false
    $paragraph = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        $t = $line.Trim()

        if (-not $started) {
            if ([string]::IsNullOrWhiteSpace($t)) { continue }
            if ($t -like '#*') { continue }
            if ($t -match '^##\s+') { continue }
            # first non-heading, non-empty line
            $started = $true
        }

        if ($started) {
            if ([string]::IsNullOrWhiteSpace($t)) { break }
            $paragraph.Add($line)
        }
    }

    if ($paragraph.Count -eq 0) {
        return $null
    }

    return ($paragraph -join "`r`n").Trim()
}

$changelogPath = Join-Path $PSScriptRoot 'CHANGELOG.md'
$version = Get-LatestVersionFromChangelog -ChangelogPath $changelogPath
$readmePath = Join-Path $PSScriptRoot 'README.md'
$appDescription = Get-AppDescriptionFromReadme -ReadmePath $readmePath

if ([string]::IsNullOrWhiteSpace($Tag)) {
    $Tag = "v$version"
}

if ([string]::IsNullOrWhiteSpace($Title)) {
    $Title = "DidoGest $version (portable)"
}

if (-not $SkipBuild) {
    $makePortable = Join-Path $PSScriptRoot 'MAKE_PORTABLE.ps1'
    if (-not (Test-Path $makePortable)) {
        throw "Script non trovato: $makePortable"
    }
    Write-Host "Rigenero portable tramite MAKE_PORTABLE.ps1..." -ForegroundColor Cyan
    & $makePortable
}

if (-not (Test-Path $ZipPath)) {
    throw "ZIP portable non trovato: $ZipPath"
}

$distRoot = Join-Path $PSScriptRoot 'portable\dist'
New-Item -ItemType Directory -Force -Path $distRoot | Out-Null

$versionedZip = Join-Path $distRoot ("DidoGest-Portable-$version-win-x64.zip")
Copy-Item -Force -LiteralPath $ZipPath -Destination $versionedZip

$hash = Get-FileHash -Algorithm SHA256 -LiteralPath $versionedZip
$shaPath = "$versionedZip.sha256"
"$($hash.Hash.ToLowerInvariant())  $([IO.Path]::GetFileName($versionedZip))" | Out-File -LiteralPath $shaPath -Encoding ascii -Force

$notesBody = Extract-ChangelogSection -ChangelogPath $changelogPath -Version $version
$notesPath = Join-Path $distRoot ("RELEASE_NOTES_$version.md")

$notesHeader = @(
    "# $Title",
    "",
    "- **Data**: $(Get-Date -Format 'yyyy-MM-dd')",
    "- **Artefatto**: $([IO.Path]::GetFileName($versionedZip))",
    ""
) -join "`r`n"

$descBlock = ''
if (-not [string]::IsNullOrWhiteSpace($appDescription)) {
    $descBlock = @(
        "## Descrizione",
        $appDescription.Trim(),
        ""
    ) -join "`r`n"
}

if ([string]::IsNullOrWhiteSpace($notesBody)) {
    $notesBody = "(Nessuna sezione trovata in CHANGELOG.md per $version)"
}

("$notesHeader`r`n`r`n$descBlock## Note`r`n`r`n$notesBody".Trim() + "`r`n") | Out-File -LiteralPath $notesPath -Encoding utf8 -Force

# Ensure gh is available
if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw "GitHub CLI (gh) non trovato. Installa da https://github.com/cli/cli/releases e riprova."
}

# Ensure gh is authenticated
& gh auth status -h github.com | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Non risulti autenticato su GitHub tramite gh. Esegui: gh auth login (oppure usa un token con: gh auth login --with-token)"
}

Write-Host "Creo la release su GitHub: $Repo ($Tag)" -ForegroundColor Cyan

& gh release view $Tag --repo $Repo | Out-Null
$releaseExists = ($LASTEXITCODE -eq 0)

if ($releaseExists) {
    Write-Host "Release già esistente: upload asset (clobber)" -ForegroundColor Yellow
    & gh release upload $Tag $versionedZip $shaPath --repo $Repo --clobber
    if ($LASTEXITCODE -ne 0) {
        throw "Errore durante l'upload degli asset (gh exit code: $LASTEXITCODE)."
    }
}
else {
    $ghArgs = @('release', 'create', $Tag, $versionedZip, $shaPath, '--repo', $Repo, '--title', $Title, '--notes-file', $notesPath)
    if ($Draft) { $ghArgs += '--draft' }
    if ($Prerelease) { $ghArgs += '--prerelease' }

    & gh @ghArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Errore durante la creazione della release (gh exit code: $LASTEXITCODE)."
    }
}

Write-Host "OK - Release creata e asset caricati." -ForegroundColor Green
Write-Host "Asset: $versionedZip" -ForegroundColor Green
Write-Host "SHA : $shaPath" -ForegroundColor Green
Write-Host "Note: $notesPath" -ForegroundColor Green
