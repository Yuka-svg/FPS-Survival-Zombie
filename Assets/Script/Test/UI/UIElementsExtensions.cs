using UnityEngine;
using UnityEngine.UIElements;

public static class UIElementsExtensions
{
    public static void SetBackgroundImageSafe(this VisualElement element, Sprite sprite)
    {
        if (element == null) return;
        if (sprite != null)
            element.style.backgroundImage = new StyleBackground(sprite);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }

    public static void SetBackgroundImageSafe(this VisualElement element, Texture2D texture)
    {
        if (element == null) return;
        if (texture != null)
            element.style.backgroundImage = new StyleBackground(texture);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }

    public static void SetBackgroundImageSafe(this VisualElement element, VectorImage vectorImage)
    {
        if (element == null) return;
        if (vectorImage != null)
            element.style.backgroundImage = Background.FromVectorImage(vectorImage);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }

    public static void SetBackgroundImageSafe(this VisualElement element, RenderTexture renderTexture)
    {
        if (element == null) return;
        if (renderTexture != null && renderTexture.IsCreated())
            element.style.backgroundImage = Background.FromRenderTexture(renderTexture);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }
}
