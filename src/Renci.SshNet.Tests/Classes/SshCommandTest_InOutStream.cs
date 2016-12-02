using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Renci.SshNet.Channels;
using Renci.SshNet.Common;
using Renci.SshNet.Tests.Common;

namespace Renci.SshNet.Tests.Classes
{
    [TestClass]
    public class SshCommandTest_InOutStream : TestBase
    {
        protected override void OnInit()
        {
            base.OnInit();
        }

        [TestMethod]
        [TestCategory("integration")]
        public void Test_Run_ReadWrite()
        {
            var host = Resources.HOST;
            var username = Resources.USERNAME;
            var password = Resources.PASSWORD;

            using (var client = new SshClient(host, username, password))
            {
                #region Example SshCommand RunCommand Result
                client.Connect();

				using(SshCommand cmd = client.CreateCommand("dd bs=1024"))
				{
					ManualResetEvent wait = new ManualResetEvent(false);

					IAsyncResult r = cmd.BeginExecute();
					new Action(() =>
					{
						byte[] dummyInput = new byte[1024];
						for(int i = 0; i < 1024; i++)
						{
							cmd.InputStream.Write(dummyInput, 0, dummyInput.Length);
						}

						cmd.InputStream.Close();
						wait.Set();
					}).BeginInvoke(null, null);

					int total = 0;
					int count;
					byte[] tmp = new byte[4096];
					while((count = cmd.OutputStream.Read(tmp, 0, tmp.Length)) > 0)
					{
						total += count;
					}

					wait.WaitOne();

					cmd.EndExecute(r);
					
					Assert.IsTrue(total == 1024 * 1024);
				}

                client.Disconnect();
                #endregion
            }
        }
    }
}
