using UnityEngine;
using cowsins;

public class GiftBox : Pickeable
{
    [Header("Random Model")]
    [Tooltip("Danh sách model Present Box, mỗi lần spawn sẽ chọn ngẫu nhiên 1 model.")]
    [SerializeField] private GameObject[] randomModels;

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

        ApplyRandomModel();
    }

    private void ApplyRandomModel()
    {
        if (randomModels == null || randomModels.Length == 0) return;
        if (graphics == null) return;

        GameObject selected = randomModels[Random.Range(0, randomModels.Length)];
        if (selected == null) return;

        for (int i = graphics.childCount - 1; i >= 0; i--)
            Destroy(graphics.GetChild(i).gameObject);

        GameObject model = Instantiate(selected, graphics);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;
        model.transform.localScale = Vector3.one * 1.5f;
    }

    private void Start()
    {
        SnapToGround();
    }

    public override void Interact(Transform player)
    {
        base.Interact(player);

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
