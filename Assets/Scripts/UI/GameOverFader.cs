using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;


public class GameOverFader : MonoBehaviour
{
    [Header("Refs")]
    public CanvasGroup overlay;      // Overlay 오브젝트의 CanvasGroup
    public Image black;              // Black Image (선택)
    public TMP_Text title;           // "GAME OVER"
    public CanvasGroup buttons;      // 버튼들 (선택)

    [Header("Settings")]
    public float fadeDuration = 2f;
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0, 1,1);
    public bool pauseOnDone = true;          // 완료 시 Time.timeScale=0
    public float titleFadeDuration = 2f;
    public float buttonsFadeDuration = 2f;

    bool _busy;

    [ContextMenu("Init Overlay")]
    public void ShowGameOver()
    {
        if (!_busy)
        {
            StartCoroutine(CoShow());
            
        }

    }

    [ContextMenu("Hide Overlay")]
    public void HideGameOver()
    {
        if (!_busy) StartCoroutine(CoHide());
    }

    IEnumerator CoShow()
    {
        _busy = true;
        float t = 0f;
        overlay.blocksRaycasts = true;   // UI 클릭/입력 차단
        overlay.interactable = false;

        // 타이틀 처음엔 살짝 축소 + 투명
        if (title) title.alpha = 0f;

        // 페이드 인 (언스케일드)
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = ease.Evaluate(Mathf.Clamp01(t / fadeDuration));
            overlay.alpha = a;
            yield return null;
        }
        overlay.alpha = 1f;

        
        if (title)
        {
            // 나타나기
            float tt = 0f;
            while (tt < titleFadeDuration)
            {
                tt += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(tt / titleFadeDuration);
                title.alpha = k;
                yield return null;
            }
        }

        if (pauseOnDone) Time.timeScale = 0f; // 게임 정지
        _busy = false;

        // 1초 대기 후 로드씬
        yield return new WaitForSecondsRealtime(1f);
        SceneManager.LoadScene(0);
    }

    IEnumerator CoHide()
    {
        _busy = true;
        Time.timeScale = 1f; // 재개
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.unscaledDeltaTime;
            float a = ease.Evaluate(1f - Mathf.Clamp01(t / fadeDuration));
            overlay.alpha = a;
            yield return null;
        }
        overlay.alpha = 0f;
        overlay.blocksRaycasts = false;
        _busy = false;
    }
}
