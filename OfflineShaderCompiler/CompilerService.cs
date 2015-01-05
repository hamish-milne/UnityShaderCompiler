using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using Microsoft.Win32;

namespace OfflineShaderCompiler
{
	/// <summary>
	/// The exception thrown when the compiler process produces invalid output
	/// </summary>
	public class ExternalCompilerException : Exception
	{
		/// <summary>
		/// The message
		/// </summary>
		public override string Message
		{
			get
			{
				return "The compiler's output was invalid: " + base.Message;
			}
		}

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="message"></param>
		public ExternalCompilerException(string message)
			: base(message)
		{
		}
	}

	/// <summary>
	/// An interface to UnityShaderCompiler.exe
	/// </summary>
	public class CompilerService : IDisposable
	{
		/// <summary>
		/// This is replaced with a unique ID in the pipe name
		/// </summary>
		public const string IDString = "%ID%";

		/// <summary>
		/// The size by which to increase the command buffer each time
		/// </summary>
		const int blockSize = 4096;

		Process process;
		NamedPipeServerStream pipe;
		string includePath;
		byte[] buffer = new byte[blockSize];
		Dictionary<string, Binding> allBindings
			= new Dictionary<string, Binding>();

		/// <summary>
		/// Closes the service process and releases all resources
		/// </summary>
		public void Dispose()
		{
			if (!IsDisposed)
			{
				WriteMessage("");
				pipe.Dispose();
				process.Dispose();
			}
		}

		/// <summary>
		/// Whether the process has been closed or not
		/// </summary>
		public bool IsDisposed
		{
			get { return !pipe.IsConnected; }
		}

		~CompilerService()
		{
			Dispose();
		}

		/// <summary>
		/// Unity's include path, which is used internally by the compiler
		/// </summary>
		/// <remarks>
		/// Normally C:/Program Files (x86)/Unity/Editor/Data/CGIncludes
		/// </remarks>
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

		// Used to save a bit of code when inheriting constructors
		static string defaultBasePath;
		static string DefaultBasePath(string compilerPath)
		{
			if(defaultBasePath == null)
				defaultBasePath = Path.GetDirectoryName(Path.GetDirectoryName(compilerPath));
			return defaultBasePath;
		}

		// Simple escaping of shell arguments
		static string EscapeShellArg(string input)
		{
			return input.Replace('\\', '/').Replace("\"", "\\\"");
		}

		// Adds a binding option to the list
		void AddBinding(Binding kw)
		{
			allBindings.Add(kw.Command, kw);
		}

		/// <summary>
		/// Creates a new instance with default parameters and no logging
		/// </summary>
		public CompilerService()
			: this("")
		{
		}

		/// <summary>
		/// Creates a new instance with default parameters and a log file
		/// </summary>
		/// <param name="logFile">The log file to use, or "" for none</param>
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

		/// <summary>
		/// Creates a new instance with a specified compiler path.
		/// Use this when you don't want to access the registry
		/// </summary>
		/// <inheritdoc />
		/// <param name="compilerPath">The path to the compiler</param>
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

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <inheritdoc />
		/// <param name="includePath">The path to the standard CG includes</param>
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

		/// <summary>
		/// Creates a new instance with all parameters specified
		/// </summary>
		/// <inheritdoc />
		/// <param name="pipeName">The pipe to create</param>
		public CompilerService(string logFile, string compilerPath, string basePath, string includePath, string pipeName)
		{
			// Add all the binding commands that we know about.
			// These are outputted by the DirectX platforms usually
			AddBinding(new Input());
			AddBinding(new ConstBuffer());
			AddBinding(new Const());
			AddBinding(new BindCB());
			AddBinding(new SetBuffer());
			AddBinding(new SetTexture());
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

		// Static array used while parsing platform numbers
		static int[] platformParams = new int[13];

		/// <summary>
		/// Gets platform information from the compiler
		/// </summary>
		/// <returns>The platform information. In my experience, the values are identical across all systems</returns>
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

		// Expands the command buffer as necessary, in steps of 'blockSize'
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

		// Writes a message to the compiler
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

		// Reads a message from the compiler
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

		// Escapes compiler input, replacing newlines with "\\n" and escaping backslashes
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

		// Parses the escape sequences of compiler output strings
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

		// Stores keyword instances for improved memory and comparison performance
		static Dictionary<int, string> keywordCache = new Dictionary<int, string>();

		// Used to reduce the number of keyword string objects
		static string GetKeyword(string str)
		{
			var hash = str.GetHashCode();
			if (!keywordCache.TryGetValue(hash, out str))
				keywordCache.Add(hash, str);
			return str;
		}

		// Char array stored statically for slightly improved performance
		static char[] spaceSeparator = new char[] { ' ' };

		// Used to cache the keywords outputted in a particular snippet
		static List<string> foundKeywords = new List<string>();

		// Processes the 'keywords' and 'keywordsEnd' commands after a particular snippet
		// Right now it expects all keywords to be outputted right after a snippet, and for only that snippet
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

		// Processes an 'err' output
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

		/// <summary>
		/// Preprocesses a given ShaderLab file, outputting the errors,
		/// code snippets and configurations found
		/// </summary>
		/// <param name="source">The source code</param>
		/// <param name="location">The folder this file is found in, for error reporting</param>
		/// <returns>An object containing all the relevant output</returns>
		public PreprocessResult Preprocess(string source, string location)
		{
			if (IsDisposed)
				throw new ObjectDisposedException("CompilerService");

			// Send all the input data
			WriteMessage("c:preprocess");
			WriteMessage(EscapeInput(source));
			WriteMessage(location);
			WriteMessage(IncludePath);
			WriteMessage("0"); // No idea what this is for

			var snips = new List<Snip>();
			var intParams = new int[9];
			var errors = new List<Error>();

			// Unknown number of output lines
			while(true)
			{
				var line = ReadMessage();
				var tokens = line.Split(spaceSeparator);
				if (tokens.Length < 1)
					continue;
				switch(tokens[0])
				{
					case "snip:":
						// An individual snippet
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
						// An error associated with the ShaderLab code (not the Cg/GLSL code.)
						// Usually found at the end of the output, but could in theory be anywhere.
						ProcessErrors(tokens, errors);
						break;
					case "shader:":
						// The preprocessed ShaderLab code, which is further parsed by Unity.
						// This marks the end of the output
						if (tokens.Length < 2)
							throw new ExternalCompilerException("Invalid 'shader' message");
						int ok = 0;
						int unknownID = 0; // Always 0 or absent.
						try
						{
							ok = Int32.Parse(tokens[1]);
							if (tokens.Length > 2)
								unknownID = Int32.Parse(tokens[2]);
						} catch(FormatException)
						{
							throw new ExternalCompilerException("Shader token is not a number");
						}
						return new PreprocessResult(location, UnescapeOutput(ReadMessage()), snips, errors, unknownID, (ok != 0));
				}
			}
		}

		/// <summary>
		/// Compiles the given snippet
		/// </summary>
		/// <param name="snip">The snippet</param>
		/// <param name="configuration">The configuration index (as in <c>Snip.Configurations</c>)</param>
		/// <param name="platform">The platform to compile to</param>
		/// <param name="location">The file location, used in error reporting</param>
		/// <returns>A new object containing all the relevant output</returns>
		public CompileResult CompileSnippet(Snip snip, int configuration, Platform platform, string location)
		{
			if (snip == null)
				throw new ArgumentNullException("snip");
			var config = snip.Configurations[configuration];
			return CompileSnippet(snip.Text, location, config.Keywords, platform, config.Function);
		}

		public string Compile(string source, string location)
		{
			return Compile(Preprocess(source, location));
		}

		public string Compile(PreprocessResult preprocessResult)
		{
			return "";
		}

		/// <inheritdoc />
		/// <param name="keywords">The shader configuration keywords</param>
		/// <param name="unknownParam">Unknown. Usually 0; other values can cause no output</param>
		public CompileResult CompileSnippet(string snip, string location, string[] keywords, Platform platform, Function function, int unknownParam = 0)
		{
			if (IsDisposed)
				throw new ObjectDisposedException("CompilerService");

			// Send all the input data
			WriteMessage("c:compileSnippet");
			WriteMessage(EscapeInput(snip));
			WriteMessage(location);
			WriteMessage(IncludePath);
			// Send the keywords (one per line)
			int keywordCount = keywords == null ? 0 : keywords.Length;
			WriteMessage(keywordCount.ToString());
			for (int i = 0; i < keywordCount; i++)
				WriteMessage(keywords[i]);
			WriteMessage(unknownParam.ToString());
			WriteMessage(((int)function).ToString());
			WriteMessage(((int)platform).ToString());
			
			var errors = new List<Error>();
			var bindings = new List<Binding>();

			// Unknown number of output lines
			while(true)
			{
				var line = ReadMessage();
				var tokens = line.Split(spaceSeparator);

				if (tokens.Length < 1)
					continue;

				// Check if this is a binding and parse appropriately
				Binding kw;
				allBindings.TryGetValue(tokens[0], out kw);
				if(kw != null)
				{
					kw.Parse(line, tokens, bindings);
					continue;
				}

				// Otherwise check for 'err' and 'shader'
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
