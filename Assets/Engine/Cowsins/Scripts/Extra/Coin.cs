using UnityEngine;

namespace cowsins
{
    public class Coin : Trigger
    {
        [SerializeField] private int minCoins, maxCoins;

        [SerializeField] private AudioClip collectCoinSFX;

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
            MagnetCoin();
        }

        private void MagnetCoin()
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
            int amountOfCoins = Random.Range(minCoins, maxCoins);
            CoinManager.Instance.AddCoins(amountOfCoins, true);
            UIEvents.onCoinsChange?.Invoke(CoinManager.Instance.coins, true);
            SoundManager.Instance.PlaySound(collectCoinSFX, 0, 1, false);
            Destroy(this.gameObject);
        }


#if SAVE_LOAD_ADD_ON
        public override void LoadedState()
        {
            Destroy(this.gameObject);
        }
#endif
    }

}