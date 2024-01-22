pushd
cd $PSScriptRoot

$lib = 'C:\Program Files\dotnet\shared\Microsoft.NETCore.App\8.0.1'
$ilrepack = '~\.nuget\packages\ilrepack\2.0.27\tools\ILRepack.exe'
$assembly = 'Community.PowerToys.Run.Plugin.GithubRepo.dll'
$merge = 'System.Runtime.Caching.dll'

foreach($arch in @('x64','ARM64')){
	pushd
	cd ".\bin\$arch\Release\net8.0-windows"


	Compress-Archive $assembly, .\plugin.json, .\images "$PSScriptRoot\GithubRepo_$arch.zip" -Force
	popd
}

popd