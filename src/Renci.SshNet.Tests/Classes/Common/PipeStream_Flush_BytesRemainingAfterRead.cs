using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Renci.SshNet.Common;

namespace Renci.SshNet.Tests.Classes.Common
{
    [TestClass]
    public class PipeStream_Flush_BytesRemainingAfterRead
    {
        private Pipe _pipeStream;
        private byte[] _readBuffer;
        private int _bytesRead;
        private IAsyncResult _asyncReadResult;

        [TestInitialize]
        public void Init()
        {
            _pipeStream = new Pipe();
            _pipeStream.InStream.WriteByte(10);
            _pipeStream.InStream.WriteByte(13);
            _pipeStream.InStream.WriteByte(15);
            _pipeStream.InStream.WriteByte(18);
            _pipeStream.InStream.WriteByte(23);
            _pipeStream.InStream.WriteByte(28);

            _bytesRead = 0;
            _readBuffer = new byte[7];

            Action readAction = () =>
            {
                Thread.Sleep(100);  //Give time to call Flush....
                _bytesRead = _pipeStream.OutStream.Read(_readBuffer, 0, _readBuffer.Length);
            };
            _asyncReadResult = readAction.BeginInvoke(null, null);

            Act();

            _asyncReadResult.AsyncWaitHandle.WaitOne(50);

            //Write data again...
            _pipeStream.InStream.WriteByte(23);
            _pipeStream.InStream.WriteByte(28);
        }

        protected void Act()
        {
            _pipeStream.InStream.Flush();

            // give async read time to complete
            _asyncReadResult.AsyncWaitHandle.WaitOne(100);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void AsyncReadShouldHaveFinished()
        {
            Assert.IsTrue(_asyncReadResult.IsCompleted);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void ReadShouldReturnNumberOfBytesAvailableThatAreWrittenToBuffer()
        {
            Assert.AreEqual(6, _bytesRead);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void BytesAvailableInStreamShouldHaveBeenWrittenToBuffer()
        {
            Assert.AreEqual(10, _readBuffer[0]);
            Assert.AreEqual(13, _readBuffer[1]);
            Assert.AreEqual(15, _readBuffer[2]);
            Assert.AreEqual(18, _readBuffer[3]);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void RemainingBytesCanBeRead()
        {
            var buffer = new byte[3];

            var bytesRead = _pipeStream.OutStream.Read(buffer, 0, 2);

            Assert.AreEqual(2, bytesRead);
            Assert.AreEqual(23, buffer[0]);
            Assert.AreEqual(28, buffer[1]);
            Assert.AreEqual(0, buffer[2]);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void ReadingMoreBytesThanAvailableDoesNotBlock()
        {
            var buffer = new byte[4];

            var bytesRead = _pipeStream.OutStream.Read(buffer, 0, buffer.Length);

            Assert.AreEqual(2, bytesRead);
            Assert.AreEqual(23, buffer[0]);
            Assert.AreEqual(28, buffer[1]);
            Assert.AreEqual(0, buffer[2]);
            Assert.AreEqual(0, buffer[3]);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void ReadToBlockUntilBytesAreAvailable()
        {
            _pipeStream.OutStream.Flush();

            var buffer = new byte[4];
            int bytesRead = int.MaxValue;

            Thread readThread = new Thread(() =>
            {
                bytesRead = _pipeStream.OutStream.Read(buffer, 0, buffer.Length);
            });
            readThread.Start();

            Assert.IsFalse(readThread.Join(500));
            readThread.Abort();

            Assert.AreEqual(int.MaxValue, bytesRead);
            Assert.AreEqual(0, buffer[0]);
            Assert.AreEqual(0, buffer[1]);
            Assert.AreEqual(0, buffer[2]);
            Assert.AreEqual(0, buffer[3]);
        }
    }
}
