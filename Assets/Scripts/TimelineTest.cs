using UnityEngine;
using UnityEngine.Playables;

public class TimelineTest : MonoBehaviour
{
    public PlayableDirector director;
    public Vector3 finalPositionPlayer; // Timeline 끝날 때 위치
    public Vector3 finalPositionBoss;


    void OnDisable()
    {
        director.Stop();
    }

    public void OnTimelineStopped()
    {
        finalPositionPlayer = Player.Instance.transform.position;
        finalPositionBoss = Boss.Instance.transform.position;
    }
}
