using System.Diagnostics;
using System.IO;
using System.ServiceProcess;
using NUnit.Framework;

namespace WinServiceWrapper.Integration.Tests
{
	[TestFixture]
	public class IntegrationTests
	{
		string _fileName;
		Process _process;

		[TestFixtureSetUp]
		public void run_service_lifetime()
		{
			_fileName = "dummyout.txt";
			if (File.Exists(_fileName)) File.Delete(_fileName);
			Call("WinServiceWrapper.exe", "install start");

			Call("WinServiceWrapper.exe", "pause");
			Call("WinServiceWrapper.exe", "continue");

			Call("WinServiceWrapper.exe", "stop");
			var service = new ServiceController("MyAppsServiceName");
			service.WaitForStatus(ServiceControllerStatus.Stopped);
			Call("WinServiceWrapper.exe", "uninstall");
		}

		[TestFixtureTearDown]
		public void cleanup()
		{
			if (File.Exists(_fileName)) File.Delete(_fileName);
			if (File.Exists(@"C:\Temp\winserwrap_tests_out.txt")) File.Delete(@"C:\Temp\winserwrap_tests_out.txt");
			if (File.Exists(@"C:\Temp\winserwrap_tests_err.txt")) File.Delete(@"C:\Temp\winserwrap_tests_err.txt");
		}

		[Test]
		public void startup_passes_args_to_real_process()
		{
			Assert.That(File.ReadAllText(_fileName), Contains.Substring("start, args"));
		}

		[Test]
		public void shutdown_should_try_to_soft_terminate_real_process()
		{
			Assert.That(File.ReadAllText(_fileName), Contains.Substring("Parent died, so must I! Alas!"));
		}

		[Test]
		public void pause_calls_another_instance_with_arguments()
		{
			Assert.That(File.ReadAllText(_fileName), Contains.Substring("pause, args"));
		}

		[Test]
		public void continue_calls_another_instance_with_arguments()
		{
			Assert.That(File.ReadAllText(_fileName), Contains.Substring("continue, args"));
		}

		[Test]
		public void writes_logs ()
		{
			Assert.That(File.ReadAllText(@"C:\Temp\winserwrap_tests_out.txt"), Is.Not.Empty);
			Assert.That(File.ReadAllText(@"C:\Temp\winserwrap_tests_err.txt"), Is.Not.Empty);
		}


		void Call(string target, string args)
		{
			_process = Process.Start(new ProcessStartInfo
			{
				Arguments = args,
				FileName = Path.GetFullPath(target)
			});
			_process.WaitForExit();
		}
	}
}
