# Microsoft.DiaSymReader.PortablePdb

Reader of [Portable PDBs](https://github.com/dotnet/core/blob/master/Documentation/diagnostics/portable_pdb.md) format that implements DiaSymReader interfaces ([ISymUnmanagedReader](https://msdn.microsoft.com/en-us/library/ms232131.aspx), [ISymUnmanagedBinder](https://msdn.microsoft.com/en-us/library/ms232451.aspx), etc.).

The definitions of the DiaSymReader COM interfaces are provided by [Microsoft.DiaSymReader](https://www.nuget.org/packages/Microsoft.DiaSymReader) package.
The implementation of these interfaces for Windows PDBs is provided by [Microsoft.DiaSymReader.Native](https://www.nuget.org/packages/Microsoft.DiaSymReader.Native) package. 

It is recommended that new applications and libraries read Portable PDBs directly using APIs provided by [System.Reflection.Metadata](https://www.nuget.org/packages/System.Reflection.Metadata) package. These APIs are much more efficient than DiaSymReader APIs. Microsoft.DiaSymReader.PortablePdb bridge is recommended for existings apps that already use DiaSymReader APIs and need to be able to read Portable PDBs without significant changes to their source.

[//]: # (Begin current test results)

### Windows - Unit Tests
||Debug x86|Debug x64|Release x86|Release x64|Core CLR
|:--:|:--:|:--:|:--:|:--:|:--:|
|**master**|[![Build Status](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_debug_unit32/badge/icon)](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_debug_unit32/)|[![Build Status](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_debug_unit64/badge/icon)](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_debug_unit64/)|[![Build Status](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_release_unit32/badge/icon)](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_release_unit32/)|[![Build Status](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_release_unit64/badge/icon)](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_release_unit64/)|[![Build Status](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_release_core/badge/icon)](http://dotnet-ci.cloudapp.net/job/dotnet_symreader-portable/job/master/job/windows_release_core/)

[//]: # (End current test results)

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/).  For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

