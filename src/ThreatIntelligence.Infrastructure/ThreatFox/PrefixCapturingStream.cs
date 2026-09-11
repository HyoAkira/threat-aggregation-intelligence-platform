namespace ThreatIntelligence.Infrastructure.ThreatFox;

/// <summary>
/// Buffers up to the configured maximum bytes from the wrapped stream, exposes them as
/// <see cref="Prefix"/>, then replays them before continuing with the wrapped stream. Lets a feed
/// body be inspected for diagnostics without buffering the whole payload or requiring a seekable
/// stream. Not thread-safe; each parse attempt should get its own instance.
/// </summary>
internal sealed class PrefixCapturingStream(Stream inner, int maxPrefixBytes) : Stream
{
    private readonly MemoryStream _prefix = new();
    private bool _prefixComplete;

    /// <summary>The bytes read so far; valid after <see cref="CaptureAsync"/>.</summary>
    public ReadOnlyMemory<byte> Prefix => _prefix.GetBuffer().AsMemory(0, (int)_prefix.Length);

    /// <summary>Reads the prefix once. Safe to call repeatedly.</summary>
    public async ValueTask CaptureAsync(CancellationToken cancellationToken)
    {
        if (_prefixComplete)
        {
            return;
        }

        var buffer = new byte[Math.Min(4096, Math.Max(1, maxPrefixBytes))];
        while (_prefix.Length < maxPrefixBytes)
        {
            var remaining = maxPrefixBytes - (int)_prefix.Length;
            var read = await inner
                .ReadAsync(buffer.AsMemory(0, Math.Min(buffer.Length, remaining)), cancellationToken)
                .ConfigureAwait(false);
            if (read == 0)
            {
                break;
            }

            await _prefix.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        _prefixComplete = true;
        _prefix.Position = 0;
    }

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await CaptureAsync(cancellationToken).ConfigureAwait(false);

        if (_prefix.Position < _prefix.Length)
        {
            var read = await _prefix.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read > 0)
            {
                return read;
            }
        }

        return await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        CaptureAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();

        if (_prefix.Position < _prefix.Length)
        {
            var read = _prefix.Read(buffer, offset, count);
            if (read > 0)
            {
                return read;
            }
        }

        return inner.Read(buffer, offset, count);
    }

    public override bool CanRead => true;

    public override bool CanSeek => false;

    public override bool CanWrite => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush()
    {
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();

    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _prefix.Dispose();
        }

        base.Dispose(disposing);
    }
}
