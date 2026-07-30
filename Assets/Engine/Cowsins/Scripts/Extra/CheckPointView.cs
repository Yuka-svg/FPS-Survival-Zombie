using System.Collections;
using UnityEngine;
using UnityEngine.UIElements;
namespace cowsins
{
    public class CheckPointView : MonoBehaviour
    {
        public enum MeasureType
        {
            metres, kilometres, inches, feet, yards, miles
        }

        private Label _text;
        private VisualElement _root;

        [Tooltip("Select a measure unit among the following"), SerializeField]
        private MeasureType measureType;

        [Tooltip("number of decimals to display"), Range(0, 10), SerializeField]
        private int decimals;

        [Tooltip("How fast you want the text to display the new distance"), SerializeField]
        private float updatePeriod;

        // [Tooltip("When enabled, the checkpoint icon and distance text render on top of everything (visible through walls). No longer uses shader tricks — screen-space UIDocument is always on top.")]
        // [SerializeField] private bool seeThrough = true;

        [Tooltip("Maximum distance at which the checkpoint view is visible. Set to 0 or less to always show.")]
        [SerializeField] private float maxViewDistance = 50f;

        private Transform playerTransform;
        private UIDocument _doc;
        private bool _ready;

        private readonly float[] ConversionFactors =
        {
            1f, 0.001f, 39.37f, 3.28084f, 1.09361f, 0.000621371192f
        };

        private readonly string[] UnitLabels =
        {
            "m", "km", "inch", "feet", "yards", "miles"
        };

        private void Start()
        {
            playerTransform = GameObject.FindGameObjectWithTag("Player")?.transform;
            BuildUI();
            StartCoroutine(UpdateValue());
        }

        private void BuildUI()
        {
            _doc = GetComponent<UIDocument>();
            if (_doc == null)
            {
                var go = new GameObject("CheckPointUI_Doc", typeof(UIDocument));
                go.transform.SetParent(transform, false);
                _doc = go.GetComponent<UIDocument>();
            }
            _doc.sortingOrder = 500;

            var hudDoc = FindFirstObjectByType<UIDocument>();
            if (hudDoc != null) _doc.panelSettings = hudDoc.panelSettings;

            _root = new VisualElement();
            _root.name = "CheckPointView";
            _root.style.position = Position.Absolute;
            _root.style.left = 0;
            _root.style.top = 0;
            _root.style.width = 200;
            _root.style.height = 60;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            _root.style.display = DisplayStyle.None;

            var icon = new VisualElement();
            icon.name = "Icon";
            icon.style.width = 24;
            icon.style.height = 24;
            icon.style.backgroundColor = new Color(1, 1, 0, 0.8f);
            icon.style.marginBottom = 2;
            _root.Add(icon);

            _text = new Label();
            _text.name = "DistanceText";
            _text.style.fontSize = 18;
            _text.style.color = Color.white;
            _text.style.unityTextAlign = TextAnchor.MiddleCenter;
            _root.Add(_text);

            _doc.rootVisualElement.Add(_root);
            _ready = true;
        }

        private IEnumerator UpdateValue()
        {
            var wait = new WaitForSeconds(updatePeriod);

            while (true)
            {
                UpdateDistanceText();
                yield return wait;
            }
        }

        private void UpdateDistanceText()
        {
            if (!_ready || playerTransform == null) return;

            float baseDistance = Vector3.Distance(transform.position, playerTransform.position);

            bool shouldShow = maxViewDistance <= 0f || baseDistance <= maxViewDistance;
            if (_root != null)
                _root.style.display = shouldShow ? DisplayStyle.Flex : DisplayStyle.None;

            if (!shouldShow) return;

            float converted = baseDistance * ConversionFactors[(int)measureType];
            string text = converted.ToString($"F{decimals}") + UnitLabels[(int)measureType];

            if (_text != null)
                _text.text = text;

            if (_root != null && Camera.main != null)
            {
                Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 2f);
                if (screenPos.z > 0)
                {
                    _root.style.left = screenPos.x - 100;
                    _root.style.top = Screen.height - screenPos.y - 30;
                }
                else
                {
                    _root.style.display = DisplayStyle.None;
                }
            }
        }
    }
}
