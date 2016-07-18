csc /target:library /debug:portable /optimize- /deterministic Scopes.cs
copy /y Scopes.pdb Scopes.pdbx
copy /y Scopes.dll Scopes.dllx

csc /target:library /debug+ /optimize- /deterministic Scopes.cs