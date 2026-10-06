param(
    [Parameter(Mandatory=$true)][ValidateSet('Inspect','Visibility','Verify','Execute')][string]$Mode,
    [string]$Source='', [string]$Configuration='', [string]$Plan='',
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$InteropPath=''
)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Use Windows PowerShell 5.1 for SolidWorks COM.' }
if (-not [IO.Path]::IsPathRooted($Output)) { throw 'Output must be absolute.' }
if (Test-Path -LiteralPath $Output) { throw 'Refusing an existing facts/report path.' }
# PSScriptRoot is unavailable when executing a scriptblock; preserve the actual script path.
if (-not $PSScriptRoot) {
    if (-not $env:SW_DRAWING_SKILL_ROOT) { throw 'For scriptblock invocation set SW_DRAWING_SKILL_ROOT to the skill directory.' }
    $scriptDirectory=Join-Path $env:SW_DRAWING_SKILL_ROOT 'scripts'
} else { $scriptDirectory=$PSScriptRoot }
if (-not $InteropPath) {
    $clsid=(Get-ItemProperty -LiteralPath 'Registry::HKEY_CLASSES_ROOT\SldWorks.Application\CLSID').'(default)'
    $server=(Get-ItemProperty -LiteralPath "Registry::HKEY_CLASSES_ROOT\CLSID\$clsid\LocalServer32").'(default)'
    if ($server -match '^"([^\"]+\.exe)"') { $exe=$Matches[1] }
    elseif ($server -match '^(.+?\.exe)') { $exe=$Matches[1] }
    else { throw 'Cannot locate registered SolidWorks executable; supply InteropPath.' }
    $InteropPath=Join-Path (Split-Path -Parent $exe) 'api\redist\SolidWorks.Interop.sldworks.dll'
}
if (-not (Test-Path -LiteralPath $InteropPath)) { throw 'Installed SolidWorks interop DLL missing.' }
Add-Type -Path $InteropPath
Add-Type -ReferencedAssemblies @($InteropPath,'System.Web.Extensions.dll') -Path @((Join-Path $scriptDirectory 'DrawingEngine.cs'),(Join-Path $scriptDirectory 'InspectGeometry.cs'))
$result=$null
try {
    if ($Mode -eq 'Inspect') { $result=[DrawingEngine]::Inspect($Source,$Configuration) }
    else {
        if (-not [IO.Path]::IsPathRooted($Plan)) { throw 'Plan must be absolute.' }
        if ($Mode -eq 'Verify') { $result=[DrawingEngine]::VerifySaved($Plan) }
        elseif ($Mode -eq 'Visibility') { $result=[DrawingEngine]::Visibility($Plan) }
        else { $result=[DrawingEngine]::Execute($Plan) }
    }
} finally {
    if ($result) {
        [IO.Directory]::CreateDirectory((Split-Path -Parent $Output)) | Out-Null
        $result | ConvertTo-Json -Depth 100 | Set-Content -Encoding UTF8 -LiteralPath $Output
        Write-Output "REPORT=$Output"
        Write-Output "STATUS=$($result.status)"
        if ($result.status -eq 'FAILED') { Write-Output "ERROR=$($result.error)" }
    }
}
if ($result.status -eq 'FAILED') { exit 1 }
