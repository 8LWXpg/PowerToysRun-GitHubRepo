pushd
cd $PSScriptRoot

# Script for copying the release files and creating the release zip file
$version = "1.0.0"
$release = "C:\Users\erict\Downloads\winget-powertoys-$version.zip"
$path = "..\..\..\..\..\x64\Release\RunPlugins\Winget"

# pack the files from path and excluding
7z a -aoa -bb0 -bso0 -xr!PowerToys* -xr!Backup* -xr!Ijwhost* -tzip $release $path

popd