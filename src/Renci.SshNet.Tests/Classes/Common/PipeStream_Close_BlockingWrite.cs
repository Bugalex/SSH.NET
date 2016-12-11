using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Renci.SshNet.Common;

namespace Renci.SshNet.Tests.Classes.Common
{
    [TestClass]
    public class PipeStream_Close_BlockingWrite
    {
        private Pipe _pipeStream;
        private Exception _writeException;
        private IAsyncResult _asyncWriteResult;

        [TestInitialize]
        public void Init()
        {
            _pipeStream = new Pipe {Capacity = 3};

            Action writeAction = () =>
                {
                    _pipeStream.InStream.WriteByte(10);
                    _pipeStream.InStream.WriteByte(13);
                    _pipeStream.InStream.WriteByte(25);

                    // attempting to write more bytes than the max. buffer length should block
                    // until bytes are read or the stream is closed
                    try
                    {
                        _pipeStream.InStream.WriteByte(35);
                    }
                    catch (Exception ex)
                    {
                        _writeException = ex;
                        throw;
                    }
                };
            _asyncWriteResult = writeAction.BeginInvoke(null, null);
            // ensure we've started writing
            _asyncWriteResult.AsyncWaitHandle.WaitOne(50);

            Act();
        }

        protected void Act()
        {
            _pipeStream.Dispose();

            // give async write time to complete
            _asyncWriteResult.AsyncWaitHandle.WaitOne(100);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void BlockingWriteShouldHaveBeenInterrupted()
        {
            Assert.IsTrue(_asyncWriteResult.IsCompleted);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void WriteShouldHaveThrownObjectDisposedException()
        {
            Assert.IsNotNull(_writeException);
            Assert.AreEqual(typeof (ObjectDisposedException), _writeException.GetType());
        }
    }
}
