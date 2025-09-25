using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

public class DoFController : MonoBehaviour
{
    [Header("References")]
    public Transform player;        // 플레이어 트랜스폼
    public Camera mainCamera;       // 메인 카메라
    public Volume globalVolume;     // Global Volume (URP Volume)

    [Header("Focus Settings")]
    [Range(0f, 20f)] public float focusLerpSpeed = 10f;
    public Vector2 focusDistanceClamp = new Vector2(0.1f, 100f);

    [Header("Focal Length Pulse (Durations)")]
    [Tooltip("기본 초점거리(mm). 펄스 후 복귀값")]
    public float baseFocalLength = 85f;
    [Tooltip("펄스 타깃 초점거리(mm)")]
    public float pulseTargetFocalLength = 300f;
    [Tooltip("타깃까지 가는 시간(초)")]
    public float pulseUpDuration = 0.5f;
    [Tooltip("기본값으로 돌아오는 시간(초)")]
    public float pulseDownDuration = 0.5f;
    [Tooltip("펄스 중 조리개(선택). <=0이면 조절 안함")]
    public float pulseAperture = 2.0f;

    [Header("Focal Length Pulse (Curve)")]
    [Tooltip("커브 기반 펄스 사용 (totalDuration + curve)")]
    public bool useCurvePulse = false;
    [Tooltip("총 펄스 시간(0→1→0). 커브 기반일 때 사용")]
    [Min(0f)] public float pulseTotalDuration = 1.0f;
    [Tooltip("0→1→0 형태 권장. autoMirrorCurve=On이면 후반은 자동 반전")]
    public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1f);
    [Tooltip("커브 후반 0.5~1 구간을 1→0으로 자동 미러링")]
    public bool autoMirrorCurve = true;

    [Header("Time Scale")]
    [Tooltip("Time.timeScale의 영향을 무시하고 진행(초점 추적 + 펄스 둘 다)")]
    public bool unscaledTime = true;

    DepthOfField dof;
    float currentFocusDistance;
    Coroutine pulseCo;

    void Awake()
    {
        if (!EnsureDepthOfField(out dof))
        {
            Debug.LogError("❌ Global Volume의 Profile에 DepthOfField가 없습니다.");
            enabled = false;
            return;
        }

        dof.mode.value = DepthOfFieldMode.Bokeh;

        // 시작값들 초기화
        currentFocusDistance = Mathf.Clamp(5f, focusDistanceClamp.x, focusDistanceClamp.y);
        dof.focusDistance.value = currentFocusDistance;

        if (baseFocalLength <= 0f) baseFocalLength = Mathf.Max(1f, dof.focalLength.value);
        dof.focalLength.value = baseFocalLength;
    }

    void Update()
    {
        if (player == null || mainCamera == null || dof == null) return;

        float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        Vector3 camPos = mainCamera.transform.position;
        Vector3 toPlayer = player.position - camPos;
        float forwardDist = Mathf.Max(0f, Vector3.Dot(toPlayer, mainCamera.transform.forward));
        float targetFocus = Mathf.Clamp(forwardDist, focusDistanceClamp.x, focusDistanceClamp.y);

        if (focusLerpSpeed > 0f)
            currentFocusDistance = Mathf.Lerp(currentFocusDistance, targetFocus, dt * focusLerpSpeed);
        else
            currentFocusDistance = targetFocus;

        dof.focusDistance.value = currentFocusDistance;
    }

    // ---- 펄스 실행 API ----
    public void PulseFocalLength()
    {
        if (!EnsureDepthOfField(out dof)) return;
        if (pulseCo != null) StopCoroutine(pulseCo);

        if (useCurvePulse)
            pulseCo = StartCoroutine(CoPulseFocalLengthCurve(pulseTargetFocalLength, pulseTotalDuration, baseFocalLength));
        else
            pulseCo = StartCoroutine(CoPulseFocalLength(pulseTargetFocalLength, pulseUpDuration, baseFocalLength, pulseDownDuration));
    }

    public void PulseFocalLength(float target, float upTime, float backValue, float downTime)
    {
        if (!EnsureDepthOfField(out dof)) return;
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(CoPulseFocalLength(target, upTime, backValue, downTime));
    }

    public void PulseFocalLengthCurve(float target, float totalDuration, float backValue)
    {
        if (!EnsureDepthOfField(out dof)) return;
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(CoPulseFocalLengthCurve(target, totalDuration, backValue));
    }

    // ---- 기존: up/down 기간 기반 펄스 ----
    IEnumerator CoPulseFocalLength(float target, float upTime, float backValue, float downTime)
    {
        dof.focalLength.overrideState = true;
        dof.aperture.overrideState = true;

        float start = dof.focalLength.value;
        float t = 0f;

        float originalAperture = dof.aperture.value;
        if (pulseAperture > 0f) dof.aperture.value = pulseAperture;

        // 올라가기
        while (t < 1f)
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += (upTime <= 0f ? 1f : dt / Mathf.Max(0.0001f, upTime));
            dof.focalLength.value = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        // 내려오기
        t = 0f;
        start = dof.focalLength.value;
        while (t < 1f)
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += (downTime <= 0f ? 1f : dt / Mathf.Max(0.0001f, downTime));
            dof.focalLength.value = Mathf.Lerp(start, backValue, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        dof.focalLength.value = backValue;
        if (pulseAperture > 0f) dof.aperture.value = originalAperture;
        pulseCo = null;
    }

    // ---- 신규: 커브 기반(0→1→0) 펄스 ----
    IEnumerator CoPulseFocalLengthCurve(float target, float totalDuration, float backValue)
    {
        dof.focalLength.overrideState = true;
        dof.aperture.overrideState = true;

        float baseVal = backValue;                          // 복귀 기준
        float startVal = dof.focalLength.value;             // 시작 시점 값(보통 base와 동일)
        float originalAperture = dof.aperture.value;
        if (pulseAperture > 0f) dof.aperture.value = pulseAperture;

        float t = 0f;
        totalDuration = Mathf.Max(0.0001f, totalDuration);

        while (t < 1f)
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt / totalDuration;

            float shaped;
            if (autoMirrorCurve)
            {
                // 0~0.5: 0→1, 0.5~1: 1→0
                if (t <= 0.5f)
                    shaped = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                else
                    shaped = Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
            }
            else
            {
                shaped = Mathf.Clamp01(pulseCurve.Evaluate(Mathf.Clamp01(t)));
            }

            // base↔target 사이를 커브로 보간
            dof.focalLength.value = Mathf.Lerp(baseVal, target, shaped);
            yield return null;
        }

        // 종료 정리
        dof.focalLength.value = backValue;
        if (pulseAperture > 0f) dof.aperture.value = originalAperture;
        pulseCo = null;
    }

    // ---- ContextMenu 테스트 ----
    [ContextMenu("Test: Pulse (Durations) 300 -> base")]
    void TestPulse_Durations()
    {
        globalVolume.weight = 1f;
        useCurvePulse = false;
        PulseFocalLength();
        Debug.Log($"✅ Duration Pulse: {pulseTargetFocalLength}mm → {baseFocalLength}mm (unscaledTime={unscaledTime})");
    }

    [ContextMenu("Test: Pulse (Curve) 300 -> base")]
    void TestPulse_Curve()
    {
        globalVolume.weight = 1f;
        useCurvePulse = true;
        PulseFocalLength();
        Debug.Log($"✅ Curve Pulse: {pulseTargetFocalLength}mm → {baseFocalLength}mm over {pulseTotalDuration:F2}s (autoMirror={autoMirrorCurve}, unscaledTime={unscaledTime})");
    }

    // ---- 공용 유틸 ----
    bool EnsureDepthOfField(out DepthOfField depthOfField)
    {
        depthOfField = null;
        if (globalVolume == null) { Debug.LogError("Global Volume 참조 없음"); return false; }

        var profile = globalVolume.profile;
        if (profile == null)
        {
            if (globalVolume.sharedProfile != null)
            {
                profile = Instantiate(globalVolume.sharedProfile);
                globalVolume.profile = profile;
            }
            else
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                globalVolume.profile = profile;
            }
        }

        if (!profile.TryGet(out depthOfField))
            depthOfField = profile.Add<DepthOfField>(true);

        depthOfField.mode.overrideState = true;
        depthOfField.mode.value = DepthOfFieldMode.Bokeh;

        depthOfField.focusDistance.overrideState = true;
        depthOfField.focalLength.overrideState = true;
        depthOfField.aperture.overrideState = true;

        globalVolume.priority = Mathf.Max(globalVolume.priority, 100f);
        return true;
    }
}
