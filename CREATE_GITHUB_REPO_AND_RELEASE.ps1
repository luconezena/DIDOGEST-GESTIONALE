[CmdletBinding()]
param(
    # New repository name on GitHub (e.g. DidoGest)
    [Parameter(Mandatory = $true)]
    [string]$RepoName,

    # Public or private repo
    [ValidateSet('public', 'private')]
    [string]$Visibility = 'public',

    # Optional GitHub owner/org. If omitted, uses authenticated gh user.
    [string]$Owner,

    # Release tag (e.g. v1.0.2). If omitted, inferred from CHANGELOG.md.
    [string]$Tag,

    # Create the release as draft
    [switch]$Draft,

    # Skip rebuilding portable
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

if (-not (Get-Command gh -ErrorAction SilentlyContinue)) {
    throw 'GitHub CLI (gh) non trovato. Installa da https://github.com/cli/cli/releases e riprova.'
}

& gh auth status -h github.com | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Non risulti autenticato su GitHub tramite gh. Esegui: gh auth login (oppure gh auth login --with-token).'
}

if ([string]::IsNullOrWhiteSpace($Owner)) {
    $Owner = (& gh api user -q .login)
    if ([string]::IsNullOrWhiteSpace($Owner)) {
        throw 'Impossibile determinare l\'owner. Passa -Owner "tuoUsername".'
    }
}

$fullRepo = "$Owner/$RepoName"

# Ensure we are in repo root (this script is stored there)
Set-Location -LiteralPath $PSScriptRoot

# Init git if needed
if (-not (Test-Path (Join-Path $PSScriptRoot '.git'))) {
    & git init | Out-Null
}

# Ensure git identity (local repo config only)
$gitName = (& git config user.name)
$gitEmail = (& git config user.email)
if ([string]::IsNullOrWhiteSpace($gitName)) {
    & git config user.name $Owner | Out-Null
}
if ([string]::IsNullOrWhiteSpace($gitEmail)) {
    & git config user.email "$Owner@users.noreply.github.com" | Out-Null
}

# Commit initial state if no commits
$hasCommits = $true
& git rev-parse --verify HEAD 2>$null | Out-Null
if ($LASTEXITCODE -ne 0) { $hasCommits = $false }

if (-not $hasCommits) {
    & git add -A
    & git commit -m "Initial commit" | Out-Null
}

Write-Host "Creo repo GitHub: $fullRepo ($Visibility)" -ForegroundColor Cyan

# Create repo on GitHub and add remote
$visFlag = if ($Visibility -eq 'public') { '--public' } else { '--private' }

# If origin exists, keep it; otherwise create and set it
& git remote get-url origin 2>$null | Out-Null
$hasOrigin = ($LASTEXITCODE -eq 0)

if (-not $hasOrigin) {
    & gh repo create $fullRepo $visFlag --source . --remote origin --push
    if ($LASTEXITCODE -ne 0) {
        throw "Errore durante la creazione/push del repo (gh exit code: $LASTEXITCODE)."
    }
}
else {
    Write-Host "Remote origin già presente: eseguo push" -ForegroundColor Yellow
    & git push -u origin HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "Errore durante il push (git exit code: $LASTEXITCODE)."
    }
}

# Create release + upload portable assets
$publishScript = Join-Path $PSScriptRoot 'PUBLISH_PORTABLE_GITHUB.ps1'
if (-not (Test-Path $publishScript)) {
    throw "Script non trovato: $publishScript"
}

$publishArgs = @('-Repo', $fullRepo)
if ($Draft) { $publishArgs += '-Draft' }
if ($SkipBuild) { $publishArgs += '-SkipBuild' }
if (-not [string]::IsNullOrWhiteSpace($Tag)) { $publishArgs += @('-Tag', $Tag) }

Write-Host "Pubblico la portable come GitHub Release..." -ForegroundColor Cyan
& $publishScript @publishArgs
