using UnityEngine;
using System.Collections;

public class GroundSlashShooter : MonoBehaviour
{
    [Header("Refs")]
    public GameObject projectile;
    public Transform firePoint;

    [Header("Fire")]
    public float fireRate = 4.0f;
    public bool yawOnly = true;
    public bool keepFirePointRoll = false;

    [Header("Orientation")]
    public Vector3 prefabLocalForward = Vector3.forward; // 모델 앞축
    public Vector3 prefabLocalUp = Vector3.up;           // 모델 업축

    private Quaternion orientationOffset = Quaternion.identity;
    private float _nextFireTime;

    void Awake()
    {
        // 프리팹 로컬축 -> Unity 가정(+Z,+Y)로 맵핑
        var qF = Quaternion.FromToRotation(prefabLocalForward.normalized, Vector3.forward);
        var upAfter = qF * prefabLocalUp.normalized;
        var qU = Quaternion.FromToRotation(upAfter, Vector3.up);
        orientationOffset = qU * qF;
    }

    [ContextMenu("FireAt (0,0,0)")]
    public void TestFireAtOrigin() => FireAt(Vector3.zero, ignoreCooldown: true);

    public bool FireAt(Vector3 worldTarget, bool ignoreCooldown = false, float? speedOverride = null)
    {
        if (!CanFire(ignoreCooldown)) return false;
        _nextFireTime = Time.time + 1f / Mathf.Max(0.0001f, fireRate);

        if (projectile == null || firePoint == null)
        {
            Debug.LogWarning("[GroundSlashShooter] projectile/firePoint 미설정");
            return false;
        }

        // 1) 목표 회전 계산 (FirePoint → Target)
        Quaternion aimRot = AimFrom(firePoint, worldTarget, yawOnly, keepFirePointRoll);
        Quaternion finalRot = aimRot * orientationOffset;

        // 2) 생성 시점에 '바로' 회전 적용 (중요!)
        var proj = Instantiate(projectile, firePoint.position, finalRot);

        // 3) Rigidbody에도 명시적으로 반영
        if (proj.TryGetComponent<Rigidbody>(out var rb))
        {
            rb.rotation = finalRot;          // 즉시
            rb.angularVelocity = Vector3.zero;
            // 다음 Fixed에서 한 번 더 확정
            StartCoroutine(ConfirmRotationNextFixed(rb, finalRot));

            float speed = speedOverride ?? (proj.TryGetComponent<GroundSlash>(out var gs) ? gs.speed : 30f);
            rb.linearVelocity = proj.transform.forward * speed;
        }
        else
        {
            Debug.LogWarning("[GroundSlashShooter] Projectile에 Rigidbody가 없습니다.");
        }

        return true;
    }

    IEnumerator ConfirmRotationNextFixed(Rigidbody rb, Quaternion rot)
    {
        yield return new WaitForFixedUpdate();
        if (rb) rb.MoveRotation(rot);
    }

    public bool FireAt(Transform target, bool ignoreCooldown = false, float? speedOverride = null)
    {
        if (target == null) return false;
        return FireAt(target.position, ignoreCooldown, speedOverride);
    }

    public bool CanFire(bool ignoreCooldown = false)
    {
        if (projectile == null || firePoint == null) return false;
        return ignoreCooldown || Time.time >= _nextFireTime;
    }

    static Quaternion AimFrom(Transform origin, Vector3 target, bool yawOnly, bool keepRoll)
    {
        Vector3 dir = target - origin.position;
        if (yawOnly) dir = Vector3.ProjectOnPlane(dir, Vector3.up);
        if (dir.sqrMagnitude < 1e-6f) return origin.rotation;

        Quaternion look = Quaternion.LookRotation(dir.normalized, Vector3.up);
        if (keepRoll)
        {
            var eLook = look.eulerAngles;
            var eSrc = origin.rotation.eulerAngles;
            look = Quaternion.Euler(eLook.x, eLook.y, eSrc.z);
        }
        return look;
    }
}
