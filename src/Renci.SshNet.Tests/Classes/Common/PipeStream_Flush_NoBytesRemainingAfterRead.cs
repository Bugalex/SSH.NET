using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Renci.SshNet.Common;

namespace Renci.SshNet.Tests.Classes.Common
{
    [TestClass]
    public class PipeStream_Flush_NoBytesRemainingAfterRead
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

            _bytesRead = 0;
            _readBuffer = new byte[4];

            Action readAction = () => _bytesRead = _pipeStream.OutStream.Read(_readBuffer, 0, _readBuffer.Length);
            _asyncReadResult = readAction.BeginInvoke(null, null);
            _asyncReadResult.AsyncWaitHandle.WaitOne(50);

            Act();
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
            Assert.AreEqual(2, _bytesRead);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void BytesAvailableInStreamShouldHaveBeenWrittenToBuffer()
        {
            Assert.AreEqual(10, _readBuffer[0]);
            Assert.AreEqual(13, _readBuffer[1]);
            Assert.AreEqual(0, _readBuffer[2]);
            Assert.AreEqual(0, _readBuffer[3]);
        }
    }
}
