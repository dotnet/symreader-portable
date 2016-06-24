[CmdletBinding(PositionalBinding=$false)]
Param(
  [string] $configuration = "Debug",
  [string] $nugetVersion = "3.5.0-beta",
  [string] $testFilter = "*.UnitTests.dll",
  [string] $xUnitVersion = "2.1.0"
)

function Create-Directory([string[]] $path) {
  if (!(Test-Path -path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function Locate-ArtifactsPath {
  $rootPath = Locate-RootPath
  $artifactsPath = Join-Path -path $rootPath -ChildPath "artifacts\"

  Create-Directory -path $artifactsPath
  return Resolve-Path -path $artifactsPath
}

function Locate-PackagesPath {
  if ($env:NUGET_PACKAGES -eq $null) {
    $env:NUGET_PACKAGES =  Join-Path -path $env:UserProfile -childPath ".nuget\packages\"
  }

  $packagesPath = $env:NUGET_PACKAGES

  Create-Directory -path $packagesPath
  return Resolve-Path -path $packagesPath
}

function Locate-RootPath {
  $scriptPath = Locate-ScriptPath
  $rootPath = Join-Path -path $scriptPath -childPath "..\..\..\"
  return Resolve-Path -path $rootPath
}

function Locate-ScriptPath {
  $myInvocation = Get-Variable -name "MyInvocation" -scope "Script"
  $scriptPath = Split-Path -path $myInvocation.Value.MyCommand.Definition -parent
  return Resolve-Path -path $scriptPath
}

function Locate-xUnit-x86 {
  $xUnitPath = Locate-xUnitPath
  $xUnit = Join-Path -path $xUnitPath -childPath "xunit.console.x86.exe"

  if (!(Test-Path -path $xUnit)) {
    throw "The specified xUnit version ($xUnitVersion) could not be located."
  }

  return Resolve-Path -path $xUnit
}

function Locate-xUnit-x64 {
  $xUnitPath = Locate-xUnitPath
  $xUnit = Join-Path -path $xUnitPath -childPath "xunit.console.exe"

  if (!(Test-Path -path $xUnit)) {
    throw "The specified xUnit version ($xUnitVersion) could not be located."
  }

  return Resolve-Path -path $xUnit
}

function Locate-xUnitPath {
  $packagesPath = Locate-PackagesPath
  $xUnitPath = Join-Path -path $packagesPath -childPath "xunit.runner.console\$xUnitVersion\tools\"

  Create-Directory -path $xUnitPath
  return Resolve-Path -path $xUnitPath
}

function Locate-xUnitLogPath {
  $xUnitLogPath = "Binaries\$configuration\xUnitTestResults"

  Create-Directory -path $xUnitLogPath
  return Resolve-Path -path $xUnitLogPath
}

function Locate-xUnitTestBinaries {
  $xUnitLogPath = Join-Path "Binaries" $configuration
  $testBinaries = Get-ChildItem -path $binariesPath -filter $testFilter -recurse -force

  $xUnitTestBinaries = @()

  foreach ($xUnitTestBinary in $testBinaries) {
    $xUnitTestBinaries += $xUnitTestBinary.FullName
  }

  return $xUnitTestBinaries
}

function Perform-Test-x86([string[]] $files) {
  $xUnit = Locate-xUnit-x86
  $xUnitLogPath = Locate-xUnitLogPath

  $xUnitResultLog = Join-Path $xUnitLogPath "xUnit-x86.xml"

  Write-Host "Starting test x86..."
  & $xUnit @files -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

function Perform-Test-x64([string[]] $files) {
  $xUnit = Locate-xUnit-x64
  $xUnitLogPath = Locate-xUnitLogPath

  $xUnitResultLog = Join-Path $xUnitLogPath "xUnit-x64.xml"

  Write-Host "Starting test x64..."
  & $xUnit @files -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

$xUnitTestBinaries = @(Locate-xUnitTestBinaries)
Write-Host "Found tests: $xUnitTestBinaries"

Perform-Test-x86 $xUnitTestBinaries
Perform-Test-x64 $xUnitTestBinaries
