using UnityEngine;
using UnityEngine.Events;

namespace cowsins
{
    public class Experience : Trigger
    {
        [SerializeField] private float minXp, maxXp;

        [SerializeField, Tooltip("Sound on picking up ")] private AudioClip pickUpSFX;

        public UnityEvent onCollect;

        [SerializeField] private Transform graphics;

        [Tooltip("Apply the selected effect")]
        public bool rotates, translates;

        [Tooltip("Change the speed of the selected effect"), SerializeField]
        private float rotationSpeed, translationSpeed;

        private float timer = 0f;

        private Transform player;
        private IntelligenceSkillSystem intelligence;

        private void Start()
        {
            GameObject playerObj =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObj != null)
            {
                player = playerObj.transform;
            }

            // IntelligenceSkillSystem lives on a manager GameObject (not the
            // tagged Player root), so search globally instead of via GetComponent.
            intelligence = FindAnyObjectByType<IntelligenceSkillSystem>();
        }

        private void Update()
        {
            Movement();
            MagnetXP();
        }

        private void MagnetXP()
        {
            if (player == null || intelligence == null)
                return;

            if (intelligence.XPPickupRadius <= 0)
                return;

            float distance =
                Vector3.Distance(transform.position, player.position);

            if (distance <= intelligence.XPPickupRadius)
            {
                transform.position = Vector3.MoveTowards(
                    transform.position,
                    player.position,
                    15f * Time.deltaTime);
            }
        }

        public override void TriggerEnter(Collider other)
        {
            if (ExperienceManager.Instance == null || !ExperienceManager.Instance.useExperience) return; // If we are not using XP in our game we should not pick up XP or we should not be able to.

            onCollect?.Invoke();

            // Generate a random amount of XP.
            float amount = Random.Range(minXp, maxXp);

            // IntelligenceSkillSystem lives on a manager GameObject, not on the
            // player collider that entered the trigger, so search globally.
            var intelligenceStats =
                FindAnyObjectByType<IntelligenceSkillSystem>();

            if (intelligenceStats != null)
            {
                amount *= intelligenceStats.XPMultiplier;
            }

            ExperienceManager.Instance.AddExperience(amount);
            UIEvents.onExperienceCollected?.Invoke(true);

            // Play SFX
            SoundManager.Instance.PlaySound(pickUpSFX, 0, 0, false);
            // Destroy the XP.
            Destroy(this.gameObject);
        }

        private void Movement()
        {
            if (!rotates && !translates) return;
            if (rotates) graphics.Rotate(Vector3.up * rotationSpeed * Time.deltaTime); // Rotate over time
            if (translates) // Go up and down
            {
                timer += Time.deltaTime * translationSpeed; // Timer that controls the movement
                float translateMotion = Mathf.Sin(timer) / 2000f;
                graphics.transform.localPosition = new Vector3(graphics.transform.localPosition.x, graphics.transform.localPosition.y + translateMotion, graphics.transform.localPosition.z);
            }
        }

#if SAVE_LOAD_ADD_ON
        public override void LoadedState()
        {
            Destroy(this.gameObject);
        }
#endif
    }
}
