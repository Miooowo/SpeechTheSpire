using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.RestSite;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.RestSite;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Nodes.Vfx.Utilities;
using MegaCrit.Sts2.Core.TestSupport;

namespace SpeechTheSpire.Patches;

/// <summary>
/// 休息点气泡台词来自 PCK 内 <c>localization/*/rest_site_speech.json</c> 表 <c>rest_site_speech</c>（与 card_speech 相同机制）。
/// 键：<c>SILENT</c>；<c>{DEFAULT|IRONCLAD|NECROBINDER}_{HEAL|SMITH|…}</c> 与可选 <c>_ENG</c>（中文界面下故障机器人用英文源做十六进制）。
/// 无内置兜底：表或键缺失则不显示气泡。
/// </summary>
[HarmonyPatch(typeof(NRestSiteRoom), "OnAfterPlayerSelectedRestSiteOption")]
public static class RestSiteSpeechBubblePatch
{
	private const string RestSiteSpeechTable = "rest_site_speech";

	private static readonly HashSet<string> SpokenOptionIds = new(StringComparer.OrdinalIgnoreCase)
	{
		"HEAL", "SMITH", "HATCH", "COOK", "DIG", "LIFT"
	};

	private const float RestSiteBubbleScale = 1.55f;
	private const double RestSiteBubbleMinSeconds = 5.25;
	private const double RestSiteBubbleMaxSeconds = 14.0;
	private const double RestSiteBubbleSecondsPerChar = 0.28;

	[HarmonyPostfix]
	public static void Postfix(NRestSiteRoom __instance, RestSiteOption option, bool success, ulong playerId)
	{
		if (TestMode.IsOn || !success)
			return;

		string oid = option.OptionId;
		if (!SpokenOptionIds.Contains(oid))
			return;

		NRestSiteCharacter? character = __instance.Characters.FirstOrDefault(c => c.Player.NetId == playerId);
		if (character == null)
			return;

		string characterId = character.Player.Character.Id.Entry;
		string bubbleText = FormatRestSiteLine(characterId, oid);
		if (string.IsNullOrEmpty(bubbleText))
			return;

		int slotIndex = __instance.Characters.IndexOf(character);
		if (slotIndex < 0)
			return;

		bool useRightSide = slotIndex == 0 || slotIndex == 3;
		DialogueSide side = useRightSide ? DialogueSide.Right : DialogueSide.Left;

		if (!TryGetBubbleSpawnGlobalPosition(character, slotIndex, out Vector2 globalPos))
			return;

		double displaySeconds = GetRestSiteBubbleDisplaySeconds(bubbleText);
		NSpeechBubbleVfx? bubble = NSpeechBubbleVfx.Create(bubbleText, side, globalPos, displaySeconds);
		if (bubble == null)
			return;

		character.AddChildSafely(bubble);
		bubble.GlobalPosition = globalPos;
		ApplyRestSiteBubbleScale(bubble, character);
	}

	private static string FormatRestSiteLine(string characterId, string optionId)
	{
		string id = characterId?.ToLowerInvariant() ?? "";

		if (id == "silent")
		{
			if (TryGetSpeechRow("SILENT", out string silentLine, out _))
				return silentLine;
			return "……";
		}

		string prefix = id switch
		{
			"ironclad" => "IRONCLAD",
			"necrobinder" => "NECROBINDER",
			_ => "DEFAULT"
		};

		string rowKey = $"{prefix}_{optionId}";
		if (!TryGetSpeechRow(rowKey, out string locLine, out string engLine))
			return "";

		if (id == "defect")
			return SpeechBubbleHex.CardNameToHexAscii(engLine);

		bool isZh = LocManager.Instance?.Language == "zhs";
		string display = locLine;

		if (id == "regent")
			return display + (isZh ? "！" : "!");

		return display;
	}

	/// <summary>读取当前语言行的正文与 _ENG（供故障机器人十六进制）；无 _ENG 时用正文代替。</summary>
	private static bool TryGetSpeechRow(string rowKey, out string locLine, out string engLine)
	{
		locLine = "";
		engLine = "";
		try
		{
			LocTable? table = LocManager.Instance?.GetTable(RestSiteSpeechTable);
			if (table == null || !table.HasEntry(rowKey))
				return false;

			locLine = table.GetRawText(rowKey);
			string engKey = rowKey + "_ENG";
			engLine = table.HasEntry(engKey) ? table.GetRawText(engKey) : locLine;
			return true;
		}
		catch
		{
			return false;
		}
	}

	private static void ApplyRestSiteBubbleScale(NSpeechBubbleVfx bubble, NRestSiteCharacter character)
	{
		SceneTreeTimer timer = character.GetTree().CreateTimer(0.0);
		timer.Connect(SceneTreeTimer.SignalName.Timeout, Callable.From(() =>
		{
			if (!GodotObject.IsInstanceValid(bubble) || !bubble.IsInsideTree())
				return;
			Vector2 sz = bubble.Size;
			if (sz.X > 0f && sz.Y > 0f)
				bubble.PivotOffset = sz * 0.5f;
			bubble.Scale = new Vector2(RestSiteBubbleScale, RestSiteBubbleScale);
		}));
	}

	private static double GetRestSiteBubbleDisplaySeconds(string text)
	{
		double byLen = string.IsNullOrEmpty(text) ? RestSiteBubbleMinSeconds : text.Length * RestSiteBubbleSecondsPerChar;
		return Math.Clamp(Math.Max(RestSiteBubbleMinSeconds, byLen), RestSiteBubbleMinSeconds, RestSiteBubbleMaxSeconds);
	}

	private static bool TryGetBubbleSpawnGlobalPosition(NRestSiteCharacter character, int slotIndex, out Vector2 globalPos)
	{
		string anchorPath = slotIndex < 2 ? "%ThoughtBubbleLeft" : "%ThoughtBubbleRight";
		Control? anchor = character.GetNodeOrNull<Control>(anchorPath);
		if (anchor != null && anchor.IsInsideTree())
		{
			globalPos = anchor.GlobalPosition;
			return true;
		}

		Control hit = character.Hitbox;
		Vector2 hitGlobal = hit.GlobalPosition;
		Vector2 size = hit.Size;
		bool useRightSide = slotIndex == 0 || slotIndex == 3;
		globalPos = useRightSide
			? hitGlobal + new Vector2(-size.X * 0.75f, -size.Y * 0.375f)
			: hitGlobal + new Vector2(size.X * 0.75f, -size.Y * 0.375f);
		return true;
	}
}
