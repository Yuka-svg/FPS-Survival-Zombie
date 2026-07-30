using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class CeilingZombieController : MonoBehaviour
{
    [Header("References")]
    public Transform targetSphere; // Kéo khối Sphere vào đây
    public Transform playerVision; // Kéo khối Sphere (hoặc Main Camera) vào đây để làm hướng nhìn
    public Animator animator;      // Kéo chính con Zombie có chứa BlendTree vào đây

    [Header("UI Elements (Dùng Canvas)")]
    public Image vignetteImage;    // Kéo UI Image (chứa Sprite đỏ) vào đây

    [Header("Movement Settings")]
    public float crawlSpeed = 1.5f;
    public float scrambleSpeed = 4.5f;
    public float rotationSpeed = 5f;

    [Header("Detection Settings")]
    public float chaseDistance = 15f;
    public float fovAngle = 60f;   // Góc nhìn để kích hoạt bò nhanh

    [Header("Attack Settings")]
    public float dangerRadius = 8f;
    public float snapDistance = 1.5f;

    private bool isDead = false;

    void Update()
    {
        if (isDead || targetSphere == null || animator == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, targetSphere.position);

        // 1. Xử lý UI đỏ màn hình
        UpdateVignette(distanceToTarget);

        // 2. Kích hoạt The Snap
        if (distanceToTarget <= snapDistance)
        {
            StartCoroutine(ExecuteSnap());
            return; // Dừng chạy các logic bên dưới
        }

        // 3. Xử lý Di chuyển & BlendTree Locomotion
        if (distanceToTarget <= chaseDistance)
        {
            bool isSpotted = CheckIfSpotted();

            // Nếu Sphere "nhìn" vào Zombie -> Chạy nhanh. Nếu không -> Bò từ từ
            float currentSpeed = isSpotted ? scrambleSpeed : crawlSpeed;
            float animSpeedValue = isSpotted ? 2f : 1f;

            // Damping 0.15f giúp chuyển state trong BlendTree mượt mà
            animator.SetFloat("Speed", animSpeedValue, 0.15f, Time.deltaTime);
            MoveTowardsTarget(currentSpeed);
        }
        else
        {
            // Đứng ngoài tầm -> Nằm im nhún nhún (Crouch Idle)
            animator.SetFloat("Speed", 0f, 0.15f, Time.deltaTime);
        }
    }

    bool CheckIfSpotted()
    {
        if (playerVision == null) return false;

        Vector3 dirToZombie = (transform.position - playerVision.position).normalized;
        // Kiểm tra góc giữa trục Z (forward) của vật thể nhìn và hướng tới Zombie
        float angle = Vector3.Angle(playerVision.forward, dirToZombie);
        return angle < fovAngle / 2f;
    }

    void MoveTowardsTarget(float speed)
    {
        // Khóa trục Y bằng chính trục Y hiện tại của Zombie để test trên mặt phẳng
        Vector3 targetPos = new Vector3(targetSphere.position.x, transform.position.y, targetSphere.position.z);
        Vector3 direction = (targetPos - transform.position).normalized;

        if (direction != Vector3.zero)
        {
            Quaternion lookRot = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.Slerp(transform.rotation, lookRot, Time.deltaTime * rotationSpeed);
        }
        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
    }

    void UpdateVignette(float distance)
    {
        if (vignetteImage == null) return;

        if (distance <= dangerRadius && distance > snapDistance)
        {
            float intensity = 1f - ((distance - snapDistance) / (dangerRadius - snapDistance));
            Color c = vignetteImage.color;
            c.a = intensity;
            vignetteImage.color = c;
        }
        else if (distance > dangerRadius)
        {
            Color c = vignetteImage.color;
            c.a = 0f;
            vignetteImage.color = c;
        }
    }

    IEnumerator ExecuteSnap()
    {
        isDead = true;

        // Trả Zombie về trạng thái Idle để nhường chỗ cho UI
        animator.SetFloat("Speed", 0f);

        Debug.Log("--- BẮT ĐẦU SNAP ---");
        Debug.Log("SOUND PLAY: RẮC!!!");

        yield return new WaitForSeconds(0.15f);

        if (vignetteImage != null) vignetteImage.color = Color.black;

        Debug.Log("GAME OVER: BỊ VẶN CỔ!");
    }
}