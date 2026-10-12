using UnityEngine;

public class SlashEffect : MonoBehaviour
{
    public enum SlashType 
    { 
        Forward, // 正劈：由上往下縱向直劈
        Upward,  // 上劈：從左往右橫切上方
        Downward // 下劈：從左往右掃過下方
    }

    public static void CreateSlash(Vector3 centerPos, Vector3 sliceRight, Vector3 sliceNormal, float size, Color slashColor, float facingDir, SlashType type)
    {
        // 1. 建立空物件承載 Mesh 與材質
        GameObject slashObj = new GameObject("SliceSlashTrail");
        slashObj.transform.position = centerPos;

        var meshFilter = slashObj.AddComponent<MeshFilter>();
        var meshRenderer = slashObj.AddComponent<MeshRenderer>();

        // 2. 建立專用材質 (使用 Unity 內建的 Sprites/Default，支持 Alpha 與頂點色)
        Material slashMat = new Material(Shader.Find("Sprites/Default"));
        slashMat.color = slashColor;
        meshRenderer.material = slashMat;

        // 3. 確定掃掠的起點與終點 (以面向負 X、朝左為標準基準，右向自動鏡像)
        bool isFacingLeft = (facingDir < 0);
        float startAngle = 0f;
        float totalSweep = 0f;

        switch (type)
        {
            case SlashType.Forward:
                // 正劈：由上往下縱向劈砍 (大幅寬厚弧線)
                if (isFacingLeft)
                {
                    startAngle = 110f;  // 頭頂偏後方
                    totalSweep = 160f;  // 順向往左下重砍
                }
                else
                {
                    startAngle = 70f;   // 頭頂偏後方
                    totalSweep = -160f; // 順向往右下重砍
                }
                break;

            case SlashType.Upward:
                // 上劈：從左往右 (橫掃頭頂)
                startAngle = 165f;
                totalSweep = -150f;
                break;

            case SlashType.Downward:
                // 下劈：從左往右 (掠過下方)
                startAngle = -165f;
                totalSweep = 150f;
                break;
        }

        // 4. 掛載動態刀刃與流體軌跡控制器
        var controller = slashObj.AddComponent<DynamicBladeController>();
        // 增加 size：大刀鋒效果。調整 duration：0.11 秒的極速爆發與收攏
        controller.Init(meshFilter, slashMat, sliceRight.normalized, Vector3.up, size * 1.2f, slashColor, startAngle, totalSweep, 0.11f);
    }
}

public class DynamicBladeController : MonoBehaviour
{
    private MeshFilter meshFilter;
    private Mesh mesh;
    private Material mat;
    private Color baseColor;

    private Vector3 rightAxis;
    private Vector3 upAxis;
    private float slashRadius;
    private float startAngleDeg;
    private float sweepAngleDeg;
    private float duration;

    private float timer = 0f;
    private const int SEGMENTS = 30; // 增加段數，讓曲線與刃帶圓潤、不出現折角

    public void Init(MeshFilter mf, Material m, Vector3 right, Vector3 up, float radius, Color color, float startAngle, float sweepAngle, float slashDuration)
    {
        meshFilter = mf;
        mat = m;
        rightAxis = right;
        upAxis = up;
        slashRadius = radius;
        baseColor = color;
        startAngleDeg = startAngle;
        sweepAngleDeg = sweepAngle;
        duration = slashDuration;

        mesh = new Mesh();
        meshFilter.mesh = mesh;
    }

    void Update()
    {
        timer += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(timer / duration);

        // 動力學演算法：
        // 讓刀尖（head）在 0.05 秒內狂飆到終點，拖出巨大的刀弧；
        // 讓刀尾（tail）在後面稍作延遲再快速追趕，在空中留下實體。
        float headProgress = Mathf.Sin(progress * Mathf.PI * 0.5f);
        float tailProgress = Mathf.Pow(progress, 2.5f); // 延遲與收攏係數

        if (progress >= 1.0f)
        {
            if (mat != null) Destroy(mat);
            Destroy(gameObject);
            return;
        }

        RebuildBladeMesh(tailProgress, headProgress, progress);
    }

    void RebuildBladeMesh(float tailT, float headT, float overallT)
    {
        // 30 段的多邊形面片（62個頂點、60個三角面），形成紮實圓潤的月牙弧帶
        Vector3[] vertices = new Vector3[(SEGMENTS + 1) * 2];
        int[] triangles = new int[SEGMENTS * 6];
        Color[] colors = new Color[(SEGMENTS + 1) * 2];

        float currentTailAngle = startAngleDeg + sweepAngleDeg * tailT;
        float currentHeadAngle = startAngleDeg + sweepAngleDeg * headT;

        // 增大刀刃的質量與厚度（約半徑的 40% ~ 45%），確保它不是一根細線
        float maxBladeWidth = slashRadius * 0.45f;

        for (int i = 0; i <= SEGMENTS; i++)
        {
            float segRatio = (float)i / SEGMENTS; // 0 = 尾端 (氣流/刀尾), 1 = 刀尖 (最亮刀鋒)
            float angle = Mathf.Lerp(currentTailAngle, currentHeadAngle, segRatio) * Mathf.Deg2Rad;

            // 刀鋒幾何形狀：在刀刃中段、刀尖前側最厚實，刀尾與最前端收成銳利角度
            float widthTaper = Mathf.Sin(segRatio * Mathf.PI);
            float innerRadius = slashRadius - (maxBladeWidth * 1.1f * widthTaper);
            float outerRadius = slashRadius + (maxBladeWidth * 0.2f * widthTaper);

            Vector3 dir = rightAxis * Mathf.Cos(angle) + upAxis * Mathf.Sin(angle);

            // 內側與外側邊緣頂點
            int vIdx0 = i * 2 + 0;
            int vIdx1 = i * 2 + 1;
            vertices[vIdx0] = dir * innerRadius;
            vertices[vIdx1] = dir * outerRadius;

            // 分層頂點色：刀尖與刀身核心極度明亮（白/亮灰），刀刃邊緣與刀尾迅速淡出
            Color col = baseColor;
            // 沿長度方向 (segRatio)：刀尾透明度漸變
            col.a = baseColor.a * Mathf.Sin(segRatio * Mathf.PI * 0.5f);
            
            // 隨時間 (overallT) 進行後半段淡出
            col.a *= Mathf.Lerp(1.0f, 0.0f, Mathf.Pow(overallT, 1.5f));

            // 外刃與內刃的微幅色彩差，產生流光漸層
            colors[vIdx0] = col * 0.8f; // 內刃微暗
            colors[vIdx1] = col * 1.3f; // 外刃最亮 (刀鋒處)

            if (i < SEGMENTS)
            {
                int baseIdx = i * 2;
                if (sweepAngleDeg < 0)
                {
                    triangles[i * 6 + 0] = baseIdx;
                    triangles[i * 6 + 1] = baseIdx + 1;
                    triangles[i * 6 + 2] = baseIdx + 2;

                    triangles[i * 6 + 3] = baseIdx + 2;
                    triangles[i * 6 + 4] = baseIdx + 1;
                    triangles[i * 6 + 5] = baseIdx + 3;
                }
                else
                {
                    triangles[i * 6 + 0] = baseIdx;
                    triangles[i * 6 + 1] = baseIdx + 2;
                    triangles[i * 6 + 2] = baseIdx + 1;

                    triangles[i * 6 + 3] = baseIdx + 1;
                    triangles[i * 6 + 2] = baseIdx + 3;
                    triangles[i * 6 + 4] = baseIdx + 3;
                }
            }
        }

        mesh.Clear();
        mesh.vertices = vertices;
        mesh.colors = colors; // 套用頂點色
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
    }
}