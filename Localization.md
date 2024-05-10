# Localization

## On github.dev

1. Fork this repository.
1. Open github.dev by pressing <kbd>.</kbd> on keyboard.
![Open github.dev](https://user-images.githubusercontent.com/856858/130119109-4769f2d7-9027-4bc4-a38c-10f297499e8f.gif)
1. Install [ResX Viewer/Editor](https://marketplace.visualstudio.com/items?itemName=8LWXpg.code-resx) (yes, I made an extension for this).
1. Copy `./GitHubRepo/Properties/Resources.resx` to `./GitHubRepo/Properties/Resources.<locale>.resx`.
1. Change the `Value`s to the translated text.
1. Add `"$releasePath/<locale>"` to the `$items` array in `./GitHubRepo/release.ps1`.
1. Submit a PR.
