using System;
using System.Diagnostics;
using System.IO;

namespace WinServiceWrapper
{
	public class WrapperService
	{
		readonly string _displayName;
		readonly string _target;
		readonly string _startArgs;
		readonly string _pauseArgs;
		Process _childProcess;
		readonly string _continueArgs;
		readonly string _stopArgs;
		volatile bool _stopping;

		public WrapperService(string displayName, string target, string startArgs, string pauseArgs, string continueArgs, string stopArgs)
		{
			_displayName = displayName;
			_target = target;
			_startArgs = startArgs;
			_pauseArgs = pauseArgs;
			_continueArgs = continueArgs;
			_stopArgs = stopArgs;
		}

		public void Start()
		{
			_stopping = false;
			try
			{
				_childProcess = Process.Start(new ProcessStartInfo
				{
					FileName = Path.GetFullPath(_target),
					Arguments = _startArgs,
					WorkingDirectory = Directory.GetCurrentDirectory(),
					CreateNoWindow = true,
					UseShellExecute = false,
					RedirectStandardInput = true
				});
				_childProcess.EnableRaisingEvents = true;
				_childProcess.Exited += ChildProcessDied;
			}
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		public void Stop()
		{
			_stopping = true;
			try
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
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		public void Pause()
		{
			_stopping = false;
			try
			{
				Call(_pauseArgs);
			}
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		public void Continue()
		{
			_stopping = false;
			try
			{
				Call(_continueArgs);
			}
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		void WriteWrapperFailure(Exception exception)
		{
			var log = CheckSource("Windows Service Wrapper: "+_displayName);

			EventLog.WriteEntry(log, 
				"Wrapper failure: "
				+ exception.GetType().Name + ": " + exception.Message +"\r\n"
				+ exception.StackTrace,
				EventLogEntryType.Error);
		}
		void WriteChildFailure(string data)
		{
			var log = CheckSource("Windows Service Wrapper: " + _displayName);
			EventLog.WriteEntry(log, "Child process failure: " + data, EventLogEntryType.Error);
		}
		string CheckSource(string name)
		{
			if (EventLog.SourceExists(name)) return name;

			try
			{
				EventLog.CreateEventSource(name, "Application");
				return name;
			}
			catch (Exception)
			{
				return ".NET Runtime";
			}
		}

		void ChildProcessDied(object sender, EventArgs e)
		{
			if (_stopping) return;
			WriteChildFailure("error code: " + _childProcess.ExitCode);
			Environment.Exit(_childProcess.ExitCode);
		}

		void DirectStop()
		{
			_childProcess.StandardInput.Write("\x3");
			_childProcess.StandardInput.Flush();
			_childProcess.StandardInput.Close();
			WaitForExit_ForceKillAfterTenSeconds();
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