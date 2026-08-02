using System.Collections;
using UnityEngine;
using TMPro;

namespace cowsins
{
    public class CheckPointView : MonoBehaviour
    {
        public enum MeasureType
        {
            metres, kilometres, inches, feet, yards, miles
        }

        [Tooltip("Select a measure unit among the following"), SerializeField]
        private MeasureType measureType;

        [Tooltip("number of decimals to display"), Range(0, 10), SerializeField]
        private int decimals;

        [Tooltip("How fast you want the text to display the new distance"), SerializeField]
        private float updatePeriod;

        [Tooltip("Distance text rendered above the checkpoint"), SerializeField]
        private TextMeshProUGUI text;

        [Tooltip("Maximum distance at which the checkpoint view is visible. Set to 0 or less to always show.")]
        [SerializeField] private float maxViewDistance = 50f;

        private Transform playerTransform;
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
            _ready = true;
            StartUpdateRoutine();
        }

        private void OnEnable()
        {
            // Deactivating the GameObject (e.g. player respawn flow) kills the
            // UpdateValue coroutine, and Start() does not re-run on reactivation.
            if (_ready) StartUpdateRoutine();
        }

        private void StartUpdateRoutine()
        {
            StopAllCoroutines();
            StartCoroutine(UpdateValue());
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
            if (text != null)
                text.gameObject.SetActive(shouldShow);

            if (!shouldShow) return;

            float converted = baseDistance * ConversionFactors[(int)measureType];
            if (text != null)
                text.text = converted.ToString($"F{decimals}") + UnitLabels[(int)measureType];
        }
    }
}
