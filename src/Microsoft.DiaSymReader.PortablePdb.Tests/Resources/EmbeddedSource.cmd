csc /target:library /debug:portable /optimize- /deterministic /pathmap:%~dp0=C:\ /embed EmbeddedSource.cs EmbeddedSourceSmall.cs
copy /y EmbeddedSource.pdb EmbeddedSource.pdbx
copy /y EmbeddedSource.dll EmbeddedSource.dllx

csc /target:library /debug+ /optimize- /deterministic /pathmap:%~dp0=C:\ /embed EmbeddedSource.cs EmbeddedSourceSmall.cs

