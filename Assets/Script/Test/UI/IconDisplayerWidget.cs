using UnityEngine;
using UnityEngine.UIElements;

[DisallowMultipleComponent]
public class IconDisplayerWidget : MonoBehaviour
{
    public Sprite iconSprite;
    public Vector2 iconSize = new Vector2(100f, 67f);
    public float worldScale = 0.0037f;

    private GameObject _uiGO;
    private UIDocument _doc;

    private void Awake()
    {
        Build();
    }

    private void Build()
    {
        _uiGO = new GameObject("IconDisplayUI", typeof(UIDocument));
        _uiGO.transform.SetParent(transform, false);
        _uiGO.transform.localPosition = Vector3.zero;
        _uiGO.transform.localRotation = Quaternion.identity;
        _uiGO.transform.localScale = Vector3.one;

        _doc = _uiGO.GetComponent<UIDocument>();
        _doc.worldSpaceSize = new Vector2(iconSize.x * worldScale, iconSize.y * worldScale);

        var root = new VisualElement();
        root.name = "IconRoot";
        root.style.position = Position.Absolute;
        root.style.left = 0;
        root.style.top = 0;
        root.style.width = iconSize.x;
        root.style.height = iconSize.y;

        if (iconSprite != null)
        {
            root.style.backgroundImage = new StyleBackground(iconSprite);
        }

        _doc.rootVisualElement.Add(root);
    }

    private void OnDestroy()
    {
        if (_uiGO != null)
            Destroy(_uiGO);
    }
}
