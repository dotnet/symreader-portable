# Microsoft.DiaSymReader.PortablePdb

Reader of [Portable PDBs](https://github.com/dotnet/core/blob/master/Documentation/diagnostics/portable_pdb.md) format that implements DiaSymReader interfaces ([ISymUnmanagedReader](https://msdn.microsoft.com/en-us/library/ms232131.aspx), [ISymUnmanagedBinder](https://msdn.microsoft.com/en-us/library/ms232451.aspx), etc.).

The definitions of the DiaSymReader COM interfaces are provided by [Microsoft.DiaSymReader](https://www.nuget.org/packages/Microsoft.DiaSymReader) package.
The implementation of these interfaces for Windows PDBs is provided by [Microsoft.DiaSymReader.Native](https://www.nuget.org/packages/Microsoft.DiaSymReader.Native) package. 

It is recommended that new applications and libraries read Portable PDBs directly using APIs provided by [System.Reflection.Metadata](https://www.nuget.org/packages/System.Reflection.Metadata) package. These APIs are much more efficient than DiaSymReader APIs. Microsoft.DiaSymReader.PortablePdb bridge is recommended for existings apps that already use DiaSymReader APIs and need to be able to read Portable PDBs without significant changes to their source.

Pre-release builds are available from Azure DevOps public feed: `https://pkgs.dev.azure.com/dnceng/public/_packaging/dotnet5/nuget/v3/index.json` ([browse](https://dev.azure.com/dnceng/public/_packaging?_a=feed&feed=dotnet5)).

[![Build Status](https://dnceng.visualstudio.com/public/_apis/build/status/SymReaderPortable%20PR?branchName=master)](https://dnceng.visualstudio.com/public/_build/latest?definitionId=296?branchName=master)
