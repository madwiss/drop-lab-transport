Drop Protocol v1 — Specification

Status: Draft for 7-day MVP Protocol version: 1

1. Purpose

Drop Protocol v1 defines the application-level contract used by Drop
devices to negotiate and transfer files over a reliable ordered
byte-stream transport.

The protocol must remain independent from discovery and from the
concrete transport mechanism.

For the initial MVP, the baseline transport is TCP over the local
network.

Future transports may include direct peer-to-peer mechanisms without
changing the logical transfer protocol.

2. Core requirements

The protocol must support:

-   device identification
-   protocol version negotiation
-   one or more files per transfer session
-   transfer offer
-   accept or decline
-   streamed file data
-   SHA-256 integrity verification
-   cancellation
-   explicit completion
-   explicit errors
-   large files without loading the entire file into memory

Files are opaque byte streams. Drop must not modify, compress,
transcode, resize, or reinterpret file contents.

3. Transport assumptions

Drop Protocol v1 assumes an underlying transport that provides:

-   reliable delivery
-   ordered delivery
-   bidirectional communication

Baseline MVP transport:

TCP.

Discovery is not part of this specification.

mDNS or another discovery mechanism provides the address and port
required to establish the connection.

4. Connection model

One TCP connection represents one Drop transfer session.

A session may contain one or more files.

Files are transferred sequentially in v1.

Parallel file payloads are not required.

5. Encoding

Control messages use UTF-8 JSON.

Every JSON control message is framed with a 4-byte unsigned big-endian
length prefix.

Frame:

    +----------------------+-----------------------+
    | JSON length (4 byte) | UTF-8 JSON payload    |
    +----------------------+-----------------------+

The length describes only the JSON payload, not the 4-byte prefix.

Maximum JSON control-message size for v1:

1 MiB.

Receivers must reject larger control frames before allocating unbounded
memory.

6. Binary file payload framing

File bytes are not encoded inside JSON.

After a FILE_START control message, exactly the declared number of raw
file bytes follows on the stream.

Example:

    JSON FRAME: FILE_START
    RAW FILE BYTES: exactly size bytes
    JSON FRAME: FILE_END

The receiver must read exactly the declared file size.

A shorter stream is an error.

Bytes beyond the declared size must not be treated as part of that file.

This design prevents base64 overhead and supports large streamed files
efficiently.

7. Common control-message fields

Every control message must contain:

    {
      "type": "MESSAGE_TYPE",
      "protocolVersion": 1
    }

Messages associated with a transfer must also contain:

    {
      "transferId": "uuid"
    }

File-specific messages additionally contain:

    {
      "fileId": "uuid"
    }

UUID strings should use the canonical textual representation.

8. Device information

A Drop device is represented as:

    {
      "deviceId": "550e8400-e29b-41d4-a716-446655440000",
      "name": "NICO-PC",
      "platform": "windows",
      "appVersion": "0.1.0",
      "protocolVersion": 1
    }

Required fields:

-   deviceId
-   name
-   platform
-   appVersion
-   protocolVersion

Allowed MVP platform values:

-   windows
-   ios
-   android

deviceId is generated locally and persists across normal app restarts.

The human-readable device name is not a security identity.

9. Session flow

Normal transfer flow:

    Sender                      Receiver
      |                            |
      |-------- HELLO ------------>|
      |<------- HELLO_ACK ---------|
      |                            |
      |-------- OFFER ------------>|
      |<------- ACCEPT ------------|
      |                            |
      |------ FILE_START --------->|
      |====== raw file bytes =====>|
      |------- FILE_END ---------->|
      |<------ FILE_RESULT --------|
      |                            |
      |  repeat FILE_START...      |
      |  for remaining files       |
      |                            |
      |------- COMPLETE ---------->|
      |<---- COMPLETE_ACK ---------|
      |                            |

The receiver may send DECLINE instead of ACCEPT.

Either peer may send CANCEL or ERROR when applicable.

10. HELLO

Sent by the connecting peer immediately after the transport connection
is established.

Example:

    {
      "type": "HELLO",
      "protocolVersion": 1,
      "device": {
        "deviceId": "550e8400-e29b-41d4-a716-446655440000",
        "name": "NICO-PC",
        "platform": "windows",
        "appVersion": "0.1.0",
        "protocolVersion": 1
      }
    }

The receiver validates protocol compatibility before continuing.

11. HELLO_ACK

Example:

    {
      "type": "HELLO_ACK",
      "protocolVersion": 1,
      "device": {
        "deviceId": "b65ac109-7056-44af-9fd0-28c980550001",
        "name": "DROP-PC",
        "platform": "windows",
        "appVersion": "0.1.0",
        "protocolVersion": 1
      }
    }

If the protocol version is unsupported, the peer should send ERROR and
close the connection.

12. OFFER

The sender offers the complete logical transfer before file payload
transmission begins.

Example:

    {
      "type": "OFFER",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "files": [
        {
          "fileId": "9b1e56df-8c60-41f4-9235-b5915dc50001",
          "name": "IMG_1234.MOV",
          "size": 1834219921,
          "mimeType": "video/quicktime"
        }
      ],
      "totalBytes": 1834219921
    }

Required per-file fields:

-   fileId
-   name
-   size

mimeType is optional and informational.

The receiver must not trust the supplied filename as a filesystem path.

13. ACCEPT

Example:

    {
      "type": "ACCEPT",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001"
    }

The sender must not transmit file payload bytes before receiving ACCEPT.

Trusted-device auto-accept is receiver-side policy and does not change
this protocol requirement: the receiver still sends ACCEPT.

14. DECLINE

Example:

    {
      "type": "DECLINE",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "reason": "user_declined"
    }

After DECLINE, no file payload is sent.

The connection may then close.

15. FILE_START

Before each raw payload:

    {
      "type": "FILE_START",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "fileId": "9b1e56df-8c60-41f4-9235-b5915dc50001",
      "name": "IMG_1234.MOV",
      "size": 1834219921,
      "sha256": "lowercase-hex-sha256"
    }

Required fields:

-   transferId
-   fileId
-   name
-   size
-   sha256

For Protocol v1, the sender calculates SHA-256 before sending
FILE_START.

This requires one source-file read for hashing followed by one read for
transmission.

This tradeoff is accepted for the MVP because it keeps integrity
behavior deterministic and cross-platform implementation simple.

A future protocol version may support single-pass alternatives.

Immediately after FILE_START, the sender transmits exactly size raw
bytes.

16. FILE_END

After exactly the declared number of raw bytes:

    {
      "type": "FILE_END",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "fileId": "9b1e56df-8c60-41f4-9235-b5915dc50001"
    }

The receiver should have calculated SHA-256 incrementally while writing
the raw payload.

17. FILE_RESULT

If integrity succeeds:

    {
      "type": "FILE_RESULT",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "fileId": "9b1e56df-8c60-41f4-9235-b5915dc50001",
      "status": "ok"
    }

If integrity fails:

    {
      "type": "FILE_RESULT",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "fileId": "9b1e56df-8c60-41f4-9235-b5915dc50001",
      "status": "hash_mismatch"
    }

The sender must not treat the file as successfully transferred until
status is ok.

The receiver must not expose a hash-mismatched partial file as a valid
completed file.

18. COMPLETE

After all offered files have received successful FILE_RESULT responses:

    {
      "type": "COMPLETE",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001"
    }

19. COMPLETE_ACK

Example:

    {
      "type": "COMPLETE_ACK",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001"
    }

After COMPLETE_ACK, the session may close cleanly.

20. CANCEL

Either peer may cancel an active transfer.

Example:

    {
      "type": "CANCEL",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "reason": "user_cancelled"
    }

Upon cancellation:

-   stop sending payload data as soon as practical
-   close active file streams
-   clean up incomplete receiver files
-   transition the transfer to cancelled
-   close the session if it cannot safely continue

Because raw payload bytes are length-framed rather than independently
chunk-framed, cancellation during a file payload may require closing the
TCP connection to resynchronize safely in Protocol v1.

This is acceptable for the MVP.

21. ERROR

Example:

    {
      "type": "ERROR",
      "protocolVersion": 1,
      "transferId": "bc69d089-9300-4a2d-b237-c131520a0001",
      "code": "INVALID_MESSAGE",
      "message": "Invalid control message"
    }

transferId may be omitted if the error occurs before a transfer exists.

Initial error codes may include:

-   UNSUPPORTED_PROTOCOL
-   INVALID_MESSAGE
-   INVALID_STATE
-   INVALID_FILE_METADATA
-   FILE_TOO_LARGE
-   HASH_MISMATCH
-   DISK_ERROR
-   INSUFFICIENT_SPACE
-   IO_ERROR
-   TRANSFER_CANCELLED
-   INTERNAL_ERROR

Human-readable message is for diagnostics and must not be used as the
programmatic error identifier.

22. File writing rules

The receiver must never write directly to the final destination filename
while a transfer is incomplete.

Example temporary filename:

    IMG_1234.MOV.drop-partial

Recommended flow:

1.  Sanitize the incoming filename.
2.  Resolve a unique final destination name.
3.  Create a temporary partial file inside the destination directory.
4.  Stream payload bytes into the temporary file.
5.  Calculate SHA-256 incrementally.
6.  Flush and close the temporary file.
7.  Compare SHA-256.
8.  If valid, atomically rename/move to the resolved final filename
    where supported.
9.  Send FILE_RESULT with status ok.

On failure, the partial file must be deleted unless an explicitly
implemented resume mechanism requires retaining it.

Resume is not required in Protocol v1.

23. Filename safety

Incoming filenames are labels, not paths.

Receivers must:

-   strip directory components
-   reject or replace path separators
-   prevent .. traversal
-   prevent absolute-path interpretation
-   handle platform-invalid characters safely
-   ensure the resolved output remains inside the configured destination
    directory
-   avoid silently overwriting existing files

Example duplicate resolution:

    photo.jpg
    photo (1).jpg
    photo (2).jpg

24. File sizes

File size is a non-negative 64-bit integer.

Implementations must support sizes larger than 2 GiB.

All byte counters used for file size and progress must use 64-bit-safe
representations.

No implementation may allocate memory proportional to the total file
size.

25. Streaming buffer

The exact I/O buffer size is implementation-specific.

A reasonable starting range is 64 KiB to 1 MiB.

Implementations may tune this later based on measurements.

The protocol does not expose the internal buffer size.

26. Timeouts

Implementations must not wait forever for an inactive peer.

Exact timeout values may be tuned during implementation.

At minimum, timeout handling is required for:

-   connection establishment
-   handshake
-   waiting for transfer acceptance
-   stalled payload reads/writes

Timeouts should result in a clear failed state and safe partial-file
cleanup.

27. State model

Conceptual sender states:

    idle
    connecting
    handshaking
    offering
    waiting_for_accept
    transferring
    completing
    completed
    declined
    cancelled
    failed

Conceptual receiver states:

    idle
    connected
    handshaking
    offered
    waiting_for_user
    receiving
    verifying
    completed
    declined
    cancelled
    failed

Implementations do not need to use these exact type names, but invalid
protocol transitions must be rejected.

28. Discovery metadata

Discovery is outside the transfer protocol, but LAN discovery should
expose enough information to establish a connection.

Suggested mDNS service type:

    _drop._tcp

Suggested advertised metadata:

-   deviceId
-   deviceName
-   platform
-   protocolVersion
-   appVersion

Do not place secrets in mDNS metadata.

The final service name/metadata encoding may be adjusted during
implementation if platform interoperability requires it.

29. Trust

Trust is a local application policy, not a Protocol v1 identity
guarantee.

A receiver may store a peer deviceId as trusted and automatically issue
ACCEPT according to user settings.

For the MVP:

-   unknown peers require user acceptance by default
-   trust must be explicitly granted
-   trusted status is stored locally
-   no cloud account is involved

A stronger cryptographic device identity/pairing design may replace this
later.

30. Security limitations of v1

The initial LAN-first protocol prioritizes a working 7-day MVP.

Plain TCP alone does not provide confidentiality or authenticated peer
identity.

Before production release beyond controlled MVP testing, the security
model must be reviewed and transport encryption/authentication should be
implemented.

Do not implement custom cryptography.

If standard TLS with a practical local trust model can be added within
the MVP deadline without destabilizing interoperability, it may be
introduced while preserving the application-level protocol.

31. Compatibility

Protocol v1 peers must reject unsupported protocol versions cleanly.

Future versions should preserve explicit version negotiation.

Do not silently reinterpret unknown fields or message types in ways that
could corrupt a transfer.

Unknown optional JSON fields may be ignored when doing so is safe.

32. Logging

Development implementations should log:

-   connection opened/closed
-   peer device ID/name
-   transfer ID
-   file ID
-   message type
-   declared file size
-   bytes transferred
-   state changes
-   SHA-256 verification success/failure
-   error code

Never log file contents.

33. Protocol v1 acceptance tests

Before Protocol v1 is considered implemented:

1.  Windows sender and receiver complete a valid transfer.
2.  SHA-256 of source and destination matches.
3.  A zero-byte file works.
4.  A file larger than 2 GiB is supported by the code path.
5.  Memory usage does not scale with total file size.
6.  A deliberately wrong SHA-256 is rejected.
7.  A truncated payload is rejected.
8.  Duplicate destination filenames do not overwrite existing files.
9.  Path traversal filenames cannot escape the destination.
10. Multiple files can transfer sequentially in one session.
11. Decline works.
12. Cancellation fails safely.
13. Unsupported protocol versions fail cleanly.

34. MVP implementation rule

This specification is the canonical interoperability contract for Drop
Protocol v1.

Windows, iOS, and Android implementations must not independently change
message framing or semantics.

If implementation reveals that the specification must change:

1.  update this file first
2.  document the reason
3.  update affected tests
4.  then update platform implementations

GitHub remains the source of truth.
