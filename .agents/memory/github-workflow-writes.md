---
name: GitHub workflow write restrictions
description: Connector behavior when syncing GitHub Actions workflow files through the REST API
---

The GitHub connector may deny reads or writes to `.github/workflows/*` through the REST API while allowing ordinary repository operations. Do not assume the whole repository is inaccessible when one workflow path fails.

**Why:** A repository sync succeeded for other files after the workflow path was excluded; access to that path returned a Cloudflare 403 or GitHub 404 through the connector.

**How to apply:** Preserve the remote workflow, sync the remaining files, and tell the user which workflow was not updated. Do not repeatedly retry the same connector path; use a user-authorized Git workflow or GitHub UI if the workflow itself must be changed.
