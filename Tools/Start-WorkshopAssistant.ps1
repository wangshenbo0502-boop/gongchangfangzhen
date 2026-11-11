param([switch]$CheckOnly)
$ErrorActionPreference = 'Stop'
$serviceDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../Services/WorkshopAssistant'))
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker Desktop is required.'
}
Push-Location -LiteralPath $serviceDirectory
try {
    if (-not (Test-Path -LiteralPath '.env')) {
        throw 'Create Services/WorkshopAssistant/.env from .env.example and configure the credentials.'
    }
    & docker info --format '{{.ServerVersion}}' 2>$null
    if ($LASTEXITCODE -ne 0) {
        $desktop = Join-Path $env:ProgramFiles 'Docker/Docker/Docker Desktop.exe'
        if (-not (Test-Path -LiteralPath $desktop)) { throw 'Start Docker Desktop first.' }
        Start-Process -FilePath $desktop -WindowStyle Hidden
        $deadline = (Get-Date).AddSeconds(90)
        do {
            Start-Sleep -Seconds 3
            & docker info --format '{{.ServerVersion}}' 2>$null
            if ($LASTEXITCODE -eq 0) { break }
        } while ((Get-Date) -lt $deadline)
        if ($LASTEXITCODE -ne 0) { throw 'Docker engine did not become ready. Check Docker Desktop / WSL.' }
    }
    if (-not $CheckOnly) {
        & docker compose up -d --build
        if ($LASTEXITCODE -ne 0) { throw 'Docker Compose failed.' }
    }
    & docker compose ps
    try {
        Invoke-RestMethod -Uri 'http://127.0.0.1:8018/health' -TimeoutSec 8
    } catch {
        Write-Host 'Service is still starting. First startup downloads the local Chinese embedding model.'
    }
} finally {
    Pop-Location
}
