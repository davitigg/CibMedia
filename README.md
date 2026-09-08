# CibMedia

Android TV app for browsing movies and shows from TMDb, resuming what you were watching,
playing HLS or progressive streams, and opening a title from a phone on the same network. The
box resolves its own streams. .NET for Android, with AndroidX Leanback for the UI and
Media3/ExoPlayer for playback.

## Running it

```powershell
./run.ps1                                              # Debug, onto the only attached device
./run.ps1 -Configuration Release -Ip 192.168.0.51 -Log  # Release, over network debugging
./run.ps1 -Device emulator-5554                        # Debug, onto a running Android TV emulator
```

`run.ps1` builds, installs and launches, and `-Log` follows logcat filtered to the `CibMedia`
tag. Judge how the app feels on Release, on real hardware: Debug inflates UI cost around 2.5x.

```bash
dotnet test tests/CibMedia.Core.Tests
```

## Projects

| Project                     | Holds                                                                               |
|-----------------------------|-------------------------------------------------------------------------------------|
| `src/CibMedia.Core`         | Records, view models, the TMDb client. No Android reference.                        |
| `src/CibMedia.Playback`     | The provider stacks a title or a live page resolves through. No Android reference.  |
| `src/CibMedia.AndroidTv`    | The Android head: Activities, Leanback fragments and presenters, platform adapters. |
| `tools/CibMedia.Playback.Cli` | Resolves the same titles from a desktop, with a debugger attached.                |
| `tests/CibMedia.Core.Tests` | xUnit over `CibMedia.Core`, with a fake for every port in `Core/Abstractions`.      |

Screens are `ViewModel<TState>` in Core plus a Leanback fragment in the head that renders `State` and
nothing else. Android APIs reach Core only through the `I*` ports in `Core/Abstractions`, bound
to their adapters in `AppServices`.

## The TMDb key

The app needs a TMDb key for the catalogue. On first run, and from the API keys card in
Settings afterwards, the setup screen takes it: typed on the remote, or pasted from a phone
into the page the box serves at the address behind the QR code (`http://<box-ip>:8730/setup`).
The key is stored only once TMDb has accepted it.

On the emulator there is no phone on the same network: forward the port and paste the key
from the host instead.

```bash
adb -s emulator-5554 forward tcp:18730 tcp:8730
# then open http://localhost:18730/setup while the setup screen is showing
```

## Opening a title from a phone

Remote play is off until the Remote play card in Settings switches it on. The card then shows
the address the box answers on. Post a title to it and the TV opens that title's details page
at the episode named; whoever is in front of the TV presses Play:

```bash
curl -X POST http://192.168.0.50:8730/playback/movie \
  -H 'Content-Type: application/json' -d '{"tmdbId":550}'

curl -X POST http://192.168.0.50:8730/playback/tv-show \
  -H 'Content-Type: application/json' -d '{"tmdbId":1396,"season":2,"episode":4}'
```

Naming a season and an episode the show has lands on that episode; a season on its own lands
on that season. An episode the season does not have falls back to the season, and a season
the show does not have is ignored altogether: the page then opens where omitting both opens
it, wherever watch history left the show. A title the catalogue does not have opens no page
at all and says so on the TV.

A liveball url has no details page to open, so it plays instead: the TV goes straight to the
player and starts the stream it resolves that page to. Sending another while one is
playing switches the player to it, and a page with nothing on it leaves the stream alone:
the switch commits only once the new page has resolved to something playable.

```bash
curl -X POST http://192.168.0.50:8730/playback/liveball \
  -H 'Content-Type: application/json' -d '{"url":"https://liveball.example/match/123"}'
```

The endpoints are served only while the app is on screen: Android does not let a backgrounded
app start an Activity, so a command sent then has nowhere to go.

## Releasing, and how a box updates itself

`manifest.json` at the repo root is what every box reads at launch, from
`raw.githubusercontent.com`. It names the newest `versionCode`, the apk that carries it and that
apk's sha256, and it carries a `playback` block.

The two halves move independently. Editing the `playback` block and pushing repoints every box
on its next launch — no apk, no release, no prompt — because `versionCode` did not change. An
apk moves only when `release.ps1` runs:

```powershell
./release.ps1 -VersionName 2.1 -Notes 'What changed'   # -WhatIf publishes nothing
```

That bumps `versionCode`, builds signed Release, hashes the apk, rewrites `manifest.json`,
creates the GitHub release and pushes. It refuses to run without `keystore/signing.props`:
every published build carries the same signing key, and one signed with another cannot install
over an installed app.

A box offers the release at its next launch and installs nothing without an answer. What it
downloads is checked against the published sha256 before the installer sees it, and a version
turned down is not offered again until a newer one is published. Installing needs the box to
have granted CibMedia "install unknown apps" once, which Android asks for on the first attempt.

## Where streams come from

`src/CibMedia.Playback/playback.json` names the stacks a build resolves through and the hosts
they read. It ships as an APK asset, and the box prefers one written to its own storage, so a
document published later replaces it whole.

The same resolvers run from a desktop, which is where a stack that has changed shape is
debugged:

```bash
dotnet run --project tools/CibMedia.Playback.Cli -- movie 603
dotnet run --project tools/CibMedia.Playback.Cli -- tv 1399 1 1
dotnet run --project tools/CibMedia.Playback.Cli -- liveball https://liveball.example/match/123
dotnet run --project tools/CibMedia.Playback.Cli -- providers
```

A `playback.json` in the working directory overrides the one the harness shipped with. Liveball
refuses Windows whatever the address it comes from, so a liveball page is exercised on a device.
