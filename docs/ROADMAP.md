Drop — 7 Day MVP Roadmap

Objective

Ship a usable cross-platform local file-transfer MVP within 7 days.

The roadmap is intentionally aggressive. Each day must end with
something testable.

Day 1 — Transfer core

Goal: Prove reliable lossless file transfer on Windows.

Tasks: - finalize Drop Protocol v1 - create Windows project - implement
TCP receiver - implement TCP sender - stream files without loading them
fully into memory - SHA-256 verification - basic transfer state model -
automated tests

Acceptance criteria: - Windows A can send a file to Windows B - received
file is bit-for-bit identical - multi-gigabyte streaming architecture is
in place - hash mismatch is detected - build and tests pass

Day 2 — Discovery and Windows UX

Goal: Two Windows devices discover each other automatically.

Tasks: - mDNS advertisement - mDNS discovery - device model -
nearby-device list - incoming transfer UI - destination folder
handling - duplicate filename handling - trusted-device model - basic
Windows tray/background behavior

Acceptance criteria: - no manual IP entry - nearby Windows devices
appear automatically - file can be accepted and received through UI

Day 3 — iOS sender

Goal: Send a file from iPhone to Windows.

Tasks: - create iOS project - implement Drop protocol client - LAN
discovery - connect to Windows receiver - Files / Photos selection -
file streaming - progress UI - SHA-256 validation flow - real-device
testing

Acceptance criteria: - iPhone can discover Windows Drop receiver -
iPhone can send a real file successfully - received Windows file matches
original

Day 4 — iOS Share Extension

Goal: Enable system-level sharing from iPhone.

Tasks: - Share Extension - NSItemProvider integration - one-file flow -
multi-file flow - nearby destination selection - transfer initiation -
error handling - trusted-device shortcut where practical

Acceptance criteria: - Photos / Files → Share → Drop → Windows works -
no manual network configuration

Day 5 — Android

Goal: Add Android interoperability.

Tasks: - create Android project - implement Drop protocol - LAN
discovery - sender - receiver if feasible within the day - Android
Sharesheet integration - progress UI - real-device tests

Acceptance criteria: - Android can send a file to Windows - Android
Share → Drop works - file integrity verified

Stretch: - Android ↔ iPhone over LAN

Day 6 — Reliability and direct P2P investigation

Goal: Make the MVP robust.

Tasks: - large-file tests - multiple-file transfers - cancellation -
interrupted network handling - partial-file cleanup - duplicate names -
invalid metadata - low disk space handling - timeout behavior - sleep /
wake testing - investigate direct peer-to-peer transport - implement
direct P2P only if reliable and deadline-safe

Test sizes: - 1 KB - 10 MB - 1 GB - 10+ GB where practical - 100+ files

Acceptance criteria: - no known data corruption path - failed transfers
do not appear successful - incomplete files are handled safely - major
reproducible bugs are fixed or documented

Day 7 — Polish and packaging

Goal: Produce demonstrable installable builds.

Tasks: - UX cleanup - device naming - onboarding only where required -
icon / basic branding - Windows packaging - Android APK - iOS
installable development/TestFlight build as appropriate -
documentation - final regression test - demo flow

Acceptance criteria: - end-to-end demo works on real devices -
installable/testable builds exist - known limitations documented - core
2–3 action sharing flow is demonstrable

Daily development rule

Do not start the next platform before the current day’s critical
acceptance criteria are met.

At the end of every significant milestone: - build - test - commit -
push - update CURRENT_STATE.md

Priority order if schedule slips

Cut features in this order:

1.  UI polish
2.  advanced transfer history
3.  transfer speed display
4.  direct peer-to-peer without LAN
5.  receiver support on mobile
6.  non-essential settings

Do not cut: - file integrity - local transfer - Windows receiver -
iPhone → Windows - Android → Windows - system share integration -
automatic LAN discovery
