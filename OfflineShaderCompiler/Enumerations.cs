using System;
using System.Collections;
using System.Collections.Generic;

namespace OfflineShaderCompiler
{
	/// <summary>
	/// Compiler error level
	/// </summary>
	public enum ErrorLevel
	{
		Info = 0,
		Warning = 1,
		Error = 2
	}

	/// <summary>
	/// The various platforms supported by Unity
	/// </summary>
	public enum Platform
	{
		OpenGL = 0,
		D3D9 = 1,
		Xbox360 = 2,
		PS3 = 3,
		D3D11 = 4,
		GLES = 5,
		GLESDesktop = 6,
		Flash = 7,
		D3D11_9x = 8,
		GLES3 = 9,
		PSP2 = 10,
		PS4 = 11,
	}

	/// <summary>
	/// The platform IDs in bitwise form
	/// </summary>
	public enum PlatformBitwise
	{
		OpenGL = 1,
		D3D9 = 2,
		Xbox360 = 4,
		PS3 = 8,
		D3D11 = 16,
		GLES = 32,
		GLESDesktop = 64,
		Flash = 128,
		D3D11_9x = 256,
		GLES3 = 512,
		PSP2 = 1024,
		PS4 = 2048,
	}

	public static class Extensions
	{
		class PlatformEnumerable : IEnumerable<Platform>
		{
			PlatformBitwise platforms;

			public IEnumerator<Platform> GetEnumerator()
			{
				return new PlatformEnumerator(platforms);
			}

			IEnumerator IEnumerable.GetEnumerator()
			{
				return GetEnumerator();
			}

			public PlatformEnumerable(PlatformBitwise platforms)
			{
				this.platforms = platforms;
			}
		}

		class PlatformEnumerator : IEnumerator<Platform>
		{
			PlatformBitwise platforms;
			int current = -1;

			public void Reset()
			{
				current = -1;
			}

			public void MoveNext()
			{
				while (((int)platforms & (1 << ++current)) == 0) ;
			}

			public Platform Current
			{
				get { return (Platform)(1 << current); }
			}

			object IEnumerator.Current
			{
				get { return Current; }
			}

			public PlatformEnumerator(PlatformBitwise platforms)
			{
				this.platforms = platforms;
			}
		}

		public IEnumerable<Platform> Enumerate(this PlatformBitwise platforms)
		{
			return new PlatformEnumerable(platforms);
		}
	}

	/// <summary>
	/// Whether the shader is for vertices or pixels
	/// </summary>
	public enum Function
	{
		Vertex = 0,
		Fragment = 1,
	}

	/// <summary>
	/// The data type for a binding
	/// </summary>
	public enum DataType
	{
		Float = 0,
		Int = 1,
		Bool = 2, // Need to confirm this. Probably right
	}
}
