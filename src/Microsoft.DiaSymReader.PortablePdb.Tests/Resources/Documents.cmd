csc /target:library /debug:portable /optimize- /deterministic /pathmap:%~dp0=/ /checksumalgorithm:sha256 Documents.cs
copy /y Documents.pdb Documents.pdbx
copy /y Documents.dll Documents.dllx

csc /target:library /debug+ /optimize- /deterministic /pathmap:%~dp0=/ /checksumalgorithm:sha256 Documents.cs


