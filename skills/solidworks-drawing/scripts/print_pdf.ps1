param(
 [Parameter(Mandatory=$true)][string]$Drawing,
 [Parameter(Mandatory=$true)][string]$Pdf,
 [Parameter(Mandatory=$true)][string]$Output,
 [string]$InteropPath=''
)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Use Windows PowerShell 5.1.' }
foreach ($p in @($Drawing,$Pdf,$Output)) { if (-not [IO.Path]::IsPathRooted($p)) { throw 'Absolute paths required.' } }
if (-not $Drawing.EndsWith('.slddrw',[StringComparison]::OrdinalIgnoreCase) -or -not $Pdf.EndsWith('.pdf',[StringComparison]::OrdinalIgnoreCase)) { throw 'SLDDRW input and PDF output required.' }
foreach ($p in @($Pdf,$Output)) { if (Test-Path -LiteralPath $p) { throw "Refusing existing output: $p" } }
if ($PSScriptRoot) { $scriptDirectory=$PSScriptRoot }
else { if (-not $env:SW_DRAWING_SKILL_ROOT) { throw 'Set SW_DRAWING_SKILL_ROOT for scriptblock invocation.' }; $scriptDirectory=Join-Path $env:SW_DRAWING_SKILL_ROOT 'scripts' }
if (-not $InteropPath) {
 $clsid=(Get-ItemProperty -LiteralPath 'Registry::HKEY_CLASSES_ROOT\SldWorks.Application\CLSID').'(default)'
 $server=(Get-ItemProperty -LiteralPath "Registry::HKEY_CLASSES_ROOT\CLSID\$clsid\LocalServer32").'(default)'
 if ($server -match '^"([^\"]+\.exe)"') { $exe=$Matches[1] }
 elseif ($server -match '^(.+?\.exe)') { $exe=$Matches[1] }
 else { throw 'Supply InteropPath.' }
 $InteropPath=Join-Path (Split-Path -Parent $exe) 'api\redist\SolidWorks.Interop.sldworks.dll'
}
Add-Type -Path $InteropPath
Add-Type -ReferencedAssemblies @($InteropPath,'System.Drawing.dll') -Path (Join-Path $scriptDirectory 'PrintPdf.cs')
$result=[PrintPdf]::Run($Drawing,$Pdf)
[IO.Directory]::CreateDirectory((Split-Path -Parent $Output)) | Out-Null
$result | ConvertTo-Json -Depth 10 | Set-Content -Encoding UTF8 -LiteralPath $Output
Write-Output "REPORT=$Output"
Write-Output "STATUS=$($result.status)"
if ($result.status -eq 'PRINT_FAILED') { exit 1 }
