# Support and Feedback

XboxJellyfish is an independent Jellyfin client. Support for the client is separate from support for Jellyfin server software, plugins, hosting, and accounts.

## Before opening a report

1. Reproduce the issue once and note the exact screen and action.
2. Check the release notes for known issues.
3. Confirm whether the same account and media work in another Jellyfin client.
4. Run **Settings → API Self-Check** when the issue concerns server connectivity.
5. Remove secrets from screenshots and logs: access tokens, passwords, Quick Connect codes, private URLs, and IP addresses.

## What to include

- App version and installation source.
- Xbox model, OS version, and whether the app is sideloaded or a development build.
- Jellyfin server version, client/server network location, and relevant plugin or reverse-proxy details.
- Controller model and whether the issue affects buttons, triggers, or focus navigation.
- Media details for playback issues: container, video/audio codecs, subtitle format, and whether direct play or transcoding was attempted.
- Reproduction steps, expected result, actual result, and any visible error text.
- A redacted self-check result or log excerpt, if relevant.

## Good feedback

Use one issue per report. A short title such as `Playback stops after seeking in HEVC file` is more useful than `player broken`. Feature requests should describe the user problem and proposed outcome, not only a particular implementation.

## Security and privacy reports

Do not publish credentials, tokens, unredacted server logs, or an exploitable vulnerability in a public issue. Use the repository's private security contact if one is provided. If no private channel exists, contact the maintainers before disclosure.

## Scope

The project maintainers can help with XboxJellyfish behavior. Jellyfin server configuration, media files, reverse proxies, certificates, and account permissions may require help from the server administrator or the relevant upstream project.
