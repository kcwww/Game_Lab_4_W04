using UnityEngine;

public class ParryingPivot : MonoBehaviour
{
    private Rigidbody rb;

    [SerializeField] private Transform targetPos; // 플레이어 피봇

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }


    private void FixedUpdate()
    {
        rb.position = targetPos.position;
        rb.rotation = targetPos.rotation;
    }
}
