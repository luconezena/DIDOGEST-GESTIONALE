[CmdletBinding()]
param(
    # New repository name on GitHub (e.g. DidoGest)
    [Parameter(Mandatory = $true)]
    [string]$RepoName,

    # Optional repository description (shown on GitHub). If omitted, it is inferred from README.md.
    [string]$RepoDescription,

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
        throw "Impossibile determinare l'owner. Passa -Owner 'tuoUsername'."
    }
}

function Get-AppDescriptionFromReadme {
    param([Parameter(Mandatory = $true)][string]$ReadmePath)

    if (-not (Test-Path $ReadmePath)) {
        return $null
    }

    $lines = Get-Content -LiteralPath $ReadmePath -Encoding utf8
    $started = $false
    $paragraph = New-Object System.Collections.Generic.List[string]
    foreach ($line in $lines) {
        $t = $line.Trim()

        if (-not $started) {
            if ([string]::IsNullOrWhiteSpace($t)) { continue }
            if ($t -like '#*') { continue }
            if ($t -match '^##\s+') { continue }
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

    return ($paragraph -join " ").Trim()
}

function To-GitHubRepoSlug {
    param([Parameter(Mandatory = $true)][string]$Name)

    # GitHub repo name rules (practical): allow letters, digits, '.', '-', '_'. No spaces.
    $slug = $Name.Trim()
    $slug = [regex]::Replace($slug, '\s+', '-')
    $slug = [regex]::Replace($slug, '[^A-Za-z0-9._-]', '-')
    $slug = [regex]::Replace($slug, '-{2,}', '-')
    $slug = $slug.Trim('-')

    if ([string]::IsNullOrWhiteSpace($slug)) {
        throw "Nome repo non valido dopo sanitizzazione: '$Name'"
    }
    return $slug
}

$repoSlug = To-GitHubRepoSlug -Name $RepoName
$fullRepo = "$Owner/$repoSlug"

if ([string]::IsNullOrWhiteSpace($RepoDescription)) {
    $RepoDescription = Get-AppDescriptionFromReadme -ReadmePath (Join-Path $PSScriptRoot 'README.md')
}

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
$remotes = @(& git remote)
$hasOrigin = ($remotes -contains 'origin')

if (-not $hasOrigin) {
    # If the repo already exists (created manually), just add origin and push.
    & gh repo view $fullRepo --json name -q .name 2>$null | Out-Null
    $repoExists = ($LASTEXITCODE -eq 0)

    if ($repoExists) {
        Write-Host "Repo già esistente su GitHub: aggiungo origin e pusho" -ForegroundColor Yellow
        & git remote add origin "https://github.com/$fullRepo.git" 2>$null | Out-Null
        & git push -u origin HEAD
        if ($LASTEXITCODE -ne 0) {
            throw "Errore durante il push verso origin (git exit code: $LASTEXITCODE)."
        }
    }
    else {
        $createOut = & gh repo create $fullRepo $visFlag --source . --remote origin --push 2>&1
        if ($LASTEXITCODE -ne 0) {
            $msg = ($createOut | Out-String).Trim()
            if ($msg -match 'Resource not accessible by personal access token\s*\(createRepository\)') {
                throw "Il token attuale non ha i permessi per CREARE un nuovo repository (createRepository). Se stai usando un token fine-grained (inizia con 'github_pat_'), crea un token CLASSIC (inizia con 'ghp_') con scope 'repo' oppure fai login OAuth con: gh auth login --web. In alternativa crea il repo manualmente su GitHub (nome: $repoSlug) e rilancia lo script.\nDettaglio: $msg"
            }
            throw "Errore durante la creazione/push del repo (gh exit code: $LASTEXITCODE).\nDettaglio: $msg"
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($RepoDescription)) {
        try {
            & gh repo edit $fullRepo --description $RepoDescription | Out-Null
        }
        catch {
            Write-Host "WARN: impossibile impostare la descrizione del repo (permessi token insufficienti)." -ForegroundColor Yellow
        }
    }
}
else {
    Write-Host "Remote origin già presente: eseguo push" -ForegroundColor Yellow
    & git push -u origin HEAD
    if ($LASTEXITCODE -ne 0) {
        throw "Errore durante il push (git exit code: $LASTEXITCODE)."
    }

    if (-not [string]::IsNullOrWhiteSpace($RepoDescription)) {
        try {
            & gh repo edit $fullRepo --description $RepoDescription | Out-Null
        }
        catch {
            Write-Host "WARN: impossibile impostare la descrizione del repo (permessi token insufficienti)." -ForegroundColor Yellow
        }
    }
}

# Create release + upload portable assets
$publishScript = Join-Path $PSScriptRoot 'PUBLISH_PORTABLE_GITHUB.ps1'
if (-not (Test-Path $publishScript)) {
    throw "Script non trovato: $publishScript"
}

Write-Host "Pubblico la portable come GitHub Release..." -ForegroundColor Cyan
if ($Draft) {
    if ($SkipBuild) {
        if (-not [string]::IsNullOrWhiteSpace($Tag)) { & $publishScript -Repo $fullRepo -Tag $Tag -Draft -SkipBuild }
        else { & $publishScript -Repo $fullRepo -Draft -SkipBuild }
    }
    else {
        if (-not [string]::IsNullOrWhiteSpace($Tag)) { & $publishScript -Repo $fullRepo -Tag $Tag -Draft }
        else { & $publishScript -Repo $fullRepo -Draft }
    }
}
else {
    if ($SkipBuild) {
        if (-not [string]::IsNullOrWhiteSpace($Tag)) { & $publishScript -Repo $fullRepo -Tag $Tag -SkipBuild }
        else { & $publishScript -Repo $fullRepo -SkipBuild }
    }
    else {
        if (-not [string]::IsNullOrWhiteSpace($Tag)) { & $publishScript -Repo $fullRepo -Tag $Tag }
        else { & $publishScript -Repo $fullRepo }
    }
}
