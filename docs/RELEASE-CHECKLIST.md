# Release Checklist

Use this checklist for every candidate package. Attach the completed record to the release. A checked build/desktop item is not evidence of Xbox validation.

## Release record

- [ ] Version, commit SHA, package hash, build date, and signing identity recorded
- [ ] Target architecture and package identity match the intended channel
- [ ] Release notes describe new behavior, known limitations, and unresolved issues
- [ ] Store listing, privacy URL, support URL, and independent-project disclaimer reviewed
- [ ] License and monetization decision record reviewed (`LICENSING-AND-MONETIZATION.md`)

## Build and package

- [ ] Clean restore and Release build complete
- [ ] Package installs on the supported non-Xbox validation environment
- [ ] Package launches and uninstall/upgrade paths are recoverable
- [ ] Certificate/dependency instructions match the artifact and distribution channel
- [ ] No debug endpoints, test credentials, tokens, or private URLs are included

## Functional smoke test

- [ ] Login, Quick Connect, user switching, server switching, browsing, search, and playback checked
- [ ] Audio/subtitle selection, quality selection, seek, resume, Next Up, and transcode fallback checked
- [ ] Certificate trust and API self-check checked
- [ ] Error, offline, unauthorized, empty, and recovery states checked
- [ ] Manual Xbox matrix is attached or explicitly marked **NOT RUN** (`XBOX-MANUAL-TEST-MATRIX.md`)

## Xbox gate

- [ ] Xbox hardware/model and OS are recorded
- [ ] Xbox test matrix completed by a named tester
- [ ] Certification/store requirements reviewed for the actual submission channel
- [ ] **Do not mark release-ready for Xbox until the Xbox gate is complete.**
