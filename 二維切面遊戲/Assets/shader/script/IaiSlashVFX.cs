using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class IaiSlashVFX : MonoBehaviour
{
    public static void PlayIaiCutEffect(Vector3 startPos, Vector3 endPos, Vector3 sliceRight, Vector3 sliceNormal, Color bladeColor)
    {
        GameObject fxObj = new GameObject("Iai_Slash_VFX");
        fxObj.transform.position = Vector3.zero;

        var vfx = fxObj.AddComponent<IaiSlashVFX>();
        vfx.StartCoroutine(vfx.EffectRoutine(startPos, endPos, sliceRight, sliceNormal, bladeColor));
    }

    IEnumerator EffectRoutine(Vector3 start, Vector3 end, Vector3 sliceRight, Vector3 sliceNormal, Color color)
    {
        // 1. 生成貫穿路徑的主斬擊細線 (LineRenderer)
        GameObject lineObj = new GameObject("IaiMainLine");
        lineObj.transform.SetParent(transform);
        var line = lineObj.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.positionCount = 2;
        line.SetPosition(0, start);
        line.SetPosition(1, end);
        line.startWidth = 0.08f;
        line.endWidth = 0.08f;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = color;
        line.endColor = color;

        // 2. 沿線隨機分佈 3~5 道次元交叉斬痕
        int cutCount = Mathf.Clamp(Mathf.RoundToInt(Vector3.Distance(start, end) * 0.8f), 3, 6);
        List<GameObject> cuts = new List<GameObject>();

        for (int i = 0; i < cutCount; i++)
        {
            float t = (float)(i + 1) / (cutCount + 1);
            Vector3 point = Vector3.Lerp(start, end, t) + (Vector3)Random.insideUnitCircle * 0.2f;

            // 稍微傾斜的月牙交叉小刀光
            float facing = (Random.value > 0.5f) ? 1f : -1f;
            SlashEffect.CreateSlash(point, sliceRight, sliceNormal, 1.1f, color, facing, SlashEffect.SlashType.Forward);
        }

        // 3. 軌跡維持極短暫發光，隨後漸隱
        float timer = 0f;
        float duration = 0.18f;
        while (timer < duration)
        {
            timer += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(color.a, 0f, timer / duration);
            Color fadeCol = color;
            fadeCol.a = alpha;
            if (line != null)
            {
                line.startColor = fadeCol;
                line.endColor = fadeCol;
            }
            yield return null;
        }

        Destroy(gameObject);
    }
}