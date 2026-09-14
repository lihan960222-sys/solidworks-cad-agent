param(
 [ValidateSet('Build','Assemble')][string]$Mode='Build',
 [string]$RunDirectory,
 [string]$InteropDirectory=$env:SW_INTEROP_DIR,
 [string]$PartTemplate=$env:SW_PART_TEMPLATE,
 [string]$AssemblyTemplate=$env:SW_ASSEMBLY_TEMPLATE,
 [string]$Ffmpeg=$env:FFMPEG_EXE,
 [switch]$Execute
)
$ErrorActionPreference='Stop'
if(-not $Execute){'No recording or CAD action. Explicit -Execute and configured paths are required.';return}
if($PSVersionTable.PSEdition -ne 'Desktop'){throw 'Use Windows PowerShell 5.1.'}
if(-not $RunDirectory -or -not(Test-Path -LiteralPath $RunDirectory -PathType Container)){throw 'An existing private RunDirectory is required.'}
if(-not $Ffmpeg -or -not(Test-Path -LiteralPath $Ffmpeg -PathType Leaf)){throw 'A valid FFmpeg executable is required.'}
$runPath=(Resolve-Path -LiteralPath $RunDirectory).Path
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if($runPath -eq $repoPath -or $runPath.StartsWith($repoPath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep private data outside the public repository.'}
if($Mode -eq 'Build'){
 if(-not $PartTemplate -or -not(Test-Path -LiteralPath $PartTemplate -PathType Leaf)){throw 'A valid PartTemplate is required.'}
 $env:SW_PART_TEMPLATE=$PartTemplate
}else{
 if(-not $AssemblyTemplate -or -not(Test-Path -LiteralPath $AssemblyTemplate -PathType Leaf)){throw 'A valid AssemblyTemplate is required.'}
 $env:SW_ASSEMBLY_TEMPLATE=$AssemblyTemplate
}
# Compile before recording; this invocation never connects to SolidWorks.
& (Join-Path $PSScriptRoot 'Invoke-Reference.ps1') -Mode Compile -InteropDirectory $InteropDirectory
foreach($name in @('models','vendor','checks','recordings')){New-Item -ItemType Directory -Path (Join-Path $runPath $name) -Force|Out-Null}
Add-Type -AssemblyName System.Windows.Forms
$bounds=[Windows.Forms.Screen]::PrimaryScreen.Bounds
$video=Join-Path $runPath ('recordings\'+$Mode+'_'+(Get-Date -Format 'yyyyMMdd_HHmmss_fff')+'.mp4')
if(Test-Path -LiteralPath $video){throw 'Recording target exists.'}
$psi=New-Object Diagnostics.ProcessStartInfo
$psi.FileName=$Ffmpeg
$psi.Arguments='-hide_banner -loglevel warning -f gdigrab -framerate 20 -draw_mouse 1 -offset_x '+$bounds.X+' -offset_y '+$bounds.Y+' -video_size '+$bounds.Width+'x'+$bounds.Height+' -i desktop -an -vf "scale=trunc(iw/2)*2:trunc(ih/2)*2" -c:v mpeg4 -q:v 3 -pix_fmt yuv420p -movflags +faststart "'+$video+'"'
$psi.UseShellExecute=$false;$psi.CreateNoWindow=$true;$psi.RedirectStandardInput=$true;$psi.RedirectStandardError=$true
$rec=New-Object Diagnostics.Process;$rec.StartInfo=$psi
$started=$false
try{
 $started=$rec.Start();if(-not $started){throw 'Recorder start failed'}
 Start-Sleep -Milliseconds 500
 if($rec.HasExited){throw $rec.StandardError.ReadToEnd()}
 if($Mode -eq 'Build'){[GripperBuild]::Run($runPath)}else{[GripperAssemble]::Run($runPath)}
}finally{
 if($started){
  if(-not $rec.HasExited){$rec.StandardInput.WriteLine('q');$rec.StandardInput.Flush();if(-not $rec.WaitForExit(15000)){throw 'Recorder finalization timed out; inspect process before retrying.'}}
  $rec.StandardError.ReadToEnd()|Write-Output
 }
}
if($rec.ExitCode -ne 0){throw 'Recorder failed'}
'RECORDED='+$video
