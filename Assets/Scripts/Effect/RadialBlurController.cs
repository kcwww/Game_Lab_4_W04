using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
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
    [Range(0.0f, 1.0f)] public float blurWidth = 0.10f;     // 퍼짐 정도
    [Range(1, 32)] public int sampleCount = 12;        // 품질

    [Header("Blend Strength (Final)")]
    [Range(0f, 1f)] public float blurStrength = 0.0f;     // 평상시 0

    [Header("Radius Mask")]
    [Range(0f, 1f)] public float radius = 0.35f;
    [Range(0f, 0.5f)] public float feather = 0.10f;
    public bool invertCenterBlur = false;

    [Header("Editor Pulse (for testing)")]
    [Tooltip("Pulse의 목표 강도(절대값). 예: 0.8")]
    [Range(0f, 1f)] public float editorPeakStrength = 0.8f;
    [Tooltip("Pulse 전체 지속 시간(초)")]
    public float editorDuration = 0.4f;

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

        // 셰이더 파라미터
        radialBlurMat.SetVector("_Center", center);
        radialBlurMat.SetFloat("_CenterOffsetY", centerOffsetY);

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

    [ContextMenu("Pulse (Editor)")]
    void PulseEditor() => PulseAbsolute(editorPeakStrength, editorDuration);

    /// <summary>
    /// blurStrength를 절대 강도(0~1)까지 올렸다가 원래 값으로 되돌리는 Pulse.
    /// </summary>
    public void PulseAbsolute(float peakStrength, float duration)
    {
        StopAllCoroutines();
        StartCoroutine(PulseAbsoluteRoutine(Mathf.Clamp01(peakStrength), Mathf.Max(0.01f, duration)));
    }

    System.Collections.IEnumerator PulseAbsoluteRoutine(float peakStrength, float duration)
    {
        float start = blurStrength;   // 현재 강도(평소 0)
        float half = duration * 0.5f;

        // Up: start -> peak
        float t = 0f;
        while (t < half)
        {
            t += Application.isPlaying ? Time.deltaTime : (1f / 60f);
            float k = Mathf.Sin(Mathf.Clamp01(t / half) * (Mathf.PI * 0.5f)); // easeOutSine
            blurStrength = Mathf.Lerp(start, peakStrength, k);
            Apply();
            yield return null;
        }

        // Down: peak -> start
        t = 0f;
        while (t < half)
        {
            t += Application.isPlaying ? Time.deltaTime : (1f / 60f);
            float k = 1f - Mathf.Cos(Mathf.Clamp01(t / half) * (Mathf.PI * 0.5f)); // easeInSine
            blurStrength = Mathf.Lerp(peakStrength, start, k);
            Apply();
            yield return null;
        }

        blurStrength = start; // 복원
        Apply();
    }
}
