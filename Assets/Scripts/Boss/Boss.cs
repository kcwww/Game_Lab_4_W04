using System.Collections;
using System.Collections.Generic;
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
    private const string GroundSlashAnim = "isGroundSlash";
    private const string GroundSlashOnAnim = "isGroundSlashOn";
    //private const string 
    private const string HorizontalText = "가로베기";
    private const string VerticalText = "섬광일도";
    private const string GroundSlashText = "대지참격";
    private const string LayserText = "신기문물";

    [Header("Parrying")]
    public bool isParrying { get; private set; } = false; // 플레이어 패링 성공 여부
    private bool isParryingDamage = false; // 패링 데미지 들어온지 여부

    [Header("Attack")]
    private float attackRange = 10f; // 공격 탐지 거리
    private float curAttackTimer = 5f; // 현재 공격 쿨타임
    private float attackTimer = 5f; // 공격 쿨타임
    private bool isAttack = false; // 공격 중인지 체크
    private GroundSlashShooter groundSlashShooter;
    public GameObject[] turretObject; // 포탑 위치
    public Transform alter;
    public bool[] isTurret = {true, true, true, true}; // 포탑 활성화 여부
    private int turretCount = 4; // 현재 남은 포탑 갯수

    //private float horizontalSpeed = 15f; // 가로베기 이동 속도
    //private bool isHorizontal = false; // 발도 체크
    private const float radiusRange = 2f; // 대시 범위 증감량
    private const float dashRadius = 5f; // 대시 기본 범위
    private float radius = 5f; // 기본 반지름 값
    private int patterCount = 3;

    [Header("Status")]
    private float speed = 5f;
    private float rotationSpeed = 5f;

    [Header("Particle")]
    private const float slashTimer = 0.5f;


    [Header("Hit")]
    private bool isHitDelay = false; // 현재 피격 딜레이중인가

    private void Awake()
    {
        if(Instance == null) Instance = this;

        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();
        groundSlashShooter = GetComponent<GroundSlashShooter>();
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
        isParrying = false;
    }

    private void Player_CheckParringDistance(object sender, System.EventArgs e)
    {
        if (isHitDelay) return; // 아직 맞은 상태라면 추가 반격x
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

        int curPatterCount = patterCount;
        if (turretCount == 0) curPatterCount--;

        int ran = Random.Range(0, curPatterCount);

        switch (ran)
        {
            case 0:
                HorizontalSmash();
                break;
            case 1:
                DashAttack();
                break;
            case 2:
                GroundSlash();
                break;
        }

        //Layser();

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
        Vector3 endPos = target.position - dir; // 2 정도의 거리만큼 뒤에 도착

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

        //rb.MovePosition(target.position);

        yield return null; // 한 프레임 대기

        IngameManager.Instance.slashParticle.SetActive(true);
        IngameManager.Instance.hitParticle.SetActive(true);
        IngameManager.Instance.hitParticle.transform.position = Player.Instance.transform.position;
        if (!isParryingDamage)
        {
            IngameManager.Instance.xParticle.SetActive(true);
            rb.MovePosition(endPos);

            Player.Instance.Damaged(1); // 임의로
        }
        isParrying = false;
        yield return new WaitForSeconds(slashTimer);
        IngameManager.Instance.slashParticle.SetActive(false);
        IngameManager.Instance.hitParticle.SetActive(false);
        IngameManager.Instance.xParticle.SetActive(false);

        //yield return new WaitForSecondsRealtime(0.1f); // 패링 유예기간

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
        IngameManager.Instance.SlowTimer();
        isParryingDamage = true;
        isParrying = false;

        StartCoroutine(HitMove());
        //rb.AddForce((rb.position - target.position).normalized * 45, ForceMode.Impulse);
    }

    private IEnumerator HitMove()
    {
        yield return new WaitForSeconds(0.25f); // 피격 모션 대기
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
        Vector3 endPos = target.position - playerDir; // 최종 목적지(플레이어)

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
        endPos = target.position - playerDir; // 마지막 위치 다시 갱신
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

        //rb.MovePosition(target.position);

        yield return null; // 한 프레임 대기

        IngameManager.Instance.slashParticle.SetActive(true);
        IngameManager.Instance.hitParticle.SetActive(true);
        IngameManager.Instance.hitParticle.transform.position = Player.Instance.transform.position;
        if (!isParryingDamage)
        {
            IngameManager.Instance.xParticle.SetActive(true);
            rb.MovePosition(endPos);

            Player.Instance.Damaged(1); // 임의로
        }

        isParrying = false;
        yield return new WaitForSeconds(slashTimer);
        IngameManager.Instance.slashParticle.SetActive(false);
        IngameManager.Instance.hitParticle.SetActive(false);
        IngameManager.Instance.xParticle.SetActive(false);

        //yield return new WaitForSecondsRealtime(0.1f); // 패링 유예기간

        // 4. 대시 끝
        isAttack = false;

        yield return null;
    }

    private void GroundSlash()
    {
        anim.SetBool(GroundSlashAnim, true);

        StartCoroutine(GroundSlashRoutine());
    }

    private IEnumerator GroundSlashRoutine()
    {
        foreach (var v in GroundSlashText)
        {
            aiText.text += v;
            yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(0.3f);
        aiText.text = "";

        Player.Instance.GetEnemyPos(transform);
        //Player.Instance.SlashParrying(); // 참격 전용

        anim.SetBool(GroundSlashAnim, false);
        anim.SetTrigger(GroundSlashOnAnim);

        yield return new WaitForSeconds(1f); // 애니메이션 시간 대기

        groundSlashShooter.FireAt(Player.Instance.transform);

        yield return new WaitForSeconds(3.5f); // 남은 대기 시간

        isAttack = false;
    }

    private void Layser()
    {
        anim.SetBool(HorizontalAnim, true);

        StartCoroutine(LayserhRoutine());
    }

    private IEnumerator LayserhRoutine()
    {
        // 1. 터렛 체크
        List<int> pivots = new List<int>();
        
        for(int i=0; i<4; i++)
        {
            if (isTurret[i]) pivots.Add(i); // 터렛이 활성화된 상태라면
        }

        int index = pivots[Random.Range(0, pivots.Count)];


        // 2. 방향 계산
        Vector3 dir = (alter.position - transform.position);
        dir.y = 0;
        dir = dir.normalized;
        Vector3 startPos = rb.position;
        Vector3 endPos = alter.position + dir * 2 + new Vector3(0,10,0); // 조금 뒤에 뒤랑 위로

        dir = (turretObject[index].transform.position - transform.position);
        dir.y = 0;
        dir = dir.normalized;

        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

        IngameManager.Instance.bossParticle.transform.position = startPos;
        IngameManager.Instance.bossParticle.SetActive(true);

        float dashDuration = 0.05f; // 대시 시간
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            elapsed += Time.fixedDeltaTime;

            float t = elapsed / dashDuration;
            // 처음부터 끝까지 일정하게 빠르게 (쓸림 X, 급가속 X)
            rb.MovePosition(Vector3.Lerp(startPos, endPos, t));
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(endPos); // 마지막 위치 보정
        yield return new WaitForSeconds(0.2f);

        IngameManager.Instance.bossParticle.SetActive(false);

        // 방향 재 갱신
        dir = (rb.position - startPos);
        dir.y = 0;
        dir = dir.normalized;
        
        // 텍스트 실행


        yield return new WaitForSeconds(0.3f); // 잠시 대기

        foreach (var v in LayserText)
        {
            aiText.text += v;
            yield return new WaitForSeconds(0.15f);
        }

        yield return new WaitForSeconds(0.3f);

        // 4. 공격 모션 실행
        anim.SetBool(HorizontalAnim, false);
        anim.SetTrigger(SmashAnim);
        aiText.text = "";

        IngameManager.Instance.slashTurretParticle.SetActive(true);
        IngameManager.Instance.slashTurretParticle.transform.position = turretObject[index].transform.position;
        IngameManager.Instance.hitParticle.SetActive(true);
        IngameManager.Instance.hitParticle.transform.position = turretObject[index].transform.position;
        IngameManager.Instance.xTurretParicle.SetActive(true);
        IngameManager.Instance.xTurretParicle.transform.position = turretObject[index].transform.position;

        yield return new WaitForSeconds(1f);


        IngameManager.Instance.slashTurretParticle.SetActive(false);
        IngameManager.Instance.hitParticle.SetActive(false);
        IngameManager.Instance.xTurretParicle.SetActive(false);
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up); // 복귀 방향 맞추기

        // 5. 레이저 발사

        yield return new WaitForSeconds(2f); // 애니메이션 시간 대기

        // 6. 전장 복귀 및 있던 자리에 파티클 생성하기

        elapsed = 0f;

        IngameManager.Instance.bossParticle.transform.position = transform.position;
        IngameManager.Instance.bossParticle.SetActive(true);

        // 전장 복귀
        while (elapsed < dashDuration)
        {
            elapsed += Time.fixedDeltaTime;

            float t = elapsed / dashDuration;
            // 처음부터 끝까지 일정하게 빠르게 (쓸림 X, 급가속 X)
            rb.MovePosition(Vector3.Lerp(endPos, startPos, t));
            yield return new WaitForFixedUpdate();
        }

        rb.MovePosition(startPos);
        IngameManager.Instance.bossParticle.SetActive(false);

        //groundSlashShooter.FireAt(Player.Instance.transform);

        yield return new WaitForSeconds(3.5f); // 

        isAttack = false;
    }

    public void TurretRemove(int index)
    {
        turretCount--;
        turretObject[index].SetActive(false);
        isTurret[index] = false;
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

    private IEnumerator HitCoroutine()
    {
        isHitDelay = true;
        yield return new WaitForSeconds(1f);
        isHitDelay = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if(other.CompareTag("Parrying"))
        {
            if (Player.Instance.parryingSucces) // 판정 성공일때만 진행
            {
                if (isParryingDamage || !isParrying || isHitDelay) return; // 이미 맞았거나, 패링 상태가 아니라면
                ParryingDamage();
                InputManager.Instance.OnMotor();
                Player.Instance.StartParrying();
                Player.Instance.GetEnemyPos(rb.transform);
                StartCoroutine(HitCoroutine());
            }
        }

        /*if (other.CompareTag("Player")) // 플레이어와 충돌 했을 때
        {
            //if (isParryingDamage || !isParrying) return; // 이미 패링 맞은 상태거나 패링 준비(공격 실행)이 아니라면 리턴
            

            //Player.Instance.Damaged(1); // 임의로
        }*/
    }

    /*private void OnCollisionEnter(Collision collision)
    {
        if(collision.gameObject.CompareTag("Player")) // 플레이어와 충돌 했을 때
        {
            if (isParryingDamage) return; // 이미 패링 맞은 상태라면

            Player.Instance.Damaged(5); // 임의로
        }
    }*/
}
