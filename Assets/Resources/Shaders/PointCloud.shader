Shader "Custom/PointCloud"
{
    Properties
    {
        _PointSize("Point Size", Float) = 0.05
        _PointColor("Point Color", Color) = (1,1,1,1)
    }
    
    SubShader
    {
        Tags { 
            "RenderType"="Opaque" 
            "Queue"="Geometry+1000"
            "IgnoreProjector"="True"
            "ForceNoShadowCasting"="True"
        }
        
        Cull Off
        ZWrite On
        ZTest LEqual
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma require geometry
            #pragma geometry geom
            
            #include "UnityCG.cginc"

            struct PointData
            {
                float3 position;
                float intensity;
            };
            
            StructuredBuffer<PointData> _PointBuffer;
            float _PointSize;
            fixed4 _PointColor;

            struct appdata
            {
                uint id : SV_VertexID;
            };

            struct v2g
            {
                float4 pos : SV_POSITION;
                float intensity : TEXCOORD0;
            };

            struct g2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float intensity : TEXCOORD1;
            };

            v2g vert (appdata v)
            {
                v2g o;
                PointData data = _PointBuffer[v.id];
                o.pos = UnityWorldToClipPos(float4(data.position, 1.0));
                o.intensity = data.intensity;
                return o;
            }

            [maxvertexcount(4)]
            void geom(point v2g IN[1], inout TriangleStream<g2f> stream)
            {
                float halfSize = _PointSize * 0.5;
                g2f o;
                
                // 중심점
                float4 center = IN[0].pos;
                
                // 4개의 정점 생성 (작은 사각형)
                o.intensity = IN[0].intensity;
                
                // 정점 1: 왼쪽 아래
                o.pos = center + float4(-halfSize, -halfSize, 0, 0);
                o.uv = float2(0, 0);
                stream.Append(o);
                
                // 정점 2: 오른쪽 아래
                o.pos = center + float4(halfSize, -halfSize, 0, 0);
                o.uv = float2(1, 0);
                stream.Append(o);
                
                // 정점 3: 왼쪽 위
                o.pos = center + float4(-halfSize, halfSize, 0, 0);
                o.uv = float2(0, 1);
                stream.Append(o);
                
                // 정점 4: 오른쪽 위
                o.pos = center + float4(halfSize, halfSize, 0, 0);
                o.uv = float2(1, 1);
                stream.Append(o);
                
                stream.RestartStrip();
            }

            fixed4 frag (g2f i) : SV_Target
            {
                // 원형으로 렌더링
                float2 center = float2(0.5, 0.5);
                float distance = length(i.uv - center);
                
                if (distance > 0.5) discard;
                
                fixed4 col = _PointColor;
                col.rgb *= i.intensity;
                return col;
            }
            ENDCG
        }
    }
    FallBack Off
}