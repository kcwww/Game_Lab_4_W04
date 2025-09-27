using UnityEngine;

public class JellyBone : MonoBehaviour
{
    [Header("기준(안흔들릴) 위치/회전은 시작값")]
    public float stiffness = 40f;   // 스프링 탄성
    public float damping = 8f;      // 감쇠(마찰)
    public Vector3 maxOffset = new Vector3(0.08f, 0.12f, 0.08f); // 최대 흔들림 제한
    public float externalForceScale = 0.6f; // 부모 가속 → 흔들림 세기

    Transform parentTf;
    Vector3 restLocalPos, vel, localPos;
    Vector3 prevParentPos, parentVel, prevParentVel;

    void Start()
    {
        parentTf = transform.parent;
        restLocalPos = transform.localPosition;
        localPos = restLocalPos;
        if (parentTf) prevParentPos = parentTf.position;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // 부모 가속 추정
        Vector3 parentPos = parentTf ? parentTf.position : Vector3.zero;
        Vector3 curParentVel = parentTf ? (parentPos - prevParentPos) / dt : Vector3.zero;
        Vector3 parentAcc = (curParentVel - prevParentVel) / dt;
        Vector3 localAcc = parentTf ? parentTf.InverseTransformDirection(parentAcc) : parentAcc;

        // 스프링-감쇠 통합(질량=1)
        Vector3 x = localPos - restLocalPos;
        Vector3 force = (-stiffness * x) + (-damping * vel) + (externalForceScale * localAcc);
        vel += force * dt;
        localPos += vel * dt;

        // 한도 제한
        Vector3 offset = localPos - restLocalPos;
        offset = Vector3.Max(Vector3.Min(offset, maxOffset), -maxOffset);
        localPos = restLocalPos + offset;

        transform.localPosition = localPos;

        // 기록
        prevParentVel = curParentVel;
        prevParentPos = parentPos;
    }
}