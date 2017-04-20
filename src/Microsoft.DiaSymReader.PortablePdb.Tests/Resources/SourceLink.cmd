csc /target:library /debug:portable /optimize- /deterministic /out:SourceLink.dll /sourcelink:SourceLink.json Documents.cs
csc /target:library /debug:embedded /optimize- /deterministic /out:SourceLink.Embedded.dll /sourcelink:SourceLink.json Documents.cs

copy /y SourceLink.dll SourceLink.dllx
copy /y SourceLink.pdb SourceLink.pdbx

csc.exe /target:library /debug:full /optimize- /deterministic /out:SourceLink.dll /sourcelink:SourceLink.json Documents.cs