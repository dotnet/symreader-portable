csc /target:library /debug:portable /optimize- /deterministic /pathmap:%~dp0=C:\ /embed EmbeddedSource.cs EmbeddedSourceSmall.cs
copy /y EmbeddedSource.pdb EmbeddedSource.pdbx
copy /y EmbeddedSource.dll EmbeddedSource.dllx

@REM -- TODO: Native support not there yet, hence no /embed, add it when implemented and unskip EmbeddedSource_Native test.
csc /target:library /debug+ /optimize- /deterministic /pathmap:%~dp0=C:\ EmbeddedSource.cs EmbeddedSourceSmall.cs

