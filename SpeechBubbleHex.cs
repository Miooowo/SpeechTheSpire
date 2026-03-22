using System.Globalization;
using System.Text;

namespace SpeechTheSpire;

/// <summary>故障机器人台词：ASCII 转十六进制词（与出牌气泡一致）。</summary>
public static class SpeechBubbleHex
{
	public static string CardNameToHexAscii(string text)
	{
		if (string.IsNullOrEmpty(text))
			return string.Empty;
		byte[] bytes = Encoding.ASCII.GetBytes(text);
		var sb = new StringBuilder();
		for (int i = 0; i < bytes.Length; i += 4)
		{
			if (sb.Length > 0)
				sb.Append(' ');
			uint word = 0;
			for (int j = 0; j < 4 && i + j < bytes.Length; j++)
				word = (word << 8) | bytes[i + j];
			sb.Append("0x").Append(word.ToString("X8"));
		}
		return sb.ToString();
	}

	public static string EnglishTitleCaseFromEntry(string entry)
	{
		if (string.IsNullOrEmpty(entry))
			return entry;
		string withSpaces = entry.Replace('_', ' ');
		return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(withSpaces.ToLowerInvariant());
	}
}
