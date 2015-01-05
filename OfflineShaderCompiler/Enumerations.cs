using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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

	/// <summary>
	/// Whether the shader is for vertices or pixels
	/// </summary>
	public enum Function
	{
		Vertex = 0,
		Fragment = 1,
	}
}
