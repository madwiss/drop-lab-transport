Drop

Drop is a cross-platform local file-transfer app for iPhone, Android,
and Windows.

The goal is simple: make sending an original file to a nearby device
feel like a universal AirDrop experience.

Product goal

A normal transfer should require approximately 2–3 user actions.

Example:

    Photos / Files
    → Share
    → Drop
    → Nearby device

Drop should hide networking complexity from the user.

No manual IP addresses, QR codes, cloud upload, accounts, or visible
network configuration should be required in the normal flow.

Core principles

-   Local-first transfers
-   No cloud dependency for file transport
-   No accounts or login
-   Automatic nearby-device discovery
-   No compression
-   No image resizing
-   No video transcoding
-   Original files preserved bit-for-bit
-   SHA-256 integrity verification
-   Streaming support for large files
-   Minimal user interface
-   Reliability before unnecessary features

Target platforms

-   Windows
-   iOS
-   Android

MVP deadline

The current goal is a genuinely usable MVP within 7 days.

The MVP is intentionally narrow. The project prioritizes working
end-to-end transfers over feature breadth or architectural complexity.

MVP networking

The baseline implementation is LAN-first:

-   automatic discovery using mDNS / Bonjour-style service discovery
-   local TCP transport
-   Drop Protocol v1 for application-level transfer

The user should not need to know that these technologies are being used.

Direct peer-to-peer transports such as Wi-Fi Aware or Wi-Fi Direct may
be added behind the same abstractions after the reliable LAN path is
working and current platform support has been verified.

File integrity

Drop treats files as opaque byte streams.

File contents must never be intentionally modified during transfer.

Protocol v1 verifies each completed file using SHA-256 before the
receiver exposes it as successfully received.

Repository structure

    drop/
    ├── AGENTS.md
    ├── README.md
    ├── apps/
    │   ├── windows/
    │   ├── ios/
    │   └── android/
    ├── docs/
    │   ├── PRODUCT.md
    │   ├── ARCHITECTURE.md
    │   ├── ROADMAP.md
    │   ├── CURRENT_STATE.md
    │   └── TESTING.md
    └── protocol/
        └── SPEC.md

Documentation

Before making significant changes, read:

-   AGENTS.md — rules for coding agents and contributors
-   docs/PRODUCT.md — product definition and MVP scope
-   docs/ARCHITECTURE.md — architecture and technical boundaries
-   docs/ROADMAP.md — 7-day implementation plan
-   docs/CURRENT_STATE.md — current implementation status
-   docs/TESTING.md — testing strategy
-   protocol/SPEC.md — canonical Drop Protocol v1 specification

Development workflow

GitHub is the source of truth.

Before starting work on a development machine:

    git pull

After completing a tested milestone:

    git add .
    git commit -m "descriptive commit message"
    git push

Update docs/CURRENT_STATE.md whenever the project reaches a meaningful
new state.

Agent workflow

Coding agents should:

1.  Read AGENTS.md.
2.  Read the relevant project documentation.
3.  Keep changes scoped to the requested task.
4.  Build the affected project.
5.  Run relevant tests.
6.  Report changed files and executed tests.
7.  Update CURRENT_STATE.md for meaningful milestones.

Do not independently change the cross-platform protocol contract inside
platform-specific implementations.

Protocol changes must be made first in protocol/SPEC.md.

Initial implementation order

1.  Windows transfer core
2.  Windows LAN discovery and minimal UX
3.  iPhone → Windows
4.  iOS Share Extension
5.  Android → Windows
6.  Cross-mobile interoperability
7.  Direct peer-to-peer transport where reliable and deadline-safe

Current status

Project foundation and protocol specification are being prepared.

See docs/CURRENT_STATE.md for the authoritative current status.

License

No public license has been selected yet.

The repository is currently intended to remain private during MVP
development.
