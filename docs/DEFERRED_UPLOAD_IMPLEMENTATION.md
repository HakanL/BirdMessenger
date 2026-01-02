# TUS Deferred Upload Implementation

## Overview

This document describes the implementation of TUS protocol's deferred upload feature in BirdMessenger, which allows uploading files/streams without knowing the total size upfront.

## TUS Specification Reference

According to the [TUS Resumable Upload Protocol v1.0](https://github.com/tus/tus-resumable-upload-protocol/blob/main/protocol.md):

> If the length was deferred using `Upload-Defer-Length: 1`, the Client MUST set the `Upload-Length` header in the next `PATCH` request, once the length is known. Once set the length MUST NOT be changed.

## Implementation Details

### Upload Creation

When creating an upload with unknown size:
- Set `IsUploadDeferLength = true` in `TusCreateRequestOption`
- The CREATE request includes `Upload-Defer-Length: 1` header
- No `Upload-Length` header is sent

### Upload Process

#### Case 1: Size Known Before or During Upload

If the stream supports seeking (`CanSeek = true`), the total size is determined from `Stream.Length` and:
- The first PATCH request includes the `Upload-Length` header
- Subsequent PATCH requests do not need to include it (already set)

#### Case 2: Size Unknown Until Stream Ends

For streams that don't support seeking (`CanSeek = false`) or where `Length` is not available:
1. During upload, PATCH requests are sent WITHOUT the `Upload-Length` header
2. When the stream reaches EOF (end of file):
   - A final PATCH request is sent with:
     - `Upload-Length` header set to the total bytes uploaded
     - `Upload-Offset` header set to the current offset (same value as Upload-Length)
     - Empty body (Content-Length: 0)
   - This informs the server of the final file size and completes the upload

### Code Implementation

The implementation is in `HttpClientExtension.cs`:

1. **TusPatchWithChunkAsync** (lines 175-352)
   - Handles chunked uploads
   - After reaching end of stream, sends final PATCH if length was deferred

2. **TusPatchWithStreamingAsync** (lines 377-548)
   - Handles streaming uploads
   - After stream is sent, sends final PATCH if length was deferred

Both methods check:
- `!totalSize.HasValue` - size was not known during upload
- `tusHeadResp.UploadLength < 0` - server has not received Upload-Length yet
- `reachedEndOfStream` (chunk mode) - stream has ended

### Example Scenario

```csharp
// Create upload with deferred length
var createRequest = new TusCreateRequestOption
{
    Endpoint = tusEndpoint,
    IsUploadDeferLength = true,
    Metadata = metadata
};
var createResponse = await httpClient.TusCreateAsync(createRequest, ct);

// Upload from a non-seekable stream
var patchRequest = new TusPatchRequestOption
{
    FileLocation = createResponse.FileLocation,
    Stream = nonSeekableStream, // e.g., NetworkStream, GZipStream
    UploadType = UploadType.Chunk
};
var patchResponse = await httpClient.TusPatchAsync(patchRequest, ct);

// The implementation automatically:
// 1. Sends PATCH requests without Upload-Length during upload
// 2. Sends final PATCH with Upload-Length when stream ends
```

## Compliance Verification

The implementation follows the TUS specification:
- ✅ Uses `Upload-Defer-Length: 1` when size is unknown
- ✅ Sets `Upload-Length` in a PATCH request once length is known
- ✅ Ensures `Upload-Length` is set exactly once
- ✅ Handles both chunked and streaming upload modes
- ✅ Sends final PATCH with empty body to communicate final size

## References

- [TUS Resumable Upload Protocol v1.0](https://github.com/tus/tus-resumable-upload-protocol/blob/main/protocol.md)
- [Creation Extension - Upload-Defer-Length](https://github.com/tus/tus-resumable-upload-protocol/blob/main/protocol.md#upload-defer-length)
