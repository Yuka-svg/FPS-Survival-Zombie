using UnityEngine;
using cowsins;

public class GiftBox : Pickeable
{
    public override void Awake()
    {
        base.Awake();
        interactText = "Mở quà [E]";
        rotates = true;
        translates = true;

        GetComponent<Collider>().isTrigger = true;
        GetComponent<Rigidbody>().isKinematic = true;

        typeof(Interactable).GetField("instantInteraction",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)
            ?.SetValue(this, true);
    }

    private void Start()
    {
        SnapToGround();
    }

    public override void Interact(Transform player)
    {
        base.Interact(player);

        Debug.Log("[GiftBox] Interact called. AdRewardManager.Instance = " + (AdRewardManager.Instance != null ? "NOT NULL" : "NULL"));

        if (AdRewardManager.Instance != null)
            AdRewardManager.Instance.ShowAd(player);

        Destroy(gameObject);
    }

    private void SnapToGround()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;

        int groundLayer = LayerMask.GetMask("Ground");
        if (groundLayer == 0) return;

        Vector3 origin = transform.position + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 10f, groundLayer, QueryTriggerInteraction.Ignore))
        {
            float halfHeight = col.bounds.extents.y;
            Vector3 pos = transform.position;
            pos.y = hit.point.y + halfHeight;
            transform.position = pos;
        }
    }
}
