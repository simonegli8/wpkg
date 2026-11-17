SET PackageVersion=2.0.1
SET Configuration=Debug

del nupkg\*.nupkg
del nupkg\*.snupkg

dotnet pack wpkg.slnx -c %Configuration% -p:Version=%PackageVersion% -p:FileVersion=%PackageVersion% -p:AssemblyVersion=%PackageVersion%