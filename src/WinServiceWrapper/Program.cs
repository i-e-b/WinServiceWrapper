﻿using System;
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
			var pauseArgs = ConfigurationManager.AppSettings["PauseCommand"];
			var continueArgs = ConfigurationManager.AppSettings["ContinueCommand"];

			HostFactory.Run(x =>
			{
				x.Service<WrapperService>(s =>
				{
					s.ConstructUsing(hostSettings => new WrapperService(target, startArgs, pauseArgs, continueArgs));

					s.WhenStarted(tc => tc.Start());
					s.WhenStopped(tc => tc.Stop());
					s.WhenPaused(tc => tc.Pause());
					s.WhenContinued(tc => tc.Continue());
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
		readonly string _pauseArgs;
		Process _host;
		readonly string _continueArgs;

		public WrapperService(string target, string startArgs, string pauseArgs, string continueArgs)
		{
			_target = target;
			_startArgs = startArgs;
			_pauseArgs = pauseArgs;
			_continueArgs = continueArgs;
		}

		public void Start()
		{
			_host = Process.Start(new ProcessStartInfo
			{
				FileName = Path.GetFullPath(_target),
				Arguments = _startArgs,
				WorkingDirectory = Directory.GetCurrentDirectory(),
				UseShellExecute = false,
				RedirectStandardInput = true
			});
		}

		public void Stop()
		{
			_host.StandardInput.Write("\x3");
			_host.StandardInput.Flush();
			_host.StandardInput.Close();
			if (!_host.WaitForExit(10000))
			{
				_host.Kill();
			}
		}

		public void Pause()
		{
			Call(_pauseArgs);
		}


		void Call(string args)
		{
			var process = Process.Start(new ProcessStartInfo{
				Arguments = args,
				FileName = Path.GetFullPath(_target)
			});
			process.WaitForExit();
		}

		public void Continue()
		{
			Call(_continueArgs);
		}
	}
}
