Shader "Hidden/VoxelizeSlicing/VoxelSliceShader"
{
    Properties 
    { 
        _Color ("Color", Color) = (1,0,0,1) 
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2 // Back
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _Color;
            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                return o;
            }

            float4 frag(v2f i) : SV_Target { return _Color; }
            ENDHLSL
        }
    }
}
