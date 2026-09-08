Drop — Current State

Project status

Project bootstrap and the first Windows transfer-core milestone are complete.

Current milestone

Day 1 Task 2 — streamed Windows TCP file-transfer core and automated tests (completed).

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
-   async TCP sender and receiver implement the automated single-file Protocol v1 flow: HELLO / HELLO_ACK, OFFER / ACCEPT, FILE_START, raw payload, FILE_END, FILE_RESULT, COMPLETE / COMPLETE_ACK
-   sender performs the Protocol v1 two-pass file read: SHA-256 first, then bounded-buffer streaming
-   receiver streams to a uniquely named `.drop-partial` file and calculates SHA-256 incrementally
-   received files are finalized only after exact byte-count and SHA-256 verification
-   incoming filenames are sanitized, constrained to the destination directory, and resolved without overwriting duplicates
-   truncated payloads, hash mismatches, and cancellation fail safely and clean up partial files
-   file sizes, byte counters, results, and progress reports use 64-bit `long` values
-   real TCP loopback tests cover small binary, zero-byte, and multi-megabyte files, SHA-256 equality, wrong-hash rejection, truncation, traversal safety, duplicate names, and deterministic receiver cancellation
-   2026-09-09 Release build: succeeded with 0 warnings and 0 errors
-   2026-09-09 automated tests: 17 passed, 0 failed, 0 skipped

In progress

-   no implementation task is currently in progress

Not started

Windows

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

Day 2 Task 1 — implement reusable Windows LAN mDNS advertisement and discovery,
kept separate from the transfer protocol and UI, so nearby Windows peers can be
found without manual IP or port entry.

Known issues

-   the initial TCP transfer API supports exactly one file per session; sequential multi-file sessions remain to be implemented
-   the receiver auto-accepts for the headless transfer-core proof; user acceptance and trusted-device policy are not implemented
-   session inactivity/connect timeouts are not implemented yet
-   Protocol v1 currently uses plain TCP and does not authenticate or encrypt peers
-   the streaming path is 64-bit safe, but a physical multi-gigabyte transfer has not yet been run

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

2026-09-09 — Day 1 Task 2 Windows streamed TCP transfer core completed.
