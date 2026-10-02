# Changelog

All notable changes to **XboxJellyfish** (formerly JellyfinXbox).

## [1.0.0] — 2026-08-03

### Rename & branding
- Renamed the app from "Jellyfin" to **XboxJellyfish** for compliance with [Jellyfin's branding policy](https://jellyfin.org/docs/general/contributing/branding/) (the "Jellyfin" name is reserved for the project and its official clients).
- Package identity changed to `com.boxjellyfish.app` (publisher `CN=AutoClaw` unchanged, so the sideload cert still matches).
- New pixel-art jellyfish logo (Jellyfin purple→blue gradient, transparent background) generated with Pixellab; all manifest tile assets regenerated from the master (Store, 44–310 squares, Wide310×150, SplashScreen, 1024 icon); in-app Shell rail + Login logo now use the artwork.

### Critical fixes
- **Session/DI bug:** `JellyfinApiClient` held all session state but was registered as a transient typed client — every page resolved a fresh unauthenticated copy, so login never survived navigation. Now a singleton with a shared `HttpClient`.
- **No persistence:** server URL, token, user, and settings were memory-only. Added `ISessionStore` (portable) + `SessionStore` (UWP `LocalSettings`) with restore-on-construct, persist-on-change, clear-on-logout, plus app `Suspending` handling.

### Playback reliability
- **Device profile overclaims:** removed codecs Xbox Media Foundation cannot decode (TrueHD, DTS, WMA, Vorbis, MP2, AIFF; WMV/MPG/MPEG/AVI/WebM containers). TrueHD/DTS audio now transcodes instead of black-screening. Profile kept: MKV/MOV/MP4/TS + AV1/H264/HEVC/VP9 + AAC/AC3/EAC3/MP3/FLAC/Opus.
- **No transcode fallback:** `Player_MediaFailed` now retries once with the server's `TranscodingUrl` before reporting stopped.
- **Seek slider (H4):** was OneWay-bound with no handler (dragging did nothing). Now TwoWay-bound to a `SliderSeconds` property with a drag gate so playback updates don't fight the user's thumb; keyboard arrows work natively.

### Gamepad input (H2)
- `GamepadService` was dead code (never instantiated). Now registered as a singleton and started from the Shell (`Loaded` — the shell is `Window.Content`, so `OnNavigatedTo` never fires).
- Shell: Menu → Settings, View → Quick Connect.
- Player: right/left trigger → hold-to-repeat seek (400 ms cadence), View → track panel, A → play/pause, B → stop & back, Menu → show transport.
- Reveal focus visuals enabled on `Windows.Xbox`.
- Handler delegates stored as fields so `+=`/`-=` match exactly.

### Security (H1)
- Replaced the unconditional `ServerCertificateCustomValidationCallback => true` with **per-server certificate trust**: `CertificateTrustService` persists approved thumbprints; untrusted servers trigger a login-page prompt (host/issuer/thumbprint shown), trust persists across launches, and login auto-retries after accepting.

### Jellyfin API contract (v12 spec)
Cross-referenced every endpoint against the published OpenAPI spec (`api.jellyfin.org`, v12.0.0) and fixed six breaks:
1. **PlaybackInfo body** — device profile was sent as a string under the wrong property (`DeviceProfileJson`); now posted as the `DeviceProfile` object so direct-play decisions actually reach the server.
2. **Items routes restructured** — `/Users/{id}/Items*` → `/Items`, `/UserItems/Resume`, `/UserViews`, `/UserPlayedItems`, `/UserFavoriteItems` (userId as query param).
3. **Quick Connect** — `/QuickConnect/Init` → `/Initiate`; authorize takes the display code; token exchange is `POST /Users/AuthenticateWithQuickConnect` (previously read from the wrong endpoint).
4. `QuickConnectAuthorizeResponse.Authorized` → `Authenticated` (actual field).
5. **Session reporting** — `/Sessions/Playing*` now sends JSON bodies (form-encoded was rejected).
6. **Stream URLs** — `ApiKey=` → `api_key=` (lowercase; Jellyfin's auth middleware reads it case-sensitively) — stream URLs would otherwise 401.

### Diagnostics & UX
- **API Self-Check page** (Settings → API Self-Check): probes server info, session, views, resume, library, search, next-up, playback info + stream URL auth, images, and Quick Connect; reports per-call success/detail/elapsed on-device.
- **Switch User** action in Settings (returns to the public-user picker).
- Login pre-fills the server URL from a restored session.

### Toolchain & warnings
- All 66 `[ObservableProperty]` fields converted to **partial properties** (MVVMTK0045 AOT/WinRT compatibility); `LangVersion preview` required (MVVMTK0041); property initializers moved into constructors.
- MVVMTK0034 (`_settings` direct reference) and all nullability warnings fixed.
- `MaxVersionTested` → 10.0.26100.0 on both device families; removed unused/restricted capabilities (`mediaRelay`, library caps) and the dead `jellyfin://` protocol extension.

### CI / packaging
- The workflow "passed" for weeks while **never producing a package** (artifacts contained only the 762-byte `.cer`). Fixed:
  - `GenerateAppxPackageOnBuild=true` is what actually triggers MSIX packaging (`-t:Build` alone only compiled).
  - Signing PFX imported into `Cert:\CurrentUser\My` and signing via `PackageCertificateThumbprint` (key-file import failed with APPX0105/0107).
  - Manifest `BackgroundColor="Translucent"` rejected by MakeAppx → `transparent`.
  - Collect step now **throws** when no package is produced (was a silent warning).
- **Self-contained packaging:** the MSIX was framework-dependent (no .NET runtime bundled) and died right after the splash on machines without the .NET UWP runtime. Now `SelfContained=true` + `RuntimeIdentifier=win-x64` — the runtime ships inside the MSIX (~45 MB).
- README corrected: VS 2026/18 (not 2022), SDK 26100, x64-only (no ARM64), accurate install instructions for Windows and Xbox.
