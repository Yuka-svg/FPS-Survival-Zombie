#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UIElements;
namespace cowsins
{
    public class PointCapture : Trigger
    {
        [System.Serializable]
        public class PointCaptureEvents
        {
            public UnityEvent OnCapture;
        }
        public PointCaptureEvents captureEvents;

        [Tooltip(" how fast the point will be captured "), SerializeField]
        private float captureSpeed;

        [Tooltip(" If true, progress will gradually be lost when player leaves the point ")]
        public bool loseProgressIfNotCapturing;

        [Tooltip(" Speed of progress loss "), SerializeField]
        private float losingProgressCaptureSpeed;

        private bool beingCaptured;

        private bool captured;

        [SaveField] private float progress;

        private VisualElement _progressBar;
        private VisualElement _progressFill;
        private GameObject _docGO;

        private void Start()
        {
            progress = 0;
            captured = false;
        }

        void Update()
        {
            if (!beingCaptured && progress > 0 && loseProgressIfNotCapturing) progress -= Time.deltaTime * losingProgressCaptureSpeed;
        }

        private void BuildUI()
        {
            if (_docGO != null) return;
            _docGO = new GameObject("PointCaptureUI_Doc", typeof(UIDocument));
            _docGO.transform.SetParent(transform, false);
            var doc = _docGO.GetComponent<UIDocument>();
            doc.sortingOrder = 100;

            var root = new VisualElement();
            root.name = "PointCaptureRoot";
            root.style.position = Position.Absolute;
            root.style.left = 0;
            root.style.right = 0;
            root.style.top = 0;
            root.style.bottom = 0;
            root.pickingMode = PickingMode.Ignore;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.FlexEnd;
            root.style.paddingBottom = 104;

            var track = new VisualElement();
            track.name = "Track";
            track.style.width = 160;
            track.style.height = 20;
            track.style.backgroundColor = new Color(0, 0, 0, 0.33f);
            track.style.unitySliceScale = 0.64f;
            root.Add(track);

            _progressFill = new VisualElement();
            _progressFill.name = "Fill";
            _progressFill.style.position = Position.Absolute;
            _progressFill.style.left = 0;
            _progressFill.style.top = 0;
            _progressFill.style.bottom = 0;
            _progressFill.style.width = Length.Percent(0);
            _progressFill.style.backgroundColor = Color.white;
            track.Add(_progressFill);

            doc.rootVisualElement.Add(root);
        }

        public override void TriggerStay(Collider other)
        {
            if (!captured)
            {
                if (_docGO == null) BuildUI();
                beingCaptured = true;
                progress += Time.deltaTime * captureSpeed;

                if (progress >= 100)
                {
                    captured = true;
                    OnCapture();
                }

#if SAVE_LOAD_ADD_ON
                SaveTrigger();
#endif
            }

            if (_progressFill == null) return;
            _progressFill.style.width = Length.Percent(Mathf.Clamp01(progress / 100f) * 100f);
        }

        public override void TriggerExit(Collider other)
        {
            if (!captured)
            {
                beingCaptured = false;
                if (_docGO != null)
                {
                    Destroy(_docGO);
                    _docGO = null;
                }
            }
        }

        public virtual void OnCapture()
        {
            captureEvents.OnCapture.Invoke();

            Debug.Log("You captured the point!");

            if (_docGO != null)
            {
                Destroy(_docGO);
                _docGO = null;
            }
            Destroy(this.gameObject);
        }

#if SAVE_LOAD_ADD_ON
        public override void LoadedState()
        {
            if (progress >= 100)
            {
                Destroy(_docGO);
                Destroy(this.gameObject);
            }
        }
#endif
    }
#if UNITY_EDITOR
    [System.Serializable]
    [CustomEditor(typeof(PointCapture))]
    public class PointCaptureEditor : Editor
    {

        override public void OnInspectorGUI()
        {
            serializedObject.Update();
            PointCapture myScript = target as PointCapture;

            EditorGUILayout.LabelField("SETTINGS", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureSpeed"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("loseProgressIfNotCapturing"));
            if (myScript.loseProgressIfNotCapturing)
            {
                EditorGUILayout.PropertyField(serializedObject.FindProperty("losingProgressCaptureSpeed"));
            }
            EditorGUILayout.PropertyField(serializedObject.FindProperty("captureEvents"));
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
