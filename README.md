# Blinkjot

**Jot it down in a blink.** Blinkjot is a tiny Android app for capturing
thoughts the moment you have them. Open it and tap one of three big buttons:

- **Note**: the keyboard is already open, just type.
- **Voice**: recording starts immediately and keeps going with the screen locked.
- **Photo**: the camera opens for a photo or a video.

Every capture becomes a task. Everything is **saved on the phone first**, so
the app always works offline. When it can reach your server it syncs in the
background, even with the screen locked, into [Plane](https://plane.so) tasks.
Voice notes are transcribed for you.

## Features

- Three-button home screen with your recent open tasks (title + first line).
- Pick the project and status *after* capturing; capturing never waits on that.
- Add more voice notes, photos or videos to any task.
- Two-way sync. Tasks created or deleted in Plane show up on (or vanish from) the phone.
- Background sync with Android WorkManager, plus a microphone foreground
  service so recordings survive a locked screen.
- Settings for a server behind [Pangolin](https://github.com/fosrl/pangolin):
  access token, Basic auth, or a custom header.
- Big, thumb-friendly UI in the Microsoft To Do style.

## Install

Download `blinkjot.apk` from the
[latest release](https://github.com/Bortronx/blinkjot/releases/latest) and open
it on your phone. Allow "Install unknown apps" for your browser. Then:

1. Allow the microphone, camera and notifications.
2. Optional: **⋮ → Settings** → enter your sync server address and sign-in,
   then tap **Test connection** and **Save**.
3. Tap **Allow background sync** if it's shown.

Without a server, Blinkjot is a fully local capture app.

## The sync server

Blinkjot talks to a small HTTP/JSON sync server. That server creates the
Plane tasks, uploads attachments and runs speech-to-text. The API is
documented in [docs/server-api.md](docs/server-api.md).

## Why a native app and not a PWA?

A browser PWA can't keep recording with the screen locked (Android Chrome
pauses `MediaRecorder`). It also can't reliably sync in the background (iOS
has no Background Sync API). Blinkjot is a **.NET MAUI Blazor Hybrid** app:

- The UI is plain Blazor (Razor + CSS).
- A few small native Android pieces handle recording and background work.

## Project layout

| Path | Role |
|------|------|
| `Blinkjot/Components/Pages/` | Screens: Home, TaskPage, Record, Menu, Settings, AllTasks |
| `Blinkjot/wwwroot/app.css` | The whole look, one stylesheet |
| `Blinkjot/Services/` | `LocalStore` (JSON + files on the phone), `SyncService` (push → upload files → pull), `SettingsService` |
| `Blinkjot/Shared/Contracts.cs` | JSON contracts shared with the server |
| `Blinkjot/Platforms/Android/` | `Recorder` (foreground service), `BackgroundSync` (WorkManager), `Phone` (camera, permissions, battery) |
| `build-apk.cs` / `.cmd` | Local build → `dist/blinkjot.apk` |
| `.github/workflows/android.yml` | CI build; a `v*` tag publishes a GitHub Release |

## Build it yourself

You need the .NET 10 SDK with the MAUI Android workload, plus a JDK 17+ and the
Android SDK (Android Studio provides both).

```powershell
dotnet workload install maui-android
dotnet build Blinkjot/Blinkjot.csproj -t:InstallAndroidDependencies -f net10.0-android -p:AcceptAndroidSDKLicenses=True
dotnet run build-apk.cs      # → dist/blinkjot.apk
```

`build-apk.cs` also picks up a user-local SDK in `%LOCALAPPDATA%\dotnet-maui`
if one is present (no admin rights needed).

### Signing and releases

Android only installs an update if it's signed with the **same key** as the
installed app. Release builds are signed with the key stored in the GitHub
secrets `BLINKJOT_KEYSTORE_B64` (base64 of the keystore, key alias `blinkjot`)
and `BLINKJOT_KEYSTORE_PASS`.

To sign a local build with that key, set these environment variables before
running `build-apk.cs`:

- `BLINKJOT_KEYSTORE` = the keystore path;
- `BLINKJOT_KEYSTORE_PASS` = its password.

Without them, the build uses your Android debug key (fine for testing).

To publish a release:

```powershell
git tag v1.1.0; git push origin v1.1.0
```

## iPhone

The Blazor UI and services are cross-platform, but only Android is built today.
An iOS version needs:

- an `AVAudioRecorder` recorder with the `audio` background mode;
- `BGTaskScheduler` for sync;
- a Mac with Xcode to build it.

## License

[MIT](LICENSE)
