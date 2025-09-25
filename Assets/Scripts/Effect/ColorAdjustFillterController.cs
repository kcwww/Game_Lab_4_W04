using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Collections;

[DisallowMultipleComponent]
public class ColorAdjustFilterController : MonoBehaviour
{
    [Header("URP Volume")]
    public Volume globalVolume;

    [Header("Time / Curve")]
    public bool unscaledTime = true;
    public bool useCurvePulse = true;
    [Min(0.01f)] public float pulseTotalDuration = 0.18f;
    public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1);
    public bool autoMirrorCurve = true;

    [Header("Affect White Balance")]
    public bool affectWhiteBalance = true;

    // 내부 캐시
    ColorAdjustments _colorAdj;
    WhiteBalance _whiteBal;
    Coroutine pulseCo;

    // ---------- Public API ----------
    public void SetEnabled(bool on)
    {
        if (!EnsureColor(out var ca)) return;

        ca.active = on;
        ca.postExposure.overrideState = on;
        ca.colorFilter.overrideState = on;
        ca.saturation.overrideState = on;

        if (affectWhiteBalance && EnsureWB(out var wb))
        {
            wb.active = on;
            wb.temperature.overrideState = on;
            wb.tint.overrideState = on;
        }
    }

    public void SetValues(float postExposure, Color colorFilter, float saturation, float? wbTemperature = null, float? wbTint = null)
    {
        if (!EnsureColor(out var ca)) return;

        ca.postExposure.value = postExposure;
        ca.colorFilter.value = colorFilter;
        ca.saturation.value = saturation;

        if (affectWhiteBalance && EnsureWB(out var wb))
        {
            if (wbTemperature.HasValue) wb.temperature.value = wbTemperature.Value;
            if (wbTint.HasValue) wb.tint.value = wbTint.Value;
        }
    }

    /// <summary>
    /// 0→1→0 펄스 (delta/target 기반)
    /// </summary>
    public void PulseDeltas(float exposureDelta, Color colorFilterTarget, float saturationDelta, float wbTempDelta = 0f, float wbTintDelta = 0f, float? totalDuration = null)
    {
        if (pulseCo != null) StopCoroutine(pulseCo);
        float T = totalDuration ?? pulseTotalDuration;
        pulseCo = StartCoroutine(CoPulse(exposureDelta, colorFilterTarget, saturationDelta, wbTempDelta, wbTintDelta, T));
    }

    IEnumerator CoPulse(float expDelta, Color cfTarget, float satDelta, float wbTempDelta, float wbTintDelta, float totalDuration)
    {
        if (!EnsureColor(out var ca)) yield break;
        EnsureWB(out var wb); // optional

        // 보장: 켜기 및 override on
        SetEnabled(true);

        float T = Mathf.Max(0.0001f, totalDuration);
        float t = 0f;

        // base snapshot
        float baseExp = ca.postExposure.value;
        Color baseCF = ca.colorFilter.value;
        float baseSat = ca.saturation.value;

        float baseTemp = (wb != null ? wb.temperature.value : 0f);
        float baseTint = (wb != null ? wb.tint.value : 0f);

        while (t < T)
        {
            float dt = Application.isPlaying ? (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) : (1f / 60f);
            t += dt;
            float x = Mathf.Clamp01(t / T);

            float shaped;
            if (useCurvePulse)
            {
                shaped = autoMirrorCurve
                    ? (x <= 0.5f ? Mathf.SmoothStep(0f, 1f, x / 0.5f) : Mathf.SmoothStep(1f, 0f, (x - 0.5f) / 0.5f))
                    : Mathf.Clamp01(pulseCurve.Evaluate(x));
            }
            else
            {
                shaped = (x <= 0.5f ? Mathf.SmoothStep(0f, 1f, x / 0.5f) : Mathf.SmoothStep(1f, 0f, (x - 0.5f) / 0.5f));
            }

            ca.postExposure.value = baseExp + expDelta * shaped;
            ca.colorFilter.value = Color.Lerp(baseCF, cfTarget, shaped);
            ca.saturation.value = baseSat + satDelta * shaped;

            if (affectWhiteBalance && wb != null)
            {
                wb.temperature.value = baseTemp + wbTempDelta * shaped;
                wb.tint.value = baseTint + wbTintDelta * shaped;
            }

            yield return null;
        }

        // restore
        ca.postExposure.value = baseExp;
        ca.colorFilter.value = baseCF;
        ca.saturation.value = baseSat;

        if (affectWhiteBalance && wb != null)
        {
            wb.temperature.value = baseTemp;
            wb.tint.value = baseTint;
        }

        pulseCo = null;
    }

    // ===== 즉시 피크 → 감쇠(1→0) 전용 API =====
    public void PulseFlashDecay(
        float exposureDelta,
        Color colorFilterTarget,
        float saturationDelta,
        float wbTempDelta = 0f,
        float wbTintDelta = 0f,
        float? decayDuration = null,
        bool linear = true,                 // true: 선형 1→0, false: SmoothStep 1→0
        AnimationCurve customDecay = null   // 선택: 커스텀 커브 사용(있으면 우선)
    )
    {
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(CoPulseFlashDecay(
            exposureDelta, colorFilterTarget, saturationDelta,
            wbTempDelta, wbTintDelta, decayDuration ?? pulseTotalDuration,
            linear, customDecay
        ));
    }

    IEnumerator CoPulseFlashDecay(
        float expDelta, Color cfTarget, float satDelta,
        float wbTempDelta, float wbTintDelta, float decayDuration,
        bool linear, AnimationCurve customDecay
    )
    {
        if (!EnsureColor(out var ca)) yield break;
        EnsureWB(out var wb); // optional
        SetEnabled(true);

        float T = Mathf.Max(0.0001f, decayDuration);

        // base snapshot
        float baseExp = ca.postExposure.value;
        Color baseCF = ca.colorFilter.value;
        float baseSat = ca.saturation.value;
        float baseTemp = (wb != null ? wb.temperature.value : 0f);
        float baseTint = (wb != null ? wb.tint.value : 0f);

        // 0) 즉시 피크 값으로 세팅
        ca.postExposure.value = baseExp + expDelta;
        ca.colorFilter.value = cfTarget;
        ca.saturation.value = baseSat + satDelta;
        if (affectWhiteBalance && wb != null)
        {
            wb.temperature.value = baseTemp + wbTempDelta;
            wb.tint.value = baseTint + wbTintDelta;
        }

        // 1) 1 → 0 감쇠
        float t = 0f;
        while (t < T)
        {
            float dt = Application.isPlaying ? (unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime) : (1f / 60f);
            t += dt;
            float x = Mathf.Clamp01(t / T);

            // shaped: 1→0
            float shaped;
            if (customDecay != null) shaped = Mathf.Clamp01(1f - customDecay.Evaluate(x));
            else if (!linear) shaped = Mathf.SmoothStep(1f, 0f, x);
            else shaped = 1f - x;

            ca.postExposure.value = baseExp + expDelta * shaped;
            // 감쇠 시에는 base↔target 사이를 shaped로 보간
            ca.colorFilter.value = Color.Lerp(baseCF, cfTarget, shaped);
            ca.saturation.value = baseSat + satDelta * shaped;

            if (affectWhiteBalance && wb != null)
            {
                wb.temperature.value = baseTemp + wbTempDelta * shaped;
                wb.tint.value = baseTint + wbTintDelta * shaped;
            }

            yield return null;
        }

        // 2) 복귀
        ca.postExposure.value = baseExp;
        ca.colorFilter.value = baseCF;
        ca.saturation.value = baseSat;
        if (affectWhiteBalance && wb != null)
        {
            wb.temperature.value = baseTemp;
            wb.tint.value = baseTint;
        }
        pulseCo = null;
    }

    // ---------- Ensure ----------
    bool EnsureColor(out ColorAdjustments ca)
    {
        ca = null;
        if (!globalVolume) return false;

        var profile = globalVolume.profile;
        if (!profile)
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

        if (_colorAdj == null && !profile.TryGet(out _colorAdj))
            _colorAdj = profile.Add<ColorAdjustments>(true);

        ca = _colorAdj;
        return ca != null;
    }

    bool EnsureWB(out WhiteBalance wb)
    {
        wb = null;
        if (!globalVolume) return false;
        var profile = globalVolume.profile;
        if (!profile) return false;

        if (_whiteBal == null && !profile.TryGet(out _whiteBal))
            _whiteBal = profile.Add<WhiteBalance>(true);

        wb = _whiteBal;
        return wb != null;
    }
}
