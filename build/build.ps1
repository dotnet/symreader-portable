[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $solution = "",
  [string] $verbosity = "minimal",
  [switch] $restore,
  [switch] $build,
  [switch] $rebuild,
  [switch] $test,
  [switch] $sign,
  [switch] $pack,
  [switch] $ci,
  [switch] $clearCaches,
  [Parameter(ValueFromRemainingArguments=$true)][String[]]$properties
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"

$RepoRoot = Join-Path $PSScriptRoot "..\"
$DotNetRoot = Join-Path $RepoRoot ".dotnet"
$DotNetExe = Join-Path $DotNetRoot "dotnet.exe"
$BuildProj = Join-Path $PSScriptRoot "build.proj"
$DependenciesProps = Join-Path $PSScriptRoot "Versions.props"
$ArtifactsDir = Join-Path $RepoRoot "artifacts"
$LogDir = Join-Path (Join-Path $ArtifactsDir $configuration) "log"
$TempDir = Join-Path (Join-Path $ArtifactsDir $configuration) "tmp"

function Create-Directory([string[]] $path) {
  if (!(Test-Path -path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function GetDotNetCliVersion {
  [xml]$xml = Get-Content $DependenciesProps
  return $xml.Project.PropertyGroup.DotNetCliVersion
}

function InstallDotNetCli {
  
  Create-Directory $DotNetRoot
  $dotnetCliVersion = GetDotNetCliVersion

  $installScript="https://raw.githubusercontent.com/dotnet/cli/release/2.0.0/scripts/obtain/dotnet-install.ps1"
  Invoke-WebRequest $installScript -OutFile "$DotNetRoot\dotnet-install.ps1"
  
  & "$DotNetRoot\dotnet-install.ps1" -Version $dotnetCliVersion -InstallDir $DotNetRoot
  if ($lastExitCode -ne 0) {
    throw "Failed to install dotnet cli (exit code '$lastExitCode')."
  }
}

function Build {  
  if ($ci) {
    Create-Directory($logDir)
    $binlog = "/bl:" + (Join-Path $LogDir "Build.binlog")
  } else {
    $binlog = ""
  }
  
  & $DotNetExe msbuild $BuildProj /m /v:$verbosity $binlog /p:Configuration=$configuration /p:SolutionPath=$solution /p:Restore=$restore /p:Build=$build /p:Rebuild=$rebuild /p:Test=$test /p:Sign=$sign /p:Pack=$pack /p:CIBuild=$ci $properties

  if ($lastExitCode -ne 0) {
    throw "Build failed (exit code '$lastExitCode')."
  }
}

if ($ci) {
  Create-Directory $TempDir
  $env:TEMP = $TempDir
  $env:TMP = $TempDir
}

# clean nuget packages -- necessary to avoid mismatching versions of swix microbuild build plugin and VSSDK on Jenkins
$nugetRoot = (Join-Path $env:USERPROFILE ".nuget\packages")
if ($clearCaches -and (Test-Path $nugetRoot)) {
  Remove-Item $nugetRoot -Recurse -Force
}

if ($restore) {
  InstallDotNetCli
}

Build
