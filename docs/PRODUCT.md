# Drop — Product Definition

## Vision

Drop is a cross-platform local file-transfer app designed to feel like a universal AirDrop for iPhone, Android, and Windows.

The product should make sending a file to a nearby device feel immediate and obvious.

The user should not need to understand networking, pairing technologies, IP addresses, Wi-Fi modes, or transport protocols.

## MVP goal

Deliver a genuinely usable MVP within 7 days.

The MVP must prioritize:

1. Reliability
2. Extremely low-friction UX
3. Local transfer
4. File integrity
5. Cross-platform interoperability

## Core user promise

A user should be able to send a file to a nearby device in approximately 2–3 actions.

Ideal examples:

### iPhone

Photos / Files
→ Share
→ Drop
→ Nearby device

### Android

Gallery / Files
→ Share
→ Drop
→ Nearby device

### Desktop

Open Drop
→ Choose nearby device
→ Choose file

## Supported MVP platforms

- Windows
- iOS
- Android

## Required MVP capabilities

### Nearby device discovery

Drop should automatically discover compatible nearby devices when possible.

The user must not manually enter:

- IP addresses
- hostnames
- ports
- network credentials

### Local transfer

File contents must be transferred locally.

The MVP must not depend on a cloud server for transporting file data.

Internet access must not be required when devices can communicate locally.

### Original file preservation

Drop must never:

- compress files
- resize images
- transcode videos
- change file formats
- intentionally modify file contents

The received file must be bit-for-bit identical to the source.

### Integrity verification

Transfers must be verified using SHA-256 or an equivalent strong integrity mechanism.

A transfer must not be reported as successfully completed until integrity verification succeeds.

### File support

The transfer system should treat files as opaque byte streams.

The MVP should support:

- photos
- videos
- documents
- archives
- arbitrary file types
- multiple files per transfer

### Large files

Transfers must use streaming.

The implementation must not require loading the entire file into memory.

The target is to support multi-gigabyte files.

### Progress

The user should see useful transfer status including:

- file name
- progress
- transferred bytes
- total bytes

Transfer speed may be displayed where practical.

### Receiving

The receiver must be able to:

- accept a transfer
- decline a transfer
- save files safely
- handle duplicate filenames
- report transfer failure clearly

### Trusted devices

The MVP should support a lightweight trust model.

A user may mark a device as trusted.

Trusted devices may be allowed to auto-accept transfers if the user enables that behavior.

## UX principles

### Invisible networking

Drop chooses the discovery and transport mechanism automatically.

The normal user flow must not expose:

- mDNS
- TCP
- TLS
- Wi-Fi Aware
- Wi-Fi Direct
- Bluetooth
- ports
- IP addresses

### Minimal interface

The initial UI should focus on:

- nearby devices
- send
- receive status
- settings required for basic operation

Avoid unnecessary navigation.

### Fast first success

A new user should be able to complete their first transfer with minimal setup.

## MVP scope priorities

### Priority 1

Reliable Windows ↔ Windows transfer core.

This validates the protocol and file-transfer implementation.

### Priority 2

Automatic LAN discovery.

### Priority 3

iPhone ↔ Windows transfer.

### Priority 4

iOS Share Extension.

### Priority 5

Android ↔ Windows transfer and Android share integration.

### Priority 6

iPhone ↔ Android interoperability.

### Priority 7

Direct peer-to-peer transport without an existing LAN where platform APIs allow it reliably within the deadline.

## Explicitly out of scope for the 7-day MVP

Unless required for core operation, do not build:

- user accounts
- authentication services
- cloud file storage
- browser-based transfer
- social features
- chat
- contact lists
- remote transfer over the internet
- file editing
- media compression
- media conversion
- complex transfer history
- analytics dashboards
- subscription systems
- payment systems
- organization/team management
- advanced theming
- unnecessary onboarding flows

## Success criteria for Day 7

The MVP is considered successful if:

1. Windows can reliably send and receive files locally.
2. iPhone can send files to Windows using Drop.
3. Android can send files to Windows using Drop.
4. System share integration works on mobile where implemented.
5. Files arrive without quality loss or modification.
6. Integrity verification confirms the received file matches the source.
7. Multi-gigabyte transfers work without excessive memory usage.
8. The normal LAN workflow does not require manual network configuration.
9. The product can be demonstrated end-to-end on real devices.
10. Installable/testable builds exist for the implemented platforms.

## Product quality bar

Drop should feel simple even if the internal implementation is not.

Technical complexity is acceptable only when it removes complexity from the user's experience.