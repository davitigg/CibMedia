using AndroidX.Media3.Common;
using AndroidX.Media3.DataSource;
using CibMedia.Playback.Models;

namespace CibMedia.AndroidTv.Playback;

// Most xpass servers serve each MPEG-TS segment appended to a complete 1x1 PNG, and the embed
// strips the prefix in a service worker before hls.js ever sees it. TsExtractor sniffs for 0x47 at
// offset 0, so without this the segment is rejected as an unrecognised format.
//
// It sniffs rather than being told, which is what makes it safe to wrap every request: a playlist
// and an AES key never open with the PNG magic, so both pass through byte for byte.
public sealed class PngWrappedDataSource(IDataSource inner) : Java.Lang.Object, IDataSource
{
    // What stops a body that merely opens like a PNG from being read to its end hunting a marker
    // it does not carry. Well clear of any prefix seen.
    private const int PrefixCeiling = 8192;

    // Read ahead of the player while sniffing, and handed back before anything else.
    private byte[]? _buffered;
    private int _delivered;

    public Android.Net.Uri? Uri => inner.Uri;

    public IDictionary<string, IList<string>>? ResponseHeaders => inner.ResponseHeaders;

    public void AddTransferListener(ITransferListener? transferListener)
    {
        inner.AddTransferListener(transferListener!);
    }

    public long Open(DataSpec? dataSpec)
    {
        _buffered = null;
        _delivered = 0;

        var length = inner.Open(dataSpec!);

        // A range request starts past the wrapper, so there is nothing at its head to strip. HLS
        // asks for one when the playlist carries EXT-X-BYTERANGE.
        if (dataSpec!.Position != 0) return length;

        var head = new byte[PngWrappedSegment.Magic.Length];
        var filled = Fill(head, head.Length);

        if (filled < head.Length || !PngWrappedSegment.Opens(head))
        {
            _buffered = head[..Math.Max(filled, 0)];

            return length;
        }

        var prefix = Strip();

        return prefix < 0 || length == C.LengthUnset ? length : length - prefix;
    }

    public int Read(byte[]? buffer, int offset, int length)
    {
        if (_buffered is null) return inner.Read(buffer, offset, length);

        var taken = Math.Min(_buffered.Length - _delivered, length);
        Array.Copy(_buffered, _delivered, buffer!, offset, taken);
        _delivered += taken;

        if (_delivered == _buffered.Length) _buffered = null;

        return taken;
    }

    public void Close()
    {
        _buffered = null;
        inner.Close();
    }

    // Bytes discarded, or -1 when the body opened like a PNG but carried no end chunk — in which
    // case everything read is handed back untouched and the extractor makes its own verdict.
    private long Strip()
    {
        var scan = new byte[PrefixCeiling];
        PngWrappedSegment.Magic.CopyTo(scan);
        var filled = PngWrappedSegment.Magic.Length;

        while (filled < scan.Length)
        {
            var read = inner.Read(scan, filled, scan.Length - filled);
            if (read == C.ResultEndOfInput) break;

            filled += read;

            var start = PngWrappedSegment.PayloadStart(scan.AsSpan(0, filled));
            if (start < 0) continue;

            _buffered = scan[start..filled];

            return start;
        }

        _buffered = scan[..filled];

        return -1;
    }

    private int Fill(byte[] buffer, int count)
    {
        var filled = 0;

        while (filled < count)
        {
            var read = inner.Read(buffer, filled, count - filled);
            if (read == C.ResultEndOfInput) break;

            filled += read;
        }

        return filled;
    }
}
