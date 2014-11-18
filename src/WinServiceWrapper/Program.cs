using System;
using System.Configuration;
using System.Diagnostics;
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
			var settings = ConfigurationManager.AppSettings;
			var name = settings["Name"];
			var target = settings["TargetExecutable"];
            var workingDir = settings["InitialWorkingDirectory"];

			if (string.IsNullOrWhiteSpace(name))
				throw new Exception("You must name your service in the App.Config file, key = \"Name\"");
			if (string.IsNullOrWhiteSpace(target))
				throw new Exception("You must provide a target executable to wrap, key= \"TargetExecutable\"");

			var safeName = MakeSafe(name);
			var description = settings["Description"];
			var startArgs = settings["StartCommand"];
			var stopArgs = settings["StopCommand"];
			var pauseArgs = settings["PauseCommand"];
			var continueArgs = settings["ContinueCommand"];
            var forceKill = (settings["KillTargetOnStop"]??"").ToLowerInvariant() == "true";

			var stdOutLog = settings["StdOutLog"];
			var stdErrLog = settings["StdErrLog"];
			
			// Dummy version of ourself -- just sit and wait
			if (args.FirstIs("waitForPid"))
			{
				var ppid = int.Parse(args[1]);
				Process.GetProcessById(ppid).WaitForExit();
				return;
			}

			// hack around TopShelf:
			if (args.FirstIs("pause"))
			{
				TryPauseService(safeName);
				return;
			}
			if (args.FirstIs("continue"))
			{
				TryContinueService(safeName);
				return;
			}

			HostFactory.Run(x =>
			{
				x.Service<WrapperService>(s =>
				{
                    s.ConstructUsing(hostSettings => new WrapperService(name, target, workingDir, startArgs, pauseArgs, continueArgs, stopArgs, stdOutLog, stdErrLog));

					s.WhenStarted(tc => tc.Start());
					s.WhenStopped(tc => tc.Stop(forceKill));
					s.WhenPaused(tc => tc.Pause());
					s.WhenContinued(tc => tc.Continue());

				});
				
				x.RunAsLocalService();
				
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
