using Microsoft.AspNetCore.Internal;
using System.Buffers;

namespace DanmakuR.Protocol.Buffer
{
    internal sealed class TransferrableMemoryBuffer(MemoryBufferWriter.WrittenBuffers buffers) : IDisposable
    {
        internal readonly ReadOnlySequence<byte> Sequence = buffers.Sequence;
        internal readonly List<MemoryBufferWriter.CompletedBuffer> Segments = buffers.Segments;
        internal readonly int WrittenLength = buffers.ByteLength;

        public void Dispose()
        {
            MemoryBufferWriter.WrittenBuffers originalRepresentation = new(Segments, WrittenLength);
            originalRepresentation.Dispose();
            Segments.Clear();
        }
    }
}
