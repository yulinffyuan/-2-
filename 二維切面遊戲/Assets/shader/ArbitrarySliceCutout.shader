Shader "Custom/ArbitrarySliceCutout"
{
    Properties
    {
        _MainColor ("表面顏色 (Outer Color)", Color) = (0.2, 0.7, 0.9, 1)
        _CutColor ("剖面填色 (Cut Color)", Color) = (0.95, 0.45, 0.15, 1)
        _EdgeHighlightColor ("邊界發光", Color) = (1, 1, 1, 1)

        [Header(Sonar Scan)]
        _ScanColor ("掃描光顏色", Color) = (0.3, 1, 0.5, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        // 核心：雙面著色，確保方塊內側看得見
        Cull Off
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
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float distToPlane : TEXCOORD1;
            };

            float4 _MainColor;
            float4 _CutColor;
            float4 _EdgeHighlightColor;
            float4 _ScanColor;

            // 全域切片參數
            float3 _SlicePlaneOrigin;
            float3 _SlicePlaneNormal;
            float _SliceThickness;

            // 聲納參數
            float3 _ScanOrigin;
            int _ScanActiveCount;
            float _ScanWaveWidth;
            float4 _ScanWaveData[8];

            v2f vert(appdata v)
            {
                v2f o;
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                // 計算該頂點到切面的距離
                o.distToPlane = dot(o.worldPos - _SlicePlaneOrigin, _SlicePlaneNormal);

                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag(v2f i, fixed facing : VFACE) : SV_Target
            {
                // 1. 計算該點到切面的絕對距離
                float absDist = abs(i.distToPlane);
                
                // 擴大可見容忍度：取厚度的 1.5 倍，防止頂點稀疏時整塊消失
                float visibleRange = max(_SliceThickness, 1.5);

                // --- 2. 聲納掃描檢測 ---
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

                // --- 3. 裁切核心：最寬鬆的安全範圍 ---
                // 只要在切面厚度範圍內，或者被聲納波掃到，就「絕對不 discard」！
                if (absDist > visibleRange && scanGlow <= 0.001)
                {
                    discard;
                }

                // --- 4. 被聲納波照亮的切面外部分 ---
                if (absDist > visibleRange)
                {
                    return _ScanColor * scanGlow;
                }

                // --- 5. 切面著色 ---
                // facing < 0 代表我們看到的是方塊的「內部背壁」-> 直接塗剖面色！
                if (facing < 0)
                {
                    return _CutColor;
                }

                // 正面且接近切面邊緣 -> 發光邊框
                if (absDist < 0.25)
                {
                    return _EdgeHighlightColor;
                }

                // 正面正常外表
                fixed4 finalCol = _MainColor;
                if (scanGlow > 0.001) finalCol += _ScanColor * scanGlow * 0.5;
                return finalCol;
            }
            ENDCG
        }
    }
}