Push-Location $PSScriptRoot

$ptPath = 'C:\Program Files\PowerToys'

@(
	'PowerToys.Common.UI.dll',
	'PowerToys.ManagedCommon.dll',
	'PowerToys.Settings.UI.Lib.dll',
	'Wox.Infrastructure.dll',
	'Wox.Plugin.dll',
	'LazyCache.dll',
	'Microsoft.Extensions.Caching.Abstractions.dll'
) | ForEach-Object {
	New-Item ./Lib/$_ -ItemType SymbolicLink -Value "$ptPath\$_"
}

Pop-Location
