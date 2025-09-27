using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public class GroundSlash : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 30f;

    [Header("Ground Follow")]
    public float yOffset = 0f;            // 추가 높이 (피부두께 외에 더 띄우고 싶을 때)
    public float probeStartHeight = 1f;   // 본체 위에서 레이를 시작
    public float probeDistance = 3f;      // 아래로 최대 탐지 거리
    public float probeRadius = 0.15f;     // 0이면 Raycast, >0이면 SphereCast
    public LayerMask groundMask = ~0;     // 접지 레이어

    [Header("Lifecycle")]
    public float destroyDelay = 5f;

    [Header("Slow Down")]
    public float slowDownDuration = 1.5f; // 0이면 감속 X

    Rigidbody rb;
    Collider col;
    bool stopped;
    float lastGroundY;
    float skin; // 콜라이더 하프높이/반경

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        // Rigidbody 안정화
        rb.useGravity = false;                       // 지면 추적이 y를 관리
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    void Start()
    {
        skin = ComputeSkin(col);
        if (probeRadius <= 0f) probeRadius = Mathf.Max(0.1f, skin * 0.6f); // 기본 반경 보정

        // 시작 시 접지 스냅
        Vector3 p = rb.position;
        if (TryGround(out float gy))
        {
            lastGroundY = gy;
            p.y = gy + skin + yOffset;
        }
        else
        {
            lastGroundY = p.y;
        }
        rb.position = p;

        if (slowDownDuration > 0f) StartCoroutine(SlowDown());

        if (destroyDelay > 0f) Destroy(gameObject, destroyDelay);
    }

    void FixedUpdate()
    {
        if (stopped) return;

        float targetY;
        if (TryGround(out float gy))
        {
            lastGroundY = gy;
            targetY = gy + skin + yOffset;
        }
        else
        {
            // 미탐지 시 마지막 접지높이를 유지 (갭/경사에서 튐 방지)
            targetY = lastGroundY + skin + yOffset;
        }

        // xz는 물리 속도로 진행, y만 접지에 맞춰 보정
        Vector3 pos = rb.position;
        pos.y = targetY;
        rb.MovePosition(pos); // transform.position 대신 MovePosition 사용
    }

    bool TryGround(out float groundY)
    {
        Vector3 origin = rb.position + Vector3.up * Mathf.Max(0.1f, probeStartHeight);
        float maxDist = Mathf.Max(0.2f, probeDistance);

        bool hitSomething;
        RaycastHit hit;

        if (probeRadius > 0f)
            hitSomething = Physics.SphereCast(origin, probeRadius, Vector3.down, out hit, maxDist, groundMask, QueryTriggerInteraction.Ignore);
        else
            hitSomething = Physics.Raycast(origin, Vector3.down, out hit, maxDist, groundMask, QueryTriggerInteraction.Ignore);

        Debug.DrawRay(origin, Vector3.down * maxDist, hitSomething ? Color.green : Color.red);

        if (hitSomething)
        {
            groundY = hit.point.y;
            return true;
        }

        groundY = 0f;
        return false;
    }

    IEnumerator SlowDown()
    {
        Vector3 v0 = rb.linearVelocity;
        float t = 0f;
        while (t < slowDownDuration)
        {
            float k = 1f - (t / slowDownDuration); // 1→0
            rb.linearVelocity = v0 * k;
            t += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }
        rb.linearVelocity = Vector3.zero;
        stopped = true;
    }

    float ComputeSkin(Collider c)
    {
        if (c == null) return 0.1f;

        switch (c)
        {
            case CapsuleCollider cap:
                // 바닥 접점 기준 하프높이: 축이 Y일 때 radius + (height/2 - radius)
                if (cap.direction == 1) // Y
                    return Mathf.Max(0.05f, cap.radius);
                else
                    return Mathf.Max(0.05f, cap.height * 0.5f); // 보수적
            case SphereCollider sph:
                return Mathf.Max(0.05f, sph.radius);
            case BoxCollider box:
                return Mathf.Max(0.05f, box.size.y * 0.5f);
            default:
                return Mathf.Max(0.05f, c.bounds.extents.y);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (rb == null) rb = GetComponent<Rigidbody>();
        if (col == null) col = GetComponent<Collider>();
        if (skin <= 0f) skin = ComputeSkin(col);

        Vector3 origin = (rb ? rb.position : transform.position) + Vector3.up * Mathf.Max(0.1f, probeStartHeight);
        float h = Mathf.Max(0.2f, probeDistance);
        Gizmos.color = Color.cyan;
        if (probeRadius > 0f)
        {
            Gizmos.DrawWireSphere(origin, probeRadius);
            Gizmos.DrawLine(origin, origin + Vector3.down * h);
            Gizmos.DrawWireSphere(origin + Vector3.down * h, probeRadius);
        }
        else
        {
            Gizmos.DrawLine(origin, origin + Vector3.down * h);
        }
    }
#endif
}
