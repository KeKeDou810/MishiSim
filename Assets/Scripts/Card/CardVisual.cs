using UnityEngine;
using Sirenix.OdinInspector;
using System.IO;
using System;
public class CardVisual : MonoBehaviour
{
    [SerializeField] private CardPreview _preview;
    [SerializeField] private Renderer _frontRenderer;
    [SerializeField] private Renderer _backRenderer;
    private CardDatabaseService sleeveService;
    private MaterialPropertyBlock sleeveProperties;
    private void Start()
    {
        if (_backRenderer == null) return;
        sleeveService = CardDatabaseService.Instance;
        sleeveService.SleeveChanged += ApplySleeve;
        sleeveService.EnsureSleeveTexture();
        ApplySleeve();
    }
    private void ApplySleeve()
    {
        if (_backRenderer == null || sleeveService == null || sleeveService.SleeveTexture == null) return;
        sleeveProperties ??= new MaterialPropertyBlock();
        _backRenderer.GetPropertyBlock(sleeveProperties);
        sleeveProperties.SetTexture(BaseMap, sleeveService.SleeveTexture);
        sleeveProperties.SetTexture("_MainTex", sleeveService.SleeveTexture);
        sleeveProperties.SetColor("_BaseColor", Color.white);
        sleeveProperties.SetColor("_Color", Color.white);
        _backRenderer.SetPropertyBlock(sleeveProperties);
    }

    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");

    private MaterialPropertyBlock _properties;
    private Texture2D _artworkTexture;
    public void HideFace() { if (_frontRenderer != null) _frontRenderer.enabled = false; }

    public void Bind(CardDefinition definition)
    {
        if (_frontRenderer != null) _frontRenderer.enabled = true;
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
        if (sleeveService != null) sleeveService.SleeveChanged -= ApplySleeve;
        if (_artworkTexture != null)
            Destroy(_artworkTexture);
    }
    
}
