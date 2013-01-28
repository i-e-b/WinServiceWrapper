using System.IO;
using System.Threading;

namespace DummyApp
{
	public class Program
	{
		static string _fileName;

		static void Main(string[] args)
		{
			_fileName = "dummyout.txt";
			File.AppendAllText(_fileName, string.Join(", ", args));
			while (true)
			{
				Thread.Sleep(1000);
			}
		}
	}
}
