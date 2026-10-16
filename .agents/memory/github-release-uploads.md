---
name: GitHub release uploads
description: Limits and safe alternatives for binary releases through the connected GitHub integration.
---

**Rule:** Before publishing a release asset, verify that the GitHub connector can reach the upload host. If binary upload is blocked, use an existing release workflow only when the tag points to the intended source; otherwise ask the user to upload the binary through GitHub.

**Why:** The connected GitHub App could create and inspect releases, but its API proxy rejected `uploads.github.com`. The repository's release workflow builds from tagged GitHub source, which may differ from a binary built only in this workspace.

**How to apply:** Check the repo's latest release workflow and source/tag state before creating a tag. Clean up any unpublished draft created by a failed upload, then verify the existing release and tags remain unchanged.
