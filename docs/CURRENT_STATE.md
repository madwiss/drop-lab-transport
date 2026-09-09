Drop — Current State

Project status

Project bootstrap, the Windows transfer core, reusable Windows LAN discovery, and the minimal Windows send/receive UI are complete.

Current milestone

Day 2 Task 3 — minimal Windows incoming transfer offer UI with explicit accept/decline (completed).

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
-   native WPF application added under `apps/windows/src/Drop.Windows.App` without introducing another UI framework or package
-   Windows app creates and persists a stable local device ID under local application data
-   app startup automatically starts the existing LAN discovery service and advertises a real TCP receiver endpoint
-   nearby-device list shows discovered device name, platform, and available state and updates for appeared, changed, and disappeared devices
-   selected devices that disappear are removed safely and sending is disabled when no available device is selected
-   standard Windows file picker starts a single-file send through `TcpFileSender` using the endpoint supplied by discovery
-   protocol sender now exposes optional stage progress for connecting, waiting for acceptance, preparing, transferring, and completing without coupling it to UI types
-   Windows UI shows connecting, waiting/accepted, transferring, completing, completed, failed, and cancelled states plus 64-bit byte progress
-   concurrent sends from one UI instance are prevented; the active transfer can be cancelled
-   a minimal headless receiver host accepts the existing automatically accepted Protocol v1 flow and saves verified files to `Downloads\Drop`; no receiving UI was added
-   transfer presentation-state tests cover normal stage/progress flow, conflicting-send prevention, cancellation, and failure
-   2026-09-09 full Windows Release build: succeeded with 0 warnings and 0 errors
-   2026-09-09 all automated tests: 32 passed, 0 failed, 0 skipped
-   the Protocol v1 receiver now exposes validated sender, transfer, filename, and 64-bit file-size metadata through an asynchronous decision callback before sending `ACCEPT` or `DECLINE`
-   the Windows app shows an incoming-transfer panel with sender name, filename, size/progress, and explicit Accept and Decline buttons while keeping the UI responsive
-   accepted offers continue through the existing receiver streaming path into `Downloads\Drop`, including safe filenames, duplicate-name resolution, `.drop-partial` staging, and SHA-256 verification
-   declined offers send Protocol v1 `DECLINE` with `user_declined`, close without entering the payload flow, and create no received file
-   incoming presentation states cover offer, receiving, completed, failed, declined, and cancelled; incoming byte progress is wired to the existing receiver progress reports
-   the receiver host handles one presented incoming session at a time, tracks active sessions, and waits for cancellation cleanup during application shutdown
-   protocol integration tests cover accept/integrity and decline/no-file behavior; presentation tests cover accept, decline, receiving progress, and terminal state transitions
-   2026-09-09 full Windows Release build: succeeded with 0 warnings and 0 errors
-   2026-09-09 all automated tests: 35 passed, 0 failed, 0 skipped
-   real two-PC Windows testing transferred 1.47 GB successfully in one direction but exposed asymmetric connection failures when the receiving PC advertised both its physical `192.168.0.139` LAN address and an unreachable `172.27.112.1` virtual-adapter address
-   discovery endpoint selection now prefers a non-loopback IPv4 address on one of the sender's directly connected subnets, then another private IPv4 address, then another IPv4 address, while retaining IPv6 as a fallback
-   Windows send status and trace diagnostics show the exact selected remote IP address and port during connection attempts
-   regression tests cover selecting `192.168.0.139` over `172.27.112.1` for a sender on `192.168.0.0/24` and retaining private-IPv4/IPv6 fallback behavior
-   2026-09-09 full Windows Release build: succeeded with 0 errors; package-vulnerability audit emitted 5 warnings because the NuGet service index was unavailable
-   2026-09-09 all automated tests after the endpoint fix: 40 passed, 0 failed, 0 skipped

In progress

-   no implementation task is currently in progress

Not started

Windows

-   trusted devices
-   tray/background behavior
-   packaging

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

Day 2 Task 4 — add the minimal local trusted-device model and an explicit opt-in
auto-accept policy, keeping trust as Windows application policy without changing
Protocol v1 or adding accounts, cloud identity, tray behavior, or notifications.

Known issues

-   the initial TCP transfer API supports exactly one file per session; sequential multi-file sessions remain to be implemented
-   trusted-device storage and opt-in trusted-device auto-accept are not implemented; every incoming offer requires an explicit decision
-   session inactivity/connect timeouts are not implemented yet
-   Protocol v1 currently uses plain TCP and does not authenticate or encrypt peers
-   the streaming path is 64-bit safe; a 1.47 GB physical Windows-to-Windows transfer has completed successfully, while larger multi-gigabyte testing remains outstanding
-   mDNS discovery is limited to the local multicast-capable network; Windows Firewall rules, VPN routing, access-point client isolation, or networks that suppress multicast can prevent peers from appearing
-   advertised addresses are captured when discovery starts; after a material network-interface/address change, restart discovery to refresh the advertisement
-   disappearance after an ungraceful peer exit depends on DNS TTL expiry and can be reported up to one 15-second sweep interval after expiry
-   discovery metadata and device IDs are unauthenticated presence information and must not be treated as a trust or security identity
-   deterministic same-host mDNS integration is not in the normal test suite because multicast loopback, interface selection, firewall policy, and timing vary by machine; real two-device Windows LAN verification remains required
-   the Windows UI currently sends exactly one file per picker action because the transfer core supports one file per session
-   incoming sessions are presented one at a time; additional connected senders wait until the active incoming session ends
-   incoming offers are visible only in the open application window; tray integration and Windows notifications are not implemented
-   cancellation closes the active sender connection; Protocol v1 does not send an in-band CANCEL while raw payload bytes are in flight
-   the Windows app is an unpackaged framework-dependent WPF executable; installer/package work remains for Day 7

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

2026-09-09 — fixed real-device Windows endpoint selection for multi-adapter peers and added selected-endpoint diagnostics.
