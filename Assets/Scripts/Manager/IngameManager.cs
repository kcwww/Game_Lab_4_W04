using System.Collections;
using System.Security.Cryptography;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class IngameManager : MonoBehaviour
{
    public static IngameManager Instance { get; private set; }

    [Header("HpUI")]
    [SerializeField] private Slider playerHpSlider;
    [SerializeField] private Slider bossHpSlider;
    [SerializeField] private GameObject bossNameObject;

    public int playerHp { get; private set; }
    public int bossHp { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        bossHpSlider.value = 0;
        playerHpSlider.value = 1;
    }

    private void Start()
    {
        Invoke("PlayBossUI", 2f); // 5초뒤에 실행
    }

    // Boss UI 페이드 실행
    public void PlayBossUI()
    {
        StartCoroutine(SliderFadeIn());
    }

    private IEnumerator SliderFadeIn()
    {
        yield return null;

        float startValue = 0;
        float endValue = 1;
        float t = 0f;
        float d = 0.5f;
        AnimationCurve curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        bossHpSlider.gameObject.SetActive(true);

        while(t < d)
        {
            t += Time.deltaTime;

            float u = Mathf.Clamp01(t / d);       // 0→1로 진행률 계산
            float k = Mathf.Clamp01(curve.Evaluate(u));

            bossHpSlider.value = Mathf.Lerp(startValue, endValue, k);
            yield return null;
        }

        bossNameObject.gameObject.SetActive(true);
    }
}
