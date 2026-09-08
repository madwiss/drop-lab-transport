Drop — Current State

Project status

Project bootstrap is complete and Windows protocol-core implementation is in progress.

Current milestone

Day 1 Task 1 — Windows/.NET protocol core and automated tests (completed).

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
-   .NET 10 Windows solution created under apps/windows
-   reusable Drop.Protocol library created separately from future UI
-   Protocol v1 length-prefixed JSON control-frame codec implemented
-   control-frame tests cover round trips, Unicode, fragmented reads, size validation, truncation, and invalid JSON
-   Release build and all automated tests pass

In progress

-   completing initial project documentation
-   defining Drop Protocol v1
-   defining testing requirements
-   preparing repository for first commit

Not started

Windows

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

Exact next task

Day 1 Task 2 — implement the Windows TCP sender and receiver transfer path using
the protocol core, streamed file payloads, temporary partial files, and SHA-256
integrity verification.

Known issues

TCP networking and file-transfer behavior are not implemented yet.

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

2026-09-09 — Day 1 Task 1 protocol-core scaffold completed.
