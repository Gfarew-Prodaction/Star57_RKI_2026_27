Shader "Zvezda57/Office Ink"
{
    Properties
    {
        _InkColor("Ink",Color)=(0.025,0.023,0.029,1)
        _Width("Width in pixels",Range(0.5,3))=1
        _DepthThreshold("Depth threshold",Range(0.001,0.1))=0.012
        _NormalThreshold("Normal threshold",Range(0.1,1))=0.5
        _Strength("Strength",Range(0,1))=0.82
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "OfficeInk"
            ZWrite Off ZTest Always Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
            float4 _InkColor;
            float _Width,_DepthThreshold,_NormalThreshold,_Strength;
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float depth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                float3 normal=SampleSceneNormals(uv);
                float2 stepUV=_Width/_ScaledScreenParams.xy;
                float edge=0;
                const float2 offsets[4]={float2(1,0),float2(-1,0),float2(0,1),float2(0,-1)};
                [unroll] for(int i=0;i<4;i++)
                {
                    float2 p=uv+stepUV*offsets[i];
                    float d=LinearEyeDepth(SampleSceneDepth(p),_ZBufferParams);
                    float discontinuity=abs(depth-d)/max(min(depth,d),.1);
                    float crease=1-saturate(dot(normal,SampleSceneNormals(p)));
                    edge=max(edge,max(smoothstep(_DepthThreshold,_DepthThreshold*2,discontinuity),smoothstep(_NormalThreshold,_NormalThreshold+.15,crease)));
                }
                half4 color=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                color.rgb=lerp(color.rgb,_InkColor.rgb,edge*_Strength);
                return color;
            }
            ENDHLSL
        }
    }
}
