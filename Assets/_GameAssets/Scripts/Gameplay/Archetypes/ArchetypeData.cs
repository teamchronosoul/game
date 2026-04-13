using UnityEngine;

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
}

[System.Serializable]
public class MbtiNarrative
{
    public string type; // INTJ
    [TextArea(2, 5)]
    public string text;
}