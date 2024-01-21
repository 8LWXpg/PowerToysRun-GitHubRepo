pushd
cd $PSScriptRoot

$lib = 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.1'
$ilrepack = '~\.nuget\packages\ilrepack\2.0.27\tools\ILRepack.exe'
$assembly = 'Community.PowerToys.Run.Plugin.GithubRepo.dll'
$merge = 'System.Runtime.Caching.dll'

foreach($arch in @('x64','ARM64')){
	dotnet build -c Release /p:Platform=$arch
	
	pushd
	cd ".\bin\$arch\Release\net8.0-windows"

	& $ilrepack /lib:$lib /out:$assembly $assembly $merge
	popd
}

popd