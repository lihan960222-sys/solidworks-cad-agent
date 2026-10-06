param(
 [Parameter(Mandatory=$true)][string]$PlanDirectory,
 [Parameter(Mandatory=$true)][string]$OutputDirectory,
 [string]$Python='python',
 [Parameter(Mandatory=$true)][string]$InteropPath
)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Use Windows PowerShell 5.1.' }
foreach($p in @($PlanDirectory,$OutputDirectory,$InteropPath)) { if(-not [IO.Path]::IsPathRooted($p)) { throw 'Absolute paths required.' } }
if($PSScriptRoot) { $scripts=$PSScriptRoot }
elseif($env:SW_DRAWING_SKILL_ROOT) { $scripts=Join-Path $env:SW_DRAWING_SKILL_ROOT 'scripts' }
else { throw 'Set SW_DRAWING_SKILL_ROOT for scriptblock invocation.' }
& $Python -c 'import pypdf'
if($LASTEXITCODE -ne 0) { throw 'Python with pypdf required.' }
Add-Type -Path $InteropPath
Add-Type -ReferencedAssemblies @($InteropPath,'System.Web.Extensions.dll','System.Drawing.dll') -Path @((Join-Path $scripts 'DrawingEngine.cs'),(Join-Path $scripts 'InspectGeometry.cs'),(Join-Path $scripts 'PrintPdf.cs'),(Join-Path $scripts 'GeometryPdf.cs'),(Join-Path $scripts 'DrawingDocuments.cs'))
$summary=@()
$prefix=[IO.Path]::GetFullPath($OutputDirectory).TrimEnd('\')+'\'
foreach($file in Get-ChildItem -LiteralPath $PlanDirectory -Filter '*.json' | Sort-Object Name) {
 $plan=Get-Content -Raw -Encoding UTF8 -LiteralPath $file.FullName | ConvertFrom-Json
 $facts=Get-Content -Raw -Encoding UTF8 -LiteralPath $plan.facts | ConvertFrom-Json
 $id=$file.BaseName;$reportPath=Join-Path $OutputDirectory ("reports\$id.json")
 if(Test-Path -LiteralPath $reportPath) { Write-Output "$id SKIPPED_EXISTING_REPORT";continue }
 if(-not [IO.Path]::GetFullPath($plan.output_drawing).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Native output must be within OutputDirectory.' }
 if([IO.Path]::GetFullPath($facts.path).StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Source models must be outside OutputDirectory.' }
 $pdf=Join-Path $OutputDirectory ("pdf\$id.pdf")
 $work=Join-Path $OutputDirectory ("work\$id");$copy=Join-Path $work 'native_work.SLDDRW'
 $before=[DrawingDocuments]::Titles();$created=''
 $entry=[ordered]@{id=$id;plan=$file.FullName;source=$facts.path;native=$plan.output_drawing;pdf='';status='FAILED'}
 try {
  & $Python (Join-Path $scripts 'validate_plan.py') $file.FullName | Out-Null
  if($LASTEXITCODE -ne 0) { throw 'Offline plan validation failed.' }
  foreach($p in @($pdf,$copy)) { if(Test-Path -LiteralPath $p) { throw "Existing output: $p" } }
  $native=[DrawingEngine]::Execute($file.FullName);$entry.native_report=$native;$created=$native.created_document_title
  if($native.status -eq 'FAILED') { throw $native.error }
  $entry.activation=[GeometryPdf]::ActivateSaved($plan.output_drawing)
  $hash=[GeometryPdf]::PrepareCopy($plan.output_drawing,$copy)
  $ann=Join-Path $work 'native_print.pdf';$geo=Join-Path $work 'native_geometry.pdf'
  $entry.print=[PrintPdf]::Run($copy,$ann)
  if($entry.print.status -eq 'PRINT_FAILED') { throw 'Native print channel failed.' }
  $entry.geometry=[GeometryPdf]::Run($copy,$geo)
  $merged=& $Python (Join-Path $scripts 'merge_native_pdf.py') $ann $geo $pdf
  if($LASTEXITCODE -ne 0) { throw 'PDF composition failed.' }
  $entry.pdf_checks=$merged | ConvertFrom-Json
  $entry.source_drawing_unchanged=[GeometryPdf]::HashMatches($plan.output_drawing,$hash)
  if(-not $entry.source_drawing_unchanged) { throw 'Input native drawing changed.' }
  $entry.pdf=$pdf;$entry.status='NATIVE_AND_PDF_REVIEW_REQUIRED'
 } catch { $entry.error=$_.Exception.Message }
 finally {
  try { $entry.closed_documents=[DrawingDocuments]::CloseCompleted(@($plan.output_drawing,$copy),$facts.path,$created,$before) }
  catch { $entry.cleanup_error=$_.Exception.Message }
  [IO.Directory]::CreateDirectory((Split-Path -Parent $reportPath)) | Out-Null
  $entry | ConvertTo-Json -Depth 30 | Set-Content -Encoding UTF8 -LiteralPath $reportPath
  $summary+=,$entry
  $summary | ConvertTo-Json -Depth 30 | Set-Content -Encoding UTF8 -LiteralPath (Join-Path $OutputDirectory 'batch_summary.json')
  [DrawingEngine]::ReleaseSessionReferences();[GC]::Collect();[GC]::WaitForPendingFinalizers();[GC]::Collect()
 }
 Write-Output "$id $($entry.status) CLOSED=$($entry.closed_documents)"
}
