using CibMedia.Playback.Models;
using Xunit;

namespace CibMedia.Core.Tests.Playback;

public sealed class PngWrappedSegmentTests
{
    private const int ImageLength = 32;

    [Fact]
    public void Opens_only_on_the_png_signature()
    {
        Assert.True(PngWrappedSegment.Opens(Wrapped()));
        Assert.False(PngWrappedSegment.Opens(Packets(1)));
        Assert.False(PngWrappedSegment.Opens([]));
        Assert.False(PngWrappedSegment.Opens([0x89, 0x50]));
    }

    [Fact]
    public void Starts_the_payload_past_the_image()
    {
        var wrapped = Wrapped();

        var start = PngWrappedSegment.PayloadStart(wrapped);

        Assert.Equal(ImageLength, start);
        Assert.Equal(0x47, wrapped[start]);
    }

    // One measured server runs 0xFF between the image and the stream, so the end chunk anchors the
    // search rather than ending it.
    [Fact]
    public void Starts_the_payload_past_padding_behind_the_image()
    {
        var wrapped = Wrapped(padding: 135);

        var start = PngWrappedSegment.PayloadStart(wrapped);

        Assert.Equal(ImageLength + 135, start);
        Assert.Equal(0x47, wrapped[start]);
    }

    // What a read that has not yet reached the end chunk sees, and what the caller reads on for.
    [Fact]
    public void Starts_nowhere_until_the_end_chunk_has_been_read()
    {
        Assert.Equal(-1, PngWrappedSegment.PayloadStart(Wrapped().AsSpan(0, 20)));
    }

    // The second sync byte lands 188 past the first, so a read that stops between them proves
    // nothing yet and the caller reads on rather than starting the payload early.
    [Fact]
    public void Starts_nowhere_until_a_second_sync_byte_backs_the_first()
    {
        var wrapped = Wrapped();

        Assert.Equal(-1, PngWrappedSegment.PayloadStart(wrapped.AsSpan(0, ImageLength + 100)));
        Assert.Equal(ImageLength, PngWrappedSegment.PayloadStart(wrapped));
    }

    // The chunk straddles the boundary of two reads, which is why the whole of what was read is
    // searched rather than only the newest bytes.
    [Fact]
    public void Finds_an_end_chunk_split_across_two_reads()
    {
        var wrapped = Wrapped();

        Assert.Equal(-1, PngWrappedSegment.PayloadStart(wrapped.AsSpan(0, ImageLength - 4)));
        Assert.Equal(ImageLength, PngWrappedSegment.PayloadStart(wrapped));
    }

    private static byte[] Wrapped(int padding = 0, int packets = 2)
    {
        byte[] image =
        [
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
            0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
            0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
            0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
        ];

        var filler = new byte[padding];
        Array.Fill(filler, (byte)0xFF);

        return [.. image, .. filler, .. Packets(packets)];
    }

    private static byte[] Packets(int count)
    {
        var packets = new byte[188 * count];

        for (var index = 0; index < count; index++) packets[index * 188] = 0x47;

        return packets;
    }
}
