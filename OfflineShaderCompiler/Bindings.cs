using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace UnityShaderCompiler
{
	public abstract class Binding
	{
		public abstract string Command { get; }

		public abstract void Parse(string line, IList<string> tokens, IList<Binding> bindings);
	}

	public class Input : Binding
	{
		static string[] varName = new string[]
		{
			"vertex",
			null,
			null,
			"texcoord",
			null,
			null,
			//"tangent", "normal"
		};

		static string[] semantics = new string[]
		{
			null,
			"Vertex",
			null,
			null,
			null,
			"TexCoord0",
			//"TexCoord1",
			//"TexCoord2",
		};

		string variable, semantic;

		public override string Command
		{
			get { return "input:"; }
		}

		public override string ToString()
		{
			return "Bind \"" + variable + "\" " + semantic + "\n";
		}

		public string Variable
		{
			get { return variable; }
		}

		public string Semantic
		{
			get { return semantic; }
		}

		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 4)
				throw new ExternalCompilerException("Input has too few parameters");
			int varID, semanticID, unused;
			try
			{
				varID = Int32.Parse(tokens[1]);
				semanticID = Int32.Parse(tokens[2]);
				unused = Int32.Parse(tokens[3]);
			} catch(FormatException)
			{
				throw new ExternalCompilerException("Invalid 'input' token");
			}
			if (varID < 0 || varID >= varName.Length || semanticID < 0 || semanticID >= semantics.Length)
				return;
			var variable = varName[varID];
			var semantic = semantics[semanticID];
			if (unused != 0 || variable == null || semantic == null)
				return;
			var newBinding = new Input();
			newBinding.variable = variable;
			newBinding.semantic = semantic;
			bindings.Add(newBinding);
		}
	}

	public class ConstBuffer : Binding
	{
		string name;
		int value;
		int unused;

		public override string Command
		{
			get { return "cb:"; }
		}

		public override string ToString()
		{
			return "ConstBuffer \"" + name + "\" " + value + "\n";
		}

		public string Name
		{
			get { return name; }
		}

		public int Value
		{
			get { return value; }
		}

		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 3)
				throw new ExternalCompilerException("ConstBuffer has too few parameters");
			var newBinding = new ConstBuffer();
			newBinding.name = tokens[1];
			if (!Int32.TryParse(tokens[2], out newBinding.value) || !Int32.TryParse(tokens[3], out newBinding.unused))
				throw new ExternalCompilerException("Invalid ConstBuffer token");
			bindings.Add(newBinding);
		}
	}

	public class BufferBind : Binding
	{
		public override string Command
		{
			get { return "bufferbind:"; }
		}

		public override string ToString()
		{
			return "// Don't know how bufferbind works\n";
		}

		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			bindings.Add(this);
		}
	}

	public class Const : Binding
	{
		string name;
		int value;
		int unknown1;
		int rows;
		int cols;
		int unknown2;

		public override string Command
		{
			get { return "const:"; }
		}

		public string Name
		{
			get { return name; }
		}

		public int Value
		{
			get { return value; }
		}

		public int Rows
		{
			get { return rows; }
		}

		public int Columns
		{
			get { return cols; }
		}

		public override string ToString()
		{
			string type = (rows == 1) ? "Vector " : "Matrix ";
			return type + value + " [" + name + "]\n";
		}

		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 7)
				throw new ExternalCompilerException("Const has too few parameters");

			var newBinding = new Const();
			newBinding.name = tokens[1];
			try
			{
				newBinding.value = Int32.Parse(tokens[2]);
				newBinding.unknown1 = Int32.Parse(tokens[3]);
				newBinding.rows = Int32.Parse(tokens[4]);
				newBinding.cols = Int32.Parse(tokens[5]);
				newBinding.unknown2 = Int32.Parse(tokens[6]);
			}
			catch (FormatException)
			{
				throw new ExternalCompilerException("Invalid Const token");
			}
			bindings.Add(newBinding);
		}
	}

	public class ConstBufferBind : Binding
	{
		string name;
		int value;

		public override string Command
		{
			get { return "cbbind:"; }
		}

		public string Name
		{
			get { return name; }
		}

		public int Value
		{
			get { return value; }
		}

		public override string ToString()
		{
			return "BindCB  \"" + name + "\" " + value + "\n";
		}

		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 3)
				throw new ExternalCompilerException("Const buffer bind has too few parameters");
			var newBinding = new ConstBufferBind();
			newBinding.name = tokens[1];
			if (!Int32.TryParse(tokens[2], out newBinding.value))
				throw new ExternalCompilerException("Const buffer bind value is invalid");
			bindings.Add(newBinding);
		}
	}

	public class TexBind : Binding
	{
		string name;
		int unknown1;
		int unknown2;
		int dimensions;

		public override string Command
		{
			get { return "texbind:"; }
		}

		public string Name
		{
			get { return name; }
		}

		public int Dimensions
		{
			get { return dimensions; }
		}

		public override string ToString()
		{
			return "SetTexture " + unknown1 + " [" + name + "] " + dimensions + "D " + unknown2 + "\n";
		}

		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 5)
				throw new ExternalCompilerException("TexBind has too few parameters");
			var newBinding = new TexBind();
			newBinding.name = tokens[1];
			try
			{
				newBinding.unknown1 = Int32.Parse(tokens[2]);
				newBinding.unknown2 = Int32.Parse(tokens[3]);
				newBinding.dimensions = Int32.Parse(tokens[4]);
			} catch(FormatException)
			{
				throw new ExternalCompilerException("TexBind parameter is invalid");
			}
			bindings.Add(newBinding);
		}
	}

	public class Stats : Binding
	{
		int math, texture, branch;

		public override string Command
		{
			get { return "stats:"; }
		}

		public int Math
		{
			get { return math; }
		}

		public int Texture
		{
			get { return texture; }
		}

		public int Branch
		{
			get { return branch; }
		}

		public override string ToString()
		{
			if (math + texture + branch == 0)
				return "";
			var ret = "// Stats: ";
			if (math != 0)
				ret += math + " math";
			if(texture != 0)
			{
				if (math != 0)
					ret += ", ";
				ret += texture + " texture";
			}
			if(branch != 0)
			{
				if (math + texture != 0)
					ret += ", ";
				ret += branch + " branch";
			}
			return ret + "\n";
		}

		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 4)
				throw new ExternalCompilerException("Stats has too few parameters");
			var newBinding = new Stats();
			try
			{
				newBinding.math = Int32.Parse(tokens[1]);
				newBinding.texture = Int32.Parse(tokens[2]);
				newBinding.branch = Int32.Parse(tokens[3]);
			} catch(FormatException)
			{
				throw new ExternalCompilerException("Stats has invalid parameters");
			}
			bindings.Add(newBinding);
		}
	}
}
