using System;
using System.Collections.Generic;
using System.Text;
using System.IO.Pipes;
using System.IO;

namespace GetPlatformTest
{
	class Program
	{
		static void Main(string[] args)
		{
			if (args.Length < 3)
				return;
			var pipe = new NamedPipeClientStream(args[2].Substring(9, args[2].Length - 9));
			pipe.Connect();
			var reader = new StreamReader(pipe);
			while(true)
			{
				if(reader.ReadLine().StartsWith("c:getPlatforms", StringComparison.InvariantCulture))
				{
					WriteNumber(pipe, -3085);
					WriteNumber(pipe, 0);
					WriteNumber(pipe, 0);
					WriteNumber(pipe, 1);
					WriteNumber(pipe, 0);
					WriteNumber(pipe, 2);
					WriteNumber(pipe, 8);
					WriteNumber(pipe, 1);
					WriteNumber(pipe, 1);
					WriteNumber(pipe, 2);
					WriteNumber(pipe, 10);
					WriteNumber(pipe, 0);
					WriteNumber(pipe, 0);
				}
			}
			
		}

		static void WriteNumber(Stream pipe, int number)
		{
			var bytes = Encoding.ASCII.GetBytes(number.ToString());
			pipe.Write(bytes, 0, bytes.Length);
			pipe.WriteByte(10);
		}
	}
}
