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
			var stopArgs = ConfigurationManager.AppSettings["StopCommand"];
			var pauseArgs = ConfigurationManager.AppSettings["PauseCommand"];
			var continueArgs = ConfigurationManager.AppSettings["ContinueCommand"];

			HostFactory.Run(x =>
			{
				x.Service<WrapperService>(s =>
				{
					s.ConstructUsing(hostSettings => new WrapperService(target, startArgs, pauseArgs, continueArgs, stopArgs));

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
		readonly string _pauseArgs;
		Process _childProcess;
		readonly string _continueArgs;
		readonly string _stopArgs;

		public WrapperService(string target, string startArgs, string pauseArgs, string continueArgs, string stopArgs)
		{
			_target = target;
			_startArgs = startArgs;
			_pauseArgs = pauseArgs;
			_continueArgs = continueArgs;
			_stopArgs = stopArgs;
		}

		public void Start()
		{
			_childProcess = Process.Start(new ProcessStartInfo
			{
				FileName = Path.GetFullPath(_target),
				Arguments = _startArgs,
				WorkingDirectory = Directory.GetCurrentDirectory(),
				UseShellExecute = false,
				RedirectStandardInput = true,
			});
			_childProcess.EnableRaisingEvents = true;
			_childProcess.Exited += ChildProcessDied;
		}

		void ChildProcessDied(object sender, EventArgs e)
		{
			Environment.Exit(_childProcess.ExitCode);
		}

		public void Stop()
		{
			if (string.IsNullOrWhiteSpace(_stopArgs))
			{
				DirectStop();
			}
			else
			{
				Call(_stopArgs);
				WaitForExit_ForceKillAfterTenSeconds();
			}
		}

		void DirectStop()
		{
			_childProcess.StandardInput.Write("\x3");
			_childProcess.StandardInput.Flush();
			_childProcess.StandardInput.Close();
			WaitForExit_ForceKillAfterTenSeconds();
		}

		public void Pause()
		{
			Call(_pauseArgs);
		}

		public void Continue()
		{
			Call(_continueArgs);
		}

		void WaitForExit_ForceKillAfterTenSeconds()
		{
			if (!_childProcess.WaitForExit(10000))
			{
				_childProcess.Kill();
			}
		}

		void Call(string args)
		{
			var process = Process.Start(new ProcessStartInfo{
				Arguments = args,
				FileName = Path.GetFullPath(_target)
			});
			process.WaitForExit();
		}
	}
}
