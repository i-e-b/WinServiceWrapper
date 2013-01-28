using System;
using System.IO;

namespace DummyApp
{
	public class Program
	{
		static string _fileName;

		static void Main(string[] args)
		{
			_fileName = "dummyout.txt";
			File.AppendAllText(_fileName, "\r\n"+string.Join(", ", args));

			if (args.Length < 1 || args[0] != "start") return;

			Console.CancelKeyPress += Console_CancelKeyPress;
			using (var inp = Console.OpenStandardInput())
			{
				while (true)
				{
					var x = inp.ReadByte();
					if (x == 3 || x == -1) // ctrl-c or stream closed
					{
						File.AppendAllText(_fileName, "\r\nI got a Ctrl-C");
						Environment.Exit(0);
					}
				}
			}
		}

		static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
		{
			e.Cancel = true;
			File.AppendAllText(_fileName, "\r\nI got a Ctrl-C");
			Environment.Exit(0);
		}
	}
}
