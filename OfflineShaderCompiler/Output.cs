using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OfflineShaderCompiler
{
	/// <summary>
	/// Represents a single shader configuration (platform independent)
	/// </summary>
	public struct Configuration
	{
		/// <summary>
		/// The function (Vertex or Fragment)
		/// </summary>
		public Function Function;

		/// <summary>
		/// The list of applicable keywords
		/// </summary>
		public string[] Keywords;

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="function"></param>
		/// <param name="keywords"></param>
		public Configuration(Function function, string[] keywords)
		{
			Function = function;
			Keywords = keywords;
		}
	}

	/// <summary>
	/// Holds data for a single snippet (of Cg/GLSL)
	/// </summary>
	public class Snip
	{
		int programID, platforms, unknown1, target, unknown2, hash1, hash2, hash3;
		string text;
		IList<Configuration> configurations;

		/// <summary>
		/// The program ID, used when putting compiled code back into the shader
		/// </summary>
		public int ProgramID
		{
			get { return programID; }
		}

		/// <summary>
		/// Indicates what platforms are compatible. Bitwise combination? Unsure.
		/// </summary>
		public int Platforms
		{
			get { return platforms; }
		}

		/// <summary>
		/// The specified target of this snippet. TODO: make an enum for this
		/// </summary>
		public int Target
		{
			get { return target; }
		}

		/// <summary>
		/// The source code
		/// </summary>
		public string Text
		{
			get { return text; }
		}

		/// <summary>
		/// The list of configurations
		/// </summary>
		public IList<Configuration> Configurations
		{
			get { return configurations; }
		}

		/// <summary>
		/// Creates a new instance from a list of integer parameters
		/// </summary>
		/// <param name="intParams">All the parameters</param>
		/// <param name="text">The source code</param>
		/// <param name="configurations">The list of configurations</param>
		public Snip(
			int[] intParams,
			string text,
			IList<Configuration> configurations)
		{
			this.programID = intParams[0];
			this.platforms = intParams[1];
			this.unknown1 = intParams[2]; // This is usually 3
			this.target = intParams[3];
			this.unknown2 = intParams[4]; // This is usually 0
			this.hash1 = intParams[5]; // These three change all the time
			this.hash2 = intParams[6]; // my guess is they're used to detect changes in the code
			this.hash3 = intParams[7];
			this.text = text;
			this.configurations = configurations;
		}
	}

	/// <summary>
	/// A compiler error
	/// </summary>
	public struct Error
	{
		/// <summary>
		/// The error level
		/// </summary>
		public ErrorLevel ErrorLevel;

		/// <summary>
		/// The platform
		/// </summary>
		public Platform Platform;

		/// <summary>
		/// The line number
		/// </summary>
		public int Line;

		/// <summary>
		/// The file name
		/// </summary>
		public string File;
		
		/// <summary>
		/// The error message
		/// </summary>
		public string Message;

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="errorLevel"></param>
		/// <param name="platform"></param>
		/// <param name="line"></param>
		/// <param name="file"></param>
		/// <param name="message"></param>
		public Error(ErrorLevel errorLevel, Platform platform, int line, string file, string message)
		{
			ErrorLevel = errorLevel;
			Platform = platform;
			Line = line;
			File = file;
			Message = message;
		}
	}

	/// <summary>
	/// The result from a preprocess command
	/// </summary>
	public class PreprocessResult
	{
		string shader;
		IList<Snip> snippets;
		IList<Error> errors;
		int unknownID;
		bool ok;

		/// <summary>
		/// The preprocessed ShaderLab code
		/// </summary>
		public string Shader
		{
			get { return shader; }
		}

		/// <summary>
		/// The list of snippets
		/// </summary>
		public IList<Snip> Snippets
		{
			get { return snippets; }
		}

		/// <summary>
		/// The list of errors, warnings and information
		/// </summary>
		public IList<Error> Errors
		{
			get { return errors; }
		}

		/// <summary>
		/// Whether compilation succeeded
		/// </summary>
		public bool OK
		{
			get { return ok; }
		}

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="shader"></param>
		/// <param name="snippets"></param>
		/// <param name="errors"></param>
		/// <param name="unknownID"></param>
		/// <param name="ok"></param>
		public PreprocessResult(string shader, IList<Snip> snippets, IList<Error> errors, int unknownID, bool ok)
		{
			this.shader = shader;
			this.snippets = snippets;
			this.errors = errors;
			this.unknownID = unknownID; // Always 0 or absent
			this.ok = ok;
		}

	}

	/// <summary>
	/// The information from GetPlatforms
	/// </summary>
	/// <remarks>
	/// I have *no idea* what any of these numbers mean.
	/// They're the same on every system I've tried.
	/// Spoofing them to different values has almost no effect on Unity
	/// </remarks>
	public class PlatformReport
	{
		int a, b, c, d, e, f, g, h, i, j, k, l, m;

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="intParams">The list of numbers</param>
		public PlatformReport(int[] intParams)
		{
			if (intParams == null)
				throw new ArgumentNullException("intParams");
			if (intParams.Length < 13)
				throw new ArgumentException("Array is too short");
			a = intParams[0]; // -3085 ; changing this will sometimes cause graphical glitches in the editor
			b = intParams[1]; // 0
			c = intParams[2]; // 0
			d = intParams[3]; // 1
			e = intParams[4]; // 0
			f = intParams[5]; // 2
			g = intParams[6]; // 8
			h = intParams[7]; // 1
			i = intParams[8]; // 1
			j = intParams[9]; // 2
			k = intParams[10]; // 10
			l = intParams[11]; // 0
			m = intParams[12]; // 0
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return a + "\n" + b + "\n" + c + "\n" + d + "\n" + e + "\n"
				+ f + "\n" + g + "\n" + h + "\n" + i + "\n" + j + "\n"
				+ k + "\n" + l + "\n" + m + "\n";
		}
	}

	/// <summary>
	/// The output from CompileSnippet
	/// </summary>
	public class CompileResult
	{
		bool ok;
		IList<Binding> bindings;
		string shader;

		/// <summary>
		/// Whether the compilation succeeded
		/// </summary>
		public bool OK
		{
			get { return ok; }
		}

		/// <summary>
		/// The list of bindings (and stats, since it's easier)
		/// </summary>
		public IList<Binding> Bindings
		{
			get { return bindings; }
		}

		/// <summary>
		/// The compiled code
		/// </summary>
		public string Shader
		{
			get { return shader; }
		}

		/// <summary>
		/// Creates a new instance
		/// </summary>
		/// <param name="ok"></param>
		/// <param name="bindings"></param>
		/// <param name="shader"></param>
		public CompileResult(bool ok, IList<Binding> bindings, string shader)
		{
			this.ok = ok;
			this.bindings = bindings;
			this.shader = shader;
		}
	}

}
