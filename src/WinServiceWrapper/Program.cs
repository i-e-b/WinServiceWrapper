using System;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Topshelf;

namespace WinServiceWrapper
{
	public class Program
	{
		public static void Main(string[] args)
		{
			var name = ConfigurationManager.AppSettings["Name"];
			var target = ConfigurationManager.AppSettings["TargetExecutable"];

			if (string.IsNullOrWhiteSpace(name))
				throw new Exception("You must name your service in the App.Config file, key = \"Name\"");
			if (string.IsNullOrWhiteSpace(target))
				throw new Exception("You must provide a target executable to wrap, key= \"TargetExecutable\"");

			var safeName = MakeSafe(name);
			var description = ConfigurationManager.AppSettings["Description"];
			var startArgs = ConfigurationManager.AppSettings["StartCommand"];

			HostFactory.Run(x =>
			{
				x.Service<WrapperService>(s =>
				{
					s.ConstructUsing(hostSettings => new WrapperService(target, startArgs));

					s.WhenStarted(tc => tc.Start());
					s.WhenStopped(tc => tc.Stop());
				});
				x.RunAsLocalSystem();

				x.EnablePauseAndContinue();
				x.EnableShutdown();

				x.SetDisplayName(name);
				x.SetServiceName(safeName);
				x.SetDescription(description);
			});
		}

		static string MakeSafe(string name)
		{
			var sb = new StringBuilder();
			foreach (char c in name.Where(char.IsLetterOrDigit))
			{
				sb.Append(c);
			}
			return sb.ToString();
		}
	}

	public class WrapperService
	{
		readonly string _target;
		readonly string _startArgs;
		Process _host;

		public WrapperService(string target, string startArgs)
		{
			_target = target;
			_startArgs = startArgs;
		}

		public void Start()
		{
			_host = Process.Start(new ProcessStartInfo
			{
				FileName = Path.GetFullPath(_target),
				Arguments = _startArgs,
				CreateNoWindow = true,
				WorkingDirectory = Directory.GetCurrentDirectory()
			});
		}

		public void Stop()
		{
			if (!_host.HasExited) _host.Kill();
		}
	}
}
