using UnityEngine;

public class PromptFloatEffect : MonoBehaviour
{
    public float floatAmplitude = 0.08f; // 上下漂浮幅度
    public float floatFrequency = 3.5f;  // 漂浮頻率
    private Vector3 initialLocalPos;

    void OnEnable()
    {
        initialLocalPos = transform.localPosition;
    }

    void Update()
    {
        float newY = initialLocalPos.y + Mathf.Sin(Time.unscaledTime * floatFrequency) * floatAmplitude;
        transform.localPosition = new Vector3(initialLocalPos.x, newY, initialLocalPos.z);
    }
}