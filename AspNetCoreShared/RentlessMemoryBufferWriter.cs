// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.Internal;

internal sealed class RentlessMemoryBufferWriter : IBufferWriter<byte>, IDisposable
{
	private readonly int _minimumSegmentSize;
	private int _bytesWritten;

	private readonly List<byte[]> _availableBuffers;
	private List<CompletedBuffer>? _completedSegments;
	private byte[]? _currentSegment;
	private int _position;
	private bool _sortBuffers;

	public RentlessMemoryBufferWriter(int minimumSegmentSize = 4096)
	{
		_minimumSegmentSize = minimumSegmentSize;
		_availableBuffers = new List<byte[]>(8);
	}

	public long Length => _bytesWritten;

	public void Reset()
	{
		if (_completedSegments != null)
		{
			for (var i = 0; i < _completedSegments.Count; i++)
			{
				_availableBuffers.Add(_completedSegments[i].Buffer);
			}

			if(_sortBuffers)
			{
				// Sort buffers by length, make larger first.
				_availableBuffers.Sort(static (a, b) => b.Length - a.Length);
			}

			_completedSegments.Clear();
		}

		if (_currentSegment != null)
		{
			_availableBuffers.Add(_currentSegment);
			_currentSegment = null;
		}

		_bytesWritten = 0;
		_position = 0;
	}

	public void Advance(int count)
	{
		_bytesWritten += count;
		_position += count;
	}

	public Memory<byte> GetMemory(int sizeHint = 0)
	{
		EnsureCapacity(sizeHint);

		return _currentSegment.AsMemory(_position, _currentSegment.Length - _position);
	}

	public Span<byte> GetSpan(int sizeHint = 0)
	{
		EnsureCapacity(sizeHint);

		return _currentSegment.AsSpan(_position, _currentSegment.Length - _position);
	}

	public void CopyTo(IBufferWriter<byte> destination)
	{
		if (_completedSegments != null)
		{
			// Copy completed segments
			var count = _completedSegments.Count;
			for (var i = 0; i < count; i++)
			{
				destination.Write(_completedSegments[i].Span);
			}
		}

		destination.Write(_currentSegment.AsSpan(0, _position));
	}

	public Task CopyToAsync(Stream destination, CancellationToken cancellationToken)
	{
		if (_completedSegments == null && _currentSegment is not null)
		{
			// There is only one segment so write without awaiting.
			return destination.WriteAsync(_currentSegment, 0, _position, cancellationToken);
		}

		return CopyToSlowAsync(destination, cancellationToken);
	}

	[MemberNotNull(nameof(_currentSegment))]
	private void EnsureCapacity(int sizeHint)
	{
		// This does the Right Thing. It only subtracts _position from the current segment length if it's non-null.
		// If _currentSegment is null, it returns 0.
		var remainingSize = _currentSegment?.Length - _position ?? 0;

		// If the sizeHint is 0, any capacity will do
		// Otherwise, the buffer must have enough space for the entire size hint, or we need to add a segment.
		if ((sizeHint == 0 && remainingSize > 0) || (sizeHint > 0 && remainingSize >= sizeHint))
		{
			// We have capacity in the current segment
#pragma warning disable CS8774 // Member must have a non-null value when exiting.
			return;
#pragma warning restore CS8774 // Member must have a non-null value when exiting.
		}

		AddSegment(sizeHint);
	}

	[MemberNotNull(nameof(_currentSegment))]
	private void AddSegment(int sizeHint = 0)
	{
		if (_currentSegment != null)
		{
			// We're adding a segment to the list
			if (_completedSegments == null)
			{
				_completedSegments = new List<CompletedBuffer>();
			}

			// Position might be less than the segment length if there wasn't enough space to satisfy the sizeHint when
			// GetMemory was called. In that case we'll take the current segment and call it "completed", but need to
			// ignore any empty space in it.
			_completedSegments.Add(new CompletedBuffer(_currentSegment, _position));
		}

		// Find a suitable buffer from previously allocated buffers.
		for (int i = 0; i < _availableBuffers.Count; i++)
		{
			if (_availableBuffers[i].Length >= sizeHint)
			{
				_currentSegment = _availableBuffers[i];
				_availableBuffers.RemoveAt(i);
				_position = 0;
				return;
			}
		}

		// Allocate a new buffer using the minimum segment size, unless the size hint is larger than a single segment.
		_currentSegment = AllocateBuffer(sizeHint);
		_position = 0;
	}

	private byte[] AllocateBuffer(int sizeHint)
	{
		return new byte[Math.Max(_minimumSegmentSize, sizeHint)];
	}

	private async Task CopyToSlowAsync(Stream destination, CancellationToken cancellationToken)
	{
		if (_completedSegments != null)
		{
			// Copy full segments
			var count = _completedSegments.Count;
			for (var i = 0; i < count; i++)
			{
				var segment = _completedSegments[i];
#if NETCOREAPP
				await destination.WriteAsync(segment.Buffer.AsMemory(0, segment.Length), cancellationToken).ConfigureAwait(false);
#else
				await destination.WriteAsync(segment.Buffer, 0, segment.Length, cancellationToken).ConfigureAwait(false);
#endif
			}
		}

		if (_currentSegment is not null)
		{
#if NETCOREAPP
			await destination.WriteAsync(_currentSegment.AsMemory(0, _position), cancellationToken).ConfigureAwait(false);
#else
			await destination.WriteAsync(_currentSegment, 0, _position, cancellationToken).ConfigureAwait(false);
#endif
		}
	}

	public byte[] ToArray()
	{
		if (_currentSegment == null)
		{
			return Array.Empty<byte>();
		}

		var result = new byte[_bytesWritten];

		var totalWritten = 0;

		if (_completedSegments != null)
		{
			// Copy full segments
			var count = _completedSegments.Count;
			for (var i = 0; i < count; i++)
			{
				var segment = _completedSegments[i];
				segment.Span.CopyTo(result.AsSpan(totalWritten));
				totalWritten += segment.Span.Length;
			}
		}

		// Copy current incomplete segment
		_currentSegment.AsSpan(0, _position).CopyTo(result.AsSpan(totalWritten));

		return result;
	}

	public void CopyTo(Span<byte> span)
	{
		Debug.Assert(span.Length >= _bytesWritten);

		if (_currentSegment == null)
		{
			return;
		}

		var totalWritten = 0;

		if (_completedSegments != null)
		{
			// Copy full segments
			var count = _completedSegments.Count;
			for (var i = 0; i < count; i++)
			{
				var segment = _completedSegments[i];
				segment.Span.CopyTo(span.Slice(totalWritten));
				totalWritten += segment.Span.Length;
			}
		}

		// Copy current incomplete segment
		_currentSegment.AsSpan(0, _position).CopyTo(span.Slice(totalWritten));

		Debug.Assert(_bytesWritten == totalWritten + _position);
	}


	public void Dispose()
	{
		Reset();
	}

	/// <summary>
	/// Holds a byte[] and a size value. No need to return to buffer pools.
	/// </summary>
	private readonly struct CompletedBuffer
	{
		public byte[] Buffer { get; }
		public int Length { get; }

		public ReadOnlySpan<byte> Span => Buffer.AsSpan(0, Length);

		public CompletedBuffer(byte[] buffer, int length)
		{
			Buffer = buffer;
			Length = length;
		}
	}
}
