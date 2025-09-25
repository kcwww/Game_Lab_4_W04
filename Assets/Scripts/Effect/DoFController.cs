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

    [Header("Focal Length Pulse")]
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

        // baseFocalLength를 프로파일 현재값으로 동기화(인스펙터 값이 0일 때 안전)
        if (baseFocalLength <= 0f) baseFocalLength = Mathf.Max(1f, dof.focalLength.value);
        dof.focalLength.value = baseFocalLength;
    }

    void Update()
    {
        if (player == null || mainCamera == null || dof == null) return;

        Vector3 camPos = mainCamera.transform.position;
        Vector3 toPlayer = player.position - camPos;
        float forwardDist = Mathf.Max(0f, Vector3.Dot(toPlayer, mainCamera.transform.forward));

        float targetFocus = Mathf.Clamp(forwardDist, focusDistanceClamp.x, focusDistanceClamp.y);

        if (focusLerpSpeed > 0f)
            currentFocusDistance = Mathf.Lerp(currentFocusDistance, targetFocus, Time.deltaTime * focusLerpSpeed);
        else
            currentFocusDistance = targetFocus;

        dof.focusDistance.value = currentFocusDistance;
    }

    // ---- 펄스 실행 API ----
    public void PulseFocalLength()
    {
        if (!EnsureDepthOfField(out dof)) return;
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(CoPulseFocalLength(pulseTargetFocalLength, pulseUpDuration, baseFocalLength, pulseDownDuration));
    }

    public void PulseFocalLength(float target, float upTime, float backValue, float downTime)
    {
        if (!EnsureDepthOfField(out dof)) return;
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(CoPulseFocalLength(target, upTime, backValue, downTime));
    }

    IEnumerator CoPulseFocalLength(float target, float upTime, float backValue, float downTime)
    {
        dof.focalLength.overrideState = true;
        dof.aperture.overrideState = true;

        float start = dof.focalLength.value;
        float t = 0f;

        // (선택) 펄스 시작 시 조리개도 얕은 심도로
        float originalAperture = dof.aperture.value;
        if (pulseAperture > 0f) dof.aperture.value = pulseAperture;

        // 올라가기
        while (t < 1f)
        {
            t += (upTime <= 0f ? 1f : Time.deltaTime / upTime);
            dof.focalLength.value = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        // 내려오기
        t = 0f;
        start = dof.focalLength.value;
        while (t < 1f)
        {
            t += (downTime <= 0f ? 1f : Time.deltaTime / downTime);
            dof.focalLength.value = Mathf.Lerp(start, backValue, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t)));
            yield return null;
        }

        // 원래/기본 상태로 정리
        dof.focalLength.value = backValue;
        if (pulseAperture > 0f) dof.aperture.value = originalAperture;

        pulseCo = null;
    }

    // ---- ContextMenu 테스트 ----
    [ContextMenu("Test: Pulse Focal Length 300 -> base")]
    void TestPulse()
    {
        globalVolume.weight = 1f;
        PulseFocalLength();
        Debug.Log($"✅ Pulse started: {pulseTargetFocalLength}mm → {baseFocalLength}mm");
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
