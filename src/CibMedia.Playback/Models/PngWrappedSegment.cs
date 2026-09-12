namespace CibMedia.Playback.Models;

// Many xpass servers append a segment's MPEG-TS to a complete 1x1 PNG and rely on the embed's
// service worker to strip it. Both ends of the app need to know that shape — the resolver, to
// decide whether a server carries the title, and the head's data source, to hand the player the
// transport stream behind it — so the two agree here rather than each carrying its own copy.
public static class PngWrappedSegment
{
    private const byte TransportStreamSync = 0x47;
    private const int TransportStreamPacket = 188;

    public static ReadOnlySpan<byte> Magic => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // IEND and its CRC, which closes the image.
    public static ReadOnlySpan<byte> EndChunk => [0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82];

    public static bool Opens(ReadOnlySpan<byte> head)
    {
        return head.StartsWith(Magic);
    }

    // Where the transport stream starts, or -1 while what has been read does not yet show it. The
    // end chunk anchors the search but does not end the wrapper on every server: the image runs 70
    // bytes on one and 806 on another, and one of the three measured pads the gap between the two
    // with 0xFF. So the first packet boundary past the image is what the payload starts at.
    //
    // Called against the whole of what has been read so far, so neither the chunk nor the pair of
    // sync bytes is missed for falling across two reads.
    public static int PayloadStart(ReadOnlySpan<byte> head)
    {
        var image = head.IndexOf(EndChunk);
        if (image < 0) return -1;

        for (var index = image + EndChunk.Length; index + TransportStreamPacket < head.Length; index++)
        {
            if (head[index] == TransportStreamSync && head[index + TransportStreamPacket] == TransportStreamSync)
                return index;
        }

        return -1;
    }
}
