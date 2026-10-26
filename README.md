# XboxJellyfish

A beautiful, fast **Jellyfin client** built natively for Xbox Series X/S with **hardware AV1 decoding** and **native OPUS audio** support.

## ✨ Features

- **Native AV1 Hardware Decode** — Xbox Series X/S AV1 hardware acceleration via Media Foundation
- **Native OPUS Audio** — Direct play without transcoding
- **Audio & Subtitle Track Selection** — Switch audio languages and toggle subtitles during playback
- **Quick Connect** — Link your Xbox to Jellyfin from your phone — no keyboard needed
- **Video Quality Control** — Lower bitrate (120 Mbps → 2 Mbps) to stop buffering on slow connections
- **Settings Page** — Streaming quality, subtitle mode/size, playback options, server info
- **10-Foot UI** — Designed for couch viewing with gamepad-first navigation
- **Cinematic Player** — Gradient overlays, auto-hiding transport controls, codec info display
- **Fast & Lightweight** — Custom API client, no heavy SDK, lazy loading
- **Beautiful Dark Theme** — Deep blacks with purple accent, glass morphism, smooth animations
- **Direct Play Priority** — AV1 > HEVC > H264 codec preference, falls back to transcoding
- **HDR Detection** — Automatic HDR badge display
- **BlurHash Placeholders** — Instant visual placeholders while images load
- **Continue Watching** — Resume where you left off
- **Next Up** — Episode queue for TV shows
- **Search** — Real-time search across your library
- **Switch User** — Profile switching from Settings (returns to the public-user picker)
- **API Self-Check** — On-device diagnostics that probe every Jellyfin endpoint the app uses
- **Temporary developer event log** — Local playback/navigation log available from Settings → API Self-Check; nothing is uploaded automatically
- **Per-Server TLS Trust** — Self-signed server certificates are accepted only after you explicitly trust them (persisted by thumbprint)

## 🎮 Controls

| Input | Action |
|-------|--------|
| **A** | Select / Play / Pause |
| **B** | Back / Stop |
| **D-Pad / Left Stick** | Navigate (native focus) |
| **Right Trigger** | Seek forward (hold to repeat) |
| **Left Trigger** | Seek backward (hold to repeat) |
| **Menu** (☰) | Settings (shell) / show transport (player) |
| **View** (⊞) | Quick Connect (shell) / Audio & Subtitle panel (player) |

> X (favorite) and Y (info) from the original design are not yet mapped — there are no favorite/info controls in the UI yet.

## 🏗 Architecture

```
JellyfinXbox/
├── JellyfinXbox.sln
├── JellyfinXbox/                  # UWP App (XboxJellyfish)
│   ├── App.xaml                   # DI setup, theme resources
│   ├── App.xaml.cs                # DI, TLS trust handler, suspend handling
│   ├── ShellPage.xaml             # Navigation shell + gamepad shortcuts
│   ├── Views/
│   │   ├── LoginPage.xaml         # Server connect + auth + Quick Connect + TLS trust prompt
│   │   ├── HomePage.xaml          # Library hub
│   │   ├── MediaDetailPage.xaml   # Hero backdrop + info
│   │   ├── PlayerPage.xaml        # Cinematic player + track selection + gamepad
│   │   ├── SearchPage.xaml        # Search
│   │   ├── SettingsPage.xaml      # Quality, subtitles, playback, server, actions
│   │   ├── QuickConnectPage.xaml  # Phone-to-Xbox linking
│   │   ├── LibraryPage.xaml       # Library browsing
│   │   └── DebugPage.xaml         # API self-check diagnostics
│   ├── ViewModels/                # MVVM with CommunityToolkit (partial properties)
│   ├── Services/
│   │   ├── NavigationService.cs
│   │   ├── GamepadService.cs      # Xbox controller input (poll-based)
│   │   ├── SessionStore.cs        # LocalSettings persistence (ISessionStore)
│   │   └── CertificateTrustService.cs  # Per-server cert trust allowlist
│   ├── Converters/
│   └── Styles/
├── JellyfinClient/                # API Library (portable, net8.0)
│   ├── Models/Models.cs           # Jellyfin API DTOs + device profile
│   ├── Models/QuickConnect.cs     # Quick Connect DTOs + user settings
│   ├── Models/SelfCheck.cs        # API self-check results
│   ├── Services/ISessionStore.cs  # Persistence abstraction
│   └── Services/JellyfinApiClient.cs  # Jellyfin API client (v12 contract)
└── README.md
```

## 🔧 Tech Stack

- **UWP C#** (Windows.UI.Xaml on modern .NET)
- **MVVM** with CommunityToolkit.Mvvm 8.4
- **Windows.Media.Playback** for native media pipeline
- **Media Foundation** for codec support (AV1, HEVC, H264, OPUS, FLAC, etc.)
- **Jellyfin REST API** — lightweight custom client
- **DI** via Microsoft.Extensions.DependencyInjection

## 📋 Prerequisites

1. **Windows 10/11** dev machine
2. **Visual Studio 2026 (18.x)** — required for the Modern .NET UWP toolchain
3. **UWP workload** installed via Visual Studio Installer
4. **Windows SDK** 10.0.26100.0 or later
5. **.NET 9.0 SDK** (with the Modern .NET UWP toolchain from Visual Studio 18 / .NET 9)
6. **Xbox One/Xbox Series X/S** (for deployment)
   - Enable **Dev Mode** on the console (free, but requires a Microsoft Store developer account to activate — see [Xbox Developer Mode activation](https://learn.microsoft.com/en-us/windows/uwp/xbox-apps/devkit-activation))

## 🚀 Build & Run

### Desktop (Development)
```bash
# Restore dependencies
dotnet restore JellyfinXbox.sln

# Build
dotnet build JellyfinXbox/JellyfinXbox.csproj -c Debug
```

### Visual Studio (Recommended)
1. Open `JellyfinXbox.sln` in Visual Studio 2026
2. Set **JellyfinXbox** as the startup project
3. Set target to **x64** (Xbox consoles are x64 — there is no ARM64 build)
4. Press **F5** to build and deploy

### Deploy to Xbox
1. On your Xbox, go to **Settings > Dev Home > Switch to Dev Mode**
2. Install the Dev Mode app from the Store
3. In the Dev Mode app, find your Xbox's IP address
4. In Visual Studio: **Project > Properties > Debug > Target Device** → Remote Machine
5. Enter the Xbox IP address
6. Set authentication mode to **Universal (Unencrypted Protocol)**
7. Press **F5**

### Sideloading (no Visual Studio)
```bash
# Build the .msix package (use the GitHub Actions workflow below — it
# produces a signed package + matching certificate for you)
```

> 💡 For a properly signed, Xbox-ready `.msix` (plus the matching certificate), use the GitHub Actions workflow below — it handles packaging and signing for you. The package is **self-contained** (the .NET runtime is bundled inside the MSIX), so it runs on any Windows 10/11 machine or Xbox without extra runtime installs.

## 🤖 Building with GitHub Actions (no Windows needed)

The workflow in [`.github/workflows/build-xbox-appx.yml`](.github/workflows/build-xbox-appx.yml) builds, signs, and packages the app on a free `windows-latest` runner — useful if you develop on macOS/Linux.

### Run it
1. Push this repo to GitHub
2. Go to **Actions → "Build Xbox AppX (UWP sideload)" → Run workflow** for a package artifact (it runs automatically on push to `main`/`master`)
3. When it finishes, download the **`jellyfinxbox-appx`** artifact, which contains:
   - `JellyfinXbox_*.msix` — the signed, self-contained sideload package (~45 MB)
   - `Microsoft.VCLibs.*.appx` — the VC++ runtime dependency (install alongside the MSIX)
   - `JellyfinXbox.cer` — the code-signing certificate

### Audit before release

The **Audit Xbox AppX** workflow runs automatically on pull requests and can be started manually from **Actions → Audit Xbox AppX**. It runs the client tests, builds the x64 package, verifies the packaged runtime and certificate, runs the Windows App Certification Kit, and uploads a `jellyfinxbox-audit` artifact with an audit report and SHA-256 manifest. Install that exact artifact on Xbox and complete the manual test matrix before treating the build as release-ready.

### Prepare a Store submission

After associating the project with its Partner Center app and replacing the placeholder package identity, run **Actions → Prepare Xbox Store package**. It produces the `.msixupload`/`.appxupload` artifact required for Partner Center. See [`docs/STORE-SUBMISSION.md`](docs/STORE-SUBMISSION.md); the sideload `.msix` is not the Store upload artifact.

### Install on Windows 10/11
1. Trust the certificate: double-click `JellyfinXbox.cer` → Install Certificate → **Local Machine** → **Trusted Root Certification Authorities**. (Use the `.cer` from the same zip as the `.msix` — every build mints a fresh cert.)
2. Enable sideloading: Settings → Privacy & security → For developers → **Developer Mode**.
3. Install: right-click `JellyfinXbox_1.0.0.0_x64.msix` → **Install** (or `Add-AppxPackage -Path .\JellyfinXbox_1.0.0.0_x64.msix -DependencyPath .\Microsoft.VCLibs.x64.14.00.appx` in an admin PowerShell).
4. Launch **XboxJellyfish** from Start, connect to your server, and run **Settings → API Self-Check** to validate the connection.

### Install on your Xbox
1. Put the console in **Developer Mode** — install the free **Xbox Dev Mode** app from the Store, activate it with a Store developer account, then *Switch and restart* ([official docs](https://learn.microsoft.com/en-us/windows/uwp/xbox-apps/devkit-activation))
2. Install the certificate on the console (via **Dev Home** or the **Windows Device Portal** at `http://<xbox-ip>:11443`)
3. Install the app — Dev Home's app installer (or `Add-AppxPackage` through the Device Portal) with the `.msix` file

> ⚠️ **Certificates:** by default the workflow generates a **new** self-signed certificate every run (subject `CN=AutoClaw`, matching the manifest `Publisher`), so the `.cer` must be re-installed after each build. For a stable certificate — install it once and keep it across all builds:
> 1. Create a `.pfx` on Windows: `New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=AutoClaw" -KeyExportPolicy Exportable -KeySpec Signature -KeyUsage DigitalSignature` then `Export-PfxCertificate`
> 2. Add repo **secrets** `CERT_PFX_B64` (base64 of the `.pfx`) and `CERT_PASSWORD`
> 3. Upload the matching `.cer` to your console once

## 🎬 Media Codec Support

The Xbox device profile is tuned for maximum direct play with any Jellyfin server:

| Codec | Support | Notes |
|-------|---------|-------|
| **AV1** | ✅ Native HW | Xbox Series X/S hardware decoder |
| **HEVC/H.265** | ✅ Native HW | Xbox hardware decoder |
| **H.264/AVC** | ✅ Native HW | Universal support |
| **VP9** | ✅ Native | Software decode |
| **OPUS** | ✅ Native | Direct play, no transcoding |
| **AAC** | ✅ Native | Direct play |
| **FLAC** | ✅ Native | Lossless audio direct play |
| **TrueHD** | 🔄 Transcoded | No Xbox Media Foundation decoder |
| **DTS/X** | 🔄 Transcoded | No Xbox Media Foundation decoder |

### Transcoding (fallback)
- If a source can't be direct-played (e.g. TrueHD/DTS audio, unsupported container), the player retries automatically with the server's transcode URL before giving up.
- The server transcodes to: **Video**: AV1 or H.264 via HLS — **Audio**: OPUS or AAC.

## 🎨 Design Language

- **Color**: Deep black surfaces (#0B0B0F) with purple accent (#6C63FF)
- **Typography**: Segoe UI Variable, bold titles, generous spacing
- **Cards**: 12px rounded corners, subtle elevation via theme shadows
- **Navigation**: Slim 80px side rail with icon buttons
- **Player**: Full-screen with gradient overlays, auto-hide transport, codec badges
- **Loading**: Acrylic blur + progress ring, no jarring transitions

## 📝 Roadmap

- [x] Audio/subtitle track selection UI
- [x] Settings page
- [x] Quick Connect
- [x] Video quality control
- [x] User profile switching (Settings → Switch User)
- [x] API self-check diagnostics
- [x] Gamepad controls (seek triggers, Menu/View shortcuts)
- [x] Transcode fallback when direct play fails
- [x] Per-server TLS certificate trust
- [ ] X (favorite) / Y (info) gamepad shortcuts + favorite UI
- [ ] Live TV support
- [ ] Music library view with now playing bar
- [ ] Photo slideshow mode
- [ ] Offline downloads (USB storage)
- [ ] Custom aspect ratio / zoom controls
- [ ] HDR tone mapping toggle

## 🏷 Branding

- **App name:** XboxJellyfish — per [Jellyfin's branding policy](https://jellyfin.org/docs/general/contributing/branding/), the "Jellyfin" name is reserved for the project and its official clients; third-party clients must use their own name and may reference Jellyfin as "a Jellyfin client".
- **Logo:** custom pixel-art jellyfish using Jellyfin's purple→blue color gradient on a unique shape (the gradient is permitted by the branding policy; the official logo shape is not).
- **Identity:** `com.boxjellyfish.app`, publisher `CN=AutoClaw`.

## 📄 License

MIT

## Release and support documentation

- [Store listing copy](docs/STORE-LISTING.md)
- [Features and limitations](docs/FEATURES-AND-LIMITATIONS.md)
- [Privacy policy](docs/PRIVACY.md)
- [Support guidance](docs/SUPPORT.md)
- [Independent Jellyfin disclaimer](docs/JELLYFIN-DISCLAIMER.md)
- [Release checklist](docs/RELEASE-CHECKLIST.md)
- [Xbox manual test matrix](docs/XBOX-MANUAL-TEST-MATRIX.md) — Xbox validation is explicitly pending until completed
- [Licensing and monetization decision](docs/LICENSING-AND-MONETIZATION.md)
