using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.TestSupport;

namespace SpeechTheSpire.Patches;

/// <summary>
/// 当玩家打出非打击/防御的卡牌时，在角色头顶显示气泡。
/// 静默猎人始终「……」；故障机器人为英文转十六进制；储君按稀有度加感叹号；特殊语句从 card_speech 本地化表读取。
/// </summary>
[HarmonyPatch("MegaCrit.Sts2.Core.Combat.History.CombatHistory", "CardPlayStarted")]
public static class CardPlaySpeechBubblePatch
{
	private const string CardSpeechTable = "card_speech";

	private static readonly HashSet<string> BasicStrikeDefendIds = new(StringComparer.OrdinalIgnoreCase)
	{
		"STRIKE_IRONCLAD", "STRIKE_SILENT", "STRIKE_DEFECT", "STRIKE_REGENT",
		"DEFEND_IRONCLAD", "DEFEND_SILENT", "DEFEND_DEFECT", "DEFEND_REGENT"
	};

	[HarmonyPostfix]
	public static void Postfix(CombatState combatState, CardPlay cardPlay)
	{
		if (TestMode.IsOn)
			return;
		if (BasicStrikeDefendIds.Contains(cardPlay.Card.Id.Entry))
			return;
		if (!cardPlay.IsFirstInSeries)
			return;
		if (cardPlay.Card.Owner?.Creature == null)
			return;
		if (cardPlay.Card.Owner.Creature.IsDead)
			return;
		if (NCombatRoom.Instance?.CombatVfxContainer == null)
			return;

		string characterId = cardPlay.Card.Owner.Character.Id.Entry;
		string bubbleText = GetBubbleText(cardPlay.Card, characterId);
		NSpeechBubbleVfx? bubble = NSpeechBubbleVfx.Create(bubbleText, cardPlay.Card.Owner.Creature, 1.5);
		if (bubble != null)
			NCombatRoom.Instance.CombatVfxContainer.AddChildSafely(bubble);
	}

	private static string GetBubbleText(CardModel card, string characterId)
	{
		// 静默猎人始终保持沉默
		if (characterId?.ToLowerInvariant() == "silent")
			return "……";

		// 单卡特殊语句：从 card_speech 本地化表读取
		if (TryGetSpecialCardSpeech(card.Id.Entry, characterId, out string? specialText))
			return specialText;

		switch (characterId?.ToLowerInvariant())
		{
			case "defect":
				return SpeechBubbleHex.CardNameToHexAscii(SpeechBubbleHex.EnglishTitleCaseFromEntry(card.Id.Entry));
			case "regent":
				return card.TitleLocString.GetFormattedText() + GetExclamationsForRarity(card.Rarity);
			default:
				return card.TitleLocString.GetFormattedText();
		}
	}

	private static bool TryGetSpecialCardSpeech(string cardEntry, string characterId, out string? text)
	{
		text = null;
		try
		{
			LocTable? table = LocManager.Instance?.GetTable(CardSpeechTable);
			if (table == null || !table.HasEntry(cardEntry))
				return false;

			bool isDefect = characterId?.ToLowerInvariant() == "defect";
			if (isDefect)
			{
				string engKey = cardEntry + "_ENG";
				if (table.HasEntry(engKey))
				{
					text = SpeechBubbleHex.CardNameToHexAscii(table.GetRawText(engKey));
					return true;
				}
				return false;
			}
			text = table.GetRawText(cardEntry);
			return true;
		}
		catch
		{
			return false;
		}
	}

	/// <summary>
	/// 储君按卡牌稀有度返回尾标：诅咒用「？！/?!」，其余按稀有度数量感叹号；中文用全角、英文用半角。
	/// </summary>
	private static string GetExclamationsForRarity(CardRarity rarity)
	{
		bool isZh = LocManager.Instance?.Language == "zhs";
		char exclamation = isZh ? '！' : '!';
		if (rarity == CardRarity.Curse)
			return isZh ? "？！" : "?!";
		int count = rarity switch
		{
			CardRarity.Basic => 1,
			CardRarity.Common => 1,
			CardRarity.Uncommon => 2,
			CardRarity.Rare => 3,
			CardRarity.Ancient => 5,
			_ => 1
		};
		return new string(exclamation, count);
	}

}
