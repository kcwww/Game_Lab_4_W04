using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 락온 후보 탐지기 (전체 구형 볼륨)
/// - 반경 내 콜라이더 후보 수집
/// - 필터: 화면 뷰(옵션) → LOS(옵션; 보통 플레이어에서 발사)
/// - 성능: OverlapSphereNonAlloc + 샘플링 주기
/// - 기즈모: 구형 볼륨/후보 라인
/// </summary>
public class LockOnDetector : MonoBehaviour
{
    [Header("Detection Origin (Sphere Center)")]
    [Tooltip("탐지 원점(보통 플레이어의 가슴/머리 등). 비우면 이 컴포넌트의 Transform")]
    public Transform origin;

    [Header("Detection Volume (Sphere)")]
    [Min(0.1f)] public float radius = 20f;

    [Header("Filters")]
    public LayerMask candidateLayer = ~0;               // 적/타깃 레이어
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Tooltip("시야 가림 체크(벽/지형) — origin 기준 (옵션)")]
    public bool doOcclusionCheck = false;
    public LayerMask occluderMask = 0;                  // 벽/지형 레이어

    [Header("Sampling")]
    [Tooltip("후보 스캔 주기(초). 0이면 매 프레임")]
    [Min(0f)] public float sampleInterval = 0.1f;
    [Tooltip("한 번에 담을 최대 콜라이더 수(버퍼 크기)")]
    [Min(8)] public int nonAllocCapacity = 64;

    [Header("Camera View Test")]
    [Tooltip("타깃이 실제 화면(뷰포트) 안에 있을 때만 후보로 인정")]
    public bool requireOnScreen = true;
    public Camera viewCamera;                 // 비우면 Camera.main
    [Range(0f, 0.2f)] public float viewportMargin = 0.05f; // 가장자리 여유

    [Header("LOS (Line of Sight) From Player (or Custom)")]
    [Tooltip("LOS를 쏠 시작점(보통 플레이어). 비우면 origin 사용")]
    public Transform losOrigin;

    

    [Tooltip("LOS Ray에 사용할 레이어마스크(0이면 candidate|occluder 사용)")]
    public LayerMask losMaskOverride = 0;

    // 결과
    public IReadOnlyList<Transform> Candidates => _candidates;
    readonly List<Transform> _candidates = new List<Transform>(64);

    // 내부 버퍼
    Collider[] _hits;
    float _timer;

    void Reset()
    {
        origin = transform;
        losOrigin = origin;
        radius = 20f;
        nonAllocCapacity = 64;
        sampleInterval = 0.1f;
        triggerInteraction = QueryTriggerInteraction.Ignore;
    }

    void OnEnable()
    {
        EnsureBuffer();
        ForceScan();
    }

    void OnValidate()
    {
        EnsureBuffer();
        nonAllocCapacity = Mathf.Max(8, nonAllocCapacity);
    }

    void Update()
    {
        if (sampleInterval <= 0f) Scan();
        else
        {
            _timer += Time.deltaTime;
            if (_timer >= sampleInterval)
            {
                _timer = 0f;
                Scan();
            }
        }
    }

    void EnsureBuffer()
    {
        if (_hits == null || _hits.Length != nonAllocCapacity)
            _hits = new Collider[nonAllocCapacity];
    }

    /// <summary>즉시 한 번 스캔</summary>
    public void ForceScan()
    {
        _timer = 0f;
        Scan();
    }

    void Scan()
    {
        _candidates.Clear();

        var o = origin ? origin : transform;
        var losStartT = losOrigin ? losOrigin : o;

        int count = Physics.OverlapSphereNonAlloc(
            o.position, radius, _hits, candidateLayer, triggerInteraction);

        var cam = viewCamera != null ? viewCamera : Camera.main;

        for (int i = 0; i < count; i++)
        {
            var col = _hits[i];
            if (!col) continue;
            var t = col.transform;
            if (t == o) continue;

            float dist = Vector3.Distance(o.position, t.position);

            // A) 카메라 뷰(옵션)
            if (requireOnScreen && cam)
            {
                if (!IsInCameraView(cam, t.position, viewportMargin))
                    continue;
            }

            // B) 가림(옵션; origin 기준)
            if (doOcclusionCheck)
            {
                var dir = (t.position - o.position);
                float len = dir.magnitude;
                if (len > 0.0001f)
                {
                    if (Physics.Raycast(o.position, dir / len, out var hit, len, occluderMask, triggerInteraction))
                        continue; // 벽/지형에 가려짐
                }
            }

            
            if (!IsDirectLineOfSightFrom(losStartT.position, t))
                continue;
            

            _candidates.Add(t);
            Debug.Log($"[LockOnDetector] candidate: {t.name}  dist={dist:F1}m");
        }
    }

    // ───────────── Helpers ─────────────

    static bool IsInCameraView(Camera cam, Vector3 worldPos, float margin = 0f)
    {
        Vector3 vp = cam.WorldToViewportPoint(worldPos);
        if (vp.z <= 0f) return false; // 카메라 뒤
        float m = Mathf.Clamp01(margin);
        return (vp.x >= 0f + m && vp.x <= 1f - m &&
                vp.y >= 0f + m && vp.y <= 1f - m);
    }

    /// <summary>
    /// startPos에서 타깃까지 Ray를 쐈을 때 '첫 히트'가 타깃 루트인가?
    /// (레이어마스크: losMaskOverride가 0이면 candidate|occluder 사용)
    /// </summary>
    bool IsDirectLineOfSightFrom(Vector3 startPos, Transform target)
    {
        Vector3 dir = target.position - startPos;
        float len = dir.magnitude;
        if (len <= 0.0001f) return true;

        int mask = (losMaskOverride != 0) ? (int)losMaskOverride
                                          : (int)(candidateLayer | occluderMask);

        if (Physics.Raycast(startPos, dir / len, out var hit, len, mask, triggerInteraction))
        {
            Transform hitRoot = hit.rigidbody ? hit.rigidbody.transform.root
                                              : hit.collider.transform.root;
            return hitRoot == target.root;
        }
        return false;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        var o = origin ? origin : transform;
        var pos = o.position;

        // 구형 볼륨
        Gizmos.color = new Color(0f, 0.75f, 1f, 0.7f);
        Gizmos.DrawWireSphere(pos, radius);

        // 후보 라인/마커
        if (_candidates != null)
        {
            foreach (var c in _candidates)
            {
                if (!c) continue;
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pos, c.position);
                Gizmos.DrawWireSphere(c.position, 0.25f);
            }
        }

        // 반투명 구 시각화(선택)
        Handles.color = new Color(0f, 0.75f, 1f, 0.08f);
        Handles.SphereHandleCap(0, pos, Quaternion.identity, radius * 2f, EventType.Repaint);
    }
#endif
}
