using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OfflineShaderCompiler
{
	/// <summary>
	/// An abstract binding class to make string conversions cleaner
	/// </summary>
	public abstract class Binding
	{
		/// <summary>
		/// The command used to trigger this conversion
		/// </summary>
		public abstract string Command { get; }

		/// <summary>
		/// Parses the given line
		/// </summary>
		/// <param name="line">The full line. I'll probably remove this</param>
		/// <param name="tokens">The list of tokens</param>
		/// <param name="bindings">The list of bindings (which you'll probably add to in this function)</param>
		public abstract void Parse(string line, IList<string> tokens, IList<Binding> bindings);

		/// <summary>
		/// Used to convert the binding to the text found in compiled shaders
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return "";
		}
	}

	/// <summary>
	/// An input to the shader. Binds some internal variable to a semantic
	/// </summary>
	/// <remarks>
	/// input: {Variable} {Semantic} {Unknown}
	/// </remarks>
	public class Input : Binding
	{
		/*
		input: 0 1 0

		input: 1 3 0

		input: 5 7 0

		input: 3 5 0
		 * 
		 * 
		Bind "vertex" Vertex  ( 1, 3 )
		Bind "normal" Normal
		Bind "texcoord" TexCoord0  ( 3, 5 )
		Bind "tangent" TexCoord2
		
		 */

		// I don't know if these two are correct.
		// If the variable and semantic are switched around, these are both wrong

		// Enumerates the variable names
		static string[] varName = new string[]
		{
			"vertex",
			"normal",
			null,
			"texcoord",
			null,
			null,
			"tangent",
		};

		// Enumerates the semantic names
		static string[] semantics = new string[]
		{
			null,
			"Vertex",
			null,
			"Normal",
			null,
			"TexCoord0",
			"TexCoord1",
			"TexCoord2",
		};

		string variable, semantic;
		int unused;

		/// <inheritdoc />
		public override string Command
		{
			get { return "input:"; }
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return "Bind \"" + variable + "\" " + semantic + "\n";
		}

		/// <summary>
		/// The variable name
		/// </summary>
		public string Variable
		{
			get { return variable; }
		}

		/// <summary>
		/// The semantic name
		/// </summary>
		public string Semantic
		{
			get { return semantic; }
		}

		/// <inheritdoc />
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
			newBinding.unused = unused;
			bindings.Add(newBinding);
		}
	}

	/// <summary>
	/// Sets up a constant buffer
	/// </summary>
	public class ConstBuffer : Binding
	{
		string name;
		int value;
		int unused;

		/// <inheritdoc />
		public override string Command
		{
			get { return "cb:"; }
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return "ConstBuffer \"" + name + "\" " + value + "\n";
		}

		/// <summary>
		/// The buffer name
		/// </summary>
		public string Name
		{
			get { return name; }
		}

		/// <summary>
		/// Some sort of value or ID
		/// </summary>
		public int Value
		{
			get { return value; }
		}

		/// <inheritdoc />
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

	/// <summary>
	/// Binds a buffer to a different name, I guess
	/// </summary>
	public class SetBuffer : Binding
	{
		string name;
		int id;

		/// <summary>
		/// The buffer name
		/// </summary>
		public string Name
		{
			get { return name; }
		}

		/// <summary>
		/// Some sort of ID
		/// </summary>
		public int ID
		{
			get { return id; }
		}

		/// <inheritdoc />
		public override string Command
		{
			get { return "bufferbind:"; }
		}

		/// <inheritdoc />
		public override string ToString()
		{
			return "SetBuffer " + id + " [" + name + "]\n";
		}

		/// <inheritdoc />
		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 3)
				throw new ExternalCompilerException("Buffer bind has too few parameters");
			var newKW = new SetBuffer();
			newKW.name = tokens[1];
			if (!Int32.TryParse(tokens[2], out newKW.id))
				throw new ExternalCompilerException("Buffer bind ID is not an integer");
			bindings.Add(newKW);
		}
	}

	/// <summary>
	/// Sets up a constant/global variable
	/// </summary>
	/// <remarks>
	/// Rows and columns could be switched round, idk
	/// </remarks>
	public class Const : Binding
	{
		string name;
		int id;
		bool integer;
		int rows;
		int cols;
		int unknown;

		/// <inheritdoc />
		public override string Command
		{
			get { return "const:"; }
		}

		/// <summary>
		/// The variable name
		/// </summary>
		public string Name
		{
			get { return name; }
		}

		/// <summary>
		/// Some sort of ID
		/// </summary>
		public int ID
		{
			get { return id; }
		}

		/// <summary>
		/// Whether this is an integer or a float
		/// </summary>
		public bool Integer
		{
			get { return integer; }
		}

		/// <summary>
		/// The number of rows in the matrix
		/// </summary>
		public int Rows
		{
			get { return rows; }
		}

		/// <summary>
		/// The number of columns in the matrix
		/// </summary>
		public int Columns
		{
			get { return cols; }
		}

		// Used to find the type name for any configuration
		static string[, ,] typeNames = new string[2, 4, 4]
		{
			{
				{ "Float ", "Vector ", "Vector ", "Vector " },
				{ null, null, null, null },
				{ null, null, null, null },
				{ null, null, null, "Matrix " },
			},
			{
				{ "ScalarInt ", null, null, null },
				{ null, null, null, null },
				{ null, null, null, null },
				{ null, null, null, null },
			}
		};

		// Used to make string conversion a little faster
		static string[] concatArray = new string[7];

		/// <inheritdoc />
		public override string ToString()
		{
			if(rows < 1 || cols < 1 || rows > 4 || cols > 4)
				return "// Invalid rows/cols for const\n";
			concatArray[0] = typeNames[integer ? 0 : 1, rows - 1, cols - 1];
			if(concatArray[0] == null)
				return "// I don't know the type name for " + name + "\n";
			concatArray[1] = id.ToString();
			concatArray[2] = " [";
			concatArray[3] = name;
			concatArray[4] = "] ";
			concatArray[5] = (rows == 1 && (cols == 2 || cols == 3)) ? cols.ToString() : "";
			concatArray[6] = "\n";
			return String.Concat(concatArray);
		}

		/// <inheritdoc />
		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 7)
				throw new ExternalCompilerException("Const has too few parameters");

			var newBinding = new Const();
			newBinding.name = tokens[1];
			try
			{
				newBinding.id = Int32.Parse(tokens[2]);
				newBinding.integer = (Int32.Parse(tokens[3]) != 0);
				newBinding.rows = Int32.Parse(tokens[4]);
				newBinding.cols = Int32.Parse(tokens[5]);
				newBinding.unknown = Int32.Parse(tokens[6]);
			}
			catch (FormatException)
			{
				throw new ExternalCompilerException("Invalid Const token");
			}
			bindings.Add(newBinding);
		}
	}

	/// <summary>
	/// Binds a constant buffer to another name. Dunno how this is different to BufferBind
	/// </summary>
	public class BindCB : Binding
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
			var newBinding = new BindCB();
			newBinding.name = tokens[1];
			if (!Int32.TryParse(tokens[2], out newBinding.value))
				throw new ExternalCompilerException("Const buffer bind value is invalid");
			bindings.Add(newBinding);
		}
	}

	/// <summary>
	/// Binds a texture to another name
	/// </summary>
	public class SetTexture : Binding
	{
		string name;
		int id;
		int value;
		int dimensions;

		/// <inheritdoc />
		public override string Command
		{
			get { return "texbind:"; }
		}

		/// <summary>
		/// The texture name
		/// </summary>
		public string Name
		{
			get { return name; }
		}

		/// <summary>
		/// The number of dimensions
		/// </summary>
		public int Dimensions
		{
			get { return dimensions; }
		}

		/// <summary>
		/// Some sort of value/ID/size
		/// </summary>
		public int Value
		{
			get { return value; }
		}

		/// <summary>
		/// Some sort of ID
		/// </summary>
		public int ID
		{
			get { return id; }
		}

		// Enumerates the names given to different texture dimensions
		static string[] dimensionName = new string[]
		{
			"1D ",
			"2D ",
			"3D ",
			"CUBE "
		};

		/// <inheritdoc />
		public override string ToString()
		{
			if (dimensions < 1)
				return "";
			if (dimensions > 4)
				return "// Too many dimensions\n";
			return "SetTexture " + id + " [" + name + "] " + dimensionName[dimensions - 1] + value + "\n";
		}

		/// <inheritdoc />
		public override void Parse(string line, IList<string> tokens, IList<Binding> bindings)
		{
			if (tokens.Count < 5)
				throw new ExternalCompilerException("TexBind has too few parameters");
			var newBinding = new SetTexture();
			newBinding.name = tokens[1];
			try
			{
				newBinding.id = Int32.Parse(tokens[2]);
				newBinding.value = Int32.Parse(tokens[3]);
				newBinding.dimensions = Int32.Parse(tokens[4]);
			} catch(FormatException)
			{
				throw new ExternalCompilerException("TexBind parameter is invalid");
			}
			if(newBinding.dimensions > 0)
				bindings.Add(newBinding);
		}
	}

	/// <summary>
	/// Gives statistics about the compiled snippet
	/// </summary>
	public class Stats : Binding
	{
		int math, texture, branch;

		/// <inheritdoc />
		public override string Command
		{
			get { return "stats:"; }
		}

		/// <summary>
		/// The number of math operations
		/// </summary>
		public int Math
		{
			get { return math; }
		}

		/// <summary>
		/// The number of texture operations
		/// </summary>
		public int Texture
		{
			get { return texture; }
		}

		/// <summary>
		/// The number of (dynamic) branch operations
		/// </summary>
		public int Branch
		{
			get { return branch; }
		}

		/// <inheritdoc />
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
				ret += texture + " textures";
			}
			if(branch != 0)
			{
				if (math + texture != 0)
					ret += ", ";
				ret += branch + " branches";
			}
			return ret + "\n";
		}

		/// <inheritdoc />
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
