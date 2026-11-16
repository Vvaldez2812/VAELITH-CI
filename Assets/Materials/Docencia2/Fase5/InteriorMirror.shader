Shader "VAELITH/InteriorMirror"
{
    Properties { _MirrorTex("Reflection",2D)="black"{} }
    SubShader {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Pass {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MirrorTex);SAMPLER(sampler_MirrorTex);
            struct A {float4 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float4 screen:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
            V vert(A v){V o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.screen=ComputeScreenPos(o.positionCS);return o;}
            half4 frag(V i):SV_Target{return half4(SAMPLE_TEXTURE2D(_MirrorTex,sampler_MirrorTex,i.screen.xy/i.screen.w).rgb*.92,1);}
            ENDHLSL
        }
    }
}
