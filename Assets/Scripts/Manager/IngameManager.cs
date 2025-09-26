using System.Collections;
using UnityEngine;

public class IngameManager : MonoBehaviour
{
    public static IngameManager Instance { get; private set; }

    public GameObject slashParticel;
    public Vector3 slashOffset = new Vector3(0, 2.34f, -6.76f);

    private const float slashTimer = 0.5f;

    private void Awake()
    {
        if (Instance == null) Instance = this;
    }


   /* public void OnslashParticle(Transform trans)
    {
        slashParticel.transform.position = trans.position + slashOffset;
        slashParticel.transform.rotation = trans.rotation;

        slashParticel.SetActive(true);
        StartCoroutine(OffSlashParticle());
    }

    private IEnumerator OffSlashParticle()
    {
        yield return new WaitForSeconds(slashTimer);
        slashParticel.SetActive(false);
    }*/
}
