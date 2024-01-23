pushd
cd $PSScriptRoot

$assembly = 'Community.PowerToys.Run.Plugin.GithubRepo.dll'
$version = (cat ./plugin.json | ConvertFrom-Json).Version

$archs = @('x64', 'arm64')

rm ./out/* -r -fo -ea ig
md ./out -ea ig
foreach($arch in $archs){
	$releasePath = "./bin/$arch/Release/net8.0-windows"

	dotnet build -c Release /p:Platform=$arch
	Compress-Archive "$releasePath/$assembly", "$releasePath/plugin.json", "$releasePath/images" "./out/GithubRepo_$arch.zip" -fo
}

gh release create $version (ls ./out/*.zip)
popd