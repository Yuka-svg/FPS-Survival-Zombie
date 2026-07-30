using UnityEngine;
using UnityEngine.UIElements;
using System.Collections;

namespace cowsins
{
    public class PowerUp : Trigger
    {
        [SerializeField] private bool reappears;

        [SerializeField] protected float reappearTime;

        private VisualElement _timerRoot;
        private VisualElement _timerFill;
        private GameObject _docGO;
        private bool _docReady;

        [HideInInspector] public bool used;

        protected float timer = 0;

        private Coroutine timerCoroutine;

        private void BuildTimerDoc()
        {
            if (_docReady) return;
            _docGO = new GameObject("TimerDoc", typeof(UIDocument));
            _docGO.transform.SetParent(transform, false);
            var doc = _docGO.GetComponent<UIDocument>();
            doc.worldSpaceSize = new Vector2(0.5f, 0.5f);
            doc.sortingOrder = 10;

            _timerRoot = new VisualElement();
            _timerRoot.name = "TimerRoot";
            _timerRoot.style.position = Position.Absolute;
            _timerRoot.style.left = 0;
            _timerRoot.style.right = 0;
            _timerRoot.style.top = 0;
            _timerRoot.style.bottom = 0;
            _timerRoot.style.opacity = 0.32f;

            _timerFill = new VisualElement();
            _timerFill.name = "TimerFill";
            _timerFill.style.position = Position.Absolute;
            _timerFill.style.left = 0;
            _timerFill.style.top = 0;
            _timerFill.style.bottom = 0;
            _timerFill.style.backgroundColor = Color.white;
            _timerFill.style.width = Length.Percent(100);
            _timerRoot.Add(_timerFill);

            doc.rootVisualElement.Add(_timerRoot);
            _timerRoot.style.display = DisplayStyle.None;
            _docReady = true;
        }

        private void Start()
        {
            if (reappears) BuildTimerDoc();
        }

        public override void TriggerStay(Collider other)
        {
            if (used) return;

            Interact(other.GetComponent<PlayerDependencies>());

#if SAVE_LOAD_ADD_ON
            SaveTrigger();
#endif

            if (!reappears)
            {
                Destroy(this.gameObject);
            }
            else
            {
                used = true;
                if (_timerRoot != null) _timerRoot.style.display = DisplayStyle.Flex;
                if (timerCoroutine != null) StopCoroutine(timerCoroutine);
                timerCoroutine = StartCoroutine(StartTimerCoroutine());
            }
        }

        private IEnumerator StartTimerCoroutine()
        {
            float timer = reappearTime;

            while (timer > 0)
            {
                timer -= Time.deltaTime;
                if (_timerFill != null)
                {
                    float pct = (reappearTime - timer) / reappearTime;
                    _timerFill.style.width = Length.Percent(Mathf.Clamp01(pct) * 100f);
                }
                yield return null;
            }

            used = false;
            if (_timerRoot != null) _timerRoot.style.display = DisplayStyle.None;
        }

        public virtual void Interact(PlayerDependencies player)
        {
        }

#if SAVE_LOAD_ADD_ON
        public override void LoadedState()
        {
            if (triggered && !reappears) Destroy(this.gameObject);
        }
#endif
    }
}
