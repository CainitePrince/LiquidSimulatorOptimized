Shader "Custom/InstancedShader"
{
    Properties
    {
        _SolidColor("Solid Color", Color) = (0, 0, 0, 1)
        _EmptyColor("Empty Color", Color) = (1, 1, 1, 1)
        _WaterColor("Water Color", Color) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags { "Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Opaque" }
        LOD 100
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 _SolidColor;
            float4 _EmptyColor;
            float4 _WaterColor;
            float _Ratio;

            // Add instancing support for this shader. You need to check 'Enable Instancing' on materials that use the shader.
            // See https://docs.unity3d.com/Manual/GPUInstancing.html for more information about instancing.
            // #pragma instancing_options assumeuniformscaling
            UNITY_INSTANCING_BUFFER_START(Props)
              UNITY_DEFINE_INSTANCED_PROP(float4, _Values)
            UNITY_INSTANCING_BUFFER_END(Props)

            float2 rotate(float2 uv, float th)
            {
                float s = sin(th);
                float c = cos(th);
                return mul(uv, float2x2(c, s, -s, c));
            }

            float square(float2 uv, float size, float2 offset, float angle)
            {
                float x = uv.x;
                float y = uv.y;
                float2 rotated = rotate(float2(x, y), angle);
                x = rotated.x - offset.x;
                y = rotated.y - offset.y;
                float d = max(abs(x), abs(y)) - size;
                return d;
            }

            v2f vert (appdata v)
            {
                v2f o;

                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                o.vertex = UnityObjectToClipPos(v.vertex);
    
                float4 values = UNITY_ACCESS_INSTANCED_PROP(Props, _Values);
        
                o.uv = float4((1.0f - v.uv.x) - 0.5, v.uv.y - 0.5, values.x, values.y);
    
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
    
                float liquid = i.uv.z;
                float angle = i.uv.w;
                float water = square(i.uv.xy, 0.5 * _Ratio, float2(0.0, liquid * _Ratio), angle);                
                fixed3 waterOrEmpty = lerp(_EmptyColor.rgb, _WaterColor.rgb, step(0.0, water));
                fixed3 color = lerp(waterOrEmpty, _SolidColor.rgb, step(2.0, liquid));
                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }
}