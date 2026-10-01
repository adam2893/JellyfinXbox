# XboxJellyfish Privacy Policy

**Effective date:** 2026-10-01

XboxJellyfish is an independent, community-developed client for connecting to a Jellyfin server. This policy explains what the app does with information when you install and use it.

## Information the app handles

XboxJellyfish processes the following information on your device or Xbox:

- **Server connection details:** the server URL, server name, and the certificate thumbprints you explicitly trust.
- **Authentication data:** the access token and selected user profile needed to sign in to your server. These are stored locally and sent only to the server URL you choose.
- **Playback and library requests:** searches, library metadata, playback progress, and media URLs requested from your Jellyfin server.
- **Local preferences:** settings such as playback quality, subtitle preferences, and controller preferences.

The app does not create an XboxJellyfish account, sell personal information, or operate a hosted media service. We do not receive your Jellyfin server credentials or media-library data unless you voluntarily include diagnostic details in a support request.

## Network communication

The app communicates with the Jellyfin server you configure. Your server administrator can see and control requests made to that server according to that server's own privacy policy and logs. Avoid connecting to a server you do not trust.

If you explicitly accept a self-signed certificate, XboxJellyfish records the certificate thumbprint for that server so the trust decision can be reused. Removing the server or clearing app data removes this local trust record.

## Diagnostics and support

XboxJellyfish does not include a required analytics or advertising service. If you contact maintainers, you may choose to provide logs, screenshots, device details, or server-version information. Please redact tokens, passwords, private server URLs, IP addresses, and personal data before sharing diagnostics.

## Children

XboxJellyfish is not directed at children and does not knowingly collect information from children.

## Your choices

You can remove saved server sessions, sign out, clear local app data, or uninstall the app. Your Jellyfin server administrator remains responsible for server-side account deletion, logs, backups, and retention.

## Changes and contact

We may update this policy when the app's data practices change. Publish the current policy with each release. For privacy questions, use the support channel listed in the project repository or release notes.

This document is informational and is not legal advice. Operators distributing a modified build should review it for their jurisdiction, hosting model, telemetry, and support process.
