using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using Microsoft.Win32;

namespace UnityShaderCompiler
{
	public class ExternalCompilerException : Exception
	{
		public override string Message
		{
			get
			{
				return "The compiler's output was invalid: " + base.Message;
			}
		}

		public ExternalCompilerException(string message)
			: base(message)
		{
		}
	}

	public class CompilerService : IDisposable
	{
		public const string IDString = "%ID%";
		const int blockSize = 4096;

		Process process;
		NamedPipeServerStream pipe;
		string includePath;
		byte[] buffer = new byte[blockSize];
		Dictionary<string, Binding> allBindings
			= new Dictionary<string, Binding>();

		public void Dispose()
		{
			if (!IsDisposed)
			{
				WriteMessage("");
				pipe.Dispose();
				process.Dispose();
			}
		}

		public bool IsDisposed
		{
			get { return !pipe.IsConnected; }
		}

		~CompilerService()
		{
			Dispose();
		}

		public string IncludePath
		{
			get { return includePath; }
			set
			{
				if (value == null)
					value = "";
				includePath = value.Replace('\\', '/');
			}
		}

		static string defaultBasePath;
		static string DefaultBasePath(string compilerPath)
		{
			if(defaultBasePath == null)
				defaultBasePath = Path.GetDirectoryName(Path.GetDirectoryName(compilerPath));
			return defaultBasePath;
		}

		static string EscapeShellArg(string input)
		{
			return input.Replace('\\', '/').Replace("\"", "\\\"");
		}

		void AddBinding(Binding kw)
		{
			allBindings.Add(kw.Command, kw);
		}

		public CompilerService()
			: this("")
		{
		}

		public CompilerService(string logFile)
			: this(
			logFile,
			Path.GetDirectoryName(
			Registry.CurrentUser.OpenSubKey(@"Software\Unity Technologies\Unity Editor 3.x\Location")
			.GetValue(null, "").ToString())
			+ "/Data/Tools/UnityShaderCompiler.exe"
			)
		{
		}

		public CompilerService(string logFile, string compilerPath)
			: this(
			logFile,
			compilerPath,
			DefaultBasePath(compilerPath),
			DefaultBasePath(compilerPath) + "/CGIncludes"
			)
		{
			defaultBasePath = null;
		}

		public CompilerService(string logFile, string compilerPath, string basePath, string includePath)
			: this(
			logFile,
			compilerPath,
			basePath,
			includePath,
			"UnityShaderCompiler-" + IDString
			)
		{
		}

		public CompilerService(string logFile, string compilerPath, string basePath, string includePath, string pipeName)
		{
			AddBinding(new Input());
			AddBinding(new ConstBuffer());
			AddBinding(new Const());
			AddBinding(new ConstBufferBind());
			AddBinding(new BufferBind());
			AddBinding(new TexBind());
			AddBinding(new Stats());

			IncludePath = includePath;
			if(pipeName.Contains(IDString))
				pipeName = pipeName.Replace(IDString, GetHashCode().ToString());
			process = new Process();
			process.StartInfo.FileName = compilerPath.Replace('/', '\\');
			process.StartInfo.UseShellExecute = false;
			process.StartInfo.CreateNoWindow = true;
			process.StartInfo.Arguments = String.Format("\"{0}\"  \"{1}\" \"\\\\.\\pipe\\{2}\"",
				EscapeShellArg(basePath), EscapeShellArg(logFile), EscapeShellArg(pipeName));
			pipe = new NamedPipeServerStream(pipeName);
			process.Start();
			pipe.WaitForConnection();
		}

		static int[] platformParams = new int[13];

		public PlatformReport GetPlatforms()
		{
			WriteMessage("c:getPlatforms");

			try
			{
				for (int i = 0; i < 13; i++)
					platformParams[i] = Int32.Parse(ReadMessage());
			} catch(FormatException)
			{
				throw new ExternalCompilerException("Invalid platform number");
			}
			return new PlatformReport(platformParams);
		}

		void EnsureBufferCapacity(int capacity, bool copyExisting)
		{
			if (buffer.Length >= capacity)
				return;
			var newSize = ((capacity % blockSize) + 1) * blockSize;
			if (copyExisting)
				Array.Resize(ref buffer, newSize);
			else
				buffer = new byte[newSize];
		}

		unsafe void WriteMessage(string str)
		{
			if (str == null)
				str = "";
			EnsureBufferCapacity(str.Length + 2, false);
			int numBytes;
			fixed (byte* buf = buffer)
			fixed (char* chars = str)
				numBytes = Encoding.ASCII.GetBytes(chars, str.Length, buf, buffer.Length - 1);
			if(numBytes >= 0)
			{
				buffer[numBytes] = (byte)'\n';
				pipe.Write(buffer, 0, numBytes + 1);
			}
		}

		unsafe string ReadMessage()
		{
			int bytesRead = 0;
			int integer;
			while(true)
			{
				integer = pipe.ReadByte();
				if (integer < 0 || integer == (int)'\n')
					break;
				if (bytesRead >= buffer.Length)
					EnsureBufferCapacity(bytesRead + 1, true);
				buffer[bytesRead] = (byte)integer;
				bytesRead++;
			}
			if (bytesRead == 0)
				return "";
			return Encoding.ASCII.GetString(buffer, 0, bytesRead);
		}

		string EscapeInput(string source)
		{
			var sb = new StringBuilder();
			for(int i = 0; i < source.Length; i++)
			{
				var c = source[i];
				if(c == '\r' || c == '\n')
				{
					sb.Append("\\n");
					if (c == '\r' && (i + 1) < source.Length && source[i + 1] == '\n')
						i++;
				} else if(c == '\\')
				{
					sb.Append("\\\\");
				} else
				{
					sb.Append(c);
				}
			}
			return sb.ToString();
		}

		string UnescapeOutput(string source)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < source.Length; i++)
			{
				var c = source[i];
				if ((i + 1) >= source.Length)
					sb.Append(c);
				else if (c == '\\')
				{
					char newC = '\0';
					switch (source[++i])
					{
						case 'n':
							newC = '\n';
							break;
						case '\\':
							newC = '\\';
							break;
						case 'r':
							newC = '\r';
							break;
						case 't':
							newC = '\t';
							break;
					}
					if (newC != '\0')
						sb.Append(newC);
				}
				else
					sb.Append(c);
			}
			return sb.ToString();
		}

		static Dictionary<int, string> keywordCache = new Dictionary<int, string>();

		static string GetKeyword(string str)
		{
			var hash = str.GetHashCode();
			if (!keywordCache.TryGetValue(hash, out str))
				keywordCache.Add(hash, str);
			return str;
		}

		static char[] spaceSeparator = new char[] { ' ' };
		static List<string> foundKeywords = new List<string>();

		bool ProcessKeyword(string programID, IList<Configuration> configs)
		{
			var tokens = ReadMessage().Split(spaceSeparator);
			if (tokens.Length < 1)
				throw new ExternalCompilerException("Empty message");
			if (tokens[0] == "keywordsEnd:")
			{
				if(tokens.Length != 2)
					throw new ExternalCompilerException("Invalid 'keywordsEnd' message");
				if (tokens[1] != programID)
					throw new ExternalCompilerException("Invalid program ID; expected " + programID + ", got " + tokens[1]);
				return false;
			}
			if (tokens.Length < 4)
				throw new ExternalCompilerException("Invalid 'keywords' message");
			if (tokens[0] != "keywords:")
				throw new ExternalCompilerException("Unknown command `" + tokens[0] + "', expected `keywords:'");
			int function;
			if (!Int32.TryParse(tokens[1], out function))
				throw new ExternalCompilerException("Invalid keyword ID");
			if (tokens[2] != programID)
				throw new ExternalCompilerException("Invalid program ID; expected " + programID + ", got " + tokens[2]);
			foundKeywords.Clear();
			for (int i = 3; i < tokens.Length; i++)
				foundKeywords.Add(GetKeyword(tokens[i]));
			configs.Add(new Configuration((Function)function, foundKeywords.ToArray()));
			return true;
		}

		void ProcessErrors(string[] tokens, IList<Error> errors)
		{
			if (tokens.Length != 4)
				throw new ExternalCompilerException("Invalid 'err' message");
			int errorLevel, platform, lineNumber;
			try
			{
				errorLevel = Int32.Parse(tokens[1]);
				platform = Int32.Parse(tokens[2]);
				lineNumber = Int32.Parse(tokens[3]);
			}
			catch (FormatException)
			{
				throw new ExternalCompilerException("Error token is not a number");
			}
			var file = ReadMessage();
			var message = ReadMessage();
			errors.Add(new Error((ErrorLevel)errorLevel, (Platform)platform, lineNumber, file, message));	
		}

		public PreprocessResult Preprocess(string source, string location, int unknownParam = 0)
		{
			if (IsDisposed)
				throw new ObjectDisposedException("CompilerService");
			WriteMessage("c:preprocess");
			WriteMessage(EscapeInput(source));
			WriteMessage(location);
			WriteMessage(IncludePath);
			WriteMessage(unknownParam.ToString());

			var snips = new List<Snip>();
			var intParams = new int[9];
			var errors = new List<Error>();

			while(true)
			{
				var line = ReadMessage();
				var tokens = line.Split(spaceSeparator);
				if (tokens.Length < 1)
					continue;
				switch(tokens[0])
				{
					case "snip:":
						if (tokens.Length != 10)
							throw new ExternalCompilerException("Invalid 'snip' message");
						intParams.Initialize();
						try
						{
							for (int i = 1; i < tokens.Length; i++)
								intParams[i - 1] = Int32.Parse(tokens[i]);
						}
						catch (FormatException)
						{
							throw new ExternalCompilerException("Snip token is not a number");
						}
						var snipText = UnescapeOutput(ReadMessage());
						var configs = new List<Configuration>();
						var programIDString = intParams[0].ToString();
						while (ProcessKeyword(programIDString, configs)) ;
						snips.Add(new Snip(intParams, snipText, configs));
						break;
					case "err:":
						ProcessErrors(tokens, errors);
						break;
					case "shader:":
						if (tokens.Length < 2)
							throw new ExternalCompilerException("Invalid 'shader' message");
						int ok = 0;
						int unknownID = 0;
						try
						{
							ok = Int32.Parse(tokens[1]);
							if (tokens.Length > 2)
								unknownID = Int32.Parse(tokens[2]);
						} catch(FormatException)
						{
							throw new ExternalCompilerException("Shader token is not a number");
						}
						return new PreprocessResult(UnescapeOutput(ReadMessage()), snips, errors, unknownID, (ok != 0));
				}
			}
		}

		public CompileResult CompileSnippet(string source, string location, string[] keywords, Platform platform, Function function, int unknownParam = 0)
		{
			if (IsDisposed)
				throw new ObjectDisposedException("CompilerService");
			WriteMessage("c:compileSnippet");
			WriteMessage(EscapeInput(source));
			WriteMessage(location);
			WriteMessage(IncludePath);
			int keywordCount = keywords == null ? 0 : keywords.Length;
			WriteMessage(keywordCount.ToString());
			for (int i = 0; i < keywordCount; i++)
				WriteMessage(keywords[i]);
			WriteMessage(unknownParam.ToString());
			WriteMessage(((int)function).ToString());
			WriteMessage(((int)platform).ToString());
			
			var errors = new List<Error>();
			var bindings = new List<Binding>();

			while(true)
			{
				var line = ReadMessage();
				var tokens = line.Split(spaceSeparator);

				if (tokens.Length < 1)
					continue;
				Binding kw;
				allBindings.TryGetValue(tokens[0], out kw);
				if(kw != null)
				{
					kw.Parse(line, tokens, bindings);
					continue;
				}

				switch(tokens[0])
				{
					case "err:":
						ProcessErrors(tokens, errors);
						break;
					case "shader:":
						if (tokens.Length < 2)
							throw new ExternalCompilerException("Invalid 'shader' message");
						int ok;
						if(!Int32.TryParse(tokens[1], out ok))
							throw new ExternalCompilerException("Invalid 'OK' token");
						return new CompileResult((ok != 0), bindings, UnescapeOutput(ReadMessage()));
				}
			}
		}

	}
}
