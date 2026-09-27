using UnityEngine;
using Sirenix.OdinInspector;
using System.IO;
using System;
public class CardVisual : MonoBehaviour
{
    [SerializeField] private CardPreview _preview;
    [SerializeField] private Renderer _frontRenderer;

    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

    private MaterialPropertyBlock _properties;
    private Texture2D _artworkTexture;
    public void HideFace() { if (_frontRenderer != null) _frontRenderer.enabled = false; }

    public void Bind(CardDefinition definition)
    {
        var service = CardDatabaseService.Instance;

        string path = Path.Combine(
            service.ContentRoot,
            definition.ArtworkPath);

        Texture2D nextTexture = null;

        try
        {
            byte[] bytes = File.ReadAllBytes(path);

            nextTexture = new Texture2D(2, 2);

            if (!ImageConversion.LoadImage(
                nextTexture, bytes, markNonReadable: true))
            {
                throw new InvalidDataException(
                    $"无法读取卡图：{path}");
            }

            nextTexture.wrapMode = TextureWrapMode.Clamp;

            _properties ??= new MaterialPropertyBlock();

            _frontRenderer.GetPropertyBlock(_properties);
            _properties.SetTexture(BaseMap, nextTexture);
            _frontRenderer.SetPropertyBlock(_properties);

            if (_artworkTexture != null)
                Destroy(_artworkTexture);

            _artworkTexture = nextTexture;
        }
        catch (Exception exception)
        {
            if (nextTexture != null)
                Destroy(nextTexture);

            Debug.LogException(exception, this);
        }
    }

    private void OnDestroy()
    {
        if (_artworkTexture != null)
            Destroy(_artworkTexture);
    }
    
}
