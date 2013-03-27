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
		readonly string _continueArgs;
		readonly string _stopArgs;
		volatile bool _stopping;

		Process _childProcess;
		Process _dummyProcess;

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
				if (_startArgs.Contains("{0}"))
				{
					_dummyProcess = Call(Process.GetCurrentProcess().MainModule.FileName, "waitForPid " + Process.GetCurrentProcess().Id);
					_childProcess = Call(_target, string.Format(_startArgs, _dummyProcess.Id));
				}
				else
				{
					_childProcess = Call(_target, _startArgs);
				}


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
			if (_stopping) return;
			_stopping = true;
			if (string.IsNullOrWhiteSpace(_stopArgs)) StopWithCircularMonitoring();
			else StopWithArguments();
		}

		void StopWithCircularMonitoring()
		{
			KillDummy();
			WaitForExit_ForceKillAfter90Seconds();
		}

		void StopWithArguments()
		{
			try
			{
				Call(_target, _stopArgs).WaitForExit();
				WaitForExit_ForceKillAfter90Seconds();
			}
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		public void Pause()
		{
			if (string.IsNullOrEmpty(_pauseArgs)) return;
			_stopping = false;
			try
			{
				Call(_target, _pauseArgs).WaitForExit();
			}
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		public void Continue()
		{
			if (string.IsNullOrEmpty(_continueArgs)) return;
			_stopping = false;
			try
			{
				Call(_target, _continueArgs).WaitForExit();
			}
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		void WriteWrapperFailure(Exception exception)
		{
			var log = CheckSource("Windows Service Wrapper: " + _displayName);

			EventLog.WriteEntry(log,
				"Wrapper failure: "
				+ exception.GetType().Name + ": " + exception.Message + "\r\n"
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
			KillDummy();
			Environment.Exit(_childProcess.ExitCode);
		}

		void KillDummy()
		{
			if (IsOk(_dummyProcess))
			{
				_dummyProcess.Kill();
			}
		}

		void WaitForExit_ForceKillAfter90Seconds()
		{
			if (!IsOk(_childProcess)) return;
			if (!_childProcess.WaitForExit((int)TimeSpan.FromSeconds(90).TotalMilliseconds))
			{
				WriteChildFailure("Process did not close gracefully, will be killed");
				_childProcess.Kill();
			}
		}

		static Process Call(string exePath, string args)
		{
			return Process.Start(new ProcessStartInfo
				{
					FileName = Path.GetFullPath(exePath),
					Arguments = args,

					WorkingDirectory = Path.GetDirectoryName(exePath) ?? "C:\\",

					ErrorDialog = false,
					CreateNoWindow = true,
					WindowStyle = ProcessWindowStyle.Hidden,

					UseShellExecute = true
				});
		}

		static bool IsOk(Process dummyProcess)
		{
			if (dummyProcess == null) return false;
			dummyProcess.Refresh();
			return !dummyProcess.HasExited;
		}
	}
}