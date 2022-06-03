Shader "Custom/ParticleRender"
{
    Properties
    {
        _MainTex ("Base (RGB)", 2D) = "white" {}
        _Color(" Color", Color) = (1, 0.5, 0.5, 1)
    }

    SubShader
    {
        Pass
        {
            Tags { "RenderType"="Transparent" }
            ZTest Off
            ZWrite Off
            Cull Off
            Blend SrcAlpha One

            CGPROGRAM
                #pragma vertex particle_vertex
                #pragma fragment frag

                #pragma target 5.0
                #include "UnityCG.cginc"

                struct Particle
                {
                    float3 position;
                    float3 velocity;
                    float3 color;
                };

                StructuredBuffer<Particle> particles;
                StructuredBuffer<float3> quadPoints;

                sampler2D _MainTex;

                float4 emitterPosition;
                float4 _Color;

                struct v2f
                {
                    float4 pos : SV_POSITION;
                    float2 uv : TEXCOORD0;
                    float4 color : COLOR;
                };

                v2f particle_vertex(uint id : SV_VertexID, uint inst : SV_InstanceID)
                {
                    v2f o;

                    float3 worldPosition = particles[inst].position;
                    float3 quadPoint = quadPoints[id];

                    o.pos = mul(UNITY_MATRIX_P, mul(UNITY_MATRIX_V, float4(worldPosition, 1.0f)) + float4(quadPoint, 0.0f));
                    o.uv = quadPoints[id] + 0.5f;
                    o.color = float4(particles[inst].color, 1.0f) * _Color;

                    return o;
                }

                fixed4 frag (v2f i) : COLOR
                {
                    float4 texCol = tex2Dbias(_MainTex, float4(i.uv, 0.0f, -1.0f));
                    float4 partCol = i.color;

                    return float4(1.0f - (1.0f - texCol.rgb) * (1.0f - partCol.rgb), texCol.a);
                }
            ENDCG
        }
    }
}
