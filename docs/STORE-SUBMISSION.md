# Xbox Store submission

The sideload workflow is not the Store workflow. Use **Prepare Xbox Store package** only after the project is associated with the Microsoft Store in Partner Center.

## One-time Partner Center setup

1. Create a Microsoft Partner Center developer account and enroll in the Windows and Xbox program.
2. Create the XboxJellyfish app and reserve its name.
3. Associate the project with that Store app in Visual Studio/Partner Center.
4. Replace the placeholder `Identity Name` and `Publisher` in `JellyfinXbox/Package.appxmanifest` with the assigned Store identity. The workflow intentionally fails while `com.boxjellyfish.app` or `CN=AutoClaw` is still present.
5. Publish the privacy policy and support page at stable HTTPS URLs.

## Build the upload artifact

Run **Actions → Prepare Xbox Store package**. It produces an `.msixupload`/`.appxupload` artifact using `UapAppxPackageBuildMode=StoreUpload`; it does not use the self-signed certificate from the sideload workflow. Upload that package file to the Partner Center **Packages** page, not the sideload `.msix`.

## Submission checklist

- Upload the exact Store upload artifact from the audited commit.
- Select Xbox device-family availability in Partner Center.
- Complete pricing/availability, properties, category, age rating, Store listing, Xbox images, privacy URL, support URL, and certification notes.
- Provide a test account and exact sign-in steps if certification cannot reach the app without a Jellyfin server.
- Submit for certification, review the report, and publish only after the Xbox package passes certification.

The current repository cannot complete the Partner Center association or submit on its own because those steps require the publisher's Microsoft account and product identity.
