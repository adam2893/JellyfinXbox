# XboxJellyfish Release Smoke Test

Run this checklist on the exact package intended for release. Record the app version, package hash, Xbox model, Xbox OS version, Jellyfin server version, network type, and tester/date before starting.

## Installation and first launch

- [ ] Install the package on a clean test Xbox or remove the previous test version first.
- [ ] Install or trust the matching package certificate as required for the distribution channel.
- [ ] Launch the app and confirm the shell loads without a crash, blank page, or focus trap.
- [ ] Confirm the independent-project notice and support location are included in release materials.

## Xbox controller and 10-foot navigation

- [ ] D-pad and left stick move focus in every shell, settings, login, library, detail, and dialog state.
- [ ] **A** selects controls and starts playback; it does not activate an unfocused control.
- [ ] **B** backs out of pages/dialogs and stops playback where specified.
- [ ] **Menu** opens Settings from the shell and reveals transport controls in the player.
- [ ] **View** opens Quick Connect from the shell and the audio/subtitle panel in the player.
- [ ] Left and right triggers seek backward/forward during playback and repeat correctly when held.
- [ ] Back navigation returns focus to a sensible control after closing overlays, panels, and dialogs.
- [ ] No keyboard or mouse is required for sign-in, server switching, playback, or error dismissal.

## Server connection and switching

- [ ] Connect to a reachable Jellyfin server with a valid certificate.
- [ ] Sign in, load the home/library view, search, open an item, and confirm the selected user is shown correctly.
- [ ] Sign out or switch user, then confirm the app returns to the expected user picker without stale private data.
- [ ] Add/connect to a second server and confirm its libraries, user, URL, and playback state are not mixed with the first server.
- [ ] Switch back to the first server and confirm its session and library load correctly.
- [ ] Run **Settings → API Self-Check** and verify expected endpoints pass.
- [ ] Test an unreachable host, invalid URL, refused connection, and server-side authentication failure. Each must show an actionable error and remain recoverable.

## Certificates and trust

- [ ] Connect to a server with a publicly trusted certificate; no unnecessary trust prompt appears.
- [ ] Connect to a server with a self-signed certificate; the app explains the risk and requires an explicit trust decision.
- [ ] Reject the certificate and confirm the connection fails safely without silently bypassing validation.
- [ ] Accept the certificate and confirm the server can reconnect afterward.
- [ ] Confirm trust is scoped to the intended server/certificate thumbprint and is not reused for another server.
- [ ] Change or replace the server certificate and confirm the app prompts again rather than trusting the changed certificate silently.

## Playback

- [ ] Play a direct-play H.264 or HEVC item; verify video, audio, pause, resume, seek, progress, and stop.
- [ ] Play an AV1 item on supported Xbox hardware and verify stable video and audio.
- [ ] Play an OPUS/AAC audio track and verify audible output and correct track selection.
- [ ] Open the audio/subtitle panel with the controller, change tracks, toggle subtitles, and close it without losing playback.
- [ ] Test a media item that requires transcoding (for example, unsupported audio) and verify the fallback starts or presents a clear failure.
- [ ] Change video quality and confirm the selected quality is sent to the server and reflected in playback behavior.
- [ ] Seek repeatedly, seek near the beginning/end, pause for at least 30 seconds, resume, and stop.
- [ ] Test malformed/unavailable media, a server disconnect during playback, and a dropped network connection. Verify errors are readable, controls remain usable, and retry/back navigation works.
- [ ] Confirm Continue Watching resumes near the stored position and Next Up advances to the expected episode.

## Error states and recovery

- [ ] Verify loading, empty-library, offline, unauthorized, server-error, playback-error, and certificate-error states have visible text and a recovery action.
- [ ] Confirm retry actions do not create duplicate dialogs, duplicate playback sessions, or an infinite spinner.
- [ ] Confirm errors do not expose access tokens, passwords, or unnecessary private server information.
- [ ] Suspend/resume the app and verify the session either restores safely or asks the user to reconnect.

## Release gate

- [ ] No crash, hang, focus trap, or unrecoverable error occurred.
- [ ] All failed checks have a tracked issue, owner, and release decision.
- [ ] Attach the completed checklist and package hash to the release record.
