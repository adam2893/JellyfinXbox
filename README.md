# Jellyfin for Xbox Series

A beautiful, fast Jellyfin client built natively for Xbox Series X/S with **hardware AV1 decoding** and **native OPUS audio** support.

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

## 🎮 Controls

| Input | Action |
|-------|--------|
| **A** | Select / Play |
| **B** | Back |
| **X** | Favorite |
| **Y** | Info |
| **D-Pad** | Navigate |
| **Left Stick** | Navigate |
| **Right Trigger** | Seek forward |
| **Left Trigger** | Seek backward |
| **Menu** (☰) | Settings |
| **View** (⊞) | Audio/Subtitle selection |

## 🏗 Architecture

```
JellyfinXbox/
├── JellyfinXbox.sln
├── JellyfinXbox/                  # UWP App
│   ├── App.xaml                   # DI setup, theme resources
│   ├── ShellPage.xaml             # Navigation shell
│   ├── Views/
│   │   ├── LoginPage.xaml         # Server connect + auth + Quick Connect
│   │   ├── HomePage.xaml          # Library hub
│   │   ├── MediaDetailPage.xaml   # Hero backdrop + info
│   │   ├── PlayerPage.xaml        # Cinematic player + track selection
│   │   ├── SearchPage.xaml        # Search
│   │   ├── SettingsPage.xaml      # Quality, subtitles, playback, server
│   │   ├── QuickConnectPage.xaml  # Phone-to-Xbox linking
│   │   └── LibraryPage.xaml       # Library browsing
│   ├── ViewModels/                # MVVM with CommunityToolkit
│   ├── Services/
│   │   ├── NavigationService.cs
│   │   └── GamepadService.cs      # Xbox controller input
│   ├── Converters/
│   └── Styles/
├── JellyfinClient/                # API Library (portable)
│   ├── Models/Models.cs           # Jellyfin API DTOs
│   └── Services/JellyfinApiClient.cs
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

> 💡 For a properly signed, Xbox-ready `.msix` (plus the matching certificate), use the GitHub Actions workflow below — it handles packaging and signing for you.

## 🤖 Building with GitHub Actions (no Windows needed)

The workflow in [`.github/workflows/build-xbox-appx.yml`](.github/workflows/build-xbox-appx.yml) builds, signs, and packages the app on a free `windows-latest` runner — useful if you develop on macOS/Linux.

### Run it
1. Push this repo to GitHub
2. Go to **Actions → "Build Xbox AppX (UWP sideload)" → Run workflow** (it also runs automatically on push to `main`/`master` and on pull requests)
3. When it finishes, download the **`jellyfinxbox-appx`** artifact, which contains:
   - `JellyfinXbox_*.appx` — the signed sideload package
   - `JellyfinXbox.cer` — the code-signing certificate

### Install on your Xbox
1. Put the console in **Developer Mode** — install the free **Xbox Dev Mode** app from the Store, activate it with a Store developer account, then *Switch and restart* ([official docs](https://learn.microsoft.com/en-us/windows/uwp/xbox-apps/devkit-activation))
2. Install the certificate on the console (via **Dev Home** or the **Windows Device Portal** at `http://<xbox-ip>:11443`)
3. Install the app — Dev Home's app installer (or `Add-AppxPackage` through the Device Portal) with the `.appx` file

> ⚠️ **Certificates:** by default the workflow generates a **new** self-signed certificate every run (subject `CN=AutoClaw`, matching the manifest `Publisher`), so the `.cer` must be re-installed after each build. For a stable certificate — install it once and keep it across all builds:
> 1. Create a `.pfx` on Windows: `New-SelfSignedCertificate -Type CodeSigningCert -Subject "CN=AutoClaw" -KeyExportPolicy Exportable -KeySpec Signature -KeyUsage DigitalSignature` then `Export-PfxCertificate`
> 2. Add repo **secrets** `CERT_PFX_B64` (base64 of the `.pfx`) and `CERT_PASSWORD`
> 3. Upload the matching `.cer` to your console once

## 🎬 Media Codec Support

The Xbox device profile is tuned for maximum direct play:

| Codec | Support | Notes |
|-------|---------|-------|
| **AV1** | ✅ Native HW | Xbox Series X/S hardware decoder |
| **HEVC/H.265** | ✅ Native HW | Xbox hardware decoder |
| **H.264/AVC** | ✅ Native HW | Universal support |
| **VP9** | ✅ Native SW | Software decode |
| **OPUS** | ✅ Native | Direct play, no transcoding |
| **AAC** | ✅ Native | Direct play |
| **FLAC** | ✅ Native | Lossless audio direct play |
| **TrueHD** | ✅ Pass-through | |
| **DTS/X** | ✅ Pass-through | |

### Transcoding (fallback)
When direct play isn't possible, the server transcodes to:
- **Video**: AV1 or H.264 via HLS
- **Audio**: OPUS or AAC

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
- [ ] Live TV support
- [ ] Music library view with now playing bar
- [ ] Photo slideshow mode
- [x] User profile switching (via Settings → Switch User / login screen)
- [ ] Offline downloads (USB storage)
- [ ] Custom aspect ratio / zoom controls
- [ ] HDR tone mapping toggle

## 📄 License

MIT
