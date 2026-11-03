using System.Buffers;
using System.Buffers.Text;

namespace TalepPazari.Modules.Example.Application.ExampleItems.ListExampleItems;

// Opaque cursor for keyset pagination by Id. Clients must treat it as an opaque string.
// Format: base64url (no padding) of one format-version byte followed by the 16 bytes of the last Id
// in big-endian (RFC 9562) order. The version byte lets the format change without misreading old cursors.
//
// Copied per module until a second module needs it; then it moves to a shared place (open item, T-008a).
internal static class ExampleItemCursor
{
    private const byte FormatVersion = 1;
    private const int ByteLength = 17;

    // 17 bytes encode to exactly 23 base64url characters without padding.
    private static readonly int EncodedLength = Base64Url.GetEncodedLength(ByteLength);

    public static string Encode(Guid lastId)
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        bytes[0] = FormatVersion;
        lastId.TryWriteBytes(bytes[1..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    // Total: never throws, whatever the input. Accepts only the canonical form Encode produces, so padding,
    // whitespace, other alphabets, wrong lengths, unknown versions and non-zero unused bits are all invalid.
    public static bool TryDecode(string? cursor, out Guid lastId)
    {
        lastId = Guid.Empty;
        if (cursor is null || cursor.Length != EncodedLength)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[ByteLength];
        var status = Base64Url.DecodeFromChars(cursor, bytes, out var consumed, out var written, isFinalBlock: true);
        if (status != OperationStatus.Done || consumed != cursor.Length || written != ByteLength || bytes[0] != FormatVersion)
        {
            return false;
        }

        var id = new Guid(bytes[1..], bigEndian: true);
        if (id == Guid.Empty || !string.Equals(Encode(id), cursor, StringComparison.Ordinal))
        {
            return false;
        }

        lastId = id;
        return true;
    }
}
