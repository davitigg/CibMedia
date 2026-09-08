# CLAUDE.md

## Commands

| Task              | Command                                                                    |
|-------------------|----------------------------------------------------------------------------|
| Run on a device   | `./run.ps1 [-Configuration Release] [-Device <serial>\|-Ip <addr>] [-Log]` |
| Run on the emulator | `./run.ps1 -Device emulator-5554`, then `adb forward tcp:18730 tcp:8730` and paste the key into `http://localhost:18730/setup` |
| Tests             | `dotnet test tests/CibMedia.Core.Tests`                                    |
| Compile the head  | `dotnet build src/CibMedia.AndroidTv`                                             |
| Resolve a title from a desktop | `dotnet run --project tools/CibMedia.Playback.Cli -- movie 603` (also `tv <id> <s> <e>`, `liveball <url>`, `providers`) |
| Publish a release | `./release.ps1 -VersionName 2.1 -Notes '...'` (`-WhatIf` builds and writes `manifest.json` without publishing) |

A plain `dotnet build` leaves the previous APK on the device, so "my change did nothing" is a
deploy symptom first — use `run.ps1`, or `-t:Install`. Measure performance on Release on real
hardware: Debug inflates UI cost ~2.5x and the emulator's software decoder makes its playback
numbers meaningless. `adb reboot` leaves the emulator without DNS and every rail spinning;
restart the emulator process instead. A `playback.json` beside the harness overrides the one it
shipped with, which is how a stack is pointed somewhere else without a build. Windows is refused by
liveball whatever the address — its TLS handshake is scored, and only the box's stack passes — so a
liveball page is exercised on a device.

## Where things live

| When touching                                                          | Read first                                                                                                  |
|------------------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------|
| A record, view model, port implementation — anything with no Android reference | `src/CibMedia.Core`                                                                            |
| A provider stack, its clients and parsers, or what a resolved answer costs to keep | `src/CibMedia.Playback`                                                    |
| Which stacks a build resolves through, and the hosts they read | `src/CibMedia.Playback/playback.json`                                                            |
| What a published release says about itself, or anything under `Updates/` | `manifest.json`, `src/CibMedia.Core/Updates`, `src/CibMedia.Core/Infrastructure/Updates`        |
| Downloading, verifying or installing an apk, or the prompt that offers one | `src/CibMedia.AndroidTv/Platform/Apk*.cs`, `src/CibMedia.AndroidTv/App/AppUpdateCheck.cs`        |
| A screen, card, row, or focus behaviour                                | `src/CibMedia.AndroidTv/Leanback`                                                                                  |
| An Android API behind an `I*` port from `Core/Abstractions`            | `src/CibMedia.AndroidTv/Platform`                                                                                  |
| DI registrations, Intent payloads, fragment/view-model lifetime        | `src/CibMedia.AndroidTv/App`                                                                                       |
| The player, its bottom-bar buttons and menus, its overlay              | `src/CibMedia.AndroidTv/Activities/PlaybackActivity.cs`, `src/CibMedia.AndroidTv/Playback`                                |
| The box's own HTTP surface: the setup page, the playback commands      | `src/CibMedia.Core/Abstractions/LocalHttp`, `src/CibMedia.Core/Presentation/Remote`, `src/CibMedia.AndroidTv/App/PlaybackEndpointHost.cs` |

## Architecture

Ports and adapters over three projects. `CibMedia.Core` is a plain `net10.0` library that never
references Android and holds everything worth testing; `CibMedia.Playback` resolves a title or a
page to streams and references neither Android nor Core; `CibMedia.AndroidTv` is the head that draws it.

| Layer                              | Holds                                                                                              | References             |
|------------------------------------|----------------------------------------------------------------------------------------------------|------------------------|
| `Core/Catalog`, `Core/Playback`    | Domain records — `MediaCard`, `MediaId`, `PlayableStream`, `WatchProgress`                         | nothing                |
| `Core/Common`                      | Screen primitives — `ViewModel<TState>`, `Load<T>`, `Page<T>`, `PagedFeed<T>`, `AppError`, `CancellationScope` | domain                 |
| `Core/Abstractions`                | One `I*` port per thing Core cannot do itself; `LocalHttp/` is the box's HTTP surface              | domain, `Common`       |
| `Core/Presentation`                | A `ViewModel<TState>` per screen, plus the rules screens share: `Playback/` (availability, history roll-forward), `Remote/` (the playback commands) | ports, never `Infrastructure` |
| `Core/Infrastructure`              | Port implementations — TMDb under `Tmdb/`, the resolvers under `Playback/`, caching decorators, `CacheJson` | ports, `CibMedia.Playback` |
| `CibMedia.Playback`                | The provider stacks, their wire models, and the `AddPlayback` that composes them                   | nothing                |
| `AndroidTv/*`                      | Fragments, presenters, port adapters, DI                                                           | everything             |

- **Depend on `Abstractions`, construct in `AppServices`.** A view model takes `ICatalog`, never
  `TmdbCatalog`. `AppServices` is the one file that names a concrete implementation; a
  `using CibMedia.Core.Infrastructure;` in `Presentation` means the dependency went the wrong way.
- **Cross-cutting behaviour is a decorator, not a branch.** Caching (`CachedCatalog`) wraps the
  plain implementation at registration and is invisible to callers. New policy is a new wrapper,
  not an `if` inside `TmdbCatalog`.
- **The resolvers state their own lifetimes.** How long a resolved source, an outage or a live
  page stays good is a constant in `CibMedia.Playback`, and no constant in Core names one. TMDb
  sends `max-age`, but it describes its cache tier rather than its data — those lifetimes are the
  constants in `CachedCatalog` and `TmdbArtwork`.
- **A port is what the app needs, not what Android offers.** `ICacheStore` is bytes in, bytes
  out and knows nothing about TTLs — that policy is in `CachedCatalog` and in the entry lifetimes
  the resolvers keep. Adapters under `Tv/Platform` translate; they do not decide.
- **Configuration arrives as a document.** `playback.json` ships as an APK asset and is read from
  `filesDir` when something has written one there, whole rather than merged. `AddPlayback`
  validates it as it registers, so a document that is not one fails the app at launch.
- **A fetched document is proved before it is kept.** `PlaybackDocument.IsUsable` runs the
  registration the next launch runs, and `PlaybackConfig.Write` moves the file into place only
  after that. A box the config stops from launching cannot run the updater that would correct it,
  so no weaker check will do.
- **`manifest.json` carries config and names an apk; it is not one.** `versionCode` decides
  whether a release is offered, and the `playback` block is written on every launch that reads
  it — so retuning hosts is a commit to `manifest.json` and never a release. A manifest with no
  `apkUrl` or no `sha256` offers nothing, and a download whose hash does not match the one
  published never reaches the installer.
- **State is a record, replaced whole.** `SetState` takes `old => new`; never mutate a state
  object or a collection inside one. Return the same instance when the update changed nothing —
  rendering diffs on reference equality, so a redundant notification costs a full page rebuild.
- **Anything fetched is a `Load<T>`,** one per slot, so a failing rail cannot empty the page or
  take down the four beside it. Convert at the boundary with `Load.RunAsync`: exceptions become
  an `AppError` there and never reach a screen.
- **Cancellation is owned.** Work tied to a screen takes `Lifetime`; anything the user can
  re-trigger — a season switch, a search keystroke — runs under a `CancellationScope`, so a slow
  earlier answer cannot land after a fast later one.
- **The head renders, it does not decide.** A fragment reads `State` and draws it. What to
  fetch, when to page, and what counts as an error live in Core. A new screen is a
  `ViewModel<TState>` in Core plus a fragment in Tv; a page of rails subclasses
  `RailsFragment<TKey, TViewModel>` rather than rebuilding the plumbing.
- **Push behaviour down until a test can reach it.** `tests/CibMedia.Core.Tests` references Core,
  and the resolvers through it; fakes in `tests/CibMedia.Core.Tests/Fakes` implement the ports.
  Behaviour that needs a device to test is behaviour in the wrong project.
- **An upstream's shape stops at its mapping.** TMDb's field names end in
  `Infrastructure/Tmdb/TmdbMapping` and the resolvers' in `Infrastructure/Playback/PlaybackMapping`;
  nothing above them knows an upstream field name.
- **A provider is a name, never a branch.** `PlaybackMapping` turns the resolver's provider into a
  string and nothing above it compares one to a literal: Settings offers what `IPlaybackProviders`
  lists, a title plays from what its stream carries. Turning a stack on or pointing it at other
  hosts is an edit to `playback.json`; a new stack is code in `CibMedia.Playback`.
- **Nothing exists without a caller.** No port method, record, setting or resource lands ahead of
  the screen that uses it, and one that loses its last caller is deleted rather than kept warm.
- **The UI thread is the budget.** The box has 1 GB and everything is drawn on one thread, so
  work added per card or per render is measured on Release on the device before it is argued
  about. Page-switch cost turned out to be card inflation; JNI churn and rail depth were measured
  and refuted.

## Conventions

- **Code that expresses intent needs no comment.** Write one only for what the code cannot
  say: an Android/Leanback/Media3/trimming quirk, a measured trade-off, an invariant a caller
  cannot infer. Never restate the signature, the type, or the line below it, and never narrate
  what the code used to be — git has that. No `///` docs, no commented-out code, no decision
  log or changelog in a comment. When in doubt, rename the thing instead.
- **Strip the commentary before committing.** Re-read every comment in the diff and delete the
  ones the change no longer needs: a note explaining what the line replaced, a justification the
  predicate or the name already carries, scaffolding written to think the problem through. What
  survives is the quirk, the measurement, the invariant. A comment that only made sense while the
  change was being written is not committed.
- **A name says what the thing does now.** Methods are verbs and async ones end in `Async`
  (`ReloadContinueWatchingAsync`, `EnsureConfigAsync`); a bool reads as a question (`HasFailed`);
  an override names its parameters after what they hold, never the binding's `p0`/`p1` (CA1725
  is off for `CibMedia.AndroidTv` for that reason). A name that stopped fitting is renamed with every
  caller, not explained.
- **Shared behaviour is lifted once, not copied.** Two screens that watch the same port the same
  way share a base (`DetailViewModel<TState>` under the movie and TV detail view models); a rule
  two callers apply lives in one place (`PlaybackMapping`). The second copy of a block is the
  moment to extract, and a helper with one caller is inlined back.
- One public type per file; the namespace mirrors the folder, so a file moves when its
  namespace does.
- **A refactor changes no behaviour, and proves it.** Build every project with zero warnings,
  run the tests, diff a comment-stripped copy of the tree against `HEAD` so only intended renames
  show, and walk the app on the emulator with a real key: setup page, rails, details, playback,
  resume, search, the `/playback` routes. The harness resolves the same title from a desktop, so
  a stack that changed shape is read there rather than over adb.
- Style Leanback by overriding its `lb_*` resources by name under `Resources/values`, never by
  hand-rolling equivalent views.
- Toasts go through `Design/Toasts`, which builds them on the application context: an Activity
  context carries the Display size density override, and a toast should match the box's own.
