pushd
cd $PSScriptRoot

$assembly = 'Community.PowerToys.Run.Plugin.GithubRepo.dll'
$version = (cat .\plugin.json | ConvertFrom-Json).Version

git tag $version
git push --tags

$archs = @('x64', 'arm64')
foreach($arch in $archs){
	$releasePath = ".\bin\$arch\Release\net8.0-windows"

	dotnet build -c Release /p:Platform=$arch
	Compress-Archive "$releasePath\$assembly", "$releasePath\plugin.json", "$releasePath\images" ".\GithubRepo_$arch.zip" -Force
}

gh release create $version @(@('x64','ARM64') | % {".\GithubRepo_$_.zip"})
popd