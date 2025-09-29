using System.Collections;
using UnityEngine;
using Unity.Cinemachine;

public class TPSCameraPivotAimCue : MonoBehaviour
{
    [Header("Refs")]
    public CinemachineCamera tpsCam;           // TPS 가상카메라
    public Transform defaultLookAt;            // 평소 LookAt(보통 플레이어의 머리/aim). 비우면 시작 시 tpsCam.LookAt를 캐시

    [Header("Options")]
    public bool snapAxesOnEnter = true;        // 시작 시 카메라 축을 목표 쪽으로 즉시 맞춤(OrbitalFollow 축)
    public float holdSecondsDefault = 0.6f;    // 유지 시간 기본값
    public bool keepUpdatingIfPivotMoves = true; // 피벗이 움직이면 매 프레임 추적

    // ==== 옵션/기본값 추가 ====
    [Header("Timing")]
    public float moveSecondsDefault = 0.6f;  // 타겟까지 부드럽게 도는 시간
    public float returnSecondsDefault = 0.4f;  // 원래 LookAt으로 복귀하는 시간

    [Header("Handoff")]
    public float handoffDistance = 0.15f;       // 이 거리 이하면 LookAt 원본으로 인계

    [Header("Origin Rotation Blend")]
    // 옵션: origin(원래 LookAt)의 회전을 타겟 방향으로 맞출지
    public bool applyOriginRotationOnReturn = true;
    public bool originRotationFlatOnly = true;   // Yaw만 맞추기(권장)
    public float originRotationLerpSeconds = 0.15f; // 부드럽게 맞추는 시간


    CinemachineOrbitalFollow _orbital;         // 축 스냅용(있으면 사용)
    Transform _tempAnchor;                     // 월드 포인트용 임시 앵커
    Coroutine _running;
    Transform _cachedOriginalLookAt;           // 복구용 캐시

    void Awake()
    {
        if (!tpsCam) tpsCam = FindFirstObjectByType<CinemachineCamera>();
        if (tpsCam)
        {
            _orbital = tpsCam.GetComponent<CinemachineOrbitalFollow>();
            if (!defaultLookAt && tpsCam.LookAt != null)
                defaultLookAt = tpsCam.LookAt; // 초기 LookAt을 기본값으로 캐시
        }
    }

    ///// <summary>월드 좌표를 잠깐 바라보게.</summary>
    //public void AimAtPoint(Vector3 worldPoint, float? holdSeconds = null)
    //{
    //    if (!tpsCam) return;

    //    if (_running != null) StopCoroutine(_running);
    //    if (_tempAnchor == null)
    //    {
    //        var go = new GameObject("~TPS_PivotAimAnchor");
    //        go.hideFlags = HideFlags.HideAndDontSave;
    //        _tempAnchor = go.transform;
    //    }
    //    _tempAnchor.position = worldPoint;

    //    ApplyAim(_tempAnchor, holdSeconds ?? holdSecondsDefault);
    //}

    /// <summary>피벗 트랜스폼을 잠깐 바라보게.</summary>
    public void AimAtTransform(Transform pivot, float? holdSeconds = null)
    {
        if (!pivot || !tpsCam) return;

        if (_running != null) StopCoroutine(_running);
        if (_tempAnchor == null)
        {
            var go = new GameObject("~TPS_PivotAimAnchor");
            go.hideFlags = HideFlags.HideAndDontSave;
            _tempAnchor = go.transform;
        }
        _tempAnchor.position = pivot.position;

        ApplyAim(_tempAnchor, holdSeconds ?? holdSecondsDefault);

        if (keepUpdatingIfPivotMoves)
            StartCoroutine(FollowPivotWhileActive(pivot));
    }

    void ApplyAim(Transform aimTarget, float holdSeconds)
    {
        if (!tpsCam) return;

        // 축 스냅(있을 때만)
        if (snapAxesOnEnter && _orbital)
        {
            var camPos = tpsCam.transform.position;
            Vector3 to = aimTarget.position - camPos;

            // yaw (수평)
            Vector3 flat = to; flat.y = 0f;
            if (flat.sqrMagnitude > 1e-6f)
            {
                float yaw = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                _orbital.HorizontalAxis.Value = yaw;
            }

            // pitch (필요하면 유지; 세팅 범위는 OrbitalFollow의 VerticalAxis에 따름)
            if (to.sqrMagnitude > 1e-6f)
            {
                float pitch = -Mathf.Asin(Vector3.Dot(to.normalized, Vector3.down)) * Mathf.Rad2Deg;
                _orbital.VerticalAxis.Value = pitch;
            }
        }

        // LookAt 스왑 + 복구 예약
        _cachedOriginalLookAt = tpsCam.LookAt != null ? tpsCam.LookAt : defaultLookAt;
        tpsCam.LookAt = aimTarget;
        _running = StartCoroutine(RestoreAfter(holdSeconds));
    }

    IEnumerator RestoreAfter(float seconds)
    {
        float t = Mathf.Max(0f, seconds);
        while (t > 0f)
        {
            t -= Time.deltaTime;
            yield return null;
        }
        // 원래 LookAt으로 복원(없으면 defaultLookAt 시도)
        if (tpsCam)
            tpsCam.LookAt = _cachedOriginalLookAt ? _cachedOriginalLookAt : defaultLookAt;

        _running = null;
    }

    IEnumerator FollowPivotWhileActive(Transform pivot)
    {
        while (tpsCam && tpsCam.LookAt == _tempAnchor && pivot)
        {
            _tempAnchor.position = pivot.position;
            yield return null;
        }
    }


    public void AimAtPoint(
    Vector3 worldPoint,
    float? moveSeconds = null,
    float? holdSeconds = null,
    float? returnSeconds = null)
    {
        if (!tpsCam) return;

        if (_running != null) StopCoroutine(_running);
        if (_tempAnchor == null)
        {
            var go = new GameObject("~TPS_PivotAimAnchor");
            go.hideFlags = HideFlags.HideAndDontSave;
            _tempAnchor = go.transform;
        }

        float moveTime = Mathf.Max(0f, moveSeconds ?? moveSecondsDefault);
        float holdTime = Mathf.Max(0f, holdSeconds ?? holdSecondsDefault);
        float retTime = Mathf.Max(0f, returnSeconds ?? returnSecondsDefault);

        // 시작점: 현재 카메라 forward 선상에서 target까지의 거리만큼 앞
        Vector3 camPos = tpsCam.transform.position;
        float dist = Vector3.Distance(camPos, worldPoint);
        Vector3 start = camPos + tpsCam.transform.forward * Mathf.Max(0.1f, dist);

        _tempAnchor.position = start;

        // 원래 LookAt 캐시 후 임시 앵커로 교체
        _cachedOriginalLookAt = tpsCam.LookAt != null ? tpsCam.LookAt : defaultLookAt;
        tpsCam.LookAt = _tempAnchor;

        _running = StartCoroutine(Co_SmoothAimHoldAndReturn(worldPoint, moveTime, holdTime, retTime));
    }

    IEnumerator Co_SmoothAimHoldAndReturn(Vector3 targetPoint, float moveSeconds, float holdSeconds, float returnSeconds)
    {
        float Ease(float x) => x * x * (3f - 2f * x); // smoothstep

        // 1) 타겟으로 부드럽게 회전(=임시 앵커 위치 보간)
        Vector3 s = _tempAnchor.position;
        float t = 0f;
        if (moveSeconds <= 0f)
        {
            _tempAnchor.position = targetPoint;
        }
        else
        {
            while (t < moveSeconds)
            {
                t += Time.deltaTime;
                float u = Mathf.Clamp01(t / moveSeconds);
                _tempAnchor.position = Vector3.LerpUnclamped(s, targetPoint, Ease(u));
                yield return null;
            }
            _tempAnchor.position = targetPoint;
        }

        // 1.5) 타겟에서 머무르기(선택)
        if (holdSeconds > 0f)
            yield return new WaitForSeconds(holdSeconds);

        // (A) 복귀 직전: 원래 LookAt 트랜스폼의 회전을 타겟 방향으로 맞춤(옵션)
        if (applyOriginRotationOnReturn && (_cachedOriginalLookAt || defaultLookAt))
        {
            Transform orig = _cachedOriginalLookAt ? _cachedOriginalLookAt : defaultLookAt;

            Vector3 desiredDir = targetPoint - orig.position;
            if (originRotationFlatOnly)
            {
                desiredDir.y = 0f;                            // Yaw만
                if (desiredDir.sqrMagnitude < 1e-6f)
                    desiredDir = (tpsCam.transform.forward * 0.001f) + Vector3.forward;
            }
            desiredDir.Normalize();

            Quaternion rotFrom = orig.rotation;
            Quaternion rotTo = (desiredDir.sqrMagnitude > 0f)
                ? Quaternion.LookRotation(desiredDir, Vector3.up)
                : rotFrom;

            float rTime = Mathf.Max(0f, originRotationLerpSeconds);
            if (rTime <= 0f) { orig.rotation = rotTo; }
            else
            {
                float rt = 0f;
                while (rt < rTime)
                {
                    rt += Time.deltaTime;
                    float u = Mathf.Clamp01(rt / rTime);
                    float e = Ease(u);
                    orig.rotation = Quaternion.Slerp(rotFrom, rotTo, e);
                    yield return null;
                }
                orig.rotation = rotTo;
            }
        }

        // 2) 원래 LookAt 위치로 부드럽게 복귀(원본이 움직여도 추적)
        if (_cachedOriginalLookAt || defaultLookAt)
        {
            Transform orig = _cachedOriginalLookAt ? _cachedOriginalLookAt : defaultLookAt;

            Vector3 start = _tempAnchor.position;
            t = 0f;

            if (returnSeconds <= 0f)
            {
                _tempAnchor.position = orig.position;
            }
            else
            {
                while (t < returnSeconds)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Clamp01(t / returnSeconds);

                    Vector3 dynTarget = orig.position;
                    _tempAnchor.position = Vector3.LerpUnclamped(start, dynTarget, Ease(u));

                    if (Vector3.Distance(_tempAnchor.position, dynTarget) <= handoffDistance)
                        break;

                    yield return null;
                }
                _tempAnchor.position = orig.position;
            }

            // 3) 인계: LookAt을 원래 트랜스폼으로 복원
            if (tpsCam) tpsCam.LookAt = orig;
        }

        _running = null;
    }



}
