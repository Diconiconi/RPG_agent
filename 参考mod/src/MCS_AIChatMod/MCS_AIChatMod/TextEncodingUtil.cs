using System;
using System.IO;
using System.Text;

namespace MCS_AIChatMod;

internal static class TextEncodingUtil
{
	private static readonly UTF8Encoding Utf8BomEncoding;

	private static readonly UTF8Encoding Utf8NoBomEncoding;

	private static readonly UTF8Encoding Utf8StrictEncoding;

	private static readonly Encoding Gb18030Encoding;

	public static Encoding Utf8Bom => Utf8BomEncoding;

	static TextEncodingUtil()
	{
		Utf8BomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
		Utf8NoBomEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
		Utf8StrictEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
		try
		{
			Gb18030Encoding = Encoding.GetEncoding("GB18030");
		}
		catch
		{
			try
			{
				Gb18030Encoding = Encoding.GetEncoding("GBK");
			}
			catch
			{
				Gb18030Encoding = Encoding.Default;
			}
		}
	}

	public static void WriteAllTextUtf8(string path, string content, bool withBom = true)
	{
		File.WriteAllText(path, content ?? string.Empty, withBom ? Utf8BomEncoding : Utf8NoBomEncoding);
	}

	public static void AppendAllTextUtf8(string path, string content)
	{
		if (!File.Exists(path))
		{
			WriteAllTextUtf8(path, content ?? string.Empty);
		}
		else
		{
			File.AppendAllText(path, content ?? string.Empty, Utf8NoBomEncoding);
		}
	}

	public static string ReadAllTextWithFallback(string path)
	{
		try
		{
			return File.ReadAllText(path, Utf8StrictEncoding);
		}
		catch (DecoderFallbackException)
		{
			try
			{
				return File.ReadAllText(path, Gb18030Encoding);
			}
			catch
			{
				return File.ReadAllText(path, Encoding.Default);
			}
		}
	}

	public static string[] ReadAllLinesWithFallback(string path)
	{
		string text = ReadAllTextWithFallback(path);
		return text.Replace("\r\n", "\n").Replace('\r', '\n').Split(new char[1] { '\n' }, StringSplitOptions.None);
	}
}
