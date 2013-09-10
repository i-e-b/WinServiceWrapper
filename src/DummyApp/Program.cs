using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace DummyApp
{
	public class Program
	{
		static string _fileName;

		static void Main(string[] args)
		{
			File.AppendAllText("C:\\temp\\info.txt", Directory.GetCurrentDirectory());


			_fileName = "dummyout.txt";
			File.AppendAllText(_fileName, "\r\n" + string.Join(", ", args));

			Console.Error.Write("this is some sample data on standard error");

			Console.WriteLine("Dummy app has started");

			if (args.Length > 0 && args[0] == "stop") KillAllInstances();

			if (args.Length < 1 || args[0] != "start") return;

			if (args.Contains("withException"))
				throw new Exception("Example exception");

			if (args.Contains("-p"))
			{
				var L = args.IndexOf("-p");
				var ppid = int.Parse(args[L + 1]);
				Process.GetProcessById(ppid).WaitForExit(10000);
				File.AppendAllText(_fileName, "\r\nParent died, so must I! Alas!");
				return;
			}

			while (true) { Thread.Sleep(1000); }
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
	}

	public static class Ext
	{
		public static int IndexOf<T>(this IEnumerable<T> src, T target)
		{
			int i = 0;
			foreach (var x in src)
			{
				if (Equals(target, x)) return i;
				i++;
			}
			return -1;
		}
	}
}
