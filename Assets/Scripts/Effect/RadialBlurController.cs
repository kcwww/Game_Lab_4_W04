using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;

[ExecuteAlways]
[DisallowMultipleComponent]
public class RadialBlurController : MonoBehaviour
{
    [Header("Material (Hidden/URP/RadialZoomBlur_Simple)")]
    public Material radialBlurMat;

    [Header("Camera / Target")]
    public Camera targetCamera;
    public Transform focusTarget; // 플레이어 머리, 크로스헤어 등(없으면 0.5,0.5)

    [Header("Composition")]
    [Range(-0.2f, 0.2f)] public float centerOffsetY = 0.06f;

    [Header("Zoom Blur (Shadertoy style)")]
    public float blurStart = 1.0f;
    [Range(0.0f, 1.0f)] public float blurWidth = 0.10f;   // 퍼짐 정도
    [Range(1, 32)] public int sampleCount = 12;           // 품질

    [Header("Blend Strength (Final)")]
    [Range(0f, 1f)] public float blurStrength = 0.0f;     // 평상시 0

    [Header("Radius Mask")]
    [Range(0f, 1f)] public float radius = 0.35f;
    [Range(0f, 0.5f)] public float feather = 0.10f;
    public bool invertCenterBlur = false;

    [Header("Pulse (Curve Mode)")]
    [Tooltip("커브 기반 펄스 사용 (totalDuration + curve)")]
    public bool useCurvePulse = false;
    [Tooltip("총 펄스 시간(0→1→0)")]
    [Min(0f)] public float pulseTotalDuration = 0.4f;
    [Tooltip("0→1→0 형태 권장. autoMirrorCurve=On이면 후반 자동 반전")]
    public AnimationCurve pulseCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1f);
    [Tooltip("커브 후반 0.5~1 구간을 1→0으로 자동 미러링")]
    public bool autoMirrorCurve = true;

    [Header("Time Scale")]
    [Tooltip("Time.timeScale의 영향을 무시하고 진행(Update + 모든 펄스)")]
    public bool unscaledTime = true;

    [Header("Editor Pulse (for testing)")]
    [Tooltip("Pulse의 목표 강도(절대값). 예: 0.8")]
    [Range(0f, 1f)] public float editorPeakStrength = 0.8f;
    [Tooltip("Pulse 전체(또는 up+down) 지속 시간(초). useCurvePulse에 따라 의미 다름")]
    public float editorDuration = 0.4f;

    Coroutine pulseCo;

    void Reset() => targetCamera = Camera.main;
    void OnEnable() => Apply();
    void OnValidate() => Apply();
    void Update() => Apply();

    void Apply()
    {
        if (!radialBlurMat) return;

        // Center 계산
        Vector2 center = new Vector2(0.5f, 0.5f);
        if (targetCamera && focusTarget)
        {
            Vector3 vp = targetCamera.WorldToViewportPoint(focusTarget.position);
            center = new Vector2(vp.x, vp.y);
        }
        center.y += centerOffsetY;

        // 셰이더 파라미터
        radialBlurMat.SetVector("_Center", center);
        radialBlurMat.SetFloat("_BlurStart", blurStart);
        radialBlurMat.SetFloat("_BlurWidth", blurWidth);
        radialBlurMat.SetInt("_SampleCount", Mathf.Clamp(sampleCount, 1, 32));

        radialBlurMat.SetFloat("_Strength", Mathf.Clamp01(blurStrength));

        radialBlurMat.SetFloat("_Radius", Mathf.Clamp01(radius));
        radialBlurMat.SetFloat("_Feather", Mathf.Clamp(feather, 0f, 0.5f));
        radialBlurMat.SetFloat("_Invert", invertCenterBlur ? 1f : 0f);

#if UNITY_EDITOR
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
#endif
    }

    // ---------- Public API ----------
    [ContextMenu("Pulse (Editor)")]
    void PulseEditor()
    {
        if (useCurvePulse)
            PulseCurveAbsolute(editorPeakStrength, editorDuration);
        else
            PulseAbsolute(editorPeakStrength, editorDuration);
    }

    /// <summary>
    /// blurStrength를 절대 강도(0~1)까지 올렸다가 원래 값으로 되돌리는 펄스(이징 up/down).
    /// duration은 전체 시간(up+down).
    /// </summary>
    public void PulseAbsolute(float peakStrength, float duration)
    {
        StopPulseIfAny();
        StartCoroutine(CoPulseAbsolute(Mathf.Clamp01(peakStrength), Mathf.Max(0.01f, duration)));
    }

    /// <summary>
    /// 커브 기반(0→1→0) 절대 강도 펄스. totalDuration 동안 base↔peak 사이를 커브로 왕복.
    /// </summary>
    public void PulseCurveAbsolute(float peakStrength, float totalDuration)
    {
        StopPulseIfAny();
        StartCoroutine(CoPulseCurveAbsolute(Mathf.Clamp01(peakStrength), Mathf.Max(0.01f, totalDuration)));
    }

    // ---------- Coroutines ----------
    IEnumerator CoPulseAbsolute(float peakStrength, float duration)
    {
        float baseVal = blurStrength;     // 시작/복귀 기준
        float half = duration * 0.5f;

        // Up: base -> peak
        for (float t = 0f; t < half;)
        {
            float dt = GetDeltaTime();
            t += dt;
            float k = Mathf.Sin(Mathf.Clamp01(t / half) * (Mathf.PI * 0.5f)); // easeOutSine
            blurStrength = Mathf.Lerp(baseVal, peakStrength, k);
            Apply();
            yield return null;
        }

        // Down: peak -> base
        for (float t = 0f; t < half;)
        {
            float dt = GetDeltaTime();
            t += dt;
            float k = 1f - Mathf.Cos(Mathf.Clamp01(t / half) * (Mathf.PI * 0.5f)); // easeInSine
            blurStrength = Mathf.Lerp(peakStrength, baseVal, k);
            Apply();
            yield return null;
        }

        blurStrength = baseVal; // 복원
        Apply();
        pulseCo = null;
    }

    IEnumerator CoPulseCurveAbsolute(float peakStrength, float totalDuration)
    {
        float baseVal = blurStrength;     // 시작/복귀 기준
        float t = 0f;

        while (t < totalDuration)
        {
            float dt = GetDeltaTime();
            t += dt;
            float x = Mathf.Clamp01(t / totalDuration);

            float shaped;
            if (autoMirrorCurve)
            {
                // 0~0.5: 0→1, 0.5~1: 1→0 (부드럽게)
                if (x <= 0.5f) shaped = Mathf.SmoothStep(0f, 1f, x / 0.5f);
                else shaped = Mathf.SmoothStep(1f, 0f, (x - 0.5f) / 0.5f);
            }
            else
            {
                shaped = Mathf.Clamp01(pulseCurve.Evaluate(x));
            }

            blurStrength = Mathf.Lerp(baseVal, peakStrength, shaped);
            Apply();
            yield return null;
        }

        blurStrength = baseVal; // 복원
        Apply();
        pulseCo = null;
    }

    // ---------- Utility ----------
    float GetDeltaTime()
    {
        if (Application.isPlaying)
            return unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        // 에디터/씬뷰에서도 일정하게 진행되게 고정 스텝
        return 1f / 60f;
    }

    void StopPulseIfAny()
    {
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = null;
    }
}
