Drop — Current State

Project status

Project bootstrap in progress.

No application code has been implemented yet.

Current milestone

Day 0 — Repository and project foundation.

Completed

-   GitHub repository created
-   repository cloned to development PC
-   initial repository directory structure created
-   AGENTS.md created
-   docs/PRODUCT.md created
-   docs/ARCHITECTURE.md created
-   docs/ROADMAP.md created
-   platform directories created for Windows, iOS, and Android
-   protocol directory created

In progress

-   completing initial project documentation
-   defining Drop Protocol v1
-   defining testing requirements
-   preparing repository for first commit

Not started

Windows

-   Windows solution/project
-   TCP sender
-   TCP receiver
-   file streaming
-   SHA-256 verification
-   mDNS discovery
-   Windows UI
-   trusted devices

iOS

-   Xcode project
-   discovery
-   sender
-   receiver
-   Share Extension

Android

-   Android project
-   discovery
-   sender
-   receiver
-   Sharesheet integration

Direct peer-to-peer

-   Wi-Fi Aware investigation/implementation
-   Windows direct P2P investigation/implementation

Next milestone

Day 1 — Windows transfer core.

The first implementation milestone must prove:

1.  A file can be streamed from a Windows sender to a Windows receiver
    over TCP.
2.  The full file is never loaded into memory.
3.  The receiver writes to a temporary partial file.
4.  SHA-256 integrity verification succeeds for valid transfers.
5.  Corrupt or mismatched transfers are rejected.
6.  The received file is bit-for-bit identical to the source.
7.  Automated tests and builds pass.

Immediate next tasks

1.  Finish docs/TESTING.md.
2.  Finish protocol/SPEC.md.
3.  Finish README.md.
4.  Review bootstrap documentation for consistency.
5.  Create the initial Git commit.
6.  Push the bootstrap repository to GitHub.
7.  Begin Day 1 Windows transfer-core implementation.

Known issues

None yet. Application implementation has not started.

Important constraints

-   MVP deadline: 7 days.
-   GitHub is the source of truth.
-   File transfers must remain local.
-   No cloud file transport.
-   No accounts.
-   No manual IP entry in the normal UX.
-   No QR-code requirement in the normal UX.
-   No compression, conversion, or quality loss.
-   Integrity must be verified before reporting success.
-   Avoid architecture changes that threaten the deadline.

Last updated

2026-09-09 — Initial project bootstrap.
