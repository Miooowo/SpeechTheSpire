using HarmonyLib;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Modding;

namespace SpeechTheSpire;

[ModInitializer("Init")]
public static class Entry
{
	public const string ModId = "SpeechTheSpire";
	private static Harmony? _harmony;

	public static void Init()
	{
		_harmony = new Harmony(ModId);
		_harmony.PatchAll();
		Log.Debug("SpeechTheSpire: mod initialized (card + rest site speech bubbles).");
	}
}
