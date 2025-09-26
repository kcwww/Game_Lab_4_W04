using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// - LockOnDetector의 Candidates를 점수화해서 Top1을 선택
/// - 탐지 상태(비락온): 흰색 화살표 / LockOn 활성: 빨간색 화살표
/// - 이전 타깃 스티키/스위치 히스테리시스로 튐 방지
/// 외부 입력:
///   - lockOnActive: 플레이어가 락온 버튼을 눌러 활성화/해제
///   - (선택) 강제 락온 타깃 지정 API 제공
/// </summary>
public class LockOnSelector : MonoBehaviour
{
    [Header("Refs")]
    public LockOnDetector detector;
    public Camera viewCamera; // 비우면 Camera.main

    [Header("Scoring Weights")]
    [Range(0f, 1f)] public float wCenter = 0.7f;
    [Range(0f, 1f)] public float wDistance = 0.3f;
    [Tooltip("이전 타깃일 때 가산점")]
    [Range(0f, 1f)] public float stickyBonus = 0.12f;

    [Header("Switch Hysteresis")]
    [Tooltip("현재 점수 + 임계 이상일 때만 타깃 전환")]
    [Range(0f, 0.3f)] public float switchThreshold = 0.05f;
    [Tooltip("전환 후 최소 유지 시간(초)")]
    [Range(0f, 1f)] public float switchCooldown = 0.2f;

    [Header("Indicator Colors")]
    public Color detectedColor = Color.white;          // 탐지 상태(비락온)
    public Color lockedColor = Color.red;            // 락온 상태

    [Header("State (drive this from Player Input)")]
    public bool lockOnActive = false;                  // 외부에서 on/off
    public Transform lockedTargetOverride;             // (선택) 외부가 고정시킬 타깃

    // Runtime
    Transform _currentTarget;
    float _lastSwitchTime;
    Dictionary<Transform, LockableTarget> _cache = new();

    void LateUpdate()
    {
        if (detector == null) return;

        // 우선 카메라 참조 갱신
        var cam = viewCamera != null ? viewCamera : (detector.viewCamera != null ? detector.viewCamera : Camera.main);

        // 후보가 하나도 없으면 클리어
        var candidates = detector.Candidates;
        if (candidates == null || candidates.Count == 0)
        {
            SetTarget(null, cam);
            return;
        }

        // 외부에서 강제 고정 요청이 있으면 우선
        if (lockedTargetOverride != null)
        {
            SetTarget(lockedTargetOverride, cam);
            return;
        }

        // 점수화: 화면 중앙 근접 + 거리 역가중 + 스티키 + 타깃 바이어스
        Transform best = null;
        float bestScore = float.NegativeInfinity;

        foreach (var t in candidates)
        {
            if (!t) continue;
            var lt = GetLockable(t);
            if (lt == null || !lt.isLockable) continue;

            var aim = lt.AimPointOrSelf().position;

            // 화면 중앙 근접도 (0..1)
            float centerScore = 0f;
            if (cam != null)
            {
                var dir = (aim - cam.transform.position).normalized;
                centerScore = Mathf.Clamp01(Vector3.Dot(cam.transform.forward, dir) * 0.5f + 0.5f);
            }

            // 거리 역가중 (0..1 근사)
            float dist = Vector3.Distance(transform.position, aim);
            float distScore = 1f / (1f + dist); // 가까울수록 1에 근접

            // 스티키
            float sticky = (t == _currentTarget) ? stickyBonus : 0f;

            // 타깃 자체 바이어스(보스 등)
            float bias = lt.priorityBias;

            float score = wCenter * centerScore + wDistance * distScore + sticky + bias;

            // 히스테리시스: 쿨다운 동안은 지금 타깃을 쉽게 내리지 않음
            if (Time.time < _lastSwitchTime + switchCooldown && _currentTarget != null && t != _currentTarget)
            {
                // 상대 후보의 스코어를 살짝 낮춰보는 효과 (부드럽게 유지)
                score -= switchThreshold * 0.5f;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = t;
            }
        }

        // 전환 임계 충족 시에만 바꿈
        if (_currentTarget == null)
        {
            SetTarget(best, cam);
        }
        else if (best != null && best != _currentTarget)
        {
            // 새 후보가 현 점수 + 임계보다 충분히 높을 때만 스위치
            float currentScore = ScoreOf(_currentTarget, cam);
            if (bestScore >= currentScore + switchThreshold)
                SetTarget(best, cam);
            else
                RefreshVisual(_currentTarget, cam); // 색만 갱신
        }
        else
        {
            RefreshVisual(_currentTarget, cam);
        }
    }

    // 현재 타깃 변경 + 화살표 on/off
    void SetTarget(Transform newTarget, Camera cam)
    {
        if (_currentTarget == newTarget)
        {
            RefreshVisual(_currentTarget, cam);
            return;
        }

        // 이전 타깃 끄기
        if (_currentTarget != null)
        {
            var prevLT = GetLockable(_currentTarget);
            if (prevLT != null) prevLT.SetArrowActive(false);
        }

        _currentTarget = newTarget;
        _lastSwitchTime = Time.time;

        // 새 타깃 켜기
        if (_currentTarget != null)
        {
            var lt = GetLockable(_currentTarget);
            if (lt != null)
            {
                lt.SetArrowActive(true);
                RefreshVisual(_currentTarget, cam);
            }
        }
    }

    // 락온 상태에 따라 색 갱신(빨강/흰색)
    void RefreshVisual(Transform t, Camera cam)
    {
        if (t == null) return;
        var lt = GetLockable(t);
        if (lt == null) return;

        var col = lockOnActive ? lockedColor : detectedColor;
        lt.SetArrowColor(col);
    }

    float ScoreOf(Transform t, Camera cam)
    {
        if (t == null) return float.NegativeInfinity;
        var lt = GetLockable(t);
        if (lt == null || !lt.isLockable) return float.NegativeInfinity;

        var aim = lt.AimPointOrSelf().position;

        float centerScore = 0f;
        if (cam != null)
        {
            var dir = (aim - cam.transform.position).normalized;
            centerScore = Mathf.Clamp01(Vector3.Dot(cam.transform.forward, dir) * 0.5f + 0.5f);
        }

        float dist = Vector3.Distance(transform.position, aim);
        float distScore = 1f / (1f + dist);
        float sticky = (t == _currentTarget) ? stickyBonus : 0f;

        return wCenter * centerScore + wDistance * distScore + sticky + lt.priorityBias;
    }

    LockableTarget GetLockable(Transform t)
    {
        if (!_cache.TryGetValue(t, out var lt) || lt == null)
        {
            lt = t.GetComponentInParent<LockableTarget>();
            _cache[t] = lt;
        }
        return lt;
    }

    // 외부에서 현재 선택된 타깃을 읽고 싶을 때
    public Transform CurrentTarget => _currentTarget;

    // 외부에서 강제로 락온 시작/해제 + 현재 선택 고정
    public void SetLockOnActive(bool on)
    {
        lockOnActive = on;
        // 색 즉시 갱신
        RefreshVisual(_currentTarget, viewCamera != null ? viewCamera : Camera.main);
    }

    public void ForceLockOnTarget(Transform t)
    {
        lockedTargetOverride = t;
        lockOnActive = true;
    }

    public void ClearForcedLock()
    {
        lockedTargetOverride = null;
    }
}
