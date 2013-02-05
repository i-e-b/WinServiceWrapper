using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security;
using System.Text;

namespace WinServiceWrapper
{
	public class Script_Execution
	{

		StringBuilder Output { get; set; }
		StringBuilder Error { get; set; }

		///<summary>
		/// Execute the Specified Script, with the supplied arguments
		///</summary>
		///<param name="ScriptPath">Fully Qualified Script Path</param>
		/// <param name="args">Array of Arguments to pass into the script</param>
		/// <returns>Output from the script execution</returns>
		public static string ExecuteScript(string ScriptPath, string args)
		{
			//Instantiating the class to prevent any crossover with the Output and Error async properties between different calls
			//(Since they were static, the string's persisted between different calls).
			var thisclass = new Script_Execution();
			return thisclass.DoExecuteScript(ScriptPath, args);
		}

		///<summary>
		/// Execute the Specified Script, with the supplied arguments
		/// </summary>
		/// <param name="ScriptPath">Fully Qualified Script Path</param>
		/// <param name="args">Array of Arguments to pass into the script</param>
		/// <returns>Output from the script execution</returns>
		string DoExecuteScript(string ScriptPath, string args)
		{
			var lst = new List<string>(args.Split(','));
			String Args = BuildArgsString(lst);

			if (ScriptPath.Trim() != String.Empty)
			{
				ProcessStartInfo psi = CreatePSI();

				//If the file is a vbscript file, then set it up to execute the Cscript.exe with the correct arguments.
				if (ScriptPath.Trim().ToLower().EndsWith("vbs"))
				{
					psi.FileName = Environment.GetEnvironmentVariable("WINDIR") + @"\System32\cscript.exe";
					psi.Arguments = string.Format("{0} {1} {2}", @"/nologo", "\"" + ScriptPath + "\"", Args);
				}
				else
				{
					//Just Execute the file directly.
					psi.FileName = ScriptPath;
					psi.Arguments = Args;
				}

				return Exec(psi);
			}
			//No Script Path Specified, fail out immediately.
			return "No Script Path Specified";
		}

		///<summary>/// Create the Process Object and set default Params///</summary>///<returns>Process Object</returns>
		ProcessStartInfo CreatePSI()
		{
			var psi = new ProcessStartInfo();
			psi.UseShellExecute = false;
			psi.RedirectStandardOutput = true;
			psi.RedirectStandardError = true;
			psi.ErrorDialog = true;
			psi.WindowStyle = ProcessWindowStyle.Hidden;
			psi.CreateNoWindow = true;

			//Set the working Directory to Windows. Otherwise it will throw accessdenied errors.
			psi.WorkingDirectory = Environment.GetEnvironmentVariable("WINDIR") ?? @"C:\Windows";

			//Build up the Password.
			var Secpw = new SecureString();
			foreach (Char Chr in @"Password123")
			{
				Secpw.AppendChar(Chr);
			}

			psi.LoadUserProfile = false;
			psi.UserName = @"Test.User";
			psi.Password = Secpw;
			psi.Domain = "TESTDOMAIN";


			return psi;
		}

		///<summary>/// Executes the Process and returns the output///</summary>///<param name="psi">Process Object</param>///<returns>Output from the Process</returns>
		string Exec(ProcessStartInfo psi)
		{
			string output = string.Empty;

			Output = new StringBuilder();
			Error = new StringBuilder();

			try
			{
				//Run the process and get the output.
				using (Process proc = Process.Start(psi))
				{
					proc.OutputDataReceived += ReceiveStandardOutput;
					proc.ErrorDataReceived += ReceiveStandardError;

					proc.BeginOutputReadLine();
					proc.BeginErrorReadLine();

					proc.WaitForExit(120000);
				}

				//Concatenate the Output Information.
				if (Output != null)
				{
					output = Output.ToString();
				}

				if (Error.ToString() != String.Empty)
				{
					output += "\n***SCRIPT ERROR INFO***\n" + Error;
				}
			}
			catch (Exception ex)
			{
				output = "***PROCESS ERROR INFO***\n";
				output += "Message: " + ex.Message + "\n";
				output += "InnerException: " + ex.InnerException + "\n";
				output += "StackTrace: " + ex.StackTrace + "\n";
				output += "TargetSite: " + ex.TargetSite + "\n";
				output += "Data:\n";

				foreach (string key in ex.Data.Keys)
				{
					output += key + ": " + ex.Data[key];
				}
			}

			return output;
		}

		/// <summary>
		/// Converts the List of Arguments into a string
		/// </summary>
		/// <param name="Args">List of Arguments</param>
		/// <returns>String of Arguments</returns>
		string BuildArgsString(IEnumerable<string> Args)
		{
			var ScriptArgs = new StringBuilder();
			foreach (String arg in Args)
			{
				ScriptArgs.Append(arg.Trim() + " ");
			}

			return ScriptArgs.ToString().Trim();
		}

		#region Output Handlers

		void ReceiveStandardOutput(object sendingProcess, DataReceivedEventArgs outLine)
		{
			if (String.IsNullOrEmpty(outLine.Data)) return;
			if (Output == null)
			{
				Output = new StringBuilder();
			}

			Output.AppendLine(outLine.Data);
		}

		void ReceiveStandardError(object sendingProcess, DataReceivedEventArgs outLine)
		{
			if (String.IsNullOrEmpty(outLine.Data)) return;
			if (Error == null)
			{
				Error = new StringBuilder();
			}

			Error.AppendLine(outLine.Data);
		}

		#endregion
	}
}