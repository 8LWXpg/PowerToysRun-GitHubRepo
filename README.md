# GithubRepo Plugin for PowerToys Run

This is a plugin for [PowerToys Run](https://github.com/microsoft/PowerToys/wiki/PowerToys-Run-Overview) that allows to search for Github repositories and open them in your browser.

## Features

- Search repo with query: `qurey`
- Search repo with user: `user/repo`
- Search repo with default user: `/repo`

## Installation

1. Download the latest release of the from the releases page.
2. Extract the zip file's contents to your PowerToys modules directory .
    - `%LocalAppdata%\PowerToys\RunPlugins` if installed for the current user.
    - `%ProgramFiles%\PowerToys\RunPlugins` if installed for all users.
3. Restart PowerToys.

## Usage

1. Open PowerToys Run (default shortcut is <kbd>Alt+Space</kbd>).
2. Type `gr` followed by your search query.
3. Select a search result and press `Enter` to open it in browser.

## Build

1. Follow the instructions to [build PowerToys from source](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/readme.md#compiling-powertoys).
2. Clone this repository under `.\src\modules\launcher\Plugins`.
3. In VS, add local clone as an existing project to PowerToy's Plugins folder `.\src\modules\launcher\Plugins`.
4. Build this project in VS and select `PowerLauncher` as the debug target.

## Contributing

Contributions are welcome! Please see our [contributing guidelines](CONTRIBUTING.md) for more information.
