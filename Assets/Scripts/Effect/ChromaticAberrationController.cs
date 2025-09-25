using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

[DisallowMultipleComponent]
public class ChromaticAberrationController : MonoBehaviour
{
    [Header("References")]
    public Volume volume;   // URP Global/Local Volume

    [Header("Pulse Settings")]
    [Tooltip("기본 강도(복귀값). 보통 0")]
    [Range(0f, 1f)] public float baseIntensity = 0f;

    [Tooltip("펄스 목표 강도")]
    [Range(0f, 1f)] public float pulseTargetIntensity = 0.3f;

    [Tooltip("총 펄스 시간(0→1→0). 절반은 상승, 절반은 하강")]
    [Min(0f)] public float totalDuration = 2f;

    [Tooltip("애니메이션 커브(0→1→0 형태 권장)")]
    public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1f);

    [Tooltip("커브 후반 0.5~1 구간을 1→0으로 자동 미러링할지")]
    public bool autoMirrorCurve = true;

    [Tooltip("Time.timeScale의 영향을 무시하고 진행")]
    public bool unscaledTime = true;

    ChromaticAberration ca;
    Coroutine pulseCo;

    void Awake()
    {
        if (!EnsureChromatic(out ca))
        {
            Debug.LogError("❌ Volume Profile에 Chromatic Aberration가 없습니다.");
            enabled = false;
            return;
        }

        // 시작값 정렬
        ca.intensity.value = Mathf.Clamp01(baseIntensity);
    }

    // ---------- Public API ----------
    /// <summary>기본 설정(totalDuration, pulseTargetIntensity)로 1회 펄스</summary>
    public void Pulse()
        => Pulse(pulseTargetIntensity, totalDuration);

    /// <summary>원하는 목표/시간으로 1회 펄스</summary>
    public void Pulse(float target, float duration)
    {
        if (!EnsureChromatic(out ca)) return;
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(CoPulse(target, duration));
    }

    /// <summary>즉시 세팅</summary>
    public void SetIntensity(float intensity)
    {
        if (!EnsureChromatic(out ca)) return;
        ca.intensity.overrideState = true;
        ca.intensity.value = Mathf.Clamp01(intensity);
    }

    // ---------- Core ----------
    IEnumerator CoPulse(float target, float duration)
    {
        ca.intensity.overrideState = true;

        // 커브 준비(0→1→0)
        AnimationCurve curve = pulseCurve;
        if (autoMirrorCurve)
        {
            // 0~0.5는 원 커브, 0.5~1.0은 미러(1→0)
            Keyframe start = new Keyframe(0f, 0f);
            Keyframe mid = new Keyframe(0.5f, 1f);
            Keyframe end = new Keyframe(1f, 0f);

            // 사용자가 제공한 커브를 전반부로 샘플하고, 후반부는 미러
            curve = new AnimationCurve(start, mid, end);
        }

        float t = 0f;
        float baseVal = Mathf.Clamp01(baseIntensity);
        float targetVal = Mathf.Clamp01(target);
        duration = Mathf.Max(0.0001f, duration);

        while (t < 1f)
        {
            float dt = unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            t += dt / duration;

            float shaped;
            if (autoMirrorCurve)
            {
                // 0~0.5 : 0→1, 0.5~1 : 1→0 로 매핑
                if (t <= 0.5f)
                    shaped = Mathf.SmoothStep(0f, 1f, t / 0.5f);
                else
                    shaped = Mathf.SmoothStep(1f, 0f, (t - 0.5f) / 0.5f);
            }
            else
            {
                shaped = Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(t)));
            }

            ca.intensity.value = Mathf.Lerp(baseVal, targetVal, shaped);
            yield return null;
        }

        ca.intensity.value = baseVal;   // 복귀
        pulseCo = null;
    }

    // ---------- Utility ----------
    bool EnsureChromatic(out ChromaticAberration chroma)
    {
        chroma = null;
        if (volume == null) return false;

        var profile = volume.profile;
        if (profile == null)
        {
            if (volume.sharedProfile != null)
            {
                profile = Instantiate(volume.sharedProfile);
                volume.profile = profile;
            }
            else
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                volume.profile = profile;
            }
        }

        if (!profile.TryGet(out chroma))
            chroma = profile.Add<ChromaticAberration>(true);

        chroma.intensity.overrideState = true;
        return chroma != null;
    }

    // ---------- Editor Test ----------
    [ContextMenu("Test: Pulse Chromatic (0 → target → 0)")]
    void TestPulse()
    {
        Pulse(pulseTargetIntensity, totalDuration);
        Debug.Log($"✅ Chromatic Aberration pulse: 0 → {pulseTargetIntensity} → 0 over {totalDuration:F2}s");
    }

    [ContextMenu("Test: Set Intensity 0.3 (instant)")]
    void TestSetInstant()
    {
        SetIntensity(0.3f);
        Debug.Log("✅ Chromatic Aberration intensity = 0.3 (instant)");
    }
}
