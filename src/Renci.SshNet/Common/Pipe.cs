using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Renci.SshNet.Common
{
	/// <summary>
	/// Flags to modify the behavior of the <see cref="Renci.SshNet.Common.Pipe"/> class.
	/// </summary>
	[Flags]
	public enum PipeFlags
	{
		/// <summary>
		/// The default behavior is used e.g. reads and writes block till data is available or it is enough space.
		/// </summary>
		Default = 0,

		/// <summary>
		/// Data written to the input stream will not be duplicated. This increases the speed significantly.
		/// But the data should not be modified after writing!
		/// </summary>
		NoCopy = 0x01,

		/// <summary>
		/// Writes will block till the data is read from output stream.
		/// </summary>
		Sync = 0x02,

		/// <summary>
		/// If set the pipe stream always return zero for the owning pipe.
		/// </summary>
		PipeInvisible = 0x80,
	}

	/// <summary>
	/// Pipe is a thread-safe data buffer which provides two streams, one for reading and the other 
	/// one for writing.
	/// </summary>
	/// <version>2014/11/19 1.0</version>
	/// <license>
	///	Copyright (c) 2016 Bugalex (bugalex at hotmail)
	///	
	///	Permission is hereby granted, free of charge, to any person obtaining a copy of this software and 
	///	associated documentation files (the "Software"), to deal in the Software without restriction, 
	///	including without limitation the rights to use, copy, modify, merge, publish, distribute, 
	///	sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is 
	///	furnished to do so, subject to the following conditions:
	///	
	///	The above copyright notice and this permission notice shall be included in all copies or 
	///	substantial portions of the Software.
	///	
	///	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, 
	///	INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR 
	///	PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE 
	///	LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT 
	///	OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
	///	OTHER DEALINGS IN THE SOFTWARE.
	/// </license>
	public class Pipe : IDisposable
	{
		/// <summary>
		/// The default capacity of the pipe.
		/// </summary>
		public const int DefaultCapacity = 256 * 1024 * 1024;


		/// <summary>
		/// Creates a new instance of the <see cref="Renci.SshNet.Common.Pipe"/> class.
		/// </summary>
		public Pipe() : this(DefaultCapacity, PipeFlags.Default)
		{
		}

		/// <summary>
		/// Creates a new instance of the <see cref="Renci.SshNet.Common.Pipe"/> class.
		/// </summary>
		/// <param name="flags">Flags to modify the behavior.</param>
		public Pipe(PipeFlags flags) : this(DefaultCapacity, PipeFlags.Default)
		{
		}

		/// <summary>
		/// Creates a new instance of the <see cref="Renci.SshNet.Common.Pipe"/> class.
		/// </summary>
		/// <param name="capacity">The maximum capacity in bytes.</param>
		public Pipe(int capacity) : this(capacity, PipeFlags.Default)
		{
		}

		/// <summary>
		/// Creates a new instance of the <see cref="Renci.SshNet.Common.Pipe"/> class.
		/// </summary>
		/// <param name="capacity">The maximum capacity in bytes.</param>
		/// <param name="flags">Flags to modify the behavior of the input and output stream.</param>
		public Pipe(int capacity, PipeFlags flags) : this(capacity, flags, flags)
		{
		}

		/// <summary>
		/// Creates a new instance of the <see cref="Renci.SshNet.Common.Pipe"/> class.
		/// </summary>
		/// <param name="capacity">The maximum capacity in bytes.</param>
		/// <param name="inFlags">Flags to modify the behavior of the input stream.</param>
		/// <param name="outFlags">Flags to modify the behavior of the output stream.</param>
		public Pipe(int capacity, PipeFlags inFlags, PipeFlags outFlags)
		{
			this.inStream = new InPipeStream(this);
			this.outStream = new OutPipeStream(this);

			this.inputFlags = inFlags;
			this.outputFlags = outFlags;

			this.Capacity = capacity;
		}


		InPipeStream inStream;
		/// <summary>
		/// Gets the writing only stream end of the pipe.
		/// </summary>
		public InPipeStream InStream
		{
			get
			{
				return this.inStream;
			}
		}

		OutPipeStream outStream;
		/// <summary>
		/// Gets the reading only stream end of the pipe
		/// </summary>
		public OutPipeStream OutStream
		{
			get
			{
				return this.outStream;
			}
		}

		object _syncObj = new object();

		int capacity;
		/// <summary>
		/// Gets or sets the maximum capacity of the pipe.
		/// The default capacity is 256 MB.
		/// </summary>
		/// <exception cref="System.ArgumentOutOfRangeException">The new capacity was smaller or equal 0.</exception>
		public int Capacity
		{
			get
			{
				return this.capacity;
			}
			set
			{
				if(value <= 0)
					throw new ArgumentOutOfRangeException("value", "The capacity must be larger than 0.");

				//We allow to set the maximum capacity smaller than the data already bufferd in the pipe.
				//Writes will simply wait till enough space is available.
				this.capacity = value;
				lock(this._syncObj)
					Monitor.Pulse(this._syncObj);
			}
		}

		bool isFlushing = false;

		PipeFlags inputFlags;
		/// <summary>
		/// Gets or sets the modify flags of the input stream.
		/// </summary>
		public PipeFlags InputFlags
		{
			get
			{
				return this.inputFlags;
			}
			set
			{
				inputFlags = value;
				lock(this._syncObj)
					Monitor.Pulse(this._syncObj);
			}
		}

		PipeFlags outputFlags;
		/// <summary>
		/// Gets or sets the modify flags of the output stream.
		/// </summary>
		public PipeFlags OutputFlags
		{
			get
			{
				return this.outputFlags;
			}
			set
			{
				outputFlags = value;
				lock(this._syncObj)
					Monitor.Pulse(this._syncObj);
			}
		}

		int count = 0;
		/// <summary>
		/// Gets the current amount of bytes stored in the pipe.
		/// </summary>
		public int Count
		{
			get
			{
				return this.count;
			}
		}

		/// <summary>
		/// Gets if read operations from the output stream end of pipe are possible.
		/// </summary>
		public bool CanRead
		{
			get
			{
				lock(this._syncObj)
				{
					if(this.count == 0 && this.inStream.IsClosed)
						return false;

					return true;
				}
			}
		}

		/// <summary>
		/// Gets if write operations to the input stream end of the pipe are possible.
		/// </summary>
		public bool CanWrite
		{
			get
			{
				return !this.outStream.IsClosed;
			}
		}

		int readTimeout = -1;
		/// <summary>
		/// Gets or sets the timeout of read operations in milliseconds.
		/// Negative values indicate a infinite wait.
		/// </summary>
		public int ReadTimeout
		{
			get
			{
				return this.readTimeout;
			}
			set
			{
				this.readTimeout = value;
				lock(this._syncObj)
					Monitor.Pulse(this._syncObj);
			}
		}

		int writeTimeout = -1;
		/// <summary>
		/// Gets or sets the timeout of write operations in milliseconds.
		/// </summary>
		public int WriteTimeout
		{
			get
			{
				return this.writeTimeout;
			}
			set
			{
				this.writeTimeout = value;
				lock(this._syncObj)
					Monitor.Pulse(this._syncObj);
			}
		}

		//Linked list entries to buffer the data
		PipeEntry first;	//First element
		PipeEntry last;		//Last element


		private void Pulse()
		{
			lock(this._syncObj)
				Monitor.Pulse(this._syncObj);
		}

		private void Flush(int timeout)
		{
			this.isFlushing = true;
			int waited = 0;
			lock(this._syncObj)
			{
				DateTime start = DateTime.Now;
				while(this.Count > 0)
				{
					if(timeout > 0)
					{
						waited = (int)(DateTime.Now - start).TotalMilliseconds;
						if(waited > timeout)
							throw new TimeoutException();

						Monitor.Wait(this._syncObj, timeout - waited);
					}
					else
						Monitor.Wait(this._syncObj);
				}

				this.isFlushing = false;
				Monitor.Pulse(this._syncObj);
			}
		}

		private void Clear()
		{
			lock(this._syncObj)
			{
				this.count = 0;
				this.first = 
				this.last = null;

				Monitor.Pulse(this._syncObj);
			}
		}

		private void AddData(byte[] buffer, int offset, int count, bool async)
		{
			if(buffer == null)
				throw new ArgumentNullException("buffer");
			if(offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException("offset", "The offset must be larger or equal to 0 and smaller than the buffer length.");
			if(count < 0 || count + offset > buffer.Length)
				throw new ArgumentException("count", "The count must be larger or equal to 0 and offset plus count must be smaller or equal to buffer length.");

			lock(this._syncObj)
			{
				this.WaitAdd(count, async);

				this.last = new PipeEntry(this.last, buffer, offset, count, (this.inputFlags & PipeFlags.NoCopy) == PipeFlags.Default);
				if(this.first == null)
					this.first = this.last;
				this.count += count;

				Monitor.Pulse(this._syncObj);
			}

			if((this.inputFlags & PipeFlags.Sync) == PipeFlags.Sync)
				this.Flush(this.writeTimeout);
		}
		private void WaitAdd(int count, bool async)
		{
			int waited = 0;
			DateTime start = DateTime.Now;
			while((this.count + count > this.capacity || this.isFlushing) && !this.outStream.IsClosed)
			{
				if(this.inStream.IsClosed)
					throw new InvalidOperationException("The input pipe stream is closed.");

				if(this.writeTimeout > 0)
				{
					waited = (int)(DateTime.Now - start).TotalMilliseconds;
					if(waited >= this.writeTimeout)
						throw new TimeoutException();
					Monitor.Wait(this._syncObj, this.writeTimeout - waited);
				}
				else
					Monitor.Wait(this._syncObj);
			}

			if(this.outStream.IsClosed)
				throw new InvalidOperationException("The output pipe stream is closed.");
		}

		private int RemoveData(byte[] buffer, int offset, int count, bool async)
		{
			if(buffer == null)
				throw new ArgumentNullException("buffer");
			if(offset < 0 || offset > buffer.Length)
				throw new ArgumentOutOfRangeException("offset", "The offset must be larger or equal to 0 and smaller than the buffer length.");
			if(count < 0 || count + offset > buffer.Length)
				throw new ArgumentOutOfRangeException("count", "The count must be larger or equal to 0 and offset plus count must be smaller or equal to buffer length.");

			int c = 0;
			lock(this._syncObj)
			{
				bool canRemove = this.WaitRemove(async, this.readTimeout);
				if(!canRemove)
					return 0;

				int removed;
				while(c < count && this.count > 0)
				{
					this.first = this.first.Remove(buffer, offset + c, count - c, out removed);
					if(this.first == null)
						this.last = null;
					c += removed;
					this.count -= removed;
				}

				Monitor.Pulse(this._syncObj);
			}

			return c;
		}
		private bool WaitRemove(bool async, int waitMilliSeconds)
		{
			if(this.outStream.IsClosed)
				throw new InvalidOperationException("The output pipe stream is closed.");

            if (waitMilliSeconds < 0)
                waitMilliSeconds = int.MaxValue;

            if (waitMilliSeconds > 0)
            {
                int waited = 0;
                DateTime start = DateTime.Now;
                while (this.Count == 0 && !this.outStream.IsClosed)
                {
                    if (this.inStream.IsClosed)
                        return false;

                    if (waitMilliSeconds > 0)
                    {
                        waited = (int)(DateTime.Now - start).TotalMilliseconds;
                        if (waited >= waitMilliSeconds)
                            throw new TimeoutException();
                        Monitor.Wait(this._syncObj, waitMilliSeconds - waited);
                    }
                    else
                        Monitor.Wait(this._syncObj);
                }
            }

			return !this.outStream.IsClosed && this.count > 0;
		}

		private int RemoveByte()
		{
			int b = -1;
			lock(this._syncObj)
			{
				bool canRemove = this.WaitRemove(false, this.readTimeout);
				if(!canRemove)
					return -1;

				this.first = this.first.RemoveByte(out b);
				if(this.first == null)
					this.last = null;

				this.count--;

				Monitor.Pulse(this._syncObj);
			}

			return b;
		}

		private byte[] RemoveAvailable(int maxBlockSize)
		{
			if(maxBlockSize <= 0)
				throw new ArgumentException("The value must be larger than 0.", "maxBlockSize");

			int c = 0;
			byte[] buffer = null;
			lock(this._syncObj)
			{
				bool canRemove = this.WaitRemove(false, this.readTimeout);
				if(!canRemove)
					return null;

				int count;
				if(maxBlockSize > this.count)
					count = this.count;
				else
					count = maxBlockSize;

				buffer = new byte[count];
				int removed;
				while(c < count && this.count > 0)
				{
					this.first = this.first.Remove(buffer, c, count - c, out removed);
					if(this.first == null)
						this.last = null;
					c += removed;
					this.count -= removed;
				}

				Monitor.Pulse(this._syncObj);
			}

			return buffer;
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged ResourceMessages.
		/// </summary>
		public void Dispose()
		{
			this.inStream.Dispose();
			this.outStream.Dispose();
			this.Clear();
		}


		#region Nested types

		private class PipeEntry
		{
			public PipeEntry(PipeEntry prev, byte[] data, int offset, int count, bool copy)
			{
				if(prev != null)
				{
					if(prev.next != null)
						throw new ArgumentException("The previous pipe data entry already has a next element. Inserting of data is not supported.");

					prev.next = this;
				}

				if(copy)
				{
					this.data = new byte[count];
					this.length = count;
					this.read = 0;
					Array.Copy(data, offset, this.data, 0, count);
				}
				else
				{
					this.data = data;
					this.read = offset;
					this.length = offset + count;
				}
			}


			PipeEntry next;

			byte[] data;
			int read = 0;
			int length;


			public PipeEntry Remove(byte[] buffer, int offset, int count, out int removed)
			{
				int c = this.length - read;
				if(c > count)
					c = count;

				Array.Copy(this.data, this.read, buffer, offset, c);

				removed = c;
				this.read += c;

				if(this.read == this.length)
				{
					PipeEntry e = this.next;
					this.next = null;

					return e;
				}

				return this;
			}

			public PipeEntry RemoveByte(out int b)
			{
				b = this.data[read];
				read++;

				if(this.read == this.length)
				{
					PipeEntry e = this.next;
					this.next = null;

					return e;
				}

				return this;
			}
		}

		/// <summary>
		/// Represents the writing only end of a pipe.
		/// </summary>
		public class InPipeStream : Stream
		{
			/// <summary>
			/// Creates a new instance of a input stream for a pipe.
			/// This constructor is only indent to be used by the <see cref="Renci.SshNet.Common.Pipe"/> class.
			/// </summary>
			/// <param name="pipe">The owning pipe.</param>
			internal InPipeStream(Pipe pipe)
			{
				if(pipe == null)
					throw new ArgumentNullException("pipe");
				if(pipe.inStream != null)
					throw new ArgumentException("The pipe already has a input stream.");

				this.pipe = pipe;
			}


			Pipe pipe;
			/// <summary>
			/// Gets the owning pipe of this stream.
			/// </summary>
			public Pipe Pipe
			{
				get
				{
					if((this.pipe.InputFlags & PipeFlags.PipeInvisible) != PipeFlags.Default)
						return null;
					return this.pipe;
				}
			}

			/// <summary>
			/// Gets or sets flags to modify the behavior of the pipe stream.
			/// </summary>
			public PipeFlags Flags
			{
				get
				{
					return this.pipe.InputFlags;
				}
				set
				{
					if(this.isClosed)
						throw new InvalidOperationException("The stream is closed.");

					//Set new value and keep the pipe invisible flag.
					this.pipe.InputFlags = value | (this.pipe.InputFlags & PipeFlags.PipeInvisible);
				}
			}

			object _syncObj = new object();	//Sync object for async write operations.

			bool isClosed = false;
			/// <summary>
			/// Gets a value indicating whether the current stream is closed.
			/// </summary>
			public bool IsClosed
			{
				get
				{
					return this.isClosed;
				}
			}

			/// <summary>
			/// Gets or sets the buffer capacity in bytes.
			/// </summary>
			public int Capacity
			{
				get
				{
					return this.pipe.capacity;
				}
				set
				{
					this.pipe.capacity = value;
				}
			}

			/// <summary>
			/// Gets a value indicating whether the current stream supports reading.
			/// </summary>
			/// <returns>
			/// true if the stream supports reading; otherwise, false.
			/// </returns>
			/// <filterpriority>1</filterpriority>
			public override bool CanRead
			{
				get { return false; }
			}

			/// <summary>
			/// Gets a value indicating whether the current stream supports seeking.
			/// </summary>
			/// <returns>
			/// Returns always false.
			/// </returns>
			/// <filterpriority>1</filterpriority>
			public override bool CanSeek
			{
				get { return false; }
			}

			/// <summary>
			/// Gets a value indicating whether the current stream supports writing.
			/// </summary>
			/// <returns>
			/// true if the stream supports writing; otherwise, false.
			/// </returns>
			/// <filterpriority>1</filterpriority>
			public override bool CanWrite
			{
				get { return !this.isClosed && this.pipe.CanWrite; }
			}

			/// <summary>
			/// Gets a value that determines whether the current stream can time out.
			/// </summary>
			public override bool CanTimeout
			{
				get
				{
					return true;
				}
			}

			/// <summary>
			/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
			/// </summary>
			/// <exception cref="System.NotSupportedException">The stream does not support reading. </exception>
			public override int ReadTimeout
			{
				get
				{
					throw new NotSupportedException("Read timeout for input pipes is not supported.");
				}
				set
				{
					throw new NotSupportedException("Setting a read timeout for a input pipe is not supported.");
				}
			}

			/// <summary>
			/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out. 
			/// </summary>
			public override int WriteTimeout
			{
				get
				{
					return this.pipe.WriteTimeout;
				}
				set
				{
					this.pipe.WriteTimeout = value;
				}
			}

			/// <summary>
			/// Gets the length in bytes of the stream.
			/// </summary>
			/// <returns>
			/// A long value representing the length of the stream in bytes.
			/// </returns>
			/// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed. </exception>
            /// <filterpriority>1</filterpriority>
			public override long Length
			{
				get { return this.pipe.Count; }
			}

			/// <summary>
			/// Gets or sets the position within the current stream.
			/// Attempting to modify the position will always throw a exception.
			/// </summary>
			/// <returns>
			/// The current position within the stream which is always the length of the stream.
			/// </returns>
			/// <exception cref="System.NotSupportedException">The stream does not support seeking. </exception>
			public override long Position
			{
				get
				{
					return this.Length;
				}
				set
				{
					this.Seek(value, SeekOrigin.Begin);
				}
			}


			/// <summary>
			/// Clears all buffers for this stream and wait till all data is read from the output pipe end.
			/// </summary>
			/// <exception cref="T:System.IO.IOException">An I/O error occurs.</exception>
            /// <filterpriority>2</filterpriority>
			public override void Flush()
			{
				this.pipe.Flush(this.WriteTimeout);
			}

			/// <summary>
			/// Sets the position within the current stream.
			/// Attempting to modify the position will always throw a exception.
			/// </summary>
			/// <returns>
			/// The new position within the current stream.
			/// </returns>
			/// <param name="offset">A byte offset relative to the origin parameter. </param>
			/// <param name="origin">A value of type <see cref="System.IO.SeekOrigin"></see> indicating the reference point used to obtain the new position.</param>
			/// <exception cref="System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output.</exception>
			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new NotSupportedException("Seeking a pipe stream is not possible.");
			}

			/// <summary>
			/// Sets the length of the current stream.
			/// </summary>
			/// <param name="value">The desired length of the current stream in bytes. </param>
			/// <exception cref="System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output. </exception>
			public override void SetLength(long value)
			{
				throw new NotSupportedException("Changing the length of a pipe is not possible.");
			}

			/// <summary>
			/// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
			/// </summary>
			/// <returns>
			/// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
			/// </returns>
			/// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. </param>
			/// <param name="count">The maximum number of bytes to be read from the current stream. </param>
			/// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source. </param>
			/// <exception cref="System.NotSupportedException">The stream does not support reading. </exception>
			public override int Read(byte[] buffer, int offset, int count)
			{
				throw new NotSupportedException("Reading from the input end of a pipe is not possible.");
			}
			
			/// <summary>
			/// Reads a single byte from the current stream and advances the position within the stream by one.
			/// </summary>
			/// <returns>The byte read or -1 if the end of the stream has been reached.</returns>
			/// <exception cref="System.NotSupportedException">The stream does not support reading. </exception>
			public override int ReadByte()
			{
				throw new NotSupportedException("Reading from the input end of a pipe is not possible.");
			}

            /// <summary>
            /// Begins an asynchronous read operation.
            /// </summary>
            /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
            /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
            /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
            /// <param name="callback">An optional asynchronous callback, to be called when the read is complete.</param>
            /// <param name="state">A user-provided object that distinguishes this particular asynchronous read request from other requests.</param>
            /// <returns>An <see cref="System.IAsyncResult"/> that represents the asynchronous read, which could still be pending.</returns>
            /// <exception cref="System.NotSupportedException">The stream does not support reading.</exception>
            public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
            {
                throw new NotSupportedException("Reading from the input end of a pipe is not possible.");
            }

            /// <summary>
            /// Waits for the pending asynchronous read to complete.
            /// </summary>
            /// <param name="asyncResult">The reference to the pending asynchronous request to finish.</param>
            /// <returns>The number of bytes read from the stream, between zero (0) and the number of bytes you requested. Streams return zero (0) only at the end of the stream, otherwise, they should block until at least one byte is available.</returns>
            /// <exception cref="System.NotSupportedException">The stream does not support reading. </exception>
            public override int EndRead(IAsyncResult asyncResult)
            {
                throw new NotSupportedException("Reading from the input end of a pipe is not possible.");
            }

            /// <summary>
            /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
            /// </summary>
            /// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
            /// <param name="count">The number of bytes to be written to the current stream.</param>
            /// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
            /// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
            /// <exception cref="System.NotSupportedException">The stream does not support writing.</exception>
            /// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
            /// <exception cref="System.ArgumentNullException">buffer is null.</exception>
            /// <exception cref="System.ArgumentException">The sum of offset and count is greater than the buffer length.</exception>
            /// <exception cref="System.ArgumentOutOfRangeException">offset or count is negative.</exception>
            /// <filterpriority>1</filterpriority>
            public override void Write(byte[] buffer, int offset, int count)
			{
				if(this.isClosed)
					throw new ObjectDisposedException("The input end of the pipe is closed.");

				this.pipe.AddData(buffer, offset, count, false);
			}

			/// <summary>
			/// Writes a byte to the current position in the stream and advances the position within the stream by one byte.
			/// </summary>
			/// <param name="value">The byte to write to the stream.</param>
			/// <exception cref="T:System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
			public override void WriteByte(byte value)
			{
				if(this.isClosed)
					throw new ObjectDisposedException("The input end of the pipe is closed.");

				byte[] data = new byte[] { value };

				this.pipe.AddData(data, 0, 1, false);
			}

			/// <summary>
			/// Closes the current stream and releases any resources (such as sockets and file handles) associated with the current stream. Instead of calling this method, ensure that the stream is properly disposed.
			/// </summary>
			public override void Close()
			{
				base.Close();
			}

			/// <summary>
			/// Closes the stream and relases all managed and unmanged ressources.
			/// </summary>
			/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
			protected override void Dispose(bool disposing)
			{
				if(!this.isClosed)
				{
					this.isClosed = true;
					this.pipe.Pulse();
				}
				if(disposing)
				{
					this.pipe = null;
				}

				base.Dispose(disposing);
			}
		}


		/// <summary>
		/// Represents the reading only end of a pipe.
		/// </summary>
		public class OutPipeStream : Stream
		{
            /// <summary>
            /// Creates a new instance of a output stream for a pipe.
            /// This constructor is only indent to be used by the <see cref="Renci.SshNet.Common.Pipe"/> class.
            /// </summary>
            /// <param name="pipe">The owning pipe.</param>
            internal OutPipeStream(Pipe pipe)
			{
				if(pipe == null)
					throw new ArgumentNullException("pipe");
				if(pipe.outStream != null)
					throw new ArgumentException("The pipe already has a input stream.");

				this.pipe = pipe;
			}


			Pipe pipe;
			/// <summary>
			/// Gets the owning pipe of this stream.
			/// </summary>
			public Pipe Pipe
			{
				get
				{
					if((this.pipe.OutputFlags & PipeFlags.PipeInvisible) != PipeFlags.Default)
						return null;
					return this.pipe;
				}
			}

			/// <summary>
			/// Gets or sets flags to modify the behavior of the pipe stream.
			/// </summary>
			public PipeFlags Flags
			{
				get
				{
					return this.pipe.OutputFlags;
				}
				set
				{
					if(this.isClosed)
						throw new InvalidOperationException("The stream is closed.");
					this.pipe.OutputFlags = value | (this.pipe.OutputFlags & PipeFlags.PipeInvisible);
				}
			}

			object _syncObj = new object();	//Sync object for async write operations.

			bool isClosed = false;
			/// <summary>
			/// Gets a value indicating whether the current stream is closed.
			/// </summary>
			public bool IsClosed
			{
				get
				{
					return this.isClosed;
				}
			}

			/// <summary>
			/// Gets or sets the buffer capacity in bytes.
			/// </summary>
			public int Capacity
			{
				get
				{
					return this.pipe.capacity;
				}
				set
				{
					this.pipe.capacity = value;
				}
			}

			/// <summary>
			/// Gets a value indicating whether the current stream supports reading.
			/// </summary>
			/// <returns>
			/// true if the stream supports reading; otherwise, false.
			/// </returns>
			/// <filterpriority>1</filterpriority>
			public override bool CanRead
			{
				get { return !this.isClosed && this.pipe.CanRead; }
			}

			/// <summary>
			/// Gets a value indicating whether the current stream supports seeking.
			/// </summary>
			/// <returns>
			/// true if the stream supports seeking; otherwise, false.
			/// </returns>
			/// <filterpriority>1</filterpriority>
			public override bool CanSeek
			{
				get { return false; }
			}

			/// <summary>
			/// When overridden in a derived class, gets a value indicating whether the current stream supports writing.
			/// </summary>
			/// <returns>
			/// true if the stream supports writing; otherwise, false.
			/// </returns>
			/// <filterpriority>1</filterpriority>
			public override bool CanWrite
			{
				get { return false; }
			}

			/// <summary>
			/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
			/// </summary>
			public override bool CanTimeout
			{
				get
				{
					return true;
				}
			}

			/// <summary>
			/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to read before timing out.
			/// </summary>
			public override int ReadTimeout
			{
				get
				{
					return this.pipe.ReadTimeout;
				}
				set
				{
					this.pipe.ReadTimeout = value;
				}
			}

			/// <summary>
			/// Gets or sets a value, in miliseconds, that determines how long the stream will attempt to write before timing out. 
			/// </summary>
			/// <exception cref="System.NotSupportedException">The stream does not support writing.</exception>
			public override int WriteTimeout
			{
				get
				{
					throw new NotSupportedException("Write timeout for output pipes is not supported.");
				}
				set
				{
					throw new NotSupportedException("Setting a write timeout for a output pipe is not supported.");
				}
			}

			/// <summary>
			/// Gets the length in bytes of the stream.
			/// </summary>
			/// <returns>
			/// A long value representing the length of the stream in bytes.
			/// </returns>
			/// <exception cref="System.NotSupportedException">A class derived from Stream does not support seeking.</exception>
			/// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
			/// <filterpriority>1</filterpriority>
			public override long Length
			{
				get { return this.pipe.Count; }
			}

			/// <summary>
			/// Gets or sets the position within the current stream.
			/// </summary>
			/// <returns>
			/// The current position within the stream which is always 0.
			/// </returns>
			/// <exception cref="System.NotSupportedException">The stream does not support seeking. </exception>
			public override long Position
			{
				get
				{
					return 0;
				}
				set
				{
					this.Seek(value, SeekOrigin.Begin);
				}
			}


			/// <summary>
			/// Clears all buffers for this stream.
			/// This call is always blocking no matter what flags are set.
			/// </summary>
			/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
			/// <filterpriority>2</filterpriority>
			public override void Flush()
			{
				if(this.isClosed)
					throw new InvalidOperationException("The output end of the pipe is closed.");

				this.pipe.Clear();
			}

			/// <summary>
			/// This method is not supported.
			/// </summary>
			/// <returns>
			/// The new position within the current stream.
			/// </returns>
			/// <param name="offset">A byte offset relative to the origin parameter. </param>
			/// <param name="origin">A value of type <see cref="System.IO.SeekOrigin"></see> indicating the reference point used to obtain the new position.</param>
			/// <exception cref="System.NotSupportedException">The stream does not support seeking, such as if the stream is constructed from a pipe or console output.</exception>
			public override long Seek(long offset, SeekOrigin origin)
			{
				throw new NotSupportedException("Seeking a pipe stream is not possible.");
			}

			/// <summary>
			/// Sets the length of the current stream.
			/// </summary>
			/// <param name="value">The desired length of the current stream in bytes. </param>
			/// <exception cref="System.NotSupportedException">The stream does not support both writing and seeking, such as if the stream is constructed from a pipe or console output.</exception>
			public override void SetLength(long value)
			{
				throw new NotSupportedException("Changing the length of a pipe is not possible.");
			}

			/// <summary>
			/// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
			/// </summary>
			/// <returns>
			/// The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.
			/// </returns>
			/// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream. </param>
			/// <param name="count">The maximum number of bytes to be read from the current stream. </param>
			/// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
			/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
			/// <exception cref="System.NotSupportedException">The stream does not support writing.</exception>
			/// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
			/// <exception cref="System.ArgumentNullException">buffer is null.</exception>
			/// <exception cref="System.ArgumentException">The sum of offset and count is greater than the buffer length.</exception>
			/// <exception cref="System.ArgumentOutOfRangeException">offset or count is negative.</exception>
			public override int Read(byte[] buffer, int offset, int count)
			{
				if(this.isClosed)
					throw new ObjectDisposedException("The output end of the pipe is closed.");

				return this.pipe.RemoveData(buffer, offset, count, false);
			}

			/// <summary>
			/// Reads a byte from the stream and advances the position within the stream by one byte, or returns -1 if at the end of the stream.
			/// </summary>
			/// <returns>The unsigned byte cast to an Int32, or -1 if at the end of the stream.</returns>
			/// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
			public override int ReadByte()
			{
				if(this.isClosed)
					throw new ObjectDisposedException("The output end of the pipe is closed.");

				return this.pipe.RemoveByte();
			}

			/// <summary>
			/// Reads all available data. If no data is available it will wait till at least one byte is available.
			/// </summary>
			/// <returns>The data read from the stream.</returns>
			/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
			/// <exception cref="System.NotSupportedException">The stream does not support writing.</exception>
			/// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
			/// <exception cref="System.ArgumentNullException">buffer is null.</exception>
			/// <exception cref="System.ArgumentException">The sum of offset and count is greater than the buffer length.</exception>
			/// <exception cref="System.ArgumentOutOfRangeException">offset or count is negative.</exception>
			public byte[] ReadAvailable()
			{
				return this.ReadAvailable(65536);
			}

			/// <summary>
			/// Reads all available data. If no data is available it will wait till at least one byte is available.
			/// </summary>
			/// <param name="maxBlockSize">The maximum byte count to read.</param>
			/// <returns>The data read from the stream.</returns>
			/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
			/// <exception cref="System.NotSupportedException">The stream does not support writing.</exception>
			/// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
			/// <exception cref="System.ArgumentNullException">buffer is null.</exception>
			/// <exception cref="System.ArgumentException">The sum of offset and count is greater than the buffer length.</exception>
			/// <exception cref="System.ArgumentOutOfRangeException">offset or count is negative.</exception>
			public byte[] ReadAvailable(int maxBlockSize)
			{
				if(this.isClosed)
					throw new ObjectDisposedException("The output end of the pipe is closed.");

				return this.pipe.RemoveAvailable(maxBlockSize);
			}

			/// <summary>
			/// This method is not supported.
			/// </summary>
			/// <param name="offset">The zero-based byte offset in buffer at which to begin copying bytes to the current stream.</param>
			/// <param name="count">The number of bytes to be written to the current stream.</param>
			/// <param name="buffer">An array of bytes. This method copies count bytes from buffer to the current stream.</param>
			/// <exception cref="System.NotSupportedException">The stream does not support writing.</exception>
			public override void Write(byte[] buffer, int offset, int count)
			{
				throw new NotSupportedException("Writing to the output end of a pipe is not possible.");
			}

			/// <summary>
			/// This method is not supported.
			/// </summary>
			/// <param name="value"></param>
			public override void WriteByte(byte value)
			{
				throw new NotSupportedException("Writing to the output end of a pipe is not possible.");
			}

            /// <summary>
            /// This method is not supported.
            /// </summary>
            /// <param name="offset">The zero-based byte offset in buffer at which to begin storing the data read from the current stream.</param>
            /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
            /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between offset and (offset + count - 1) replaced by the bytes read from the current source.</param>
            /// <param name="callback">An optional asynchronous callback, to be called when the read is complete.</param>
            /// <param name="state">A user-provided object that distinguishes this particular asynchronous read request from other requests.</param>
            /// <returns>An <see cref="System.IAsyncResult"/> that represents the asynchronous write, which could still be pending.</returns>
            /// <exception cref="System.NotSupportedException">The stream does not support reading.</exception>
            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
			{
				throw new NotSupportedException("Writing to the output end of a pipe is not possible.");
			}

            /// <summary>
            /// Waits for the pending asynchronous read to complete.
            /// </summary>
            /// <param name="asyncResult">The reference to the pending asynchronous request to finish.</param>
            /// <exception cref="System.NotSupportedException">The stream does not support writing. </exception>
			public override void EndWrite(IAsyncResult asyncResult)
			{
				throw new NotSupportedException("Writing to the output end of a pipe is not possible.");
			}

			/// <summary>
			/// Writes the content till EOF to a onther stream.
			/// </summary>
			/// <param name="stream">The stream to which the data should be written.</param>
			/// <exception cref="System.IO.IOException">An I/O error occurs.</exception>
			/// <exception cref="System.NotSupportedException">The stream does not support writing.</exception>
			/// <exception cref="System.ObjectDisposedException">Methods were called after the stream was closed.</exception>
			/// <exception cref="System.ArgumentNullException">buffer is null.</exception>
			/// <exception cref="System.ArgumentException">The sum of offset and count is greater than the buffer length.</exception>
			/// <exception cref="System.ArgumentOutOfRangeException">offset or count is negative.</exception>
			public void WriteTo(Stream stream)
			{
				byte[] data = new byte[4096];
				int count;
				while((count = this.Read(data, 0, data.Length)) > 0)
				{
					stream.Write(data, 0, count);
				}
			}

			/// <summary>
			/// Closes the stream and relases all managed and unmanged ressources.
			/// </summary>
			/// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
			protected override void Dispose(bool disposing)
			{
				if(!this.isClosed)
				{
					this.isClosed = true;
					this.pipe.Clear();
				}
				if(disposing)
				{
					this.pipe = null;
				}

				base.Dispose(disposing);
			}

            /// <summary>
            /// Waits till data is available to read.
            /// </summary>
            /// <param name="microSeconds">The maximum time to wait. The value will be rounded to milliseconds.</param>
            /// <param name="mode">The polling mode.</param>
            /// <returns>true, if data is ready to read.</returns>
            public bool Poll(int microSeconds, SelectMode mode)
            {
                if (mode != SelectMode.SelectRead)
                    throw new ArgumentException("Invalid poll mode.");

                try
                {
                    lock (this.pipe._syncObj)
                        return this.pipe.WaitRemove(false, microSeconds / 1000);
                }
                catch(TimeoutException) { }

                return false;
            }
        }

		#endregion
	}
}
