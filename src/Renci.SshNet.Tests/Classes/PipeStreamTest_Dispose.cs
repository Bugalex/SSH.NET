using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Renci.SshNet.Common;
using Renci.SshNet.Tests.Common;

namespace Renci.SshNet.Tests.Classes
{
    [TestClass]
    public class PipeStreamTest_Dispose : TestBase
    {
        private Pipe _pipeStream;

        protected override void OnInit()
        {
            base.OnInit();

            Arrange();
            Act();
        }

        private void Arrange()
        {
            _pipeStream = new Pipe(200 * 1024 * 1024);
        }

        private void Act()
        {
            _pipeStream.Dispose();
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void CanRead_ShouldReturnTrue()
        {
            Assert.IsFalse(_pipeStream.CanRead);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Flush_ShouldThrowObjectDisposedException()
        {
            try
            {
                _pipeStream.InStream.Flush();
                Assert.Fail();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void MaxBufferLength_Getter_ShouldReturnTwoHundredMegabyte()
        {
            Assert.AreEqual(200 * 1024 * 1024, _pipeStream.Capacity);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void MaxBufferLength_Setter_ShouldModifyMaxBufferLength()
        {
            var newValue = new Random().Next(1, int.MaxValue);
            _pipeStream.Capacity = newValue;
            Assert.AreEqual(newValue, _pipeStream.Capacity);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Length_ShouldThrowObjectDisposedException()
        {
            try
            {
                var value = _pipeStream.InStream.Length;
                Assert.Fail("" + value);
            }
            catch (ObjectDisposedException)
            {
            }
            try
            {
                var value = _pipeStream.OutStream.Length;
                Assert.Fail("" + value);
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Position_Getter_ShouldReturnZero()
        {
            Assert.AreEqual(0, _pipeStream.OutStream.Position);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Position_Setter_ShouldThrowNotSupportedException()
        {
            try
            {
                _pipeStream.OutStream.Position = 0;
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Read_ByteArrayAndOffsetAndCount_ShouldThrowObjectDisposedException()
        {
            var buffer = new byte[0];
            const int offset = 0;
            const int count = 0;

            try
            {
                _pipeStream.OutStream.Read(buffer, offset, count);
                Assert.Fail();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void ReadByte_ShouldThrowObjectDisposedException()
        {
            try
            {
                _pipeStream.OutStream.ReadByte();
                Assert.Fail();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Seek_ShouldThrowNotSupportedException()
        {
            try
            {
                _pipeStream.OutStream.Seek(0, SeekOrigin.Begin);
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
            try
            {
                _pipeStream.InStream.Seek(0, SeekOrigin.Begin);
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void SetLength_ShouldThrowNotSupportedException()
        {
            try
            {
                _pipeStream.OutStream.SetLength(0);
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
            try
            {
                _pipeStream.InStream.SetLength(0);
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Write_ByteArrayAndOffsetAndCount_ShouldThrowObjectDisposedException()
        {
            var buffer = new byte[0];
            const int offset = 0;
            const int count = 0;

            try
            {
                _pipeStream.InStream.Write(buffer, offset, count);
                Assert.Fail();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void WriteByte_ShouldThrowObjectDisposedException()
        {
            const byte b = 0x0a;

            try
            {
                _pipeStream.InStream.WriteByte(b);
                Assert.Fail();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}
