using System;
using System.Diagnostics;
using System.IO;
using System.Text;
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
		readonly string _stdOutLog;
		readonly string _stdErrLog;
		readonly UserCredentials _childUser;
		bool _shouldLogOut;
		bool _shouldLogErr;
		volatile bool _stopping;

		ProcessHost _childProcess;
		ProcessHost _dummyProcess;
		Thread _monitorThread;

		public WrapperService(string displayName, string target, string startArgs, string pauseArgs,
			string continueArgs, string stopArgs, string stdOutLog, string stdErrLog, UserCredentials childUser)
		{
			_displayName = displayName;
			_target = target;
			_startArgs = startArgs;
			_pauseArgs = pauseArgs;
			_continueArgs = continueArgs;
			_stopArgs = stopArgs;
			_stdOutLog = stdOutLog;
			_stdErrLog = stdErrLog;
			_childUser = childUser;

			PrepareLogging();
		}

		void PrepareLogging()
		{
			_shouldLogOut = MaybeCreateDirectory(_stdOutLog);
			_shouldLogErr = MaybeCreateDirectory(_stdErrLog);
		}

		static bool MaybeCreateDirectory(string path)
		{
			if (string.IsNullOrWhiteSpace(path)) return false;

			var logDir = Path.GetDirectoryName(Path.GetFullPath(path));
			if (logDir != null) Directory.CreateDirectory(logDir);
			return true;
		}

		public void Start()
		{
			_stopping = false;
			try
			{
				if (_startArgs.Contains("{0}"))
				{
					_dummyProcess = Call(Process.GetCurrentProcess().MainModule.FileName, "waitForPid " + Process.GetCurrentProcess().Id);
					_childProcess = CallAsChildUser(_target, string.Format(_startArgs, _dummyProcess.ProcessId()));
				}
				else
				{
					_childProcess = CallAsChildUser(_target, _startArgs);
				}

				if (_monitorThread == null || !_monitorThread.IsAlive)
				{
					_monitorThread = new Thread(() => MonitorChild(_childProcess)) { IsBackground = true };
					_monitorThread.Start();
				}

				if (_shouldLogOut || _shouldLogErr)
				{
					var tlogs = new Thread(() => WriteLogs(_childProcess)) {IsBackground = true};
					tlogs.Start();
				}
			}
			catch (Exception ex)
			{
				WriteWrapperFailure(ex);
			}
		}

		void WriteLogs(ProcessHost childProcess)
		{
			while (!_stopping && IsOk(childProcess))
			{
				var errTxt = childProcess.StdErr.ReadLine(Encoding.UTF8, TimeSpan.FromSeconds(1));
				var outTxt = childProcess.StdOut.ReadLine(Encoding.UTF8, TimeSpan.FromSeconds(1));

				if (string.IsNullOrEmpty(errTxt) && string.IsNullOrEmpty(outTxt))
				{
					Thread.Sleep(1000);
					continue;
				}

				if (_shouldLogOut) File.AppendAllText(_stdOutLog, outTxt);
				if (_shouldLogErr) File.AppendAllText(_stdErrLog, errTxt);
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
			while (!_stopping && IsOk(child))
			{
				Thread.Sleep(250);
			}
			if (_stopping) return;

			WriteChildFailure("error code: " + _childProcess.ExitCode());
			KillDummy();
			Environment.Exit(_childProcess.ExitCode()); // We die, so the service manager will start us up again.
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
			var fullExePath = Path.GetFullPath(exePath);
			var runningDirectory = Path.GetDirectoryName(fullExePath);

			var proc = new ProcessHost(fullExePath, runningDirectory);
			proc.Start(args);
			return proc;
		}

		ProcessHost CallAsChildUser(string exePath, string args)
		{
			var fullExePath = Path.GetFullPath(exePath);
			var runningDirectory = Path.GetDirectoryName(fullExePath);

			var proc = new ProcessHost(fullExePath, runningDirectory);
			if (_childUser.IsValid())
			{
				proc.StartAsAnotherUser(_childUser.Domain, _childUser.UserName, _childUser.Password,
					args);
			}
			else
			{
				proc.Start(args);
			}
			return proc;
		}

		static bool IsOk(ProcessHost proc)
		{
			return proc != null && proc.IsAlive();
		}
	}
}