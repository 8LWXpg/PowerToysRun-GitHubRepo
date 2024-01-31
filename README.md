# GithubRepo Plugin for PowerToys Run

This is a plugin for [PowerToys Run](https://github.com/microsoft/PowerToys/wiki/PowerToys-Run-Overview) that allows to search for GitHub repositories then open in your default browser.

## Features

### Search repo with query: `qurey`

![Search repo with query](./assets/repo.png)

### Search repo with user: `user/repo`

![Search repo with user](./assets/user.png)

### Search repo with default user: `/repo`

![Search repo with default user](./assets/default_user.png)

## Installation

1. Download the latest release of the from the releases page.
2. Extract the zip file's contents to `%LocalAppData%\Microsoft\PowerToys\PowerToys Run\Plugins`
3. Restart PowerToys.

## Usage

1. Open PowerToys Run (default shortcut is <kbd>Alt+Space</kbd>).
2. Type `gr` followed by your search query.
3. Select a search result and press `Enter` to open it in browser.

## Settings

- **Default user**: The default user to search for when typed `/`.
- **Github auth token**: The Github auth token to use for better rate limiting. You can generate a token with no scope.

## Building

1. Clone the repository and the dependencies in `/lib`.
2. run `dotnet build -c Release`.

## Debugging

1. Clone the repository and the dependencies in `/lib`.
2. Build the project in `Debug` configuration.
3. Make sure you have [gsudo](https://github.com/gerardog/gsudo) installed in the path.
4. Run `debug.ps1` (change `$ptPath` if you have PowerToys installed in a different location).
5. Attach to the `PowerToys.PowerLauncher` process in Visual Studio.

## Contributing

Contributions are welcome!
