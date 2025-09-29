using UnityEngine;

public class TimeLinePostProcess : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public PostProcessingManager postProcessingManager;

    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void OnTimelinePostProcess()
    {
        Debug.Log("TimeLinePostProcess OnEnable");
        if (postProcessingManager != null)
        {
            Debug.Log("TimeLinePostProcess OnEnable TimeLinePulse");
            postProcessingManager.TimeLinePulse();
        }
    }
}

