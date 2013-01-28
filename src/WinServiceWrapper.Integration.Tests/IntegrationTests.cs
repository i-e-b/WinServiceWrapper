using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace WinServiceWrapper.Integration.Tests
{
	[TestFixture]
    public class IntegrationTests
    {
		string _fileName;
		Process _process;

		[SetUp]
		public void cleanfiles()
		{
			_fileName = "dummyout.txt";
			if (File.Exists(_fileName)) File.Delete(_fileName);
		}

		[TearDown]
		public void cleanall()
		{
			Call("WinServiceWrapper.exe", "stop uninstall");

			if (File.Exists(_fileName)) File.Delete(_fileName);
		}

		[Test]
		public void startup_passes_args_to_real_process ()
		{
			Call("WinServiceWrapper.exe", "install start");

			Assert.That(WaitFor(() => File.Exists(_fileName), TimeSpan.FromSeconds(5)), "File didn't show up");
			Assert.That(File.ReadAllText(_fileName), Is.EqualTo("start, args"));
		}

		bool WaitFor(Func<bool> func, TimeSpan timeout)
		{
			var start = DateTime.Now;
			while ((DateTime.Now - start) < timeout)
			{
				if (func()) return true;
				Thread.Sleep(100);
			}
			return false;
		}

		void Call(string target, string args)
		{
			_process = Process.Start(new ProcessStartInfo{
				Arguments = args,
				FileName = Path.GetFullPath(target)
			});
			_process.WaitForExit();
		}
    }
}
