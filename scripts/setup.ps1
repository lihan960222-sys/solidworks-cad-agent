# Project-only preparation; no SolidWorks install or system/License changes.
[CmdletBinding()]
param(
    [string]$PythonExecutable = '',
    [string]$EnvFile = '',
    [switch]$CheckOnly,
    [switch]$SkipVenv
)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
if (-not $EnvFile) { $EnvFile = Join-Path $projectRoot 'config\solidworks.local.env' }
try {
    if (Get-Command git -ErrorAction SilentlyContinue) { Write-Host '[OK] Git' }
    else { Write-Host '[MISSING] Git - install Git for Windows' }
    if (-not (Test-Path -LiteralPath $EnvFile)) {
        if (-not $CheckOnly -and $EnvFile -eq (Join-Path $projectRoot 'config\solidworks.local.env')) {
            Copy-Item -LiteralPath (Join-Path $projectRoot 'config\solidworks.example.env') -Destination $EnvFile
            Write-Host '[INFO] Created local env template; fill paths before SolidWorks testing'
        } else { Write-Host '[MISSING] Local env file - copy config/solidworks.example.env and fill paths' }
    }
    if (-not $PythonExecutable) { $PythonExecutable = $env:PYTHON_EXECUTABLE }
    if (-not $PythonExecutable -and (Test-Path -LiteralPath $EnvFile)) {
        $settingsLines = @(Get-Content -LiteralPath $EnvFile | Where-Object { $_ -match '^\s*PYTHON_EXECUTABLE\s*=' })
        if ($settingsLines.Count -gt 1) { throw 'Duplicate PYTHON_EXECUTABLE entries' }
        if ($settingsLines.Count -eq 1) { $PythonExecutable = ($settingsLines[0] -split '=', 2)[1].Trim().Trim('"').Trim("'") }
    }
    if (-not $PythonExecutable) {
        $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
        if ($pythonCommand) { $PythonExecutable = $pythonCommand.Source }
    }
    if (-not $PythonExecutable) { throw 'Python missing: install Python 3.12 x64 and pass -PythonExecutable' }
    if (-not [IO.Path]::IsPathRooted($PythonExecutable)) {
        $candidate = Join-Path $projectRoot $PythonExecutable
        if (Test-Path -LiteralPath $candidate -PathType Leaf) { $PythonExecutable = $candidate }
    }
    & $PythonExecutable -c 'import sys,struct; print("[INFO] Python " + sys.version.split()[0]); sys.exit(0 if sys.version_info >= (3,11) and struct.calcsize("P")==8 else 1)'
    if ($LASTEXITCODE -ne 0) { throw 'Project requires Python 3.11+ x64; Python 3.12 x64 is recommended' }
    $projectPython = $PythonExecutable
    if (-not $CheckOnly) {
        if (-not $SkipVenv) {
            $venvPath = Join-Path $projectRoot '.venv'
            & $PythonExecutable -m venv $venvPath
            if ($LASTEXITCODE -ne 0) { throw 'venv creation failed; inspect the error and temporary-directory permissions' }
            $projectPython = Join-Path $venvPath 'Scripts\python.exe'
        }
        & $projectPython -m pip install -r (Join-Path $projectRoot 'requirements.txt')
        if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed; see pip output' }
    }
    Write-Host '[INFO] Install the external MCP outside this repository:'
    Write-Host '  git clone https://github.com/Slacker-LLC/solidworks-mcp <EXTERNAL_MCP_DIR>'
    Write-Host '  <MCP_VENV_PYTHON> -m pip install <EXTERNAL_MCP_DIR>'
    Write-Host '[INFO] Merge the single MCP template into Codex; start SolidWorks before smoke testing.'
    & $projectPython (Join-Path $PSScriptRoot 'check_environment.py') --env-file $EnvFile
    if ($LASTEXITCODE -ne 0) { throw 'Environment checker did not complete' }
    Write-Host '[INFO] Preparation completed; MISSING items are still unresolved. No CAD validation was performed.'
} catch {
    Write-Host ('[FAILED] Setup - ' + $_.Exception.Message)
    exit 1
}
