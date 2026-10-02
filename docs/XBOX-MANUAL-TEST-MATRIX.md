# Xbox Manual Test Matrix

Status for this repository: **NOT RUN / Xbox validation pending**. Complete this matrix on the exact signed package intended for release. Record tester, date, package hash, Xbox model, OS/build, controller, Jellyfin server version, network type, and test media.

| ID | Area | Steps | Expected result | Result / evidence |
|---|---|---|---|---|
| X-01 | Install | Install package and matching certificate in Dev Mode | Install completes without an identity/dependency error | |
| X-02 | Launch | Cold launch, suspend, resume, relaunch | Shell loads; no crash, blank view, or focus trap | |
| X-03 | Navigation | Navigate every shell, dialog, settings, login, library, and detail view with D-pad and stick | Focus is visible and reaches every actionable control | |
| X-04 | Buttons | Use A, B, Menu, and View in shell and player | Actions match the documented controls; no accidental activation | |
| X-05 | Seeking | Hold both triggers, seek near start/end, pause and resume | Seek direction, repeat behavior, progress, and recovery are correct | |
| X-06 | Login | Connect, sign in, sign out, switch user | Correct user and private library state are shown; no stale data remains | |
| X-07 | Quick Connect | Start Quick Connect and complete linking from a phone/browser | Code/status updates correctly and sign-in completes | |
| X-08 | Servers | Connect to two servers and switch between them | URL, user, libraries, settings, and playback state do not cross-contaminate | |
| X-09 | Trust | Test trusted, self-signed accepted, self-signed rejected, and changed certificate | Prompt and stored trust behavior match the security documentation | |
| X-10 | Direct play | Play H.264, HEVC, AV1, AAC/OPUS, subtitles, and an HDR sample where available | Playback is stable or a clear server fallback/error is shown | |
| X-11 | Transcoding | Play TrueHD/DTS or another known unsupported combination | Server transcode starts or a readable, recoverable failure appears | |
| X-12 | Controls | Change audio/subtitle tracks and quality while playing | Selection applies without losing playback or focus | |
| X-13 | Recovery | Disconnect network/server during login and playback; retry/back out | Error is readable, secrets are not exposed, and retry/back works | |
| X-14 | Diagnostics | Run Settings → API Self-Check | Results identify pass/fail endpoints without exposing credentials | |
| X-15 | Upgrade | Install over prior version, then uninstall | Data/session behavior matches release notes; uninstall is clean | |

## Exit rule

Every failed row needs a tracked issue, owner, severity, and explicit release decision. “Not tested” is not “passed.”
