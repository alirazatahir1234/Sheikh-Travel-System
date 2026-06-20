using FluentValidation;
using FluentValidation.Results;

namespace SheikhTravelSystem.Application.Common.IO;

/// <summary>
/// Wraps a stream and throws when more than <paramref name="maxBytes"/> are read.
/// </summary>
public sealed class MaxLengthReadStream : Stream
{
    private readonly Stream _inner;
    private readonly long _maxBytes;
    private long _totalRead;

    public MaxLengthReadStream(Stream inner, long maxBytes)
    {
        _inner = inner;
        _maxBytes = maxBytes;
    }

    public long TotalBytesRead => _totalRead;

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count)
    {
        var read = _inner.Read(buffer, offset, count);
        TrackRead(read);
        return read;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        var read = await _inner.ReadAsync(buffer.AsMemory(offset, count), cancellationToken);
        TrackRead(read);
        return read;
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _inner.Dispose();
        base.Dispose(disposing);
    }

    private void TrackRead(int read)
    {
        if (read <= 0)
            return;

        _totalRead += read;
        if (_totalRead > _maxBytes)
        {
            var message = $"File exceeds maximum size of {_maxBytes / (1024 * 1024)} MB.";
            throw new ValidationException(new[] { new ValidationFailure("File", message) });
        }
    }
}
