using UnityEngine;

/// <summary>
/// Hiệu ứng "nhảy ra" cho loot khi zombie chết: bắn loot lên theo quỹ đạo
/// parabol (vận tốc đứng + hướng ngang ngẫu nhiên), xoay trong lúc bay,
/// hạ cánh về mặt đất/mái nhà thực sự. Không cần Rigidbody, không sửa prefab —
/// tương thích với trigger-based pickup (Coin, ...).
///
/// Va chạm dùng SphereCast dọc theo toàn bộ vector di chuyển mỗi frame (cả
/// ngang lẫn đứng), nên loot không xuyên tường và không lơ lửng trên mái nhà.
/// </summary>
public class LootPop : MonoBehaviour
{
    [Header("Launch")]
    [Tooltip("Vận tốc đứng (lên) lúc bắn ra (m/s).")]
    public float upwardSpeed = 4.5f;

    [Tooltip("Vận tốc ngang tối đa (m/s), hướng ngẫu nhiên trong vòng tròn.")]
    public float horizontalSpeed = 2.5f;

    [Tooltip("Gia tốc trọng lực áp dụng cho loot (m/s^2).")]
    public float gravity = 12f;

    [Header("Spin")]
    [Tooltip("Tốc độ xoay khi đang bay (deg/s).")]
    public float launchSpinSpeed = 540f;

    [Tooltip("Tốc độ xoay sau khi hạ cánh (deg/s).")]
    public float idleSpinSpeed = 90f;

    [Header("Collision")]
    [Tooltip("Layer mask cho spherecast tìm mặt đất/tường. Mặc định hit mọi layer trừ IgnoreRaycast.")]
    public LayerMask groundMask = ~0;

    [Tooltip("Bán kính spherecast (dùng spherecast để ổn định với bề mặt hẹp).")]
    public float groundProbeRadius = 0.3f;

    [Tooltip("Cosine góc giữa normal va chạm và trục Y để phân biệt 'sàn' vs 'tường'. Normal.y >= ngưỡng này = sàn.")]
    [Range(0f, 1f)]
    public float floorNormalY = 0.6f;

    [Tooltip("Vận tốc đứng tối thiểu để tiếp tục nảy sau khi chạm sàn. Dưới ngưỡng này loot dừng hẳn.")]
    public float bounceStopThreshold = 1.5f;

    [Tooltip("Hệ số giảm vận tốc khi nảy (0 = không nảy, 1 = nảy nguyên).")]
    [Range(0f, 1f)]
    public float bounceDamping = 0.35f;

    [Tooltip("Độ cao nâng thêm so với mặt đất khi loot hạ cánh xong. Giúp loot "
             + "không bị chìm vào sàn khi pivot nằm ở giữa mesh/collider.")]
    public float restHeightOffset = 0.1f;

    [Tooltip("Tốc độ lerp mượt từ vị trí chạm sàn lên vị trí nghỉ "
             + "(restHeightOffset). Tránh bước nhảy visual khi hạ cánh.")]
    public float restSettleSpeed = 8f;

    private Vector3 velocity;
    private float spinSpeed;
    private bool landed;
    private Rigidbody rb;
    private bool rbWasKinematic;
    private Vector3 restTargetPos;
    private bool settling;

    /// <summary>True khi loot đã hạ cánh về mặt đất.</summary>
    public bool Landed => landed;

    void Awake()
    {
        enabled = false;
        rb = GetComponent<Rigidbody>();
    }

    /// <summary>Khởi động hiệu ứng bắn ra từ vị trí hiện tại.</summary>
    public void Launch(Vector3 spawnPosition)
    {
        transform.position = spawnPosition;

        // Set Rigidbody kinematic để tránh xung đột với LootPop: Pickeable.
        // FixedUpdate() gọi rb.AddForce(down) + gravity kéo xuống, nếu để
        // non-kinematic thì Rigidbody sẽ fight với transform.position do
        // LootPop set, gây tunneling xuyên sàn.
        if (rb != null)
        {
            rbWasKinematic = rb.isKinematic;
            rb.isKinematic = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Hướng ngang ngẫu nhiên trong vòng tròn.
        Vector2 h = Random.insideUnitCircle.normalized * horizontalSpeed;
        velocity = new Vector3(h.x, upwardSpeed, h.y);

        spinSpeed = launchSpinSpeed;
        landed = false;
        settling = false;
        enabled = true;
    }

    void Update()
    {
        float dt = Time.deltaTime;

        if (!landed)
        {
            velocity.y -= gravity * dt;

            Vector3 start = transform.position;
            Vector3 delta = velocity * dt;
            float distance = delta.magnitude;

            if (distance > 0.0001f &&
                Physics.SphereCast(
                    start,
                    groundProbeRadius,
                    delta / distance,
                    out RaycastHit hit,
                    distance,
                    groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                // Đặt loot tại điểm va chạm, hơi lùi ra khỏi bề mặt theo normal
                // để frame sau spherecast không bắt đầu từ bên trong collider.
                transform.position = hit.point + hit.normal * groundProbeRadius;

                if (hit.normal.y >= floorNormalY)
                {
                    // Chạm SÀN — nảy hoặc dừng.
                    if (Mathf.Abs(velocity.y) > bounceStopThreshold)
                    {
                        velocity.y = -velocity.y * bounceDamping;
                        velocity.x *= 0.5f;
                        velocity.z *= 0.5f;
                    }
                    else
                    {
                        velocity = Vector3.zero;
                        landed = true;
                        spinSpeed = idleSpinSpeed;
                        // Vị trí chạm sàn hiện tại (chỉ groundProbeRadius).
                        // Lerp mượt lên restHeightOffset trong các frame sau
                        // để tránh bước nhảy visual khi hạ cánh.
                        restTargetPos =
                            hit.point + hit.normal *
                            (groundProbeRadius + restHeightOffset);
                        settling = true;
                    }
                }
                else
                {
                    // Chạm TƯỜNG — triệt tiêu thành phần vận tốc đi vào tường,
                    // giữ thành phần tiếp tuyến (trượt dọc + rơi) để loot tiếp tục.
                    Vector3 intoSurface = Vector3.Project(velocity, hit.normal);
                    velocity -= intoSurface;
                }
            }
            else
            {
                transform.position = start + delta;
            }
        }
        else if (settling)
        {
            // Lerp mượt từ vị trí chạm sàn lên vị trí nghỉ (restHeightOffset)
            // để tránh bước nhảy visual ngay khi hạ cánh.
            transform.position = Vector3.Lerp(
                transform.position,
                restTargetPos,
                restSettleSpeed * dt);

            if (Vector3.SqrMagnitude(transform.position - restTargetPos) < 0.0001f)
            {
                transform.position = restTargetPos;
                settling = false;
            }
        }

        // Xoay quanh trục Y (và hơi nghiêng khi bay).
        transform.Rotate(Vector3.up, spinSpeed * dt, Space.World);
    }
}
