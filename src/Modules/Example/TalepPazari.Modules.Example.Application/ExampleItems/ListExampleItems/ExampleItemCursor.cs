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

    public static string Encode(Guid lastId)
    {
        Span<byte> bytes = stackalloc byte[ByteLength];
        bytes[0] = FormatVersion;
        lastId.TryWriteBytes(bytes[1..], bigEndian: true, out _);
        return Base64Url.EncodeToString(bytes);
    }

    public static bool TryDecode(string? cursor, out Guid lastId)
    {
        lastId = Guid.Empty;
        if (string.IsNullOrEmpty(cursor) || Base64Url.GetMaxDecodedLength(cursor.Length) > 64)
        {
            return false;
        }

        Span<byte> bytes = stackalloc byte[64];
        if (!Base64Url.TryDecodeFromChars(cursor, bytes, out var written)
            || written != ByteLength
            || bytes[0] != FormatVersion)
        {
            return false;
        }

        lastId = new Guid(bytes[1..ByteLength], bigEndian: true);
        return lastId != Guid.Empty;
    }
}
