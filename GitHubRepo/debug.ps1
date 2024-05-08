# this script uses [gsudo](https://github.com/gerardog/gsudo)

Push-Location
Set-Location $PSScriptRoot

# dotnet build -c Debug /p:Platform=x64

sudo {
	Start-Job { Stop-Process -Name PowerToys* } | Wait-Job

	$ptPath = 'C:\Program Files\PowerToys'
	$debug = '.\bin\x64\Debug\net8.0-windows'
	$dest = "$env:LOCALAPPDATA\Microsoft\PowerToys\PowerToys Run\Plugins\GitHubRepo"
	$files = @(
		'Community.PowerToys.Run.Plugin.GitHubRepo.deps.json',
		'Community.PowerToys.Run.Plugin.GitHubRepo.dll',
		'plugin.json',
		'Images'
	)

	Set-Location $debug
	mkdir $dest -Force -ErrorAction Ignore | Out-Null
	Copy-Item $files $dest -Force -Recurse

	& "$ptPath\PowerToys.exe"
}

Pop-Location