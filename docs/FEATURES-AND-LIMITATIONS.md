# Features and Limitations

This page describes the current product boundary. It is intentionally more conservative than marketing copy: a feature is listed as available only when it is present in the source and release materials.

## Available in the reference build

- Xbox-focused 10-foot shell and controller navigation
- Jellyfin server URL connection and user sign-in
- Quick Connect
- Home/library browsing, title search, media details, Continue Watching, and Next Up
- Playback with audio/subtitle selection and quality selection
- Direct-play preference with server-transcode fallback
- Per-server explicit trust for self-signed certificates
- Settings and API self-check diagnostics

## Known limitations

- Xbox console validation is still pending; this repository does not claim that the app has passed a complete Xbox hardware or Store certification test.
- Codec support is device and container dependent. A codec listed as native may still require transcoding because of its container, profile, bitrate, subtitles, audio format, or server policy.
- TrueHD and DTS/X are expected to require server-side transcoding in the default device profile.
- Live TV, music-library playback, photo slideshows, offline downloads, custom zoom/aspect controls, and an HDR tone-mapping toggle are not included.
- X and Y controller shortcuts are not mapped to favorites or item information.
- The app does not host media, manage Jellyfin servers, administer accounts, or replace server-side permissions and transcoding configuration.
- Self-signed certificate trust reduces transport authenticity protection. Accept a certificate only when you have verified the server and fingerprint through a trusted channel.

## Compatibility disclaimer

The server administrator controls available libraries, permissions, transcoding, reverse proxy behavior, and server logs. Network conditions and media files can change results. Report the exact app version, Xbox model/OS, Jellyfin version, media codecs/container, and whether direct play or transcoding was attempted.
