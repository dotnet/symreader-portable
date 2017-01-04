[CmdletBinding(PositionalBinding=$false)]
Param(
  [switch] $clearPackageCache,
  [string] $configuration = "Debug",
  [string] $deployHive = "SymReaderPortable",
  [string] $locateVsApiVersion = "0.2.4-beta",
  [string] $msbuildVersion = "15.0",
  [string] $nugetVersion = "3.6.0-beta1",
  [switch] $official,
  [switch] $realSign,
  [string] $signToolVersion = "0.2.4-beta",
  [switch] $skipBuild,
  [switch] $skipDeploy,
  [switch] $skipRestore,
  [switch] $skipTest,
  [switch] $skipTest32,
  [switch] $skipTest64,
  [switch] $skipTestCore,
  [string] $target = "Build",
  [string] $testFilter = "*.UnitTests.dll",
  [string] $xUnitVersion = "2.2.0-beta3-build3402"
)

set-strictmode -version 2.0
$ErrorActionPreference = "Stop"

function Create-Directory([string[]] $path) {
  if (!(Test-Path -path $path)) {
    New-Item -path $path -force -itemType "Directory" | Out-Null
  }
}

function Download-File([string] $address, [string] $fileName) {
  $webClient = New-Object -typeName "System.Net.WebClient"
  $webClient.DownloadFile($address, $fileName)
}

function Get-ProductVersion([string[]] $path) {
  if (!(Test-Path -path $path)) {
    return ""
  }

  $item = Get-Item -path $path
  return $item.VersionInfo.ProductVersion
}

function Get-RegistryValue([string] $keyName, [string] $valueName) {
  $registryKey = Get-ItemProperty -path $keyName
  return $registryKey.$valueName
}

function Locate-ArtifactsPath {
  $rootPath = Locate-RootPath
  $artifactsPath = Join-Path -path $rootPath -ChildPath "artifacts\$configuration\"

  Create-Directory -path $artifactsPath
  return Resolve-Path -path $artifactsPath
}

function Locate-BinariesPath {
  $artifactsPath = Locate-ArtifactsPath
  $binariesPath = Join-Path -path $artifactsPath -ChildPath "bin\"

  Create-Directory -path $binariesPath
  return Resolve-Path -path $binariesPath
}

function Locate-IntermediatesPath {
  $artifactsPath = Locate-ArtifactsPath
  $intermediatesPath = Join-Path -path $artifactsPath -ChildPath "obj\"

  Create-Directory -path $intermediatesPath
  return Resolve-Path -path $intermediatesPath
}

function Locate-LocateVsApi {
  $packagesPath = Locate-PackagesPath
  $locateVsApi = Join-Path -path $packagesPath -ChildPath "RoslynTools.Microsoft.LocateVS\$locateVsApiVersion\lib\net46\LocateVS.dll"

  if (!(Test-Path -path $locateVsApi)) {
    throw "The specified LocateVS API version ($locateVsApiVersion) could not be located."
  }

  return Resolve-Path -path $locateVsApi
}

function Locate-MSBuild {
  $msbuildPath = Locate-MSBuildPath
  $msbuild = Join-Path -path $msbuildPath -childPath "MSBuild.exe"

  if (!(Test-Path -path $msbuild)) {
    throw "The specified MSBuild version ($msbuildVersion) could not be located."
  }

  return Resolve-Path -path $msbuild
}

function Locate-MSBuildLogPath {
  $artifactsPath = Locate-ArtifactsPath
  $msbuildLogPath = Join-Path -path $artifactsPath -ChildPath "log\"

  Create-Directory -path $msbuildLogPath
  return Resolve-Path -path $msbuildLogPath
}

function Locate-MSBuildPath {
  $vsInstallPath = Locate-VsInstallPath
  $msbuildPath = Join-Path -path $vsInstallPath -childPath "MSBuild\$msbuildVersion\Bin"
  return Resolve-Path -path $msbuildPath
}

function Locate-NuGet {
  $rootPath = Locate-RootPath
  $nuget = Join-Path -path $rootPath -childPath "nuget.exe"

  if (Test-Path -path $nuget) {
    $currentVersion = Get-ProductVersion -path $nuget

    if ($currentVersion.StartsWith($nugetVersion)) {
      return Resolve-Path -path $nuget
    }

    Write-Host -object "The located version of NuGet ($currentVersion) is out of date. The specified version ($nugetVersion) will be downloaded instead."
    Remove-Item -path $nuget | Out-Null
  }

  Download-File -address "https://dist.nuget.org/win-x86-commandline/v$nugetVersion/NuGet.exe" -fileName $nuget

  if (!(Test-Path -path $nuget)) {
    throw "The specified NuGet version ($nugetVersion) could not be downloaded."
  }

  return Resolve-Path -path $nuget
}

function Locate-NuGetConfig {
  $rootPath = Locate-RootPath
  $nugetConfig = Join-Path -path $rootPath -childPath "nuget.config"
  return Resolve-Path -path $nugetConfig
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

function Locate-SignConfig {
  $rootPath = Locate-RootPath
  $signConfig = Join-Path -path $rootPath -childPath "build\Signing\SignToolData.json"

  if (!(Test-Path -path $signConfig)) {
    throw "The sign tool configuration file could not be located."
  }

  return Resolve-Path -path $signConfig
}

function Locate-SignTool {
  $packagesPath = Locate-PackagesPath
  $signTool = Join-Path -path $packagesPath -childPath "RoslynTools.Microsoft.SignTool\$signToolVersion\tools\SignTool.exe"

  if (!(Test-Path -path $signTool)) {
    throw "The specified sign tool version ($signToolVersion) could not be located."
  }

  return Resolve-Path -path $signTool
}

function Locate-Solution {
  $rootPath = Locate-RootPath
  $solution = Join-Path -path $rootPath -childPath "*.sln"
  return Resolve-Path -path $solution
}

function Locate-Toolset {
  $rootPath = Locate-RootPath
  $toolset = Join-Path -path $rootPath -childPath "build\Toolset\project.json"
  return Resolve-Path -path $toolset
}

function Locate-VsInstallPath {
  $locateVsApi = Locate-LocateVsApi
  $requiredPackageIds = @()

  $requiredPackageIds += "Microsoft.Component.MSBuild"
  $requiredPackageIds += "Microsoft.Net.Component.4.6.TargetingPack"
  $requiredPackageIds += "Microsoft.VisualStudio.Component.PortableLibrary"
  $requiredPackageIds += "Microsoft.VisualStudio.Component.Roslyn.Compiler"

  Add-Type -path $locateVsApi
  $vsInstallPath = [LocateVS.Instance]::GetInstallPath($msbuildVersion, $requiredPackageIds)
  return Resolve-Path -path $vsInstallPath
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
  $artifactsPath = Locate-ArtifactsPath
  $xUnitLogPath = Join-Path -path $artifactsPath -ChildPath "log\"

  Create-Directory -path $xUnitLogPath
  return Resolve-Path -path $xUnitLogPath
}

function Locate-xUnitTestBinaries {
  $binariesPath = Locate-BinariesPath
  $binariesPath = Join-Path -path $binariesPath -childPath "DesktopTests"
  $testBinaries = Get-ChildItem -path $binariesPath -filter $testFilter -recurse -force

  $xUnitTestBinaries = @()

  foreach ($xUnitTestBinary in $testBinaries) {
    $xUnitTestBinaries += $xUnitTestBinary.FullName
  }

  return $xUnitTestBinaries
}


function Perform-Build {
  Write-Host -object ""

  if ($skipBuild) {
    Write-Host -object "Skipping build..."
    return
  }

  $msbuild = Locate-MSBuild
  $msbuildLogPath = Locate-MSBuildLogPath

  $deploy = (-not $skipDeploy)
  $solution = Locate-Solution

  $solutionSummaryLog = Join-Path -path $msbuildLogPath -childPath "MSBuild.log"
  $solutionWarningLog = Join-Path -path $msbuildLogPath -childPath "MSBuild.wrn"
  $solutionFailureLog = Join-Path -path $msbuildLogPath -childPath "MSBuild.err"


  Write-Host -object "Starting solution build..."
  & $msbuild /t:$target /p:Configuration=$configuration /p:DeployExtension=$deploy /p:DeployHive=$deployHive /p:OfficialBuild=$official /m /tv:$msbuildVersion /v:m /flp1:Summary`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionSummaryLog /flp2:WarningsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionWarningLog /flp3:ErrorsOnly`;Verbosity=diagnostic`;Encoding=UTF-8`;LogFile=$solutionFailureLog /nr:false $solution

  if ($lastExitCode -ne 0) {
    throw "The build failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The build completed successfully." -foregroundColor Green
}

function Perform-RealSign {
  Write-Host -object ""

  if ($skipBuild) {
    Write-Host -object "Skipping real signing..."
    return
  }

  $binariesPath = Locate-BinariesPath
  $intermediatesPath = Locate-IntermediatesPath
  $packagesPath = Locate-PackagesPath
  $msbuild = Locate-MSBuild
  $signConfig = Locate-SignConfig
  $signTool = Locate-SignTool

  if ($realSign) {
    Write-Host -object "Starting real signing..."
    & $signTool -intermediateOutputPath $intermediatesPath -msbuildPath $msbuild -nugetPackagesPath $packagesPath -config $signConfig $binariesPath
  }
  else {
    Write-Host -object "Starting test signing..."
    & $signTool -test -intermediateOutputPath $intermediatesPath -msbuildPath $msbuild -nugetPackagesPath $packagesPath -config $signConfig $binariesPath
  }

  if ($lastExitCode -ne 0) {
    throw "The real sign task failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The real sign task completed successfully." -foregroundColor Green
}

function Perform-Restore {
  Write-Host -object ""

  if ($skipRestore) {
    Write-Host -object "Skipping restore..."
    return
  }

  $nuget = Locate-NuGet
  $nugetConfig = Locate-NuGetConfig
  $packagesPath = Locate-PackagesPath
  $toolset = Locate-Toolset
  $solution = Locate-Solution
  
  if ($clearPackageCache) {
    Write-Host -object "Clearing local package cache..."
    & $nuget locals all -clear
  }

  Write-Host -object "Starting toolset restore..."
  & $nuget restore -packagesDirectory $packagesPath -msbuildVersion $msbuildVersion -verbosity quiet -nonInteractive -configFile $nugetConfig $toolset

  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "Locating MSBuild install path..."
  $msbuildPath = Locate-MSBuildPath

  Write-Host -object "Starting solution restore..."
  & $nuget restore -packagesDirectory $packagesPath -msbuildPath $msbuildPath -verbosity quiet -nonInteractive -configFile $nugetConfig $solution

  if ($lastExitCode -ne 0) {
    throw "The restore failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The restore completed successfully." -foregroundColor Green
}

function Perform-Test-x86 {
  Write-Host -object ""

  if ($skipTest -or $skipTest32) {
    Write-Host -object "Skipping test x86..."
    return
  }

  $xUnit = Locate-xUnit-x86
  $xUnitLogPath = Locate-xUnitLogPath
  $xUnitTestBinaries = @(Locate-xUnitTestBinaries)

  $xUnitResultLog = Join-Path -path $xUnitLogPath -childPath "xUnit-x86.xml"

  Write-Host "$xUnit $xUnitTestBinaries -xml $xUnitResultLog"
  & $xUnit @xUnitTestBinaries -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

function Perform-Test-x64 {
  Write-Host -object ""

  if ($skipTest -or $skipTest64) {
    Write-Host -object "Skipping test x64..."
    return
  }

  $xUnit = Locate-xUnit-x64
  $xUnitLogPath = Locate-xUnitLogPath
  $xUnitTestBinaries = @(Locate-xUnitTestBinaries)

  $xUnitResultLog = Join-Path -path $xUnitLogPath -childPath "xUnit-x64.xml"

  Write-Host "$xUnit $xUnitTestBinaries -xml $xUnitResultLog"
  & $xUnit @xUnitTestBinaries -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

function Perform-Test-Core {
  Write-Host -object ""

  if ($skipTest -or $skipTestCore) {
    Write-Host -object "Skipping test Core..."
    return
  }

  $binariesPath = Locate-BinariesPath
  $binariesPath = Join-Path $binariesPath "CoreTests"
 
  $corerun = Join-Path $binariesPath "CoreRun.exe"
  $xUnit = Join-Path $binariesPath "xunit.console.netcore.exe"
  $xUnitLogPath = Locate-xUnitLogPath
  $xUnitTestBinaries = @(Locate-xUnitTestBinaries)

  $xUnitResultLog = Join-Path -path $xUnitLogPath -childPath "xUnit-Core.xml"

  Write-Host "$corerun $xUnit $xUnitTestBinaries -xml $xUnitResultLog"
  & $corerun $xUnit @xUnitTestBinaries -xml $xUnitResultLog

  if ($lastExitCode -ne 0) {
    throw "The test failed with an exit code of '$lastExitCode'."
  }

  Write-Host -object "The test completed successfully." -foregroundColor Green
}

Perform-Restore
Perform-Build
Perform-RealSign
Perform-Test-x86
Perform-Test-x64
Perform-Test-Core
