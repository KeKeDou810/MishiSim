using System;
using System.IO;
using System.Text;
using MoreMountains.Tools;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CardPreview : MonoBehaviour,
    MMEventListener<CardPreviewRequestEvent>
{

    [Header("UI")]
    [SerializeField] private Image artworkImage;
    [SerializeField] private TMP_Text detailsText;
    [SerializeField] private ScrollRect detailsScroll;

    private Sprite loadedSprite;
    private Texture2D loadedTexture;
    public void Clear()
    {
        ReleaseArtwork();
        if (detailsText != null) detailsText.text = "暂无卡片。";
    }

    public void OnEnable()
    {
        MMEventManager.AddListener<CardPreviewRequestEvent>(this);
    }

    private void OnDisable()
    {
        MMEventManager.RemoveListener<CardPreviewRequestEvent>(this);
    }

    public void Show(CardDefinition card, string contentRoot)
    {
        if (artworkImage == null || detailsText == null)
        {
            Debug.LogError("请绑定 Artwork Image 和 Details Text。", this);
            return;
        }

        try
        {
            string artworkPath = Path.Combine(
                contentRoot, card.ArtworkPath);

            byte[] imageBytes = File.ReadAllBytes(artworkPath);

            ReleaseArtwork();

            loadedTexture = new Texture2D(2, 2);

            if (!ImageConversion.LoadImage(
                loadedTexture, imageBytes))
            {
                throw new InvalidDataException(
                    $"图片解码失败：{artworkPath}");
            }

            loadedSprite = Sprite.Create(
                loadedTexture,
                new Rect(
                    0, 0,
                    loadedTexture.width,
                    loadedTexture.height),
                new Vector2(0.5f, 0.5f));

            artworkImage.sprite = loadedSprite;
            artworkImage.preserveAspect = true;

            detailsText.text =
                $"{card.Name}\n" +
                $"ID：{card.Id}\n" +
                $"颜色：{card.ColorName}\n" +
                $"国家：{card.Faction}\n" +
                (card.IsPlayer ? "契约附带玩家卡（不计入50张）\n" :
                    $"时间：{card.Level}\n" +
                    (card.IsDecision ? "" : $"力量：{card.Power}\n")) +
                (string.IsNullOrEmpty(card.Sign) ? "" : $"标识：{card.Sign}\n") +
                $"类型：{card.Type}\n" +
                (card.IsPlayer || card.IsDecision || string.IsNullOrEmpty(card.Race) ? "" : $"种族：{card.Race}\n") +
                (string.IsNullOrEmpty(card.EffectText) ? "" : $"\n效果：\n{card.EffectText}");
            if (detailsScroll != null)
            {
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(detailsScroll.content);
                detailsScroll.StopMovement();
                detailsScroll.verticalNormalizedPosition = 1f;
            }
        }
        catch (Exception exception)
        {
            ReleaseArtwork();
            detailsText.text = "卡片加载失败，请查看 Console。";
            Debug.LogException(exception, this);
        }
    }

    private void ReleaseArtwork()
    {
        if (artworkImage != null)
            artworkImage.sprite = null;

        if (loadedSprite != null)
            Destroy(loadedSprite);

        if (loadedTexture != null)
            Destroy(loadedTexture);

        loadedSprite = null;
        loadedTexture = null;
    }

    private void OnDestroy()
    {
        ReleaseArtwork();
    }

    public void OnMMEvent(CardPreviewRequestEvent e)
    {
        Show(e.Card, e.ContentRoot);
    }
}
