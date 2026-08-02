using UnityEngine;
using UnityEngine.UIElements;

public static class UIElementsExtensions
{
    public static bool IsValid(this Sprite sprite)
    {
        try
        {
            return sprite != null && sprite && sprite.texture != null && sprite.texture.width > 0 && sprite.texture.height > 0 && sprite.rect.width > 0 && sprite.rect.height > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValid(this Texture2D texture)
    {
        try
        {
            return texture != null && texture && texture.width > 0 && texture.height > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValid(this VectorImage vectorImage)
    {
        try
        {
            return vectorImage != null && vectorImage;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsValid(this RenderTexture renderTexture)
    {
        try
        {
            return renderTexture != null && renderTexture && renderTexture.IsCreated() && renderTexture.width > 0 && renderTexture.height > 0;
        }
        catch
        {
            return false;
        }
    }

    public static void SetBackgroundImageSafe(this VisualElement element, Sprite sprite)
    {
        if (element == null) return;
        if (sprite.IsValid())
            element.style.backgroundImage = new StyleBackground(sprite);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }

    public static void SetBackgroundImageSafe(this VisualElement element, Texture2D texture)
    {
        if (element == null) return;
        if (texture.IsValid())
            element.style.backgroundImage = new StyleBackground(texture);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }

    public static void SetBackgroundImageSafe(this VisualElement element, VectorImage vectorImage)
    {
        if (element == null) return;
        if (vectorImage.IsValid())
            element.style.backgroundImage = Background.FromVectorImage(vectorImage);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }

    public static void SetBackgroundImageSafe(this VisualElement element, RenderTexture renderTexture)
    {
        if (element == null) return;
        if (renderTexture.IsValid())
            element.style.backgroundImage = Background.FromRenderTexture(renderTexture);
        else
            element.style.backgroundImage = StyleKeyword.Null;
    }

    public static void SetBackgroundImageAndDisplaySafe(this VisualElement element, Sprite sprite, DisplayStyle validDisplay = DisplayStyle.Flex, DisplayStyle invalidDisplay = DisplayStyle.None)
    {
        if (element == null) return;
        if (sprite.IsValid())
        {
            element.style.backgroundImage = new StyleBackground(sprite);
            element.style.display = validDisplay;
        }
        else
        {
            element.style.backgroundImage = StyleKeyword.Null;
            element.style.display = invalidDisplay;
        }
    }

    public static void SetBackgroundImageAndDisplaySafe(this VisualElement element, Texture2D texture, DisplayStyle validDisplay = DisplayStyle.Flex, DisplayStyle invalidDisplay = DisplayStyle.None)
    {
        if (element == null) return;
        if (texture.IsValid())
        {
            element.style.backgroundImage = new StyleBackground(texture);
            element.style.display = validDisplay;
        }
        else
        {
            element.style.backgroundImage = StyleKeyword.Null;
            element.style.display = invalidDisplay;
        }
    }
}
