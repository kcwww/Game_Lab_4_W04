using System.Collections;
using TMPro;
using UnityEngine;

public class Boss : MonoBehaviour//, IParrying
{
    public static Boss Instance { get; private set; }

    [Header("Compenent")]
    private Animator anim;
    private Rigidbody rb;
    private Transform target;
    [SerializeField] private LayerMask groundMask;
    [SerializeField] private TextMeshProUGUI aiText;

    [Header("Const")]
    private const string WalkAnim = "isWalk";
    private const string HorizontalAnim = "isHorizontal";
    private const string SmashAnim = "isSmash";
    private const string GuardAnim = "isGuard";
    private const string DashSmashAnim = "isDashSmash";
    private const string HorizontalText = "가로베기";
    private const string VerticalText = "섬광일도";

    [Header("Parrying")]
    public bool isParrying { get; private set; } = false; // 플레이어 패링 성공 여부
    private bool isParryingDamage = false; // 패링 데미지 들어온지 여부

    [Header("Attack")]
    private float attackRange = 10f; // 공격 탐지 거리
    private float curAttackTimer = 5f; // 현재 공격 쿨타임
    private float attackTimer = 5f; // 공격 쿨타임
    private bool isAttack = false; // 공격 중인지 체크

    private float horizontalSpeed = 15f; // 가로베기 이동 속도
    private bool isHorizontal = false; // 발도 체크
    private const float radiusRange = 2f; // 대시 범위 증감량
    private const float dashRadius = 5f; // 대시 기본 범위
    private float radius = 5f; // 기본 반지름 값
    private int patterCount = 2;

    [Header("Status")]
    private float speed = 5f;
    private float rotationSpeed = 5f;

    [Header("Particle")]
    [SerializeField] private GameObject slashParticle;
    [SerializeField] private GameObject hitParticle;
    [SerializeField] private GameObject xParticle;
    private const float slashTimer = 0.5f;


    private void Awake()
    {
        if(Instance == null) Instance = this;

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();
        aiText.text = ""; // 문구 비활성화
    }

    private void Start()
    {
        target = Player.Instance.transform;

        Player.Instance.CheckParringDistance += Player_CheckParringDistance;
        Player.Instance.OnParryingEnd += Player_EndParrying;
    }

    private void Player_EndParrying(object sender, System.EventArgs e)
    {
        isParryingDamage = false;
    }

    private void Player_CheckParringDistance(object sender, System.EventArgs e)
    {
        Player.Instance.AddEnemy(rb);
    }

    private void OnDisable()
    {
        Player.Instance.CheckParringDistance -= Player_CheckParringDistance;
        Player.Instance.OnParryingEnd -= Player_EndParrying;
    }

    private void FixedUpdate()
    {
        if (isParryingDamage) return;
        Move();
        Attack();
    }

    private void Update()
    {
        if(!isAttack)
        {
            curAttackTimer -= Time.deltaTime;
        }
    }

    private void Move()
    {
        if (isAttack) return; // 공격중 실행 x
        if (Time.timeScale != 1) return;

        // 1. 방향 계산
        Vector3 dir = (target.position - transform.position);

        if (dir.magnitude <= attackRange) return; // 공격 전환

        dir = dir.normalized;

        // 2. 이동
        anim.SetBool(WalkAnim, true);
        rb.MovePosition(rb.position + dir * speed * Time.fixedDeltaTime);

        // 3. 회전
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.fixedDeltaTime);
    }

    private void Attack()
    {
        if (isAttack) return;
        if (curAttackTimer > 0) return;
        isAttack = true; // 공격 진행

        int ran = Random.Range(0, patterCount);

        switch (ran)
        {
            case 0:
                HorizontalSmash();
                break;
            case 1:
                DashAttack();
                break;
        }

        curAttackTimer = attackTimer;
    }

    // 가로베기
    private void HorizontalSmash()
    {
        anim.SetBool(HorizontalAnim, true); // 발도 준비

        StartCoroutine(SmashCoroutine());
    }

    private IEnumerator SmashCoroutine()
    {
        // 1. 가로 베기 텍스트 및 이펙트 실행
        foreach(var v in HorizontalText)
        {
            aiText.text += v;
            yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(0.5f);
        aiText.text = "";

        anim.SetBool(HorizontalAnim, false);

        // 2. 방향 계산
        Vector3 dir = (target.position - transform.position).normalized;
        Vector3 startPos = rb.position;
        Vector3 endPos = target.position - dir*2; // 2 정도의 거리만큼 뒤에 도착

        // 3. 방향 고정 및 애니메이션 실행
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        anim.SetTrigger(SmashAnim);

        isParrying = true; // 패링 타격 받기
        float dashDuration = 0.3f; // 대시 시간
        float elapsed = 0f;

        while (elapsed < dashDuration && !isParryingDamage)
        {
            elapsed += Time.fixedDeltaTime;

            float t = elapsed / dashDuration;
            // 처음부터 끝까지 일정하게 빠르게 (쓸림 X, 급가속 X)
            rb.MovePosition(Vector3.Lerp(startPos, endPos, t));
            yield return new WaitForFixedUpdate();
        }

        slashParticle.SetActive(true);
        hitParticle.SetActive(true);
        hitParticle.transform.position = Player.Instance.transform.position;
        if (!isParryingDamage) xParticle.SetActive(true);

        //IngameManager.Instance.OnslashParticle(transform);
        if (!isParryingDamage) rb.MovePosition(endPos); // 패링을 실패했을 때 마지막 위치 보정

        yield return new WaitForSeconds(slashTimer);
        slashParticle.SetActive(false);
        hitParticle.SetActive(false);
        xParticle.SetActive(false);

        // 4. 대시 끝
        isAttack = false;

        yield return null;
    }

    public void ParryingDamage()
    {
        if (!isParrying) return; // 패링 활성화가 아니라면 리턴
        if (isParryingDamage) return; // 이미 맞은 상태라면

        rb.linearVelocity = Vector3.zero;

        PostProcessingManager.Instance.PulseDefault();
        isParryingDamage = true;

        rb.AddForce((rb.position - target.position).normalized * 45, ForceMode.Impulse);
    }

    public void DashAttack()
    {
        anim.SetBool(GuardAnim, true); // 발도 준비

        StartCoroutine(IRandomDashAttack());
    }

    private IEnumerator IRandomDashAttack()
    {
        // 1. 텍스트 출력 및 대기
        // 1. 가로 베기 텍스트 및 이펙트 실행
        foreach (var v in VerticalText)
        {
            aiText.text += v;
            yield return new WaitForSeconds(0.15f);
        }
        
        yield return new WaitForSeconds(0.3f);
        aiText.text = "";

        // 2. 방향 추출
        Vector3 dir = GetRandomPoint();
        bool isGround = false;

        while (!isGround)
        {
            if (Physics.Raycast(dir + Vector3.up * 10f, Vector3.down, out RaycastHit hit, 50f, groundMask))
            {
                isGround = true;
            }
        }

        // 3. 이동 및 회전
        Vector3 playerDir = (target.position - rb.position).normalized;
        Vector3 startPos = rb.position; // 지금 현재 위치
        Vector3 endPos = target.position - playerDir * 2; // 최종 목적지(플레이어)

        // 4. 방향 고정 및 애니메이션 실행
        transform.rotation = Quaternion.LookRotation(playerDir, Vector3.up);

        float dashDuration = 0.1f; // 대시 시간
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            elapsed += Time.fixedDeltaTime;

            float t = elapsed / dashDuration;
            rb.MovePosition(Vector3.Lerp(startPos, dir, t));
            yield return new WaitForFixedUpdate();
        }

        yield return new WaitForSeconds(0.55f); // 이동 후 잠시 대기

        // 5. 플레이어로 이동
        anim.SetBool(GuardAnim, false); // 기존 애니메이션 해제
        anim.SetTrigger(DashSmashAnim);
        playerDir = (target.position - rb.position).normalized; // 마지막 방향 다시 갱신
        endPos = target.position - playerDir * 2; // 마지막 위치 다시 갱신
        transform.rotation = Quaternion.LookRotation(playerDir, Vector3.up);

        isParrying = true; // 패링 타격 받기
        dashDuration = 0.3f;
        elapsed = 0f;
        while (elapsed < dashDuration && !isParryingDamage)
        {
            elapsed += Time.fixedDeltaTime;

            float t = elapsed / dashDuration;
            rb.MovePosition(Vector3.Lerp(dir, endPos, t));
            yield return new WaitForFixedUpdate();
        }

        if (!isParryingDamage) rb.MovePosition(endPos); // 패링을 실패했을 때 마지막 위치 보정

        yield return new WaitForSeconds(slashTimer);
        slashParticle.SetActive(false);
        hitParticle.SetActive(false);
        xParticle.SetActive(false);

        // 4. 대시 끝
        isAttack = false;

        yield return null;
    }



    // 대쉬 랜덤 포인트
    private Vector3 GetRandomPoint()
    {
        float angle = Random.Range(0f, 360f);

        radius = dashRadius + Random.Range(-radiusRange, radiusRange); // 대쉬 범위 랜덤화

        // 각도로 좌표 구하기
        Vector3 dir = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

        return dir;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Player"))
        {
            if (Player.Instance.parryingSucces) // 판정 성공일때만 진행
            {
                InputManager.Instance.OnMotor();
                Player.Instance.StartParrying();
                ParryingDamage();
            }
        }
    }
}
