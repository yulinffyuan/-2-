using UnityEngine;

public class SliceIndicatorSync : MonoBehaviour
{
    void LateUpdate()
    {
        if (TransformSliceManager.Instance == null) return;

        // 位置同步到切片中心（主角身邊）
        transform.position = TransformSliceManager.Instance.PlaneOrigin;

        // 朝向同步到切面的法向量
        Vector3 normal = TransformSliceManager.Instance.PlaneNormal;
        if (normal.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(normal, Vector3.up);
        }
    }
}