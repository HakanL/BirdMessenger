# TUS Specification Compliance Verification

This document verifies that the BirdMessenger implementation complies with the TUS Resumable Upload Protocol v1.0 specification for deferred uploads.

## Specification Requirements

Based on the official TUS protocol specification at:
https://github.com/tus/tus-resumable-upload-protocol/blob/main/protocol.md

### Requirement 1: Creation with Upload-Defer-Length

**Specification:**
> The Client MUST send a `POST` request against a known upload creation URL to request a new upload resource. The request MUST include one of the following headers:
> 
> b) `Upload-Defer-Length: 1` if upload size is not known at the time.

**Implementation:** ✅ COMPLIANT

Location: `HttpClientExtension.cs`, `TusCreateAsync` method (lines 23-110)
```csharp
if(reqOption.IsUploadDeferLength)
{
    httpReqMsg.Headers.Add(TusHeaders.UploadDeferLength,"1");
}
```

When `IsUploadDeferLength = true`, the CREATE request includes the `Upload-Defer-Length: 1` header.

### Requirement 2: Setting Upload-Length Once Known

**Specification:**
> If the length was deferred using `Upload-Defer-Length: 1`, the Client MUST set the `Upload-Length` header in the next `PATCH` request, once the length is known. Once set the length MUST NOT be changed.

**Implementation:** ✅ COMPLIANT

Location: `HttpClientExtension.cs`, both `TusPatchWithChunkAsync` and `TusPatchWithStreamingAsync` methods

The implementation handles two scenarios:

#### Scenario A: Length Known During Upload
If the stream supports seeking and has a known length:
```csharp
if (tusHeadResp.UploadLength < 0)
{
    // Only set Upload-Length if we know the total size
    if (totalSize.HasValue)
    {
        httpReqMsg.Headers.Add(TusHeaders.UploadLength, totalSize.Value.ToString());
    }
}
```

The `Upload-Length` header is set in the first PATCH request when the size is known.

#### Scenario B: Length Known After Stream Ends
If the stream doesn't support seeking or length is unknown:
```csharp
// For deferred length uploads (when size was unknown), send final PATCH to set Upload-Length
// According to TUS spec: "the Client MUST set the Upload-Length header in the next PATCH request, once the length is known"
// Reference: https://github.com/tus/tus-resumable-upload-protocol/blob/main/protocol.md
if (!totalSize.HasValue && reachedEndOfStream && tusHeadResp.UploadLength < 0)
{
    // Send final PATCH with Upload-Length header and empty body
    httpReqMsg = new HttpRequestMessage(new HttpMethod("PATCH"), reqOption.FileLocation);
    httpReqMsg.Headers.Add(TusHeaders.TusResumable, reqOption.TusVersion.GetEnumDescription());
    httpReqMsg.Headers.Add(TusHeaders.UploadLength, uploadedSize.ToString());
    httpReqMsg.Headers.Add(TusHeaders.UploadOffset, uploadedSize.ToString());
    reqOption.AddCustomHttpHeaders(httpReqMsg);
    httpReqMsg.Content = new ByteArrayContent(Array.Empty<byte>());
    httpReqMsg.Content.Headers.Add(TusHeaders.ContentType, TusHeaders.UploadContentTypeValue);
    // ... send request
}
```

After the stream ends and the final size is known, a final PATCH request is sent with the `Upload-Length` header.

### Requirement 3: Upload-Length Not Changed

**Specification:**
> Once set the length MUST NOT be changed.

**Implementation:** ✅ COMPLIANT

The implementation only sets `Upload-Length` when `tusHeadResp.UploadLength < 0`, ensuring it's only set once. After the first PATCH that includes `Upload-Length`, subsequent PATCH requests do not include this header.

### Requirement 4: Server Response to HEAD

**Specification:**
> As long as the length of the upload is not known, the Server MUST set `Upload-Defer-Length: 1` in all responses to `HEAD` requests.

**Implementation:** ✅ COMPLIANT (Server-side requirement)

The client implementation correctly handles this by checking `tusHeadResp.UploadLength < 0` to determine if the server still expects the Upload-Length header.

Location: `HttpClientExtension.cs`, `TusHeadAsync` method (lines 120-166)
```csharp
if (!long.TryParse(response.GetValueOfHeaderWithoutException(TusHeaders.UploadLength), out var uploadLength))
{
    uploadLength = -1;
}
```

When the server hasn't received `Upload-Length` yet, the client correctly interprets this as `-1`.

## Edge Cases Handled

### Empty File Upload
The specification allows `Upload-Length: 0` for empty files. The implementation handles this correctly by:
- Allowing `UploadLength = 0` in creation
- Checking `totalSize.Value == uploadedSize` to determine completion

### Chunked vs Streaming Upload
Both upload modes (`TusPatchWithChunkAsync` and `TusPatchWithStreamingAsync`) implement the final PATCH request correctly.

### Final PATCH with Zero Bytes
When the stream ends and length is deferred, the final PATCH request contains:
- `Upload-Length` header with the final size
- `Upload-Offset` header with the same value
- Empty body (`Array.Empty<byte>()`)
- Correct content type (`application/offset+octet-stream`)

This is compliant with the specification, as it allows sending the `Upload-Length` header "in the next PATCH request" which can have zero bytes of content.

## Verification Summary

| Requirement | Status | Location |
|-------------|--------|----------|
| Support Upload-Defer-Length in CREATE | ✅ COMPLIANT | TusCreateAsync |
| Set Upload-Length in PATCH when known | ✅ COMPLIANT | Both PATCH methods |
| Upload-Length set only once | ✅ COMPLIANT | Conditional check |
| Handle server Upload-Defer-Length response | ✅ COMPLIANT | TusHeadAsync |
| Final PATCH with empty body | ✅ COMPLIANT | Both PATCH methods |
| Support both Chunk and Stream modes | ✅ COMPLIANT | Both implementations |

## Conclusion

The BirdMessenger implementation is **FULLY COMPLIANT** with the TUS Resumable Upload Protocol v1.0 specification for deferred uploads. The implementation correctly:

1. Creates uploads with `Upload-Defer-Length: 1` when size is unknown
2. Sets `Upload-Length` in a PATCH request once the length becomes known
3. Ensures `Upload-Length` is only set once and never changed
4. Sends a final PATCH request with empty body when needed to communicate the final size
5. Handles both chunked and streaming upload modes

## References

- TUS Protocol Specification: https://github.com/tus/tus-resumable-upload-protocol/blob/main/protocol.md
- TUS Official Site: https://tus.io/protocols/resumable-upload
