# download-whisper-model.ps1
# Download the Whisper ggml model into the publish layout so it ships with the app.
# Run ONCE on a machine that can reach HuggingFace (or a working mirror).
# Usage:  pwsh Scripts/download-whisper-model.ps1
#   (or right-click the file -> "Run with PowerShell")

$ErrorActionPreference = 'Stop'

$modelName   = 'ggml-small-q5_0.bin'   # recommended: size/accuracy trade-off for Chinese
$destRelPath = 'ClassIsland\Assets\VoiceWake\Models\ggml-model.bin'

# Source list (first reachable wins). Add/remove mirrors as needed.
$sources = @(
    "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/$modelName",
    "https://hf-mirror.com/ggerganov/whisper.cpp/resolve/main/$modelName"
)

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot '..')
$dest     = Join-Path $repoRoot $destRelPath
$tmp      = "$dest.part"

New-Item -ItemType Directory -Force -Path (Split-Path $dest) | Out-Null

$ok = $false
foreach ($url in $sources) {
    try {
        Write-Host "Trying: $url"
        Invoke-WebRequest -Uri $url -OutFile $tmp -UseBasicParsing -TimeoutSec 1800
        if (Test-Path $tmp -and (Get-Item $tmp).Length -gt 1MB) {
            Move-Item -Force $tmp $dest
            $ok = $true
            Write-Host ("Downloaded model -> {0} ({1} MB)" -f $dest, [math]::Round((Get-Item $dest).Length/1MB, 1))
            break
        }
    }
    catch {
        Write-Warning "Failed: $_"
    }
    finally {
        if (Test-Path $tmp) { Remove-Item -Force $tmp -ErrorAction SilentlyContinue }
    }
}

if (-not $ok) {
    Write-Error ("All sources failed. If your network blocks HuggingFace, use a proxy/VPN, or download '{0}' manually and place it at:`n{1}" -f $modelName, $dest)
    exit 1
}
