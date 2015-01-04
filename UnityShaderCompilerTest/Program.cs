using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;

namespace UnityShaderCompiler
{

	class Program
	{
		/*[DllImport("kernel32.dll", SetLastError = true)]
		static extern bool PeekNamedPipe(SafeHandle handle,
			byte[] buffer, uint nBufferSize, ref uint bytesRead,
			ref uint bytesAvail, ref uint BytesLeftThisMessage);

		static bool SomethingToRead(SafeHandle streamHandle)
		{
			byte[] aPeekBuffer = new byte[1];
			uint aPeekedBytes = 0;
			uint aAvailBytes = 0;
			uint aLeftBytes = 0;

			bool aPeekedSuccess = PeekNamedPipe(
				streamHandle,
				aPeekBuffer, 1,
				ref aPeekedBytes, ref aAvailBytes, ref aLeftBytes);

			if (aPeekedSuccess && aPeekBuffer[0] != 0)
				return true;
			else
				return false;
		}*/

		static List<byte> bytes = new List<byte>();

		/*static byte[] buffer = new byte[8192];

		static void AddBuffer(int count)
		{
			for (int i = 0; i < count; i++)
				bytes.Add(buffer[i]);
		}*/

		static string output;

		static string StreamIntercept(PipeStream client, PipeStream server)
		{
			/*bytes.Clear();
			int count;
			do
			{
				count = client.Read(buffer, 0, 8192);
				AddBuffer(count);
			} while (SomethingToRead(client.SafePipeHandle));
			var array = bytes.ToArray();
			server.Write(array, 0, bytes.Count);
			return Encoding.ASCII.GetString(array);*/

			bytes.Clear();
			int integer;
			do
			{
				integer = client.ReadByte();
				if (integer >= 0)
					bytes.Add((byte)integer);
			} while (integer >= 0 && integer != 10);
			var array = bytes.ToArray();
			server.Write(array, 0, bytes.Count);
			return Encoding.ASCII.GetString(array);
		}

		static void GetNumMessages(string cmd, out int input, out int output, out string waitFor)
		{
			input = 0;
			output = 0;
			waitFor = null;
			switch(cmd)
			{
				case "c:getPlatforms\n":
					output = 13;
					break;
				case "c:preprocess\n":
					input = 4;
					waitFor = "shader:";
					output = 1;
					break;
				case "c:compileSnippet\n":
					input = 7;
					waitFor = "shader:";
					output = 1;
					break;
			}
		}

		static void Main(string[] args)
		{
			try
			{
				output = "UnityShader-" + Process.GetCurrentProcess().Id + ".txt";
				var pipeName = args[2].Substring(9, args[2].Length - 9);
				var client = new NamedPipeClientStream(pipeName);
				client.Connect();
				var process = new Process();
				process.StartInfo.FileName = @"C:\Program Files (x86)\Unity\Editor\Data\Tools\UnityShaderCompiler--.exe";
				args[2] += "-dummy";
				var argList = new object[(args.Length * 2) + 1];
				argList[0] = "\"";
				for(int i = 0; i < args.Length; i++)
				{
					argList[(i * 2) + 1] = args[i];
					argList[(i * 2) + 2] = (i == args.Length - 1) ? "\"" : "\" \"";
				}
				process.StartInfo.Arguments = String.Concat(argList);
				process.StartInfo.CreateNoWindow = true;
				process.StartInfo.UseShellExecute = false;
				var server = new NamedPipeServerStream(pipeName + "-dummy");

				process.Start();
				File.AppendAllText(output, "Waiting for connection...\r\n");
				server.WaitForConnection();
				File.AppendAllText(output, "Found connection!\r\n");

				while (true)
				{
					var msg = StreamIntercept(client, server);
					File.AppendAllText(output, msg + "\r\n");
					int input, outNum;
					string waitFor;
					GetNumMessages(msg, out input, out outNum, out waitFor);
					for (int i = 0; i < input; i++)
						File.AppendAllText(output, StreamIntercept(client, server) + "\r\n");
					if (waitFor != null) do
					{
						msg = StreamIntercept(server, client);
						File.AppendAllText(output, msg + "\r\n");
					} while (!msg.StartsWith(waitFor, StringComparison.InvariantCulture));

					for (int i = 0; i < outNum; i++)
						File.AppendAllText(output, StreamIntercept(server, client) + "\r\n");
				}
			}
			catch (Exception e)
			{
				File.AppendAllText(output, e.ToString() + "\r\n");
			}
		}
	}
}
