using UnityEngine;
using Unity.Cinemachine;

public class LockOnOrchestrator : MonoBehaviour
{
    [Header("Refs")]
    public LockOnDetector detector;
    public LockOnSelector selector;
    public LockOnTargetGroupBinder binder;
    public CinemachineCamera tpsCam;
    public CinemachineCamera lockOnCam;
    public CameraDirectionFix cameraDirectionFix;

    [Header("Priorities")]
    public int priorityTPSActive = 10;
    public int priorityLockOnIdle = 5;
    public int priorityLockOnActive = 20;

    [Header("Auto Unlock (Logic)")]
    [Tooltip("타깃 끊겼을 때 완전 해제까지의 논리 유예(초)")]
    public float lostGraceSeconds = 0.35f;

    [Header("Blend Guard (On enter)")]
    [Tooltip("락온 진입 중 카메라 블렌드 보호 시간(초)")]
    public float blendGuardSeconds = 0.4f;
    [Tooltip("블렌드 가드 동안 requireOnScreen 완화")]
    public bool relaxOnScreenDuringBlend = true;

    [Header("Quick Visual Fallback (On loss)")]
    [Tooltip("타깃 상실 시 카메라만 먼저 TPS로 빠르게 복귀할지")]
    public bool quickCameraFallbackOnLoss = true;
    [Tooltip("카메라만 먼저 TPS로 돌리는 짧은 시각 유예(초)")]
    public float visualGraceSeconds = 0.08f;

    float _lostTimer;
    bool _lockOnMode;        // 락온 유지 중?
    float _blendGuardTimer;  // 진입 보호
    bool _didVisualFallback; // 이번 상실 사이클에서 카메라만 먼저 돌렸는가
    float _visualTimer;      // 시각 유예 타이머
    bool _prevRequireOnScreen;

    void Start()
    {
        if (tpsCam) tpsCam.Priority = priorityTPSActive;
        if (lockOnCam) lockOnCam.Priority = priorityLockOnIdle;
    }

    void Update()
    {
        // ── 블렌드 가드 ──
        if (_blendGuardTimer > 0f)
        {
            _blendGuardTimer -= Time.deltaTime;
            if (_blendGuardTimer <= 0f)
            {
                if (selector) selector.ClearForcedLock();
                if (relaxOnScreenDuringBlend && detector)
                    detector.requireOnScreen = _prevRequireOnScreen;
            }
        }

        // ── 락온 유지 중 상실 처리 ──
        if (_lockOnMode && selector != null && selector.lockOnActive)
        {
            bool lost = (_blendGuardTimer <= 0f) && (selector.CurrentTarget == null);

            if (lost)
            {
                // (1) 시각 유예: 아주 빠르게 TPS로 복귀
                if (quickCameraFallbackOnLoss && !_didVisualFallback)
                {
                    _visualTimer += Time.deltaTime;
                    if (_visualTimer >= Mathf.Max(0f, visualGraceSeconds))
                    {
                        // 카메라만 먼저 TPS로 복귀
                        SwitchCameraToTPS();
                        _didVisualFallback = true;
                    }
                }

                // (2) 논리 유예: 그래도 복구 못하면 완전 해제
                _lostTimer += Time.deltaTime;
                if (_lostTimer >= Mathf.Max(visualGraceSeconds, lostGraceSeconds))
                {
                    OnLockOnReleased(); // 완전 해제(논리)
                }
            }
            else
            {
                // 타깃이 다시 생김 → 유예/플래그 리셋
                _lostTimer = 0f;
                _visualTimer = 0f;
                if (_didVisualFallback)
                {
                    // 시각적으로 TPS로 내려갔었다면, 다시 LockOn 카메라로 복귀
                    SwitchCameraToLockOn();
                    _didVisualFallback = false;
                }
            }
        }
    }

    // ───────────────── 입력 이벤트에서 호출 ─────────────────

    public void OnLockOnPressed()
    {
        if (detector) detector.ForceScan();
        if (selector) selector.SetLockOnActive(true);

        var target = (selector != null) ? selector.CurrentTarget : null;
        if (target == null)
        {
            // 후보 없음: 스냅만
            if (selector) selector.SetLockOnActive(false);
            if (cameraDirectionFix) cameraDirectionFix.OnLockOnDirection();

            SwitchCameraToTPS();

            _lockOnMode = false;
            _lostTimer = 0f;
            _visualTimer = 0f;
            _didVisualFallback = false;
            _blendGuardTimer = 0f;
            return;
        }

        // 타깃 존재: 블렌드 가드(핀 고정)
        if (selector) selector.ForceLockOnTarget(target);
        if (relaxOnScreenDuringBlend && detector)
        {
            _prevRequireOnScreen = detector.requireOnScreen;
            detector.requireOnScreen = false;
        }
        _blendGuardTimer = blendGuardSeconds;

        if (cameraDirectionFix) cameraDirectionFix.OnLockOffDirection();

        SwitchCameraToLockOn();

        _lockOnMode = true;
        _lostTimer = 0f;
        _visualTimer = 0f;
        _didVisualFallback = false;
    }

    public void OnLockOnReleased()
    {
        if (selector)
        {
            selector.ClearForcedLock();
            selector.SetLockOnActive(false);
        }
        if (relaxOnScreenDuringBlend && detector)
            detector.requireOnScreen = _prevRequireOnScreen;

        SwitchCameraToTPS();

        if (cameraDirectionFix) cameraDirectionFix.OnLockOffDirection();

        _lockOnMode = false;
        _lostTimer = 0f;
        _visualTimer = 0f;
        _didVisualFallback = false;
        _blendGuardTimer = 0f;
    }

    // ── 카메라 스위치 유틸 ──
    void SwitchCameraToTPS()
    {
        if (lockOnCam) lockOnCam.Priority = priorityLockOnIdle;
        if (tpsCam) tpsCam.Priority = priorityTPSActive;
    }

    void SwitchCameraToLockOn()
    {
        if (lockOnCam) lockOnCam.Priority = priorityLockOnActive;
        if (tpsCam) tpsCam.Priority = priorityTPSActive - 1;
    }
}
