using System.Collections;
using TMPro;
using UnityEngine;

public class Slime : MonoBehaviour
{
    [Header("Compenent")]
    private Animator anim;
    private Rigidbody rb;
    private Transform target;

    [Header("Const")]
    private const string HorizontalAnim = "isHorizontal";
    private const string SmashAnim = "isSmash";

    [Header("Parrying")]
    public bool isParrying { get; private set; } = false; // 플레이어 패링 성공 여부
    private bool isParryingDamage = false; // 패링 데미지 들어온지 여부

    [Header("Attack")]
    private float attackRange = 10f; // 공격 탐지 거리
    private float curAttackTimer = 5f; // 현재 공격 쿨타임
    private float attackTimer = 5f; // 공격 쿨타임
    private bool isAttack = false; // 공격 중인지 체크

    [Header("Status")]
    private float speed = 5f;
    private float rotationSpeed = 5f;

    [Header("Particle")]
    private const float slashTimer = 0.5f;

    [Header("Hit")]
    private bool isHitDelay = false; // 현재 피격 딜레이중인가


    private bool isTutorial = false;


    private Coroutine attackCoroutine;

    private void Awake()
    {
        anim = GetComponentInChildren<Animator>();
        rb = GetComponent<Rigidbody>();
    }


    private void Start()
    {
        target = Player.Instance.transform;


        if (!GameManager.Instance.isTutorial) isTutorial = true;
        if (isTutorial) return;

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
        if (isTutorial) return;

        Player.Instance.CheckParringDistance -= Player_CheckParringDistance;
        Player.Instance.OnParryingEnd -= Player_EndParrying;
    }

    private void FixedUpdate()
    {
        if (!GameManager.Instance.isTutorial) return;
        if (isParryingDamage) return;
        Move();
        Attack();
    }

    private void Update()
    {
        if (!GameManager.Instance.isTutorial) return;
        if (!isAttack)
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
        //anim.SetBool(WalkAnim, true);
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

        HorizontalSmash();
        curAttackTimer = attackTimer;
    }

    private void HorizontalSmash()
    {
        anim.SetBool(HorizontalAnim, true); // 발도 준비

        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        attackCoroutine = StartCoroutine(SmashCoroutine());
    }

    private IEnumerator SmashCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        anim.SetBool(HorizontalAnim, false);

        // 2. 방향 계산
        Vector3 dir = (target.position - transform.position).normalized;
        Vector3 startPos = rb.position;
        Vector3 endPos = target.position - dir; // 2 정도의 거리만큼 뒤에 도착

        // 3. 방향 고정 및 애니메이션 실행
        transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        anim.SetTrigger(SmashAnim);

        isParrying = true; // 패링 타격 받기
        float dashDuration = 0.75f; // 대시 시간
        float elapsed = 0f;
        anim.SetTrigger(SmashAnim);

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

            Player.Instance.Damaged(0); // 임의로
            Player.Instance.BackStep(rb.position);
        }
        isParrying = false;
        yield return new WaitForSeconds(slashTimer);
        IngameManager.Instance.slashParticle.SetActive(false);
        IngameManager.Instance.hitParticle.SetActive(false);
        IngameManager.Instance.xParticle.SetActive(false);

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
    }
    private IEnumerator HitMove()
    {
        yield return new WaitForSeconds(0.25f); // 피격 모션 대기
        rb.AddForce((rb.position - target.position).normalized * 45, ForceMode.Impulse);
    }

    private IEnumerator HitCoroutine()
    {
        isHitDelay = true;
        yield return new WaitForSeconds(1f);
        isHitDelay = false;
    }

    public void ResetPosition(Transform pos)
    {
        if (attackCoroutine != null) StopCoroutine(attackCoroutine);
        if (!GameManager.Instance.isTutorial) return;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce((pos.position - rb.position).normalized * 45, ForceMode.Impulse);

        StartCoroutine(ResetPos(pos));
    }

    private IEnumerator ResetPos(Transform pos)
    {
        yield return new WaitForSeconds(0.5f);
        rb.position = pos.position;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!GameManager.Instance.isTutorial) return;

        if (other.CompareTag("Parrying"))
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
            else //
            {
                if (Player.Instance.isParrying) // 아직 보정이 안끝났다면, 2차 검증
                {
                    ParryingDamage();
                    InputManager.Instance.OnMotor();
                    Player.Instance.StartParrying();
                    Player.Instance.GetEnemyPos(rb.transform);
                    StartCoroutine(HitCoroutine());
                }
            }
        }
    }
}
