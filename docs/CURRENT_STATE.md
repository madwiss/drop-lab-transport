Drop — Current State

Project status

Project bootstrap, the Windows transfer core, and reusable Windows LAN discovery are complete.

Current milestone

Day 2 Task 1 — reusable Windows LAN mDNS advertisement and discovery (completed).

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
-   reusable `Drop.Discovery` library created separately from transport, Protocol v1, and future UI
-   `IDropDiscoveryService` exposes start/stop, current devices, and appeared/updated/disappeared events
-   Windows advertises `_drop._tcp` over mDNS/DNS-SD with device ID, device name, platform, protocol version, app version, and the TCP receiver port
-   mDNS records resolve into a transport-neutral nearby-device model with a stable device ID, display name, platform, protocol/app versions, address, and port
-   discovery deduplicates multiple service records and addresses by device ID, prefers a usable IPv4 endpoint when available, and ignores the local device ID
-   explicit mDNS goodbye records remove sources immediately; TTL expiry removes stale sources during a 15-second maintenance sweep
-   stop, caller cancellation, restart, and async disposal release the mDNS backend and its multicast sockets
-   deterministic discovery tests cover TXT/SRV/address parsing, validation, deduplication, ignore-self behavior, updates, source removal, TTL expiry, event flow, restart, cancellation, and disposal
-   2026-09-09 full Windows Release build: succeeded with 0 warnings and 0 errors
-   2026-09-09 all automated tests: 29 passed, 0 failed, 0 skipped

In progress

-   no implementation task is currently in progress

Not started

Windows

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

Day 2 Task 2 — create the minimal Windows application UI and nearby-device list
by consuming `IDropDiscoveryService`, without adding trust, tray behavior, or
notifications yet.

Known issues

-   the initial TCP transfer API supports exactly one file per session; sequential multi-file sessions remain to be implemented
-   the receiver auto-accepts for the headless transfer-core proof; user acceptance and trusted-device policy are not implemented
-   session inactivity/connect timeouts are not implemented yet
-   Protocol v1 currently uses plain TCP and does not authenticate or encrypt peers
-   the streaming path is 64-bit safe, but a physical multi-gigabyte transfer has not yet been run
-   mDNS discovery is limited to the local multicast-capable network; Windows Firewall rules, VPN routing, access-point client isolation, or networks that suppress multicast can prevent peers from appearing
-   advertised addresses are captured when discovery starts; after a material network-interface/address change, restart discovery to refresh the advertisement
-   disappearance after an ungraceful peer exit depends on DNS TTL expiry and can be reported up to one 15-second sweep interval after expiry
-   discovery metadata and device IDs are unauthenticated presence information and must not be treated as a trust or security identity
-   deterministic same-host mDNS integration is not in the normal test suite because multicast loopback, interface selection, firewall policy, and timing vary by machine; real two-device Windows LAN verification remains required

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

2026-09-09 — Day 2 Task 1 reusable Windows LAN mDNS advertisement and discovery completed.
