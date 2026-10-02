# Licensing and Monetization Decision Record

**Decision:** Keep the project under the repository's MIT license and distribute the reference build as free software with no advertising or in-app purchases.

## Rationale

- The client is an independent integration that uses Jellyfin APIs; the project must not imply ownership of Jellyfin software, media, servers, or marks.
- MIT gives downstream users and distributors clear permission to use, modify, and redistribute this client, subject to preserving the notice and license.
- A free, ad-free client keeps playback and sign-in focused and avoids collecting telemetry for an unimplemented commercial service.

## Boundaries and obligations

- Preserve the MIT license and copyright notice in source and redistributed copies.
- Keep the independent Jellyfin trademark disclaimer in user-facing releases; do not use Jellyfin's official logo or imply endorsement.
- Review third-party package licenses and generated assets before each release. Record any required notices with the package.
- Do not bundle, redistribute, or claim rights to users' media or a Jellyfin server.
- This decision does not grant permission to use Jellyfin trademarks beyond applicable project guidance; re-check the [branding policy](https://jellyfin.org/docs/general/contributing/branding/) before publishing.

## Commercial change gate

Any future paid tier, sponsorship, advertising, telemetry, hosted service, or store-exclusive distribution requires a new decision record and review of pricing disclosures, privacy policy, consent, support obligations, store rules, and all dependency/branding licenses. Do not add monetization by implication or through an SDK without updating those artifacts first.
