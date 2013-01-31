using System;
using System.Configuration;
using System.Linq;
using System.ServiceProcess;
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
			var stopArgs = ConfigurationManager.AppSettings["StopCommand"];
			var pauseArgs = ConfigurationManager.AppSettings["PauseCommand"];
			var continueArgs = ConfigurationManager.AppSettings["ContinueCommand"];

			// hack around TopShelf:
			if (string.Equals(args.FirstOrDefault(), "pause", StringComparison.InvariantCultureIgnoreCase))
			{
				TryPauseService(safeName);
				return;
			}
			if (string.Equals(args.FirstOrDefault(), "continue", StringComparison.InvariantCultureIgnoreCase))
			{
				TryContinueService(safeName);
				return;
			}

			HostFactory.Run(x =>
			{
				x.Service<WrapperService>(s =>
				{
					s.ConstructUsing(hostSettings => new WrapperService(name, target, startArgs, pauseArgs, continueArgs, stopArgs));

					s.WhenStarted(tc => tc.Start());
					s.WhenStopped(tc => tc.Stop());
					s.WhenPaused(tc => tc.Pause());
					s.WhenContinued(tc => tc.Continue());

				});
				x.RunAsLocalSystem();

				x.EnablePauseAndContinue();
				x.EnableServiceRecovery(sr => sr.RestartService(0));

				x.SetDisplayName(name);
				x.SetServiceName(safeName);
				x.SetDescription(description);
			});
		}

		static void TryContinueService(string serviceName)
		{
			using (var service = new ServiceController(serviceName))
			{
				service.Continue();
				service.WaitForStatus(ServiceControllerStatus.Running);
			}
		}

		static void TryPauseService(string serviceName)
		{
			using (var service = new ServiceController(serviceName))
			{
				service.Pause();
				service.WaitForStatus(ServiceControllerStatus.Paused);
			}
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
}
