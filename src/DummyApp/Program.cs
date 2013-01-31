using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DummyApp
{
	public class Program
	{
		static string _fileName;

		static void Main(string[] args)
		{
			_fileName = "dummyout.txt";
			File.AppendAllText(_fileName, "\r\n"+string.Join(", ", args));

			Console.WriteLine("Dummy app has started");

			if (args.Length > 0 && args[0] == "stop") KillAllInstances();

			if (args.Length < 1 || args[0] != "start") return;
			
			if (args.Length > 1 && args[1] == "withException")
				throw new Exception("Example exception");

			Console.CancelKeyPress += Console_CancelKeyPress;
			using (var inp = Console.OpenStandardInput())
			{
				while (true)
				{
					var x = inp.ReadByte();
					if (x != 3 && x != -1) continue;
					File.AppendAllText(_fileName, "\r\nI got a Ctrl-C");
					Environment.Exit(0);
				}
			}
		}

		static void KillAllInstances()
		{
			var me = Process.GetCurrentProcess().Id;
			File.AppendAllText(_fileName, "\r\nI am " + Process.GetCurrentProcess().ProcessName);
			var all = Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName);
			foreach (var process in all.Where(process => process.Id != me))
			{
				process.Kill();
			}
			Environment.Exit(0);
		}

		static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			e.Cancel = true;
			File.AppendAllText(_fileName, "\r\nI got a Ctrl-C");
			Environment.Exit(0);
		}
	}
}
