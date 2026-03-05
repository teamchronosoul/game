using System;
using System.Collections.Generic;
using NaughtyAttributes;
using UnityEditor;
using UnityEngine;

public class SoundsDictionary : MonoBehaviour
{
    public static SoundsDictionary instance;
    public List<SoundElement> sounds = new List<SoundElement>();
    private const string pathToAudioFolder = "_GameAssets/Sounds";

    private void Start()
    {
        instance = this;
    }
    [Button]
    public void LoadSounds()
    {
#if UNITY_EDITOR
        sounds.Clear();
        string folderFullPath = System.IO.Path.Combine("Assets", pathToAudioFolder);

        string[] guids = AssetDatabase.FindAssets("t:AudioClip", new[] {folderFullPath});
        
        foreach (var guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
            if (clip != null)
            {
                sounds.Add(new SoundElement
                {
                    key = clip.name,
                    clip = clip
                });
            }
        }
        Debug.Log($"Найдено и добавлено {sounds.Count} аудиофайлов из папки: {folderFullPath}");
#endif
    }
    public AudioClip FindSound(string key)
    {
        key = key.Split(".")[0];

        foreach (var sound in sounds)
        {
            if (sound.key == key) return sound.clip;
        }

        return null;
    }

    [Serializable]
    public struct SoundElement
    {
        public string key;
        public AudioClip clip;
    }
}