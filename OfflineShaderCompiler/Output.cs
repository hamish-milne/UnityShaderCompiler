using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityShaderCompiler
{
	public struct Configuration
	{
		public Function Function;
		public string[] Keywords;
		public Configuration(Function function, string[] keywords)
		{
			Function = function;
			Keywords = keywords;
		}
	}

	public class Snip
	{
		int programID, platforms, unknown1, target, unknown2, hash1, hash2, hash3;
		string text;
		IList<Configuration> configurations;

		public int ProgramID
		{
			get { return programID; }
		}

		public int Platforms
		{
			get { return platforms; }
		}

		public int Target
		{
			get { return target; }
		}

		public string Text
		{
			get { return text; }
		}

		public IList<Configuration> Configurations
		{
			get { return configurations; }
		}

		public Snip(
			int[] intParams,
			string text,
			IList<Configuration> configurations)
		{
			this.programID = intParams[0];
			this.platforms = intParams[1];
			this.unknown1 = intParams[2];
			this.target = intParams[3];
			this.unknown2 = intParams[4];
			this.hash1 = intParams[5];
			this.hash2 = intParams[6];
			this.hash3 = intParams[7];
			this.text = text;
			this.configurations = configurations;
		}
	}

	public struct Error
	{
		public ErrorLevel ErrorLevel;
		public Platform Platform;
		public int Line;
		public string File;
		public string Message;

		public Error(ErrorLevel errorLevel, Platform platform, int line, string file, string message)
		{
			ErrorLevel = errorLevel;
			Platform = platform;
			Line = line;
			File = file;
			Message = message;
		}
	}

	public class PreprocessResult
	{
		string shader;
		IList<Snip> snippets;
		IList<Error> errors;
		int unknownID;
		bool ok;

		public string Shader
		{
			get { return shader; }
		}

		public IList<Snip> Snippets
		{
			get { return snippets; }
		}

		public IList<Error> Errors
		{
			get { return errors; }
		}

		public bool OK
		{
			get { return ok; }
		}

		public PreprocessResult(string shader, IList<Snip> snippets, IList<Error> errors, int unknownID, bool ok)
		{
			this.shader = shader;
			this.snippets = snippets;
			this.errors = errors;
			this.unknownID = unknownID;
			this.ok = ok;
		}

	}

	public class PlatformReport
	{
		int a, b, c, d, e, f, g, h, i, j, k, l, m;

		public PlatformReport(int[] intParams)
		{
			if (intParams == null)
				throw new ArgumentNullException("intParams");
			if (intParams.Length < 13)
				throw new ArgumentException("Array is too short");
			a = intParams[0];
			b = intParams[1];
			c = intParams[2];
			d = intParams[3];
			e = intParams[4];
			f = intParams[5];
			g = intParams[6];
			h = intParams[7];
			i = intParams[8];
			j = intParams[9];
			k = intParams[10];
			l = intParams[11];
			m = intParams[12];
		}

		public override string ToString()
		{
			return a + "\n" + b + "\n" + c + "\n" + d + "\n" + e + "\n"
				+ f + "\n" + g + "\n" + h + "\n" + i + "\n" + j + "\n"
				+ k + "\n" + l + "\n" + m + "\n";
		}
	}

	public class CompileResult
	{
		bool ok;
		IList<Binding> bindings;
		string shader;

		public bool OK
		{
			get { return ok; }
		}

		public IList<Binding> Bindings
		{
			get { return bindings; }
		}

		public string Shader
		{
			get { return shader; }
		}

		public CompileResult(bool ok, IList<Binding> bindings, string shader)
		{
			this.ok = ok;
			this.bindings = bindings;
			this.shader = shader;
		}
	}

}
