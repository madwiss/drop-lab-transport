Drop — Testing Plan

Purpose

Testing must prove that Drop transfers files reliably, locally, and
without modifying their contents.

A transfer is not considered successful merely because the destination
file exists. The final file must pass integrity verification and the
application must reach a valid completed state.

Testing priorities

1.  Data integrity
2.  Reliable streaming
3.  Safe failure behavior
4.  Cross-platform interoperability
5.  Low-friction user flow
6.  Performance

Definition of a successful transfer

A transfer succeeds only when:

-   the expected number of bytes has been received
-   the destination SHA-256 matches the source SHA-256
-   the temporary file has been finalized safely
-   no protocol error occurred
-   both peers reach a valid completion state

Unit tests

Protocol

Test: - valid message parsing - unsupported protocol version - missing
required fields - malformed messages - oversized metadata - unknown
message type - invalid file size - invalid hash representation - invalid
transfer ID

Filename safety

Test: - normal filenames - duplicate filenames - empty filenames - path
traversal attempts - absolute paths - Windows reserved/problematic names
where relevant - Unicode names - emoji - very long filenames

Received metadata must never allow writing outside the configured
destination directory.

Integrity

Test: - matching SHA-256 - mismatching SHA-256 - zero-byte file -
modified byte - truncated stream - stream longer than declared size

Transfer state

Test valid and invalid state transitions including: - offered →
accepted - offered → declined - accepted → transferring - transferring →
completed - transferring → failed - transferring → cancelled

Invalid transitions must fail safely.

Integration tests

Basic transfer

Test: - one small text file - one image - one video - one archive -
arbitrary binary data

Verify SHA-256 equality independently when practical.

File sizes

Minimum test matrix: - 0 bytes - 1 byte - 1 KB - 1 MB - 10 MB - 100 MB -
1 GB - 10+ GB where practical

Large-file tests must verify that memory usage does not scale with total
file size.

Multiple files

Test: - 2 files - 10 files - 100+ files - mixed file types - duplicate
names in the same transfer - zero-byte files mixed with large files

Interrupted transfers

Interrupt: - before acceptance - immediately after acceptance - during
metadata exchange - near the beginning of file data - halfway through -
near completion

Expected behavior: - transfer reports failure/cancellation - no corrupt
file appears as successfully completed - partial file is removed or
retained only according to explicit recovery behavior - receiver remains
usable for the next transfer

Cancellation

Test: - sender cancels - receiver cancels

Expected behavior: - network activity stops - streams close - incomplete
files are cleaned up - both sides return to a usable state

Integrity failure

Deliberately produce: - wrong declared hash - modified payload -
truncated payload

Expected behavior: - receiver rejects completion - corrupt file is not
exposed as a valid received file - error is clearly logged/reported

Duplicate destination names

Given an existing:

photo.jpg

subsequent files should resolve safely, for example:

photo (1).jpg photo (2).jpg

Existing user files must never be silently overwritten.

Disk conditions

Test where practical: - insufficient disk space - destination
unavailable - destination not writable

Failure must be clear and safe.

Discovery tests

For LAN discovery:

Test: - peer appears automatically - peer disappears after leaving - app
restart - network disconnect/reconnect - multiple Drop devices -
duplicate human-readable device names - IPv4/IPv6 behavior where
applicable

Normal UX must not require manual IP or port entry.

Real-device interoperability matrix

Track each path independently.

  Sender    Receiver   Required for MVP
  --------- ---------- ------------------
  Windows   Windows    Yes
  iPhone    Windows    Yes
  Android   Windows    Yes
  Windows   iPhone     Target
  Windows   Android    Target
  iPhone    Android    Target
  Android   iPhone     Target

A path is not marked working until tested on real devices.

Mobile share integration

iOS

Test: - Photos → Share → Drop - Files → Share → Drop - single photo -
single large video - multiple photos - multiple mixed files where
supported - cancel destination selection - destination disappears -
transfer fails

Target flow:

Photos / Files → Share → Drop → Device

Android

Test: - Gallery → Share → Drop - Files → Share → Drop - single file -
multiple files - large video - destination disappears - transfer fails

Trusted devices

Test: - unknown sender requires acceptance by default - trusted sender
with auto-accept disabled still requires acceptance - trusted sender
with auto-accept enabled behaves as configured - removing trust restores
normal acceptance behavior

No unknown device may silently gain trusted status.

Security and robustness

Test: - malicious filename/path - huge declared metadata length -
invalid JSON/serialization data - unexpected disconnect - unsupported
protocol version - repeated connection attempts - declared file size
inconsistent with payload

The receiver must fail safely without writing outside the destination
directory or exhausting memory through unbounded allocations.

Performance observations

Record where practical: - total bytes - transfer duration - average
throughput - peak memory - CPU usage

Performance optimization must not compromise integrity or reliability.

Regression rule

Whenever a bug is fixed:

1.  Reproduce it reliably.
2.  Add an automated regression test where practical.
3.  Apply the fix.
4.  Verify the regression test passes.
5.  Run the relevant existing test suite.

Day 7 release checklist

Before calling the MVP ready:

-   Windows build succeeds
-   iOS build succeeds
-   Android build succeeds
-   required real-device paths have been tested
-   share integration tested on mobile
-   1 GB transfer tested successfully
-   10+ GB transfer tested where practical
-   multiple-file transfer tested
-   interrupted transfer tested
-   duplicate filenames tested
-   SHA-256 equality verified
-   no known corruption bug
-   known limitations documented
-   installable/testable builds produced

Test evidence

Important manual tests should be recorded in docs/CURRENT_STATE.md or a
dedicated test-results document with:

-   date
-   app/build version
-   sender device
-   receiver device
-   network/transport
-   test file size
-   result
-   SHA-256 result
-   relevant failure notes
