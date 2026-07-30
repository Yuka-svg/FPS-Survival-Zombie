using UnityEngine;

public class AutoDespawn : MonoBehaviour
{
    [SerializeField] private float lifetime = 120f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}