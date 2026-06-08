<#
.SYNOPSIS
    Smoke-checks the running DineOS dev stack (Windows mirror of verify-compose.sh).

.DESCRIPTION
    1. Validates docker compose config.
    2. Parses `docker compose ps --format json` and prints a formatted status table.
    3. Probes health endpoints and prints PASS / FAIL / SKIP per check.

    Exits with code 1 if any required check fails.
    Checks are SKIPPED (not failed) when the target service is not running.

.EXAMPLE
    .\scripts\devops\verify-compose.ps1
#>

$script:failures = 0

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Write-Pass($msg) { Write-Host ("  PASS  {0}" -f $msg) -ForegroundColor Green }
function Write-Fail($msg) { Write-Host ("  FAIL  {0}" -f $msg) -ForegroundColor Red;    $script:failures++ }
function Write-Skip($msg) { Write-Host ("  SKIP  {0}" -f $msg) -ForegroundColor Yellow }

# ── 1. Compose config validation ──────────────────────────────────
Write-Step "1/3  Compose config"
$configOut = docker compose config -q 2>&1
if ($LASTEXITCODE -eq 0) {
    Write-Pass "docker compose config is valid"
} else {
    Write-Fail "docker compose config reported errors — run 'docker compose config' for details"
    if ($configOut) { Write-Host $configOut }
}

# ── 2. Service status table ───────────────────────────────────────
Write-Step "2/3  Service status"

$services = [System.Collections.Generic.List[object]]::new()
$psRaw = docker compose ps --format json 2>$null
if ($psRaw) {
    $joined = ($psRaw -join "`n").Trim()
    if ($joined) {
        try {
            # Newer Compose versions emit a JSON array; handle that and single-object output.
            $parsed = $joined | ConvertFrom-Json
            if ($parsed -is [array]) {
                foreach ($item in $parsed) { $services.Add($item) }
            } elseif ($parsed) {
                $services.Add($parsed)
            }
        } catch {
            # Older Compose versions emit NDJSON (one JSON object per line).
            foreach ($line in $psRaw) {
                $trimmed = $line.Trim()
                if ($trimmed -and $trimmed -match '^\{') {
                    try { $services.Add(($trimmed | ConvertFrom-Json)) }
                    catch { Write-Warning "Could not parse docker compose ps output line: $trimmed" }
                }
            }
        }
    }
}

if ($services.Count -gt 0) {
    Write-Host ("  {0,-22}  {1,-10}  {2,-12}" -f "SERVICE", "STATE", "HEALTH")
    Write-Host ("  {0,-22}  {1,-10}  {2,-12}" -f ("─" * 22), ("─" * 10), ("─" * 12))
    foreach ($s in $services) {
        $health = if ($s.PSObject.Properties['Health'] -and $s.Health) { $s.Health } else { "—" }
        $colour = switch ($s.State) {
            "running"  { if ($health -eq "unhealthy") { "Red" } else { "Green" } }
            "exited"   { "Red" }
            default    { "Yellow" }
        }
        Write-Host ("  {0,-22}  {1,-10}  {2,-12}" -f $s.Service, $s.State, $health) -ForegroundColor $colour
    }
} else {
    Write-Host "  No containers found — is the stack running?" -ForegroundColor Yellow
}

# Names of services that currently have containers (any state)
$knownSvcs = $services | ForEach-Object { $_.Service }
function Test-ServiceRunning([string]$name) { return $knownSvcs -contains $name }

# ── 3. Health endpoint checks ─────────────────────────────────────
Write-Step "3/3  Health checks"

function Invoke-Check([string]$label, [string]$url, [string]$svc) {
    if (-not (Test-ServiceRunning $svc)) {
        Write-Skip "$label  (service '$svc' not running)"
        return
    }
    $code = 0
    try {
        $resp = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 5 -ErrorAction Stop
        $code = [int]$resp.StatusCode
    } catch [System.Net.WebException] {
        if ($_.Exception.Response) { $code = [int]$_.Exception.Response.StatusCode }
    } catch {
        $code = 0
    }
    $display = if ($code -eq 0) { "ERR" } else { [string]$code }
    if ($code -ge 200 -and $code -lt 300) {
        Write-Pass "$label  [$display]  $url"
    } else {
        Write-Fail "$label  [$display]  $url"
    }
}

Invoke-Check "API health (via Nginx)"   "http://localhost/api/v1/health"                                       "nginx"
Invoke-Check "Keycloak OIDC discovery"  "http://localhost:8080/realms/dineos/.well-known/openid-configuration" "keycloak"
Invoke-Check "Loki readiness"           "http://localhost:3100/ready"                                          "loki"
Invoke-Check "Grafana health"           "http://localhost:4000/api/health"                                     "grafana"

# ── Result ────────────────────────────────────────────────────────
Write-Host ""
if ($script:failures -eq 0) {
    Write-Host "All checks passed." -ForegroundColor Green
    exit 0
} else {
    Write-Host "$($script:failures) check(s) failed." -ForegroundColor Red
    exit 1
}
