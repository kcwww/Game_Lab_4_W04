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

    // ★ 추가: Laser 카메라
    [Header("Laser Camera (optional)")]
    public CinemachineCamera laserCam;

    [Header("Priorities")]
    public int priorityTPSActive = 10;
    public int priorityLockOnIdle = 5;
    public int priorityLockOnActive = 20;
    // ★ 추가: 레이저 우선순위는 가장 높게
    public int priorityLaserActive = 30;

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
    float _blendGuardTimer;   // 진입 보호
    bool _didVisualFallback; // 이번 상실 사이클에서 카메라만 먼저 돌렸는가
    float _visualTimer;       // 시각 유예 타이머
    bool _prevRequireOnScreen;

    public bool isLockOn { get; private set; } = false; // 현재 락온 상태인지

    // ★ 추가: Laser 오버라이드 상태
    bool _laserOverrideActive = false;
    float _laserRemain = 0f;  // 0보다 크면 타이머 중(자동 해제용)

    enum ViewMode { TPS, LockOn, Laser }
    ViewMode _viewMode = ViewMode.TPS;

    void Start()
    {
        if (tpsCam) tpsCam.Priority = priorityTPSActive;
        if (lockOnCam) lockOnCam.Priority = priorityLockOnIdle;
        if (laserCam) laserCam.Priority = priorityLockOnIdle - 1; // 가장 낮게 시작
        _viewMode = ViewMode.TPS;
    }

    void Update()
    {
        // ── Laser 오버라이드가 켜져 있으면 우선 처리 ──
        if (_laserOverrideActive)
        {
            if (_laserRemain > 0f)
            {
                _laserRemain -= Time.deltaTime;
                if (_laserRemain <= 0f)
                {
                    // 자동 복귀
                    DeactivateLaserCamera();
                }
            }
            // Laser 중엔 나머지 락온 유지/상실 로직 무시
            return;
        }

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
                        SwitchCameraToTPS();
                        _didVisualFallback = true;
                        _viewMode = ViewMode.TPS;
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
                    SwitchCameraToLockOn();
                    _didVisualFallback = false;
                    _viewMode = ViewMode.LockOn;
                }
            }
        }
    }

    // ───────────────── 입력 이벤트에서 호출 ─────────────────

    public void OnLockOnPressed()
    {
        // Laser 중이면 무시(패턴 우선)
        if (_laserOverrideActive) return;

        if (detector) detector.ForceScan();
        if (selector) selector.SetLockOnActive(true);

        var target = (selector != null) ? selector.CurrentTarget : null;
        if (target == null)
        {
            // 후보 없음: 스냅만
            if (selector) selector.SetLockOnActive(false);
            if (cameraDirectionFix) cameraDirectionFix.OnLockOnDirection();

            SwitchCameraToTPS();
            _viewMode = ViewMode.TPS;

            _lockOnMode = false;
            isLockOn = false;
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
        _viewMode = ViewMode.LockOn;

        _lockOnMode = true;
        isLockOn = true;
        _lostTimer = 0f;
        _visualTimer = 0f;
        _didVisualFallback = false;
    }

    public void OnLockOnReleased()
    {
        Debug.Log("45545");
        // Laser 중이면 무시(패턴이 끝날 때 Deactivate에서 정리)
        if (_laserOverrideActive) return;

        if (selector)
        {
            Debug.Log("455");
            selector.ClearForcedLock();
            selector.SetLockOnActive(false);
        }
        if (relaxOnScreenDuringBlend && detector)
            detector.requireOnScreen = _prevRequireOnScreen;

        SwitchCameraToTPS();
        _viewMode = ViewMode.TPS;

        Debug.Log("123");
        if (cameraDirectionFix) cameraDirectionFix.OnLockOffDirection();

        _lockOnMode = false;
        isLockOn = false;
        _lostTimer = 0f;
        _visualTimer = 0f;
        _didVisualFallback = false;
        _blendGuardTimer = 0f;
    }

    // ── 카메라 스위치 유틸 ──
    void SwitchCameraToTPS()
    {
        if (laserCam) laserCam.Priority = priorityLockOnIdle - 1;
        if (lockOnCam) lockOnCam.Priority = priorityLockOnIdle;
        if (tpsCam) tpsCam.Priority = priorityTPSActive;
    }

    void SwitchCameraToLockOn()
    {
        if (laserCam) laserCam.Priority = priorityLockOnIdle - 1;
        if (lockOnCam) lockOnCam.Priority = priorityLockOnActive;
        if (tpsCam) tpsCam.Priority = priorityTPSActive - 1;
    }

    // ★ 추가: Laser 카메라로 전환(외부에서 호출)
    // durationSec > 0 이면 자동 해제; <=0 이면 수동 Deactivate까지 유지
    public void ActivateLaserCamera(float durationSec = 0f)
    {
        if (!laserCam)
        {
            Debug.LogWarning("[LockOnOrchestrator] Laser camera is not assigned.");
            return;
        }

        // 우선순위 스위치
        if (laserCam) laserCam.Priority = priorityLaserActive;
        if (lockOnCam) lockOnCam.Priority = priorityLockOnIdle;
        if (tpsCam) tpsCam.Priority = priorityLockOnIdle;

        // 상태 플래그
        _laserOverrideActive = true;
        _laserRemain = Mathf.Max(0f, durationSec);
        _viewMode = ViewMode.Laser;

        // 락온 UI/논리는 잠시 멈춤(선택: 유지해도 됨)
        // 시각적으로 즉시 전환되도록 가드/플래그 초기화
        _didVisualFallback = false;
        _blendGuardTimer = 0f;
        _lostTimer = 0f;
        _visualTimer = 0f;
    }

    // ★ 추가: Laser 카메라 해제(외부에서 호출 또는 자동)
    public void DeactivateLaserCamera()
    {
        if (!_laserOverrideActive) return;

        _laserOverrideActive = false;
        _laserRemain = 0f;

        // 항상 TPS로 복귀
        SwitchCameraToTPS();
        _viewMode = ViewMode.TPS;

        // (선택) 락온 상태도 강제 해제하고 싶다면 주석 해제
        if (selector)
        {
            selector.ClearForcedLock();
            selector.SetLockOnActive(false);
        }
        _lockOnMode = false;
        isLockOn = false;

        // (선택)내부 타이머 / 플래그도 리셋
        _lostTimer = 0f;
        _visualTimer = 0f;
        _didVisualFallback = false;
        _blendGuardTimer = 0f;
    }


    // ★ 추가: 외부에서 특정 카메라를 강제 활성화하고 싶을 때(범용)
    public void SwitchToCamera(CinemachineCamera cam, int activePriority)
    {
        // 전부 낮춘 뒤 주어진 cam만 올려주는 간단한 유틸
        if (tpsCam) tpsCam.Priority = priorityLockOnIdle;
        if (lockOnCam) lockOnCam.Priority = priorityLockOnIdle;
        if (laserCam) laserCam.Priority = priorityLockOnIdle - 1;

        if (cam) cam.Priority = activePriority;
    }
}
