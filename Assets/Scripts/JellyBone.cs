using UnityEngine;

public class JellyBone : MonoBehaviour
{
    [Header("����(����鸱) ��ġ/ȸ���� ���۰�")]
    public float stiffness = 40f;   // ������ ź��
    public float damping = 8f;      // ����(����)
    public Vector3 maxOffset = new Vector3(0.08f, 0.12f, 0.08f); // �ִ� ��鸲 ����
    public float externalForceScale = 0.6f; // �θ� ���� �� ��鸲 ����

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

        // �θ� ���� ����
        Vector3 parentPos = parentTf ? parentTf.position : Vector3.zero;
        Vector3 curParentVel = parentTf ? (parentPos - prevParentPos) / dt : Vector3.zero;
        Vector3 parentAcc = (curParentVel - prevParentVel) / dt;
        Vector3 localAcc = parentTf ? parentTf.InverseTransformDirection(parentAcc) : parentAcc;

        // ������-���� ����(����=1)
        Vector3 x = localPos - restLocalPos;
        Vector3 force = (-stiffness * x) + (-damping * vel) + (externalForceScale * localAcc);
        vel += force * dt;
        localPos += vel * dt;

        // �ѵ� ����
        Vector3 offset = localPos - restLocalPos;
        offset = Vector3.Max(Vector3.Min(offset, maxOffset), -maxOffset);
        localPos = restLocalPos + offset;

        transform.localPosition = localPos;

        // ���
        prevParentVel = curParentVel;
        prevParentPos = parentPos;
    }
}