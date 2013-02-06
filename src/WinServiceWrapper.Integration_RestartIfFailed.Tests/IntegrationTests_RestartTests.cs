using System.Diagnostics;
using System.IO;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using NUnit.Framework;

namespace WinServiceWrapper.Integration.Tests
{
	[TestFixture]
    public class IntegrationTests_RestartTests
    {
		string _fileName;
		Process _process;

		[TestFixtureSetUp]
		public void run_service_lifetime ()
		{
			_fileName = "dummyout.txt";
			if (File.Exists(_fileName)) File.Delete(_fileName);
			Call("WinServiceWrapper.exe", "install start");
			var service = new ServiceController("MyAppsServiceName");
			service.WaitForStatus(ServiceControllerStatus.Running);

			KillHostedProcesses();
			Thread.Sleep(1000);
			service.WaitForStatus(ServiceControllerStatus.Running);
		}

		[TestFixtureTearDown]
		public void cleanup ()
		{
			Call("WinServiceWrapper.exe", "stop uninstall");
			if (File.Exists(_fileName)) File.Delete(_fileName);
		}

		[Test]
		public void should_have_started_target_twice ()
		{
			Assert.That(File.ReadAllLines(_fileName).Count(l => l.Contains("start, args")),
				Is.EqualTo(2));
		}
		
		[Test]
		public void should_have_a_current_running_process ()
		{
			var all = Process.GetProcessesByName("DummyApp");

			Assert.That(all.Count(),
				Is.EqualTo(1));
		}
		

		void KillHostedProcesses()
		{
			Call("DummyApp.exe", "stop");
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
