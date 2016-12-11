using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Renci.SshNet.Common;
using Renci.SshNet.Tests.Common;
using System;
using System.IO;

namespace Renci.SshNet.Tests.Classes.Common
{
    [TestClass]
    public class PipeStreamTest : TestBase
    {
        [TestMethod]
        [TestCategory("Pipe")]
        public void Test_PipeStream_Write_Read_Buffer()
        {
            var testBuffer = new byte[1024];
            new Random().NextBytes(testBuffer);

            var outputBuffer = new byte[1024];

            using (var stream = new Pipe(Pipe.DefaultCapacity, PipeFlags.Default, PipeFlags.Default))
            {
                stream.InStream.Write(testBuffer, 0, testBuffer.Length);

                Assert.AreEqual(stream.OutStream.Length, testBuffer.Length);

                //stream.OutStream.Read(outputBuffer, 0, outputBuffer.Length);

                //Assert.AreEqual(stream.OutStream.Length, 0);

                //Assert.IsTrue(testBuffer.IsEqualTo(outputBuffer));
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Test_PipeStream_Write_Read_Byte()
        {
            var testBuffer = new byte[1024];
            new Random().NextBytes(testBuffer);

            using (var stream = new Pipe())
            {
                stream.InStream.Write(testBuffer, 0, testBuffer.Length);
                Assert.AreEqual(stream.OutStream.Length, testBuffer.Length);
                stream.OutStream.ReadByte();
                Assert.AreEqual(stream.OutStream.Length, testBuffer.Length - 1);
                stream.OutStream.ReadByte();
                Assert.AreEqual(stream.OutStream.Length, testBuffer.Length - 2);
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Read()
        {
            const int sleepTime = 100;

            var target = new Pipe();
            target.InStream.WriteByte(0x0a);
            target.InStream.WriteByte(0x0d);
            target.InStream.WriteByte(0x09);

            var readBuffer = new byte[2];
            var bytesRead = target.OutStream.Read(readBuffer, 0, readBuffer.Length);
            Assert.AreEqual(2, bytesRead);
            Assert.AreEqual(0x0a, readBuffer[0]);
            Assert.AreEqual(0x0d, readBuffer[1]);

            Assert.IsTrue(target.Count == 1);

            var writeToStreamThread = new Thread(
                () =>
                    {
                        Thread.Sleep(sleepTime);
                        var writeBuffer = new byte[] {0x05, 0x03};
                        target.InStream.Write(writeBuffer, 0, writeBuffer.Length);
                    });
            writeToStreamThread.Start();

            readBuffer = new byte[2];
            bytesRead = target.OutStream.Read(readBuffer, 0, readBuffer.Length);
            Assert.AreEqual(1, bytesRead);
            Assert.AreEqual(0x09, readBuffer[0]);

            bytesRead = target.OutStream.Read(readBuffer, 0, readBuffer.Length);
            Assert.AreEqual(0x05, readBuffer[0]);
            Assert.AreEqual(0x03, readBuffer[1]);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void SeekShouldThrowNotSupportedException()
        {
            const long offset = 0;
            const SeekOrigin origin = new SeekOrigin();
            var target = new Pipe();

            try
            {
                target.OutStream.Seek(offset, origin);
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }

        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void SetLengthShouldThrowNotSupportedException()
        {
            var target = new Pipe();

            try
            {
                target.InStream.SetLength(1);
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void WriteTest()
        {
            var target = new Pipe();

            var writeBuffer = new byte[] {0x0a, 0x05, 0x0d};
            target.InStream.Write(writeBuffer, 0, 2);

            writeBuffer = new byte[] { 0x02, 0x04, 0x03, 0x06, 0x09 };
            target.InStream.Write(writeBuffer, 1, 2);

            var readBuffer = new byte[6];
            var bytesRead = target.OutStream.Read(readBuffer, 0, 4);

            Assert.AreEqual(4, bytesRead);
            Assert.AreEqual(0x0a, readBuffer[0]);
            Assert.AreEqual(0x05, readBuffer[1]);
            Assert.AreEqual(0x04, readBuffer[2]);
            Assert.AreEqual(0x03, readBuffer[3]);
            Assert.AreEqual(0x00, readBuffer[4]);
            Assert.AreEqual(0x00, readBuffer[5]);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void CanReadTest()
        {
            var target = new Pipe();
            Assert.IsTrue(target.OutStream.CanRead);
            Assert.IsFalse(target.InStream.CanRead);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void CanSeekTest()
        {
            var target = new Pipe(); // TODO: Initialize to an appropriate value
            Assert.IsFalse(target.OutStream.CanSeek);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void CanWriteTest()
        {
            var target = new Pipe();
            Assert.IsTrue(target.InStream.CanWrite);
            Assert.IsFalse(target.OutStream.CanWrite);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void LengthTest()
        {
            var target = new Pipe();
            Assert.AreEqual(0L, target.OutStream.Length);
            target.InStream.Write(new byte[] { 0x0a, 0x05, 0x0d }, 0, 2);
            Assert.AreEqual(2L, target.OutStream.Length);
            target.InStream.WriteByte(0x0a);
            Assert.AreEqual(3L, target.OutStream.Length);
            target.OutStream.Read(new byte[2], 0, 2);
            Assert.AreEqual(1L, target.OutStream.Length);
            target.OutStream.ReadByte();
            Assert.AreEqual(0L, target.OutStream.Length);
        }

        /// <summary>
        ///A test for MaxBufferLength
        ///</summary>
        [TestMethod]
        [TestCategory("Pipe")]
        public void MaxBufferLengthTest()
        {
            var target = new Pipe();
            Assert.AreEqual(Pipe.DefaultCapacity, target.Capacity);
            target.Capacity = 1;
            Assert.AreEqual(1L, target.Capacity);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Position_GetterAlwaysReturnsZero()
        {
            var target = new Pipe();

            Assert.AreEqual(0, target.OutStream.Position);
            target.InStream.WriteByte(0x0a);
            Assert.AreEqual(0, target.OutStream.Position);
            target.OutStream.ReadByte();
            Assert.AreEqual(0, target.OutStream.Position);
        }

        [TestMethod]
        [TestCategory("Pipe")]
        public void Position_SetterAlwaysThrowsNotSupportedException()
        {
            var target = new Pipe();

            try
            {
                target.OutStream.Position = 0;
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}