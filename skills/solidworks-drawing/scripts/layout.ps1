param([Parameter(Mandatory=$true)][string]$Plan,[Parameter(Mandatory=$true)][string]$Python,[Parameter(Mandatory=$true)][string]$InteropPath)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -eq 'Core'){throw 'Use Windows PowerShell 5.1'}
$scripts=if($PSScriptRoot){$PSScriptRoot}else{Join-Path $env:SW_DRAWING_SKILL_ROOT 'scripts'}
$env:SW_DRAWING_SKILL_ROOT=Split-Path -Parent $scripts
$p=Get-Content -Raw -Encoding UTF8 -LiteralPath $Plan | ConvertFrom-Json
$runReport=$p.report+'.run.json'
$prepared=$p.report+'.plan.json'
$audit=$p.report+'.audit.json'
foreach($file in @($p.output,$p.report,$p.pdf,$p.pdf_report,$runReport,$prepared,$audit)){if(Test-Path -LiteralPath $file){throw "Refusing existing output: $file"}}
$record=[ordered]@{status='FAILED';scope='saved native drawing annotation layout, save/reopen, PDF; excludes upstream view design and blind reconstruction'}
$before=$null
try {
 & $Python (Join-Path $scripts 'layout_guard.py') prepare $Plan $prepared
 if($LASTEXITCODE -ne 0){throw 'Layout preflight failed'}
 Add-Type -Path $InteropPath
 if(-not ('NativeDimensionLayout' -as [type])){Add-Type -ReferencedAssemblies @($InteropPath,'System.Web.Extensions.dll') -Path (Join-Path $scripts 'NativeDimensionLayout.cs')}
 if(-not ('DrawingDocuments' -as [type])){Add-Type -ReferencedAssemblies @($InteropPath) -Path (Join-Path $scripts 'DrawingDocuments.cs')}
 $before=[DrawingDocuments]::Titles()
 [NativeDimensionLayout]::Run($prepared)
 & $Python (Join-Path $scripts 'layout_guard.py') audit $prepared $audit
 if($LASTEXITCODE -ne 0){throw 'Reopened annotation audit failed'}
 & ([scriptblock]::Create([IO.File]::ReadAllText((Join-Path $scripts 'export_pdf.ps1')))) -Drawing $p.output -Pdf $p.pdf -Output $p.pdf_report -Python $Python -InteropPath $InteropPath
 $export=Get-Content -Raw -Encoding UTF8 -LiteralPath $p.pdf_report | ConvertFrom-Json
 if($export.status -ne 'COMPOSED_NATIVE_OUTPUT_REVIEW_REQUIRED'){throw 'PDF export failed; inspect preserved report'}
 $record.status='EXPORTED_REQUIRES_VISUAL_REVIEW'
} catch {$record.error=$_.Exception.Message;throw}
finally {
 if($null -ne $before){
  $copy=Join-Path (Split-Path -Parent $p.pdf) (([IO.Path]::GetFileNameWithoutExtension($p.pdf))+'_native_export\native_work.SLDDRW')
  try{[DrawingDocuments]::CloseCompleted(@($p.output,$copy),$p.source,'',$before)}catch{$record.cleanup_error=$_.Exception.Message}
 }
 $record | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $runReport -Encoding UTF8
}
