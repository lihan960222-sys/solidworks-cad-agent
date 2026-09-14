param(
 [ValidateSet('Compile','ExtractAssembly','PrepareMates','Build','CopyVendor','Assemble','CheckParts','CheckAssembly','CheckSourceAssembly','ProbeDirections')]
 [string]$Mode='Compile',
 [string]$RunDirectory,
 [string]$ExpectedSourcePath,
 [string]$InteropDirectory=$env:SW_INTEROP_DIR,
 [string]$PartTemplate=$env:SW_PART_TEMPLATE,
 [string]$AssemblyTemplate=$env:SW_ASSEMBLY_TEMPLATE,
 [switch]$Execute,
 [switch]$AllowSourceRead
)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -ne 'Desktop'){throw 'Use Windows PowerShell 5.1 (Desktop), not PowerShell 7.'}
if(-not $InteropDirectory){throw 'Set -InteropDirectory or SW_INTEROP_DIR to the installed SolidWorks api/redist folder.'}
$lib=Join-Path $InteropDirectory 'SolidWorks.Interop.sldworks.dll'
if(-not(Test-Path -LiteralPath $lib -PathType Leaf)){throw 'SolidWorks interop DLL not found.'}
Add-Type -Path $lib
Add-Type -Path (Join-Path $InteropDirectory 'SolidWorks.Interop.swconst.dll')
Add-Type -AssemblyName System.Web.Extensions
$sources=@('GripperBuild.cs','GripperAssemble.cs','GripperPrepare.cs','GripperValidate.cs','GripperAssemblyCheck.cs','DirectionProbe.cs','AssemblyExtract.cs')
foreach($name in $sources){Add-Type -ReferencedAssemblies @($lib,(Join-Path $InteropDirectory 'SolidWorks.Interop.swconst.dll'),'System.Web.Extensions') -Path (Join-Path $PSScriptRoot $name)}
if(-not $Execute -or $Mode -eq 'Compile'){'Compiled seven reference classes; no SolidWorks connection or model operation was performed.';return}
if(-not $RunDirectory -or -not(Test-Path -LiteralPath $RunDirectory -PathType Container)){throw 'Execute requires an existing private -RunDirectory.'}
$runPath=(Resolve-Path -LiteralPath $RunDirectory).Path
$repoPath=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\..'))
if($runPath -eq $repoPath -or $runPath.StartsWith($repoPath+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'Keep execution inputs/outputs outside the public repository.'}
if($Mode -in @('ExtractAssembly','PrepareMates','CopyVendor','CheckParts','CheckSourceAssembly','ProbeDirections') -and -not $AllowSourceRead){throw 'This mode can read original models. Explicit -AllowSourceRead is required; never use it in blind modeling.'}
if($Mode -eq 'Build'){
 if(-not $PartTemplate -or -not(Test-Path -LiteralPath $PartTemplate -PathType Leaf)){throw 'A valid -PartTemplate is required.'}
 $env:SW_PART_TEMPLATE=$PartTemplate
}
if($Mode -eq 'Assemble'){
 if(-not $AssemblyTemplate -or -not(Test-Path -LiteralPath $AssemblyTemplate -PathType Leaf)){throw 'A valid -AssemblyTemplate is required.'}
 $env:SW_ASSEMBLY_TEMPLATE=$AssemblyTemplate
}
foreach($name in @('inputs','models','vendor','checks','recordings')){New-Item -ItemType Directory -Path (Join-Path $runPath $name) -Force|Out-Null}
switch($Mode){
 'ExtractAssembly' {
  if(-not $ExpectedSourcePath -or -not(Test-Path -LiteralPath $ExpectedSourcePath -PathType Leaf)){throw 'ExtractAssembly requires -ExpectedSourcePath matching the active assembly.'}
  $data=[AssemblyExtract]::Run((Resolve-Path -LiteralPath $ExpectedSourcePath).Path)
  ConvertTo-Json -InputObject $data -Depth 30|Set-Content -LiteralPath (Join-Path $runPath 'inputs\assembly_original.json') -Encoding UTF8
 }
 'PrepareMates' {
  $data=Get-Content -LiteralPath (Join-Path $runPath 'inputs\assembly_original.json') -Raw -Encoding UTF8|ConvertFrom-Json
  $rows=[GripperPrepare]::Mates($data.source,(Join-Path $runPath 'inputs'))
  ConvertTo-Json -InputObject $rows -Depth 25|Set-Content -LiteralPath (Join-Path $runPath 'inputs\mate_specifications.json') -Encoding UTF8
 }
 'Build' {[GripperBuild]::Run($runPath)}
 'CopyVendor' {[GripperAssemble]::Relink($runPath)}
 'Assemble' {[GripperAssemble]::Run($runPath)}
 'CheckParts' {[GripperValidate]::Parts($runPath)}
 'CheckAssembly' {[GripperAssemblyCheck]::Run($runPath,'built')}
 'CheckSourceAssembly' {[GripperAssemblyCheck]::Run($runPath,'source')}
 'ProbeDirections' {[DirectionProbe]::Run($runPath)}
}
