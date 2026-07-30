using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    public partial class Trigger : Identifiable
    {
        [System.Serializable]
        public class Events
        {
            public UnityEvent onEnter, onStay, onExit;
        }

        [SerializeField] private Events events;

        [SaveField] protected bool triggered;
        [SerializeField] private bool rememberTriggerState;

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") && !triggered)
            {
                events.onEnter?.Invoke();
                triggered = true;
                TriggerEnter(other);
#if SAVE_LOAD_ADD_ON
                SaveTrigger();
                LoadedState(); 
#endif
            }
        }
        private void OnTriggerStay(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                events.onStay?.Invoke();
                TriggerStay(other);
            }
        }
        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                events.onExit?.Invoke();
                TriggerExit(other);
                if(!rememberTriggerState) triggered = false;
#if SAVE_LOAD_ADD_ON
                StoreData();
#endif
            }
        }

        public virtual void TriggerEnter(Collider other)
        {

        }
        public virtual void TriggerStay(Collider other)
        {
        }

        public virtual void TriggerExit(Collider other)
        {

        }
    }
}