// Toy-plastic look: soft wrapped diffuse, a tight highlight and a rim.
// Vertex colour alpha = how much of _Color tints the vertex (1 for the body, 0 for wheels/markings).
// UV0 inside 0..1 maps into the face atlas rectangle _DecalRect; UVs outside that range show no decal.
Shader "Parkology/Plastic" {
    Properties {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Decal Atlas", 2D) = "black" {}
        _DecalRect ("Decal Rect (x,y,w,h)", Vector) = (0,0,1,1)
        _DecalAngle ("Decal Angle", Float) = 0
        _Gloss ("Gloss", Range(0,1)) = 0.55
        _Rim ("Rim", Range(0,1)) = 0.14
        _Flash ("Flash", Range(0,1)) = 0
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Plastic fullforwardshadows vertex:vert addshadow
        #pragma target 3.0
        sampler2D _MainTex;
        fixed4 _Color;
        float4 _DecalRect;
        float _DecalAngle;
        half _Gloss, _Rim, _Flash;

        struct Input {
            float2 decalUV;
            float4 color : COLOR;
            float3 viewDir;
        };

        void vert(inout appdata_full v, out Input o) {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.decalUV = v.texcoord.xy;
        }

        half4 LightingPlastic(SurfaceOutput s, half3 lightDir, half3 viewDir, half atten) {
            half ndl = dot(s.Normal, lightDir);
            half wrap = saturate(ndl * 0.6 + 0.4);
            half3 h = normalize(lightDir + viewDir);
            half spec = pow(saturate(dot(s.Normal, h)), 48.0) * s.Gloss;
            half shadow = lerp(0.42, 1.0, atten);
            half4 c;
            c.rgb = (s.Albedo * _LightColor0.rgb * wrap + _LightColor0.rgb * spec * 0.3) * shadow;
            c.a = 1;
            return c;
        }

        void surf(Input IN, inout SurfaceOutput o) {
            float2 uv = IN.decalUV;
            half inside = step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
            // Rotate the face around its centre so it stays upright on screen whichever way the block turns.
            float2 r = uv - 0.5;
            float cs = cos(_DecalAngle), sn = sin(_DecalAngle);
            float2 ruv = float2(cs * r.x - sn * r.y, sn * r.x + cs * r.y) + 0.5;
            fixed4 decal = tex2D(_MainTex, _DecalRect.xy + saturate(ruv) * _DecalRect.zw);
            decal.a *= inside * step(0.0, ruv.x) * step(ruv.x, 1.0) * step(0.0, ruv.y) * step(ruv.y, 1.0);
            half3 body = lerp(IN.color.rgb, _Color.rgb * IN.color.rgb, IN.color.a);
            o.Albedo = lerp(body, decal.rgb, decal.a);
            o.Gloss = _Gloss * (1 - decal.a);
            o.Specular = 0.5;
            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), 3.0) * _Rim;
            o.Emission = rim * half3(1, 1, 1) + _Flash * half3(1, 1, 1);
            o.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
