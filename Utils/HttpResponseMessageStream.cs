namespace OrchestrationApi.Utils;

/// <summary>
/// 包装上游响应流，在释放时一并释放 <see cref="HttpResponseMessage"/>，避免连接泄漏。
/// </summary>
public sealed class HttpResponseMessageStream : Stream
{
    private readonly HttpResponseMessage _response;
    private readonly Stream _inner;
    private bool _disposed;

    public HttpResponseMessageStream(HttpResponseMessage response, Stream inner)
    {
        _response = response ?? throw new ArgumentNullException(nameof(response));
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    }

    public override bool CanRead => _inner.CanRead;
    public override bool CanSeek => _inner.CanSeek;
    public override bool CanWrite => _inner.CanWrite;
    public override long Length => _inner.Length;
    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        => _inner.ReadAsync(buffer, offset, count, cancellationToken);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        => _inner.ReadAsync(buffer, cancellationToken);

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);
    public override void SetLength(long value) => _inner.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    protected override void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _inner.Dispose();
            _response.Dispose();
        }
        _disposed = true;
        base.Dispose(disposing);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await _inner.DisposeAsync().ConfigureAwait(false);
        _response.Dispose();
        _disposed = true;
        await base.DisposeAsync().ConfigureAwait(false);
    }
}
