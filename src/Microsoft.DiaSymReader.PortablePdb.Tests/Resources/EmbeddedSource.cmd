csc /target:library /debug:portable /optimize- /deterministic /out:EmbeddedSource.dll /pathmap:%~dp0=C:\ /embed EmbeddedSource.cs EmbeddedSourceNoCode.cs EmbeddedSourceSmall.cs EmbeddedSourceNoSequencePoints.cs
copy /y EmbeddedSource.pdb EmbeddedSource.pdbx
copy /y EmbeddedSource.dll EmbeddedSource.dllx

csc /target:library /debug+ /optimize- /deterministic /out:EmbeddedSource.dll /pathmap:%~dp0=C:\ /embed EmbeddedSource.cs EmbeddedSourceNoCode.cs EmbeddedSourceSmall.cs EmbeddedSourceNoSequencePoints.cs

