pushd
cd $PSScriptRoot

$name = 'GithubRepo'
$assembly = 'Community.PowerToys.Run.Plugin.GithubRepo'
$version = "v$((cat ./plugin.json | ConvertFrom-Json).Version)"
$archs = @('x64', 'arm64')

git tag $version
git push --tags

rm ./out/*.zip -r -fo -ea ig
foreach($arch in $archs){
	$releasePath = "./bin/$arch/Release/net8.0-windows"

	dotnet build -c Release /p:Platform=$arch

	rm ./out/GithubRepo/* -r -fo -ea ig
	cp "$releasePath/$assembly.dll", "$releasePath/plugin.json", "$releasePath/Images", "$releasePath/$assembly.deps.json" "./out/$name" -r -fo
	Compress-Archive "./out/$name" "./out/$name-$version-$arch.zip" -fo
}

gh release create $version (ls ./out/*.zip)
popd