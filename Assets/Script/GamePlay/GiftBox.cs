using UnityEngine;
using cowsins;

public class GiftBox : Pickeable
{
    [Header("Random Model")]
    [Tooltip("Danh sách model Present Box, mỗi lần spawn sẽ chọn ngẫu nhiên 1 model.")]
    [SerializeField] private GameObject[] randomModels;

    public override bool InstantInteraction => true;

    public override void Awake()
    {
        base.Awake();
        interactText = "Mở quà";
        rotates = true;
        translates = true;

        GetComponent<Collider>().isTrigger = true;
        GetComponent<Rigidbody>().isKinematic = true;

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

        Collider[] modelColliders = model.GetComponentsInChildren<Collider>(true);
        foreach (var col in modelColliders)
            col.enabled = false;
    }

    private void Start()
    {
        SnapToGround();
    }

    public enum GiftRewardType { Coin, Exp, Ammo, Health, Undefined }

    private GiftRewardType _assignedType = GiftRewardType.Undefined;
    private int _assignedAmount = 0;

    public (GiftRewardType type, int amount) GetOrGenerateReward(Transform player, int defaultCoins, float defaultExp, int ammoMags, int defaultHP)
    {
        if (_assignedType == GiftRewardType.Undefined)
        {
            GiftRewardType[] types = { GiftRewardType.Coin, GiftRewardType.Exp, GiftRewardType.Ammo, GiftRewardType.Health };
            _assignedType = types[Random.Range(0, types.Length)];

            switch (_assignedType)
            {
                case GiftRewardType.Coin:
                    _assignedAmount = defaultCoins;
                    break;
                case GiftRewardType.Exp:
                    _assignedAmount = Mathf.RoundToInt(defaultExp);
                    break;
                case GiftRewardType.Health:
                    _assignedAmount = defaultHP;
                    break;
                case GiftRewardType.Ammo:
                    int magSize = 30;
                    if (player != null)
                    {
                        var wRef = player.GetComponent<IWeaponReferenceProvider>();
                        if (wRef != null && wRef.Id != null)
                        {
                            magSize = wRef.Id.magazineSize;
                        }
                    }
                    _assignedAmount = Mathf.Max(10, magSize * ammoMags);
                    break;
            }
        }
        return (_assignedType, _assignedAmount);
    }

    private bool _isOpened = false;

    public override void Interact(Transform player)
    {
        if (_isOpened) return;

        var col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        if (player != null)
        {
            var im = player.GetComponent<InteractManager>();
            if (im != null && im.HighlightedInteractable == this)
            {
                im.ForceRefreshUI(null);
            }
        }

        base.Interact(player);

        if (AdRewardManager.Instance != null)
        {
            bool shown = AdRewardManager.Instance.ShowAd(player, this);
            if (!shown)
            {
                if (col != null) col.enabled = true;
            }
        }
        else
        {
            if (col != null) col.enabled = true;
        }
    }

    public void OnAdCancelled()
    {
        _isOpened = false;
        var col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    public void OnAdCompletedAndClaimed()
    {
        _isOpened = true;
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
