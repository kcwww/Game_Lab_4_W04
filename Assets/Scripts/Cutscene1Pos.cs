using UnityEngine;

public class Cutscene1Pos : MonoBehaviour
{
    public Vector3 playerPos = new Vector3(0, 0.34f, -9.67f);
    public Vector3 bossPos = new Vector3(0.19f, 1.94f, 12.43f);

    public GameObject playerMesh;
    public GameObject bossMesh;


    public void SetPosition()
    {
        playerMesh.SetActive(false);
        bossMesh.SetActive(false);

        Player.Instance.transform.position = playerPos;
        Boss.Instance.transform.position = bossPos;
    }
}
