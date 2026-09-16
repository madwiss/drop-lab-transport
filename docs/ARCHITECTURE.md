Drop — Architecture

Goal

The architecture must support a fast 7-day MVP while preserving clean
boundaries for future transport and discovery mechanisms.

The design should optimize for:

1.  Reliability
2.  Simplicity
3.  Cross-platform interoperability
4.  Minimal user friction
5.  Replaceable discovery and transport layers

High-level architecture

Drop is split into five logical layers:

1.  Platform UI
2.  Discovery
3.  Trust / pairing
4.  Transport
5.  Transfer protocol

These layers must remain conceptually separate.

The transfer protocol must not depend on one specific discovery or
transport technology.

Trust model

Drop separates three different identities and states:

- discovery identity: information used to find nearby devices, such as device identifiers advertised during discovery
- session identity: the cryptographically verified identity associated with an authenticated session
- local trust state: a local record that allows a previously accepted peer identity to be recognized as trusted

The first trust flow uses the existing manual accept step as the point where a user can establish trust. After acceptance, the peer fingerprint and device identity can be stored locally and associated with a trusted record.

Trust records are local application state. They do not require cloud services, accounts, QR codes, or a separate pairing ceremony.

The trust layer remains independent from discovery and transport. A discovered device is not trusted automatically, and Protocol v1 LAN compatibility is preserved while trust-aware flows are introduced.

Platform applications

Windows

Target: - C# - .NET - WinUI 3 or the simplest suitable native Windows UI
stack

Responsibilities: - run receiver service - advertise device presence -
discover nearby Drop devices - send files - receive files - show
incoming transfer notifications - save received files - manage trusted
devices - expose minimal settings

iOS

Target: - Swift - SwiftUI - Network framework where appropriate - Share
Extension

Responsibilities: - discover nearby Drop devices - send and receive
files where supported - integrate with Photos / Files share flow -
manage trust state - display transfer progress

Android

Target: - Kotlin - native Android UI - Android Sharesheet integration

Responsibilities: - discover nearby Drop devices - send and receive
files - integrate with Android sharing - manage trust state - display
transfer progress

MVP networking strategy

Phase 1 — LAN-first

The first reliable transport path should work when devices are already
on the same local network.

Discovery: - mDNS / Bonjour-style local service discovery

Transport: - TCP-based local connection - optionally TLS if
implementation cost remains low and does not threaten the deadline

Reason: - simple - broadly supported - easy to test - no manual IP
entry - no cloud dependency - suitable for validating the complete
product flow quickly

The user must never manually configure the LAN connection.

Phase 2 — direct peer-to-peer

After the LAN path is stable, direct device-to-device transports may be
added behind the same abstractions.

Potential mechanisms may include: - Wi-Fi Aware - Wi-Fi Direct - other
platform-supported peer-to-peer networking APIs

These must be verified against current official platform documentation
before architectural reliance.

Direct peer-to-peer work must not delay the core usable MVP.

Discovery abstraction

Define a discovery interface conceptually equivalent to:

    start()
    stop()
    advertise(deviceInfo)
    onDeviceFound(device)
    onDeviceLost(device)

The UI should only consume discovered Drop devices.

It must not know whether a device was found through: - mDNS - Wi-Fi
Aware - Wi-Fi Direct - Bluetooth-assisted discovery - another future
mechanism

Transport abstraction

Define a transport interface conceptually equivalent to:

    connect(device)
    listen()
    send(bytes)
    receive()
    close()

The transfer protocol operates on a reliable byte stream.

For MVP: - TCP over LAN is the preferred baseline

Future transports may implement the same contract.

Remote connectivity foundation

The connection model keeps routing, transport, and authentication
boundaries separate:

- Discovery finds possible nearby peers and supplies identifiers and
  connection hints.
- Routing selects how a session reaches a peer and owns connection state,
  retries, and recovery decisions.
- Transport opens a byte channel. The current implementation uses TCP on
  the LAN.
- Transfer protocol handles Drop messages and file integrity.
- Identity/authentication verifies cryptographic peer identity when an
  authenticated session is required.
- Application UI consumes session state without depending on discovery or
  transport details.

mDNS device IDs and other discovery identifiers are presence identifiers.
They are not cryptographic identities and must never be used as proof of
peer authenticity.

Future internet connectivity can add new routing and transport
implementations behind these boundaries. The architecture does not require
or select a cloud backend, relay provider, or vendor service.

Session recovery rules

Connection attempts and inactive sessions must have explicit timeout
boundaries. Retry decisions belong to the routing/session layer and must not
silently duplicate a transfer.

Automatic retry must never replay an unsafe payload after uncertainty about
whether the receiver accepted or persisted file bytes. A new transfer attempt
requires a new verified session state.

Transfer protocol

The Drop protocol is application-level and transport-independent.

It handles: - protocol version - device identity - transfer offer -
accept / decline - metadata - file streaming - completion - integrity
verification - cancellation - errors

The canonical protocol specification lives in:

protocol/SPEC.md

Transfer model

Files must be streamed directly from source to destination.

Never load an entire large file into memory.

Conceptual flow:

    Sender
      |
      | OFFER
      v
    Receiver
      |
      | ACCEPT
      v
    Sender
      |
      | FILE METADATA
      | FILE BYTES STREAM
      v
    Receiver
      |
      | SHA-256 verification
      v
    COMPLETE

Integrity

SHA-256 is the MVP integrity mechanism.

Preferred implementation: - calculate source hash during or before
transfer - calculate destination hash while streaming - compare before
marking transfer successful

A mismatched hash must result in a failed transfer.

Incomplete or corrupt temporary files must not be exposed as successful
files.

File writing

Incoming files should initially be written to a temporary or partial
file.

Example:

    IMG_1234.MOV.drop-partial

Only after successful completion and integrity verification should the
file be renamed to its final name.

Duplicate filenames

Never silently overwrite an existing user file.

MVP behavior should generate a safe unique filename, for example:

    photo.jpg
    photo (1).jpg
    photo (2).jpg

Multiple files

A transfer session may contain one or more files.

For the initial implementation, files may be transferred sequentially
within one logical transfer session.

Parallel file transfer is not required for MVP.

Cancellation

A sender or receiver must be able to cancel an active transfer.

Cancellation should: - stop network transfer - close relevant streams -
delete incomplete temporary files - report a cancelled state

Trust model

Each Drop installation has a local device identity.

For MVP, trusted device state is stored locally on each device.

Trusted devices may be configured for automatic acceptance.

No account or cloud identity system is required.

Trust data should be replaceable later by a stronger cryptographic
pairing model without changing the transfer protocol fundamentally.

Discovery identity and authenticated session identity are separate. A
device ID, hostname, IP address, route label, or other discovery metadata
only identifies a connection candidate and never authenticates a peer.
When a transport requires authenticated identity, the session starts as
unauthenticated and becomes authenticated only after a challenge/proof
verification succeeds against the peer public-key fingerprint. A known
expected fingerprint may be supplied as policy input before verification.
Protocol v1 LAN sessions remain allowed without this authentication hook
for MVP compatibility and are represented explicitly as unauthenticated.

Security

MVP security priorities:

1.  Do not accept unsolicited files silently by default.
2.  Identify the sending device clearly.
3.  Require explicit acceptance unless the sender is trusted and
    auto-accept is enabled.
4.  Validate protocol message sizes and metadata.
5.  Sanitize received filenames.
6.  Never allow incoming paths to escape the configured destination
    directory.
7.  Limit resource usage to prevent obvious memory abuse.

If TLS or authenticated encryption can be added without threatening the
7-day deadline, prefer it.

Do not invent custom cryptography.

Protocol versioning

Every connection must declare a protocol version.

Initial version:

    Drop Protocol v1

Future protocol changes should remain backward-compatible when
practical.

Device identity

Each installation should have:

-   stable local device ID
-   human-readable device name
-   platform
-   app version
-   protocol version

Example:

    {
      "deviceId": "local-generated-id",
      "name": "NICO-PC",
      "platform": "windows",
      "appVersion": "0.1.0",
      "protocolVersion": 1
    }

The specific serialization format will be defined in protocol/SPEC.md.

Background behavior

Background execution differs by operating system.

Do not assume identical behavior across Windows, iOS, and Android.

Platform-specific background capabilities must be implemented only after
verifying current OS restrictions.

The MVP may require Drop to be running or recently active on some
platforms if the operating system does not permit reliable background
discovery or receiving.

The UI should hide networking complexity but must not falsely claim
capabilities the OS does not allow.

Logging

Development builds should log: - discovered devices - connection
attempts - transfer IDs - transfer state transitions - byte counts -
failures - hash verification result

Logs must never include file contents.

Testing strategy

Testing has three levels.

Unit tests

-   protocol parsing
-   filename sanitization
-   hash verification
-   duplicate filename logic
-   state transitions

Integration tests

-   sender ↔ receiver
-   streamed large files
-   interrupted connection
-   invalid hashes
-   multiple files

Real-device tests

-   Windows ↔ Windows
-   iPhone ↔ Windows
-   Android ↔ Windows
-   iPhone ↔ Android where implemented

Detailed cases live in:

docs/TESTING.md

Non-goals for MVP architecture

Do not add: - backend API - cloud database - user accounts - message
broker - remote relay - microservices - unnecessary dependency injection
frameworks - distributed state - event sourcing - custom encryption
algorithms

Architecture decision rule

When two approaches satisfy the MVP, choose the one that is: - easier to
implement - easier to debug - easier to test on real hardware - less
dependent on undocumented OS behavior

Maintain clean interfaces so a better implementation can replace it
later.
