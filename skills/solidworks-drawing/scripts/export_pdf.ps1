param(
 [Parameter(Mandatory=$true)][string]$Drawing,
 [Parameter(Mandatory=$true)][string]$Pdf,
 [Parameter(Mandatory=$true)][string]$Output,
 [string]$Python='python',
 [string]$InteropPath=''
)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Use Windows PowerShell 5.1.' }
foreach ($p in @($Drawing,$Pdf,$Output)) { if (-not [IO.Path]::IsPathRooted($p)) { throw 'Absolute paths required.' } }
if (-not $Drawing.EndsWith('.slddrw',[StringComparison]::OrdinalIgnoreCase) -or -not $Pdf.EndsWith('.pdf',[StringComparison]::OrdinalIgnoreCase)) { throw 'SLDDRW input and PDF output required.' }
$work=Join-Path (Split-Path -Parent $Pdf) ([IO.Path]::GetFileNameWithoutExtension($Pdf)+'_native_export')
foreach ($p in @($Pdf,$Output,$work)) { if (Test-Path -LiteralPath $p) { throw "Refusing existing output: $p" } }
if ($PSScriptRoot) { $scriptDirectory=$PSScriptRoot }
else { if (-not $env:SW_DRAWING_SKILL_ROOT) { throw 'Set SW_DRAWING_SKILL_ROOT for scriptblock invocation.' }; $scriptDirectory=Join-Path $env:SW_DRAWING_SKILL_ROOT 'scripts' }
# Verify Python and the composition dependency before CAD mutations.
& $Python -c 'import pypdf'
if ($LASTEXITCODE -ne 0) { throw 'Python with pypdf required.' }
if (-not $InteropPath) {
 $clsid=(Get-ItemProperty -LiteralPath 'Registry::HKEY_CLASSES_ROOT\SldWorks.Application\CLSID').'(default)'
 $server=(Get-ItemProperty -LiteralPath "Registry::HKEY_CLASSES_ROOT\CLSID\$clsid\LocalServer32").'(default)'
 if ($server -match '^"([^\"]+\.exe)"') { $exe=$Matches[1] }
 elseif ($server -match '^(.+?\.exe)') { $exe=$Matches[1] }
 else { throw 'Supply InteropPath.' }
 $InteropPath=Join-Path (Split-Path -Parent $exe) 'api\redist\SolidWorks.Interop.sldworks.dll'
}
Add-Type -Path $InteropPath
Add-Type -ReferencedAssemblies @($InteropPath,'System.Drawing.dll') -Path @((Join-Path $scriptDirectory 'PrintPdf.cs'),(Join-Path $scriptDirectory 'GeometryPdf.cs'))
$copy=Join-Path $work 'native_work.SLDDRW'
$report=[ordered]@{status='FAILED';method='COMPOSED_NATIVE_CHANNELS';input=$Drawing;pdf=$Pdf;working_drawing=$copy;manual_visual_review_required=$true}
$prepared=$false
try {
 $report.activation=[GeometryPdf]::ActivateSaved($Drawing)
 $hash=[GeometryPdf]::PrepareCopy($Drawing,$copy);$prepared=$true
 $annotationPdf=Join-Path $work 'native_print.pdf';$geometryPdf=Join-Path $work 'native_geometry.pdf'
 $report.print=[PrintPdf]::Run($copy,$annotationPdf)
 if ($report.print.status -eq 'PRINT_FAILED') { throw 'Native printing failed.' }
 $report.geometry=[GeometryPdf]::Run($copy,$geometryPdf)
 $merged=& $Python (Join-Path $scriptDirectory 'merge_native_pdf.py') $annotationPdf $geometryPdf $Pdf
 if ($LASTEXITCODE -ne 0) { throw 'Native PDF composition failed.' }
 $report.pdf_checks=($merged | ConvertFrom-Json)
 $report.original_drawing_hash_unchanged=[GeometryPdf]::HashMatches($Drawing,$hash)
 if (-not $report.original_drawing_hash_unchanged) { throw 'Input drawing changed.' }
 $report.status='COMPOSED_NATIVE_OUTPUT_REVIEW_REQUIRED'
} catch { $report.error=$_.Exception.Message }
finally {
 if ($prepared) { try { [GeometryPdf]::ReopenCopy($copy);$report.working_drawing_reopened=$true } catch { $report.reopen_error=$_.Exception.Message;$report.status='FAILED' } }
 [IO.Directory]::CreateDirectory((Split-Path -Parent $Output)) | Out-Null
 $report | ConvertTo-Json -Depth 20 | Set-Content -Encoding UTF8 -LiteralPath $Output
 Write-Output "REPORT=$Output"; Write-Output "STATUS=$($report.status)"
}
if ($report.status -eq 'FAILED') { throw $report.error }
