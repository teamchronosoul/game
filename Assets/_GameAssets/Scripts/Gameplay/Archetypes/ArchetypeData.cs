using UnityEngine;
using VN;

[CreateAssetMenu(fileName = "Archetype", menuName = "VN/Archetype")]
public class ArchetypeData : ScriptableObject
{
    [Header("ID")]
    public string archetypeId;     // Logics / Diplomats / Defenders / Seekers
    public string displayName;     // Логики и т.д.
    public string colorHex;

    [Header("UI")]
    public Sprite mentorSprite;
    public Sprite mentorIdSprite;
    public Sprite mirrorSprite;

    [Header("Animated Main Menu Mentor Optional")]
    [Tooltip("ID персонажа из VNCharacterDatabase, который должен стоять в главном меню после назначения этого наставника. Если пусто, VNMainMenuUGUI использует fallback или обычный mentorSprite.")]
    public string mentorCharacterId;

    [Tooltip("Если включено, для главного меню используется mainMenuMentorPose. Если выключено, используется fallback pose из VNMainMenuUGUI.")]
    public bool overrideMainMenuMentorPose;
    public VNPose mainMenuMentorPose = VNPose.Default;

    [Tooltip("Если включено, для главного меню используется mainMenuMentorEmotion. Если выключено, используется fallback emotion из VNMainMenuUGUI.")]
    public bool overrideMainMenuMentorEmotion;
    public VNEmotion mainMenuMentorEmotion = VNEmotion.Neutral;

    [Header("Mentor Intro Phrases")]
    [Tooltip("Какой встроенный набор фраз использовать, если Mentor Intro Phrases пуст. Auto пытается определить наставника по mentorCharacterId/archetypeId/displayName.")]
    public MentorIntroPhrasePreset mentorIntroPhrasePreset = MentorIntroPhrasePreset.Auto;

    [Tooltip("Если включено, пустой список Mentor Intro Phrases автоматически заполняется в редакторе встроенным набором по выбранному/определенному наставнику.")]
    public bool autoFillMentorIntroPhrasesInEditor = true;

    [Tooltip("Если список фраз пуст на runtime, меню все равно возьмет встроенный набор по preset. Выключи, если нужен полностью пустой набор.")]
    public bool useBuiltInMentorIntroPhrasesWhenEmpty = true;

    [Tooltip("Индивидуальные фразы наставника для главного меню. Первая фраза может показаться автоматически при входе, остальные идут по тапу на наставника и потом циклично повторяются.")]
    [TextArea(2, 5)]
    public string[] mentorIntroPhrases;

    [Header("MBTI Narratives (16 типов)")]
    public MbtiNarrative[] narratives;

    public string GetNarrative(string mbtiType)
    {
        foreach (var n in narratives)
        {
            if (n.type == mbtiType)
                return n.text;
        }

        return "Ты уникален.";
    }

    public string[] GetMentorIntroPhrases()
    {
        if (HasCustomMentorIntroPhrases())
            return BuildCleanMentorIntroPhrases(mentorIntroPhrases);

        if (!useBuiltInMentorIntroPhrasesWhenEmpty)
            return System.Array.Empty<string>();

        return GetBuiltInMentorIntroPhrases(ResolveMentorIntroPhrasePreset());
    }

    public MentorIntroPhrasePreset ResolveMentorIntroPhrasePreset()
    {
        if (mentorIntroPhrasePreset != MentorIntroPhrasePreset.Auto)
            return mentorIntroPhrasePreset;

        if (ContainsAny(mentorCharacterId, "shinrai", "шинрай") ||
            ContainsAny(archetypeId, "logics", "logic", "логик") ||
            ContainsAny(displayName, "logics", "logic", "логик"))
            return MentorIntroPhrasePreset.Shinrai;

        if (ContainsAny(mentorCharacterId, "kaitora", "кайтора") ||
            ContainsAny(archetypeId, "defenders", "defender", "защит") ||
            ContainsAny(displayName, "defenders", "defender", "защит"))
            return MentorIntroPhrasePreset.Kaitora;

        if (ContainsAny(mentorCharacterId, "kensui", "кенсуи") ||
            ContainsAny(archetypeId, "diplomats", "diplomat", "дипломат") ||
            ContainsAny(displayName, "diplomats", "diplomat", "дипломат"))
            return MentorIntroPhrasePreset.Kensui;

        if (ContainsAny(mentorCharacterId, "hinato", "хинато") ||
            ContainsAny(archetypeId, "seekers", "seeker", "искател") ||
            ContainsAny(displayName, "seekers", "seeker", "искател"))
            return MentorIntroPhrasePreset.Hinato;

        return MentorIntroPhrasePreset.Custom;
    }

    [ContextMenu("Fill Mentor Intro Phrases From Preset")]
    public void FillMentorIntroPhrasesFromPreset()
    {
        var phrases = GetBuiltInMentorIntroPhrases(ResolveMentorIntroPhrasePreset());
        mentorIntroPhrases = phrases == null || phrases.Length == 0
            ? System.Array.Empty<string>()
            : (string[])phrases.Clone();
    }

    private void OnValidate()
    {
        if (!autoFillMentorIntroPhrasesInEditor || HasCustomMentorIntroPhrases())
            return;

        var preset = ResolveMentorIntroPhrasePreset();
        if (preset == MentorIntroPhrasePreset.Auto || preset == MentorIntroPhrasePreset.Custom)
            return;

        FillMentorIntroPhrasesFromPreset();
    }

    private bool HasCustomMentorIntroPhrases()
    {
        if (mentorIntroPhrases == null || mentorIntroPhrases.Length == 0)
            return false;

        for (var i = 0; i < mentorIntroPhrases.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(mentorIntroPhrases[i]))
                return true;
        }

        return false;
    }

    private static string[] BuildCleanMentorIntroPhrases(string[] source)
    {
        if (source == null || source.Length == 0)
            return System.Array.Empty<string>();

        var count = 0;
        for (var i = 0; i < source.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(source[i]))
                count++;
        }

        if (count == 0)
            return System.Array.Empty<string>();

        var result = new string[count];
        var write = 0;
        for (var i = 0; i < source.Length; i++)
        {
            var phrase = source[i];
            if (string.IsNullOrWhiteSpace(phrase))
                continue;

            result[write++] = phrase.Trim();
        }

        return result;
    }

    private static string[] GetBuiltInMentorIntroPhrases(MentorIntroPhrasePreset preset)
    {
        switch (preset)
        {
            case MentorIntroPhrasePreset.Shinrai:
                return ShinraiMentorIntroPhrases;
            case MentorIntroPhrasePreset.Kaitora:
                return KaitoraMentorIntroPhrases;
            case MentorIntroPhrasePreset.Kensui:
                return KensuiMentorIntroPhrases;
            case MentorIntroPhrasePreset.Hinato:
                return HinatoMentorIntroPhrases;
            default:
                return System.Array.Empty<string>();
        }
    }

    private static bool ContainsAny(string source, params string[] values)
    {
        if (string.IsNullOrWhiteSpace(source) || values == null)
            return false;

        for (var i = 0; i < values.Length; i++)
        {
            var value = values[i];
            if (!string.IsNullOrWhiteSpace(value) &&
                source.IndexOf(value, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static readonly string[] ShinraiMentorIntroPhrases =
    {
        "You have returned. Good. Things feel more settled this way.",
        "You don’t need to explain yourself here. I see you.",
        "Rest if you need to. I shall keep everything else in order.",
        "You are back. I find that... rather reassuring.",
        "You may set your thoughts aside for a while. I shall enjoy the silence with you."
    };

    private static readonly string[] KaitoraMentorIntroPhrases =
    {
        "You’re back at the faculty. Now leave the rest to me.",
        "Stand beside me. No need to carry everything by yourself.",
        "You've come back. I've... been waiting for you.",
        "Welcome home. Take a rest. I’ll keep watch.",
        "You're safe here. I promise."
    };

    private static readonly string[] KensuiMentorIntroPhrases =
    {
        "There is no need in trying to be perfect. You already are.",
        "You do not need to explain the weight you carry. You are seen.",
        "You are home now. So tell me the secret your heart has been carrying.",
        "Let your spirit run free. This place was made to feel kind to you.",
        "I know the world can be overwhelming. Would you take a deep breath with me?"
    };

    private static readonly string[] HinatoMentorIntroPhrases =
    {
        "Look who’s here! Things get way more fun when you're around.",
        "Hey! You’re here now, so I’m calling this a good day.",
        "You’re home now, you know what that means? Less thinking, more chilling.",
        "Did I say I had some business to chat about? Yes. Was it just an excuse to see you? Maybe!",
        "There's that little spark in your eyes. I kinda missed it!"
    };
}

public enum MentorIntroPhrasePreset
{
    Auto = 0,
    Custom = 1,
    Shinrai = 2,
    Kaitora = 3,
    Kensui = 4,
    Hinato = 5
}

[System.Serializable]
public class MbtiNarrative
{
    public string type; // INTJ
    [TextArea(2, 5)]
    public string text;
}
