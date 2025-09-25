using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System.Collections;
using System.Collections.Generic;
using UnityEngine.VFX;


/// <summary>
/// ─ Three-in-one PostFX pulse coordinator with Presets & Stagger & Conflict Policy ─
/// Controls:
/// - ChromaticAberrationController
/// - DoFController
/// - RadialBlurController
/// 
/// Features:
/// - ScriptableObject Presets (PostFXPreset)
/// - Per-effect stagger (ms)
/// - Conflict policy: Overwrite / Ignore / Queue
/// </summary>
[DisallowMultipleComponent]
public class PostProcessingManager : MonoBehaviour
{
    public static PostProcessingManager Instance { get; private set; }

    public enum PulseConflictPolicy { Overwrite, Ignore, Queue }

    [Header("Controller References")]
    public ChromaticAberrationController chroma;   // optional
    public DoFController dof;                      // optional
    public RadialBlurController radial;            // optional

    [Header("Default Preset (Optional)")]
    public PostFXPreset defaultPreset;

    [Header("Conflict Policy")]
    public PulseConflictPolicy conflictPolicy = PulseConflictPolicy.Overwrite;

    // Internal state
    bool isPulsing;
    readonly Queue<PostFXPreset> presetQueue = new Queue<PostFXPreset>();
    Coroutine currentRoutine;
    Coroutine timeCoroutine;

    public VisualEffect parryVFX; // for editor preview
    public VisualEffect particleVFX;  // for hit effect

    [Header("Controllers (Add: ColorAdjust)")]
    public ColorAdjustFilterController colorCtrl; // optional


    // ───────────────────────── Public API ─────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }

    [ContextMenu("Pulse Default Preset")]
    public void PulseDefault()
    {
        parryVFX.transform.position = Player.Instance.followTarget.transform.position;
        //parryVFX.transform.rotation = Player.Instance.followTarget.transform.rotation;

        particleVFX.transform.position = Player.Instance.followTarget.transform.position;
        //particleVFX.transform.rotation = Player.Instance.followTarget.transform.rotation;

        if (defaultPreset == null)
        {
            Debug.LogWarning("No defaultPreset assigned.");
            return;
        }

        
        particleVFX.Stop();
        particleVFX.Reinit();
        particleVFX.Play();
        parryVFX.Play();

        if (timeCoroutine != null) StopCoroutine(timeCoroutine);
        timeCoroutine = StartCoroutine(TimeCoroutine());
        // temp
        // 바로 1초동안 돌아오게 코루틴

        PlayPreset(defaultPreset);
    }

    private IEnumerator TimeCoroutine()
    {
        Time.timeScale = 0.1f;

        yield return new WaitForSecondsRealtime(0.85f);

        float duration = 0.3f; // 0.1초만에 복구하고 싶다면
        float elapsed = 0f;
        float start = 0.5f;
        float end = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime; // timeScale 영향 안 받게
            Time.timeScale = Mathf.Lerp(start, end, elapsed / duration);
            yield return null;
        }

        Time.timeScale = 1f; // 보정
    }


    public void PlayPreset(PostFXPreset preset)
    {
        if (preset == null) return;

        switch (conflictPolicy)
        {
            case PulseConflictPolicy.Ignore:
                if (isPulsing) return;
                StartPresetNow(preset);
                break;

            case PulseConflictPolicy.Overwrite:
                StartPresetNow(preset, overwrite: true);
                break;

            case PulseConflictPolicy.Queue:
                if (isPulsing)
                {
                    presetQueue.Enqueue(preset);
                }
                else
                {
                    StartPresetNow(preset);
                }
                break;
        }
    }

    /// <summary>큐를 비움</summary>
    public void ClearQueue()
    {
        presetQueue.Clear();
    }

    // ───────────────────────── Core ─────────────────────────

    void StartPresetNow(PostFXPreset preset, bool overwrite = false)
    {
        if (overwrite && currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
            isPulsing = false;
        }

        currentRoutine = StartCoroutine(CoRunPreset(preset));
    }

    IEnumerator CoRunPreset(PostFXPreset p)
    {
        isPulsing = true;

        // 1) 공통 파라미터를 각 컨트롤러로 반영
        PropagateCommon(p);

        // 2) 효과별 스태거를 고려하여 개별 트리거
        //    (해당 효과가 꺼져있으면 스킵)
        float totalDuration = Mathf.Max(0.01f, p.totalDuration);

        // 세 효과를 병렬로 시작(스태거가 있으면 내부에서 대기 후 시작)
        var jobs = new List<Coroutine>(4); // 3 -> 4로

        if (p.enableChromatic && chroma)
            jobs.Add(StartCoroutine(CoStartChromaticAfterDelay(p)));

        if (p.enableDoF && dof)
            jobs.Add(StartCoroutine(CoStartDoFAfterDelay(p)));

        if (p.enableRadialBlur && radial)
            jobs.Add(StartCoroutine(CoStartRadialAfterDelay(p)));

        // ★ 추가: Color
        if (p.enableColorAdj && colorCtrl)
            jobs.Add(StartCoroutine(CoStartColorAfterDelay(p)));


        // 3) 완료 대기: preset 총 시간 + 최대 스태거(ms)
        float maxStagger = Mathf.Max(
            p.enableChromatic ? p.chromaStaggerMs : 0,
            p.enableDoF ? p.dofStaggerMs : 0,
            p.enableRadialBlur ? p.radialStaggerMs : 0,
            p.enableColorAdj ? p.colorStaggerMs : 0   // ★ 추가
        ) / 1000f;


        float wait = totalDuration + maxStagger;
        float waited = 0f;

        while (waited < wait)
        {
            float dt = p.unscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            waited += (Application.isPlaying ? dt : 1f / 60f);
            yield return null;
        }

        // 4) 정리
        foreach (var j in jobs)
        {
            if (j != null) StopCoroutine(j); // 안전 차단(대부분 이미 끝났을 것)
        }

        isPulsing = false;
        currentRoutine = null;

        // 5) 큐 정책이면 다음 프리셋 실행
        if (conflictPolicy == PulseConflictPolicy.Queue && presetQueue.Count > 0)
        {
            var next = presetQueue.Dequeue();
            StartPresetNow(next);
        }
    }

    void PropagateCommon(PostFXPreset p)
    {
        // Chromatic
        if (chroma)
        {
            chroma.unscaledTime = p.unscaledTime;
            chroma.autoMirrorCurve = p.autoMirrorCurve;
            chroma.pulseCurve = p.masterCurve;
            // baseIntensity는 컨트롤러 인스펙터에 맡김(필요 시 여기서도 조정 가능)
        }

        // DoF
        if (dof)
        {
            dof.unscaledTime = p.unscaledTime;
            dof.useCurvePulse = p.useCurvePulse;
            dof.autoMirrorCurve = p.autoMirrorCurve;
            dof.pulseCurve = p.masterCurve;
            dof.pulseTotalDuration = p.totalDuration;

            if (p.dofBackFocalLength > 0f)
                dof.baseFocalLength = p.dofBackFocalLength;

            dof.pulseTargetFocalLength = Mathf.Max(1f, p.dofTargetFocalLength);
        }

        // Radial
        if (radial)
        {
            radial.unscaledTime = p.unscaledTime;
            radial.useCurvePulse = p.useCurvePulse;
            radial.autoMirrorCurve = p.autoMirrorCurve;
            radial.pulseTotalDuration = p.totalDuration;
            radial.pulseCurve = p.masterCurve;
        }

        if (colorCtrl)
        {
            colorCtrl.unscaledTime = p.unscaledTime;
            colorCtrl.useCurvePulse = p.useCurvePulse;      // 동일 커브 정책 사용
            colorCtrl.autoMirrorCurve = p.autoMirrorCurve;
            colorCtrl.pulseCurve = p.masterCurve;
            colorCtrl.pulseTotalDuration = p.totalDuration;
            colorCtrl.affectWhiteBalance = p.colorAffectWB;
        }
    }

    // ───────────────────────── Effect Starters (with stagger) ─────────────────────────

    IEnumerator CoDelaySeconds(float seconds, bool unscaled)
    {
        if (seconds <= 0f) yield break;

        if (Application.isPlaying)
        {
            if (unscaled)
            {
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            else
            {
                // scaled
                float t = 0f;
                while (t < seconds)
                {
                    t += Time.deltaTime;
                    yield return null;
                }
            }
        }
        else
        {
            // 에디터/씬뷰 고정 스텝
            float t = 0f;
            while (t < seconds)
            {
                t += 1f / 60f;
                yield return null;
            }
        }
    }

    IEnumerator CoStartChromaticAfterDelay(PostFXPreset p)
    {
        yield return CoDelaySeconds(p.chromaStaggerMs / 1000f, p.unscaledTime);
        if (!chroma) yield break;
        chroma.Pulse(p.chromaPeakIntensity, p.totalDuration);
    }

    IEnumerator CoStartDoFAfterDelay(PostFXPreset p)
    {
        yield return CoDelaySeconds(p.dofStaggerMs / 1000f, p.unscaledTime);
        if (!dof) yield break;

        if (p.useCurvePulse)
        {
            dof.PulseFocalLengthCurve(
                target: p.dofTargetFocalLength,
                totalDuration: p.totalDuration,
                backValue: (p.dofBackFocalLength > 0f ? p.dofBackFocalLength : dof.baseFocalLength)
            );
        }
        else
        {
            float half = Mathf.Max(0.01f, p.totalDuration * 0.5f);
            dof.PulseFocalLength(
                target: p.dofTargetFocalLength,
                upTime: half,
                backValue: (p.dofBackFocalLength > 0f ? p.dofBackFocalLength : dof.baseFocalLength),
                downTime: half
            );
        }
    }

    IEnumerator CoStartRadialAfterDelay(PostFXPreset p)
    {
        yield return CoDelaySeconds(p.radialStaggerMs / 1000f, p.unscaledTime);
        if (!radial) yield break;

        if (p.useCurvePulse)
            radial.PulseCurveAbsolute(p.radialPeakStrength, p.totalDuration);
        else
            radial.PulseAbsolute(p.radialPeakStrength, p.totalDuration);
    }

    IEnumerator CoStartColorAfterDelay(PostFXPreset p)
    {
        // 지연 시작
        yield return CoDelaySeconds(p.colorStaggerMs / 1000f, p.unscaledTime);
        if (!colorCtrl) yield break;

        // 안전하게 켜기 (overrideState 보장)
        colorCtrl.SetEnabled(true);


        colorCtrl.PulseFlashDecay(
            p.colorExposureDelta,
            p.colorFilterTarget,
            p.colorSaturationDelta,
            p.colorAffectWB ? p.colorWBTempDelta : 0f,
            p.colorAffectWB ? p.colorWBTintDelta : 0f,
            p.totalDuration,
            linear: true
        );
    }


#if UNITY_EDITOR
    void OnValidate()
    {
        EditorApplication.QueuePlayerLoopUpdate();
        SceneView.RepaintAll();
    }
#endif
}
