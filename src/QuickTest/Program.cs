using System;
using System.Threading;

namespace QuickTest
{
	class Program
	{
		static void Main(string[] args)
		{
			var a = new Thread(Spinner);
			a.IsBackground = true;
			a.Start();

			var b = new Thread(Spinner);
			b.IsBackground = true;
			b.Start();

			while (true)
			{
				Thread.Sleep(1000);
			}
		}

		static void Spinner()
		{
			using (var inp = Console.OpenStandardInput())
			{
				while (true)
				{
					var x = inp.ReadByte();
					if (x == 3 || x == -1) // ctrl-c or stream closed
					{
						Console.WriteLine("BYE");
						Thread.Sleep(500);
						Environment.Exit(0);
					}
					else
					{
						Console.Write((char)x);
					}
				}
			}
		}
	}
}
