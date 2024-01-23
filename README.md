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

1. Clone the repository.
2. Clone the dependencies in `/lib`.
3. run `dotnet build -c Release`.

## Contributing

Contributions are welcome!
