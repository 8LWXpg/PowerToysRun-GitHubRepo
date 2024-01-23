pushd
cd $PSScriptRoot

$assembly = 'Community.PowerToys.Run.Plugin.GithubRepo.dll'

foreach($arch in @('x64','ARM64')){
	$releasePath = ".\bin\$arch\Release\net8.0-windows"

	dotnet build -c Release /p:Platform=$arch
	Compress-Archive "$releasePath\$assembly", "$releasePath\plugin.json", "$releasePath\images" "$PSScriptRoot\GithubRepo_$arch.zip" -Force
}

popd