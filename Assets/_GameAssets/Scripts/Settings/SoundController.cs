using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using YP;

public class SoundController : ButtonHandler
{
    [SerializeField] private bool sound;

    [SerializeField] private Sprite enableIcon;
    [SerializeField] private Sprite disableIcon;

    [SerializeField] private RectTransform icon;
    private RectTransform iconParent;

    private bool sEnabled = true;

    protected void Awake()
    {
        iconParent = GetComponent<RectTransform>();
        sEnabled = sound ? Saves.Bool[Key_Save.sound].Value : Saves.Bool[Key_Save.music].Value;
        Check();
    }

    protected override void OnClick()
    {
        sEnabled = !sEnabled;
        Check();
    }

    private void Check()
    {
        var targetX = sEnabled
            ? -iconParent.rect.x / 2 - icon.rect.width / 2
            : iconParent.rect.x / 2 + icon.rect.width / 2;

        icon.DOAnchorPosX(targetX, 0.1f)
            .SetUpdate(true)
            .OnComplete(() => Enable(sEnabled));

        if (sound)
        {
            Sound.EnableSound(sEnabled);
            Saves.Bool[Key_Save.sound].Value = sEnabled;
        }
        else
        {
            Sound.EnableMusic(sEnabled);
            Saves.Bool[Key_Save.music].Value = sEnabled;
        }
    }

    private void Enable(bool en)
    {
        icon.GetComponent<Image>().sprite = en ? enableIcon : disableIcon;
    }
}