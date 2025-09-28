using System.Collections;
using UnityEngine;

public class Layser : MonoBehaviour
{
    private Rigidbody rb;

    private void Awake()
    {
        rb= gameObject.GetComponent<Rigidbody>();
    }

    public void Shoot(Vector3 target)
    {
        StartCoroutine(ShootRoutine(target));
    }

    private IEnumerator ShootRoutine(Vector3 target)
    {
        Vector3 playerDir = (target - rb.position).normalized;
        Vector3 startPos = rb.position; // 지금 현재 위치
        Vector3 endPos = target;

        float dashDuration = 1f; // 대시 시간
        float elapsed = 0f;

        while (elapsed < dashDuration)
        {
            elapsed += Time.fixedDeltaTime;

            float t = elapsed / dashDuration;
            // 처음부터 끝까지 일정하게 빠르게 (쓸림 X, 급가속 X)
            rb.MovePosition(Vector3.Lerp(startPos, endPos, t));
            yield return new WaitForFixedUpdate();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Parrying"))
        {
            if (Player.Instance.isSlashDelay) return;
            Debug.Log("참격 패링 충돌 감지");
            if (Player.Instance.parryingSucces)
            {
                InputManager.Instance.OnMotor();
                IngameManager.Instance.SlowTimer();
                PostProcessingManager.Instance.PulseDefault();
                Player.Instance.SlashParrying();
                Player.Instance.StartParrying();
                Player.Instance.GetEnemyPos(rb.transform);
                Player.Instance.ParryingLayser();
                //StartCoroutine(HitCoroutine());
                Destroy(gameObject);
            }
        }
        else if (other.CompareTag("Player")) // 플레이어가 맞았다면
        {
            //Debug.Log("플레이어 : " + Player.Instance.isSlashDelay);
            if (Player.Instance.isSlashDelay) return;
            Player.Instance.Damaged(1); // 임의로
            Destroy(gameObject);
        }
        else if (other.CompareTag("Enemy"))
        {
            //Debug.Log("보스 : " + Player.Instance.isAISlashDelay);

            if (!Player.Instance.isAISlashDelay) return; // 플레이어가 성공에 실패했다면 피격 x
            IngameManager.Instance.DamageBoss(1); // 임의로
            Destroy(gameObject);
        }
    }
}
