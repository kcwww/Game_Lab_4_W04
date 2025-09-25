using UnityEngine;

[CreateAssetMenu(fileName = "PostFXPreset", menuName = "PostFX/Preset", order = 1000)]
public class PostFXPreset : ScriptableObject
{
    [Header("Master Time / Curve")]
    [Tooltip("Time.timeScale 무시(모든 컨트롤러에 전달)")]
    public bool unscaledTime = true;

    [Tooltip("커브 기반 펄스 사용(0→1→0). DoF/Blur 커브 모드, Chromatic은 내부 커브/미러 사용")]
    public bool useCurvePulse = true;

    [Min(0.01f)]
    [Tooltip("총 펄스 시간(0→1→0)")]
    public float totalDuration = 0.6f;

    [Tooltip("0→1→0 권장. autoMirrorCurve가 켜져 있으면 후반 1→0 자동 반전")]
    public AnimationCurve masterCurve = AnimationCurve.EaseInOut(0, 0, 0.5f, 1f);

    [Tooltip("커브 후반(0.5~1) 자동 반전")]
    public bool autoMirrorCurve = true;

    [Header("Enable / Disable Each Effect")]
    public bool enableChromatic = true;
    public bool enableDoF = true;
    public bool enableRadialBlur = true;

    [Header("Chromatic (Peak Intensity)")]
    [Range(0f, 1f)] public float chromaPeakIntensity = 0.30f;
    [Tooltip("시작 지연(ms)")] public int chromaStaggerMs = 0;

    [Header("Depth of Field (Focal Length)")]
    [Tooltip("펄스 타깃 초점거리(mm)")]
    public float dofTargetFocalLength = 300f;
    [Tooltip("복귀 초점거리(mm). 0 이하면 DoFController.baseFocalLength 사용")]
    public float dofBackFocalLength = 0f;
    [Tooltip("시작 지연(ms)")] public int dofStaggerMs = 0;

    [Header("Radial Blur (Peak Strength)")]
    [Range(0f, 1f)] public float radialPeakStrength = 0.80f;
    [Tooltip("시작 지연(ms)")] public int radialStaggerMs = 0;
}
