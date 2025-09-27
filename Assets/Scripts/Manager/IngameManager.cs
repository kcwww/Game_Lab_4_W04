using System.Collections;
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

    [Header("Counter")]
    [SerializeField] private Sprite[] counterSprites;
    [SerializeField] private GameObject couterObject;
    [SerializeField] private Image counterImage;

    [Header("GameMode")]
    public bool isCoward { get; private set; } = true; // 기본 노말 모드

    public int playerHp { get; private set; }
    private const int maxPlayerHp = 10;
    public int bossHp { get; private set; }
    private const int maxBossHp = 100;

    private Coroutine sliderCoroutine;
    private Coroutine timeCoroutine;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        bossHpSlider.value = 0;
        playerHpSlider.value = 1;

        playerHp = maxPlayerHp;
        bossHp = maxBossHp;
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

    // 플레이어 피격
    public void DamagePlayer(int value)
    {
        InputManager.Instance.OnMotor(0f, 0.35f, 0.1f, 0.5f, 0.4f, 1f);

        playerHp -= value;

        if (playerHp < 0)
        {
            Debug.Log("게임 오버");
        }

        if(sliderCoroutine != null) StopCoroutine(sliderCoroutine);
        sliderCoroutine = StartCoroutine(LerpDamage(true));
    }

    // 플레이어인지 AI인지 구분
    public IEnumerator LerpDamage(bool isPlayer)
    {
        //yield return null;

        float targetValue;
        float sliderSpeed = 3f;

        if (isPlayer)
        {
            targetValue = playerHp / (float)maxPlayerHp;

            while (playerHpSlider.value != targetValue)
            {
                playerHpSlider.value = Mathf.MoveTowards(
                    playerHpSlider.value,
                    targetValue,
                    sliderSpeed * Time.deltaTime
                );
                yield return null;
            }
        }
        else
        {
            targetValue = bossHp / (float)maxBossHp;

            while (bossHpSlider.value != targetValue)
            {
                bossHpSlider.value = Mathf.MoveTowards(
                    bossHpSlider.value,
                    targetValue,
                    sliderSpeed * Time.deltaTime
                );
                yield return null;
            }
        }
    }

    public void CounterAttackOn()
    {
        couterObject.SetActive(true);

        if (InputManager.Instance.connectGamePad) counterImage.sprite = counterSprites[1];
        else counterImage.sprite = counterSprites[0];
    }

    public void CounterAttackOff()
    {
        couterObject.SetActive(false);
    }
    // 슬로우 모션 진행
    public void SlowTimer()
    {
        if (timeCoroutine != null) StopCoroutine(timeCoroutine);
        timeCoroutine = StartCoroutine(TimeCoroutine());
    }

    // 슬로우 초기화
    public void ResetTimer()
    {
        if (timeCoroutine != null) StopCoroutine(timeCoroutine);
        Time.timeScale = 1.0f;
    }

    private IEnumerator TimeCoroutine()
    {
        Time.timeScale = 0.1f;

        CounterAttackOn(); // 카운터 온
        yield return new WaitForSecondsRealtime(0.85f); // 기본 대기 시간
        CounterAttackOff(); // 카운터 오프

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
}
