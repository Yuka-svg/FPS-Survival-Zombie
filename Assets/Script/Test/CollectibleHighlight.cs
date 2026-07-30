using UnityEngine;

namespace cowsins
{
    public class CollectibleHighlight : MonoBehaviour
    {
        private Outline outline;
        private IntelligenceSkillSystem intelligence;

        private void Start()
        {
            outline = GetComponent<Outline>();

            // IntelligenceSkillSystem lives on a manager GameObject (not the
            // tagged Player root), so search globally instead of via GetComponent.
            intelligence = FindAnyObjectByType<IntelligenceSkillSystem>();

            if (outline != null)
            {
                outline.enabled = false;
            }
        }

        private void Update()
        {
            if (outline == null ||
                intelligence == null)
                return;

            outline.enabled =
                intelligence.HighlightCollectibles;
        }
    }
}