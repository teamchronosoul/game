using UnityEngine;

[CreateAssetMenu(fileName = "ArchetypeDatabase", menuName = "VN/Archetype Database")]
public class ArchetypeDatabase : ScriptableObject
{
    public ArchetypeData[] archetypes;

    public ArchetypeData GetById(string id)
    {
        foreach (var a in archetypes)
        {
            if (a.archetypeId == id)
                return a;
        }

        Debug.LogWarning($"Archetype not found: {id}");
        return null;
    }
}