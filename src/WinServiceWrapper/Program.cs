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

			var stdOutLog = settings["StdOutLog"];
			var stdErrLog = settings["StdErrLog"];
			
			var childCreds = new UserCredentials{
				Domain = settings["RunAsDomain"],
				Password = settings["RunAsPassword"],
				UserName = settings["RunAsUser"]
			};

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
					s.ConstructUsing(hostSettings => new WrapperService(name, target, startArgs, pauseArgs, continueArgs, stopArgs, stdOutLog, stdErrLog, childCreds));

					s.WhenStarted(tc => tc.Start());
					s.WhenStopped(tc => tc.Stop());
					s.WhenPaused(tc => tc.Pause());
					s.WhenContinued(tc => tc.Continue());

				});
				if (childCreds.IsValid())
				{
					x.RunAs(childCreds.Domain + "\\" + childCreds.UserName, childCreds.Password);
				}
				else
				{
					x.RunAsLocalService();
				}

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

	public static class Ext
	{
		public static bool FirstIs(this string[] args, string target)
		{
			return string.Equals(args.FirstOrDefault(), target, StringComparison.InvariantCultureIgnoreCase);
		}
	}
}
