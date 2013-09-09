using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using RunProcess;

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

		ProcessHost _childProcess;
		ProcessHost _dummyProcess;

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
					_childProcess = Call(_target, string.Format(_startArgs, _dummyProcess.ProcessId()));
				}
				else
				{
					_childProcess = Call(_target, _startArgs);
				}

				var t = new Thread(() => MonitorChild(_childProcess));
				t.Start();
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
				using (var proc = Call(_target, _stopArgs))
					proc.WaitForExit(TimeSpan.FromMinutes(5));
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
				using (var proc = Call(_target, _pauseArgs))
					proc.WaitForExit(TimeSpan.FromMinutes(5));
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
				using (var proc = Call(_target, _continueArgs))
					proc.WaitForExit(TimeSpan.FromMinutes(5));
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

		void MonitorChild(ProcessHost child)
		{
			while (!_stopping && child.IsAlive())
			{
				Thread.Sleep(250);
			}
			if (_stopping) return;

			WriteChildFailure("error code: " + _childProcess.ExitCode());
			KillDummy();
			Environment.Exit(_childProcess.ExitCode());
		}

		void KillDummy()
		{
			if (!IsOk(_dummyProcess)) return;
			_dummyProcess.Kill();
			_dummyProcess.Dispose();
			_dummyProcess = null;
		}

		void WaitForExit_ForceKillAfter90Seconds()
		{
			if (!IsOk(_childProcess)) return;
			if (!_childProcess.WaitForExit(TimeSpan.FromSeconds(90)))
			{
				WriteChildFailure("Process did not close gracefully, will be killed");
				_childProcess.Kill();
			}
			_childProcess.Dispose();
			_childProcess = null;
		}

		static ProcessHost Call(string exePath, string args)
		{
			var proc = new ProcessHost(exePath, Path.GetDirectoryName(exePath) ?? "C:\\");
			proc.Start(args);
			return proc;
		}

		static bool IsOk(ProcessHost dummyProcess)
		{
			return dummyProcess != null && dummyProcess.IsAlive();
		}
	}
}