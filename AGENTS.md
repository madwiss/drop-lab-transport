# Drop — Agent Instructions

## Mission

Drop is a cross-platform local file-transfer application for iPhone, Android, and Windows.

The product goal is to provide an "AirDrop for everything" experience: sending an original file to a nearby device should require no more than 2–3 user actions.

Current priority: deliver a genuinely usable MVP within 7 days.

## Product principles

1. Extremely simple UX.
2. Local-first file transfers.
3. No cloud dependency for file transfer.
4. No accounts or login.
5. No manual IP addresses.
6. No QR codes as part of the normal transfer flow.
7. No visible network configuration.
8. Files must never be compressed, converted, or modified.
9. Received files must be bit-for-bit identical to the source.
10. Verify transfer integrity using SHA-256.
11. Optimize for reliability before advanced functionality.
12. Avoid overengineering.

## Target platforms

- Windows
- iOS
- Android

Development machines include Windows PCs and a Mac for iOS/Xcode development.

## Target UX

Preferred flow:

Photos / Files
→ Share
→ Drop
→ Nearby device
→ Transfer

A trusted receiving device should require no additional interaction when auto-accept is enabled.

Opening Drop directly should immediately show nearby devices.

The user should never need to understand the underlying discovery or transport technology.

## Architecture principles

Keep these concerns separated:

- Discovery
- Pairing / trust
- Transport
- Transfer protocol
- Platform UI

Do not couple the Drop transfer protocol to one discovery or transport technology.

The MVP may initially use LAN-based discovery and transport where this is the fastest reliable implementation.

Peer-to-peer technologies such as Wi-Fi Aware or Wi-Fi Direct may be added behind the same abstractions.

## Development rules

Before implementing a task:

1. Read this file.
2. Read `docs/PRODUCT.md`.
3. Read `docs/ARCHITECTURE.md`.
4. Read `docs/CURRENT_STATE.md`.
5. Read `protocol/SPEC.md` when working on networking or transfers.

For every implementation task:

- Keep changes scoped to the requested task.
- Do not refactor unrelated working code.
- Do not introduce new frameworks without a concrete need.
- Do not introduce cloud services or backend infrastructure for the MVP.
- Do not add features outside the current roadmap.
- Prefer the simplest robust implementation.
- Add or update tests for important behavior.
- Build the affected project before declaring the task complete.
- Report what was changed.
- Report tests/build commands executed.
- Report known remaining problems.
- Update `docs/CURRENT_STATE.md` when a milestone materially changes project state.

## Platform API rule

Do not assume that an iOS, Android, or Windows networking/background API supports a required behavior.

For OS-dependent behavior that affects architecture, verify against current official platform documentation before implementing or changing architecture.

## Definition of done

A feature is not complete merely because code exists.

It must:

1. Build successfully.
2. Work in the intended flow.
3. Handle basic errors safely.
4. Have relevant tests where practical.
5. Not break existing functionality.

## MVP deadline rule

The MVP deadline is 7 days.

Before making a significant architectural change, evaluate whether it threatens the deadline.

When choosing between a sophisticated architecture and a simple reliable implementation that satisfies the MVP, prefer the simple implementation while keeping clean boundaries for future replacement.