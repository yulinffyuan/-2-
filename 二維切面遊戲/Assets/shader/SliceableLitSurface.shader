Shader "Custom/SliceableLitSurface"
{
    Properties
    {
        [Header(Main Surface Outer)]
        _MainTex ("表面主貼圖 (Main Texture)", 2D) = "white" {}
        _MainColor ("表面色調 (Tint Color)", Color) = (1, 1, 1, 1)

        [Header(Cross Section Inner)]
        _CutTex ("剖面貼圖 (Cut Texture)", 2D) = "gray" {}
        _CutColor ("剖面色調 (Cut Tint)", Color) = (1, 1, 1, 1)
        _CutTexScale ("剖面貼圖平鋪比例 (Scale)", Float) = 1.0
        _EdgeHighlightColor ("邊界發光色", Color) = (1, 1, 1, 1)
        _EdgeWidth ("邊界發光寬度", Range(0.01, 0.4)) = 0.12

        [Header(Sonar Scan)]
        _ScanColor ("掃描光顏色", Color) = (0.3, 1, 0.5, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Cull Off // 雙面著色，確保膠囊體內部能被看見
        ZWrite On

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainColor;

            sampler2D _CutTex;
            float4 _CutColor;
            float _CutTexScale;
            float4 _EdgeHighlightColor;
            float _EdgeWidth;

            float4 _ScanColor;

            // 全域切片參數
            float3 _SlicePlaneOrigin;
            float3 _SlicePlaneNormal;
            float _SliceThickness;

            // 全域聲納參數
            float3 _ScanOrigin;
            int _ScanActiveCount;
            float _ScanWaveWidth;
            float4 _ScanWaveData[8];

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                // 核心關鍵 1：逐像素 (Per-pixel) 重新計算到切面的世界精確垂直距離
                float3 diffToPlane = i.worldPos - _SlicePlaneOrigin;
                float distToPlane = dot(diffToPlane, _SlicePlaneNormal);
                float absDist = abs(distToPlane);

                // 核心關鍵 2：給予膠囊體弧面充足的厚度容差 (至少 1.8 單位)
                float visibleRange = max(_SliceThickness * 0.5, 1.2);

                // --- 1. 全域聲納掃描判定 ---
                float distToWaveCenter = length(i.worldPos - _ScanOrigin);
                float scanGlow = 0.0;
                [unroll(8)]
                for (int w = 0; w < 8; w++)
                {
                    if (w < _ScanActiveCount)
                    {
                        float waveR = _ScanWaveData[w].x;
                        float waveI = _ScanWaveData[w].y;
                        if (waveI > 0.001)
                        {
                            float delta = abs(distToWaveCenter - waveR);
                            if (delta < _ScanWaveWidth)
                            {
                                scanGlow += (1.0 - (delta / _ScanWaveWidth)) * waveI;
                            }
                        }
                    }
                }
                scanGlow = saturate(scanGlow);

                // --- 2. 裁切判定：若在厚度外且沒被聲納波打到，才 discard ---
                if (absDist > visibleRange && scanGlow <= 0.001)
                {
                    discard;
                }

                // --- 3. 聲納全像顯影 (在切片外但被波照亮) ---
                if (absDist > visibleRange)
                {
                    return _ScanColor * scanGlow;
                }

                // --- 4. 剖面著色 (背面 facing < 0，代表看到切開的膠囊體內腔) ---
                if (facing < 0)
                {
                    // 萬向正交軸映射
                    float3 norm = normalize(_SlicePlaneNormal);
                    float3 helper = abs(norm.y) > 0.9 ? float3(1, 0, 0) : float3(0, 1, 0);
                    float3 uAxis = normalize(cross(norm, helper));
                    float3 vAxis = cross(norm, uAxis);

                    float2 cutUV = float2(dot(diffToPlane, uAxis), dot(diffToPlane, vAxis)) * _CutTexScale;
                    fixed4 cutTexCol = tex2D(_CutTex, cutUV);

                    // 膠囊體相切切口邊緣高光
                    if (absDist < _EdgeWidth)
                    {
                        return lerp(cutTexCol * _CutColor, _EdgeHighlightColor, 0.5);
                    }

                    return cutTexCol * _CutColor;
                }

                // --- 5. 正面表面邊界發光 (弧形外壁與切面交界處) ---
                if (absDist < _EdgeWidth)
                {
                    return _EdgeHighlightColor;
                }

                // --- 6. 正面表面正常外觀 ---
                fixed4 mainTexCol = tex2D(_MainTex, i.uv);
                fixed4 surfaceCol = mainTexCol * _MainColor;

                if (scanGlow > 0.001)
                {
                    surfaceCol += _ScanColor * scanGlow * 0.5;
                }

                return surfaceCol;
            }
            ENDCG
        }
    }
}