using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Renci.SshNet.Common;

namespace Renci.SshNet.Tests.Classes.Common
{
    [TestClass]
    public class PipeStream_Close_BlockingRead
    {
        private Pipe _pipeStream;
        private int _bytesRead;
        private IAsyncResult _asyncReadResult;

        [TestInitialize]
        public void Init()
        {
            _pipeStream = new Pipe();

            _pipeStream.InStream.WriteByte(10);
            _pipeStream.InStream.WriteByte(13);
            _pipeStream.InStream.WriteByte(25);

            _bytesRead = 123;

            Action readAction = () => _bytesRead = _pipeStream.OutStream.Read(new byte[4], 0, 4);
            _asyncReadResult = readAction.BeginInvoke(null, null);
            // ensure we've started reading
            _asyncReadResult.AsyncWaitHandle.WaitOne(50);

            Act();
        }

        protected void Act()
        {
            _pipeStream.InStream.Close();

            // give async read time to complete
            _asyncReadResult.AsyncWaitHandle.WaitOne(100);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void BlockingReadShouldHaveBeenInterrupted()
        {
            Assert.IsTrue(_asyncReadResult.IsCompleted);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void ReadShouldHaveReturnedNotZero()
        {
            Assert.AreEqual(3, _bytesRead);
        }
    }
}
