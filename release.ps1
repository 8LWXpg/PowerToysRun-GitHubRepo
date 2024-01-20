pushd
cd $PSScriptRoot

$release = '.\bin\x64\Release\net8.0-windows'

$lib = 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.1'
$ilrepack = '~\.nuget\packages\ilrepack\2.0.27\tools\ILRepack.exe'
$assembly = 'Community.PowerToys.Run.Plugin.GithubRepo.dll'
$merge = 'System.Runtime.Caching.dll'

cd $release

& $ilrepack /lib:$lib /out:$assembly $assembly $merge

Compress-Archive $assembly, .\plugin.json, .\images $PSScriptRoot\GithubRepo.zip

popd