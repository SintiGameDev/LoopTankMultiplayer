// Bildeffekt fuer das Geschwindigkeitsgefuehl der 3D-Kamera (Built-in Render Pipeline).
// Wird von TempoEffekt.cs in OnRenderImage auf das fertige Kamerabild angewendet.
// Liegt in einem Resources-Ordner, damit der Shader auch im Build enthalten ist.
//
// Vier Bausteine, alle wachsen zum Bildrand hin und lassen die Bildmitte (Fadenkreuz) scharf:
//   - radiale Unschaerfe (Bild wird zur Mitte hin verwischt)
//   - Geschwindigkeitsstreifen links und rechts
//   - Farbsaum (Rot und Blau leicht gegeneinander verschoben)
//   - Vignette (Raender dunkler)
Shader "Hidden/LoopTank/TempoEffekt"
{
    Properties
    {
        _MainTex ("Bild", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Unschaerfe;          // 0..1
            float _Streifen;            // 0..1
            float _Farbsaum;            // 0..1
            float _Vignette;            // 0..1
            float _Lauf;                // laufende Zeit fuer die Streifen
            float _Seitenverhaeltnis;   // Breite / Hoehe
            float _StreifenAnzahl;      // Faecher rund um die Bildmitte
            float _StreifenDichte;      // 0..1, Anteil der Faecher mit Streifen
            float _StreifenLaenge;      // 0..1, Laenge eines Strichs
            float _StreifenBreite;      // 0..1, Breite eines Strichs innerhalb seines Fachs
            float _StreifenBereich;     // 0..1, ab wo von der Mitte aus Streifen erscheinen
            float4 _StreifenFarbe;      // rgb = Farbe, a = Helligkeit

            float zufall(float n)
            {
                return frac(sin(n * 12.9898) * 43758.5453);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 mitte = float2(0.5, 0.5);
                float2 d = i.uv - mitte;
                float2 dk = float2(d.x * _Seitenverhaeltnis, d.y);
                float r = length(dk);

                // 0 in der Bildmitte, 1 am Rand
                float rand = smoothstep(0.18, 0.75, r);

                // --- Radiale Unschaerfe: mehrere Proben auf der Linie zur Bildmitte ---
                float weite = _Unschaerfe * rand * 0.07;
                float3 farbe = float3(0, 0, 0);
                for (int k = 0; k < 8; k++)
                {
                    float t = 1.0 - weite * (k / 7.0);
                    farbe += tex2D(_MainTex, mitte + d * t).rgb;
                }
                farbe /= 8.0;

                // --- Farbsaum ---
                float saum = _Farbsaum * rand * 0.008;
                float rot = tex2D(_MainTex, mitte + d * (1.0 - saum)).r;
                float blau = tex2D(_MainTex, mitte + d * (1.0 + saum)).b;
                farbe.r = lerp(farbe.r, rot, 0.6 * step(0.0001, saum));
                farbe.b = lerp(farbe.b, blau, 0.6 * step(0.0001, saum));

                // --- Geschwindigkeitsstreifen ---
                // Das Bild wird um die Mitte in schmale Faecher geteilt. Ein Teil der Faecher
                // traegt einen hellen Strich, der von innen nach aussen laeuft.
                float winkel = atan2(dk.y, dk.x) / 6.2831853 + 0.5;         // 0..1 rundum
                float fach = winkel * _StreifenAnzahl;
                float nummer = floor(fach);
                float z = zufall(nummer);
                float aktiv = step(1.0 - _StreifenDichte, z);

                float quer = frac(fach);
                float vonMitte = abs(quer - 0.5) * 2.0;
                float duenn = 1.0 - smoothstep(_StreifenBreite * 0.5, max(_StreifenBreite, 0.02), vonMitte);

                float lauf = frac(r * 1.3 - _Lauf * (1.2 + z * 1.6) + z * 9.0);
                float strich = smoothstep(0.0, 0.06, lauf) * (1.0 - smoothstep(_StreifenLaenge * 0.3, max(_StreifenLaenge, 0.07), lauf));

                // nur an den Seiten des Bildes, nach oben und unten ausgeblendet
                float seite = smoothstep(_StreifenBereich, min(_StreifenBereich + 0.42, 1.0), abs(d.x) * 2.0);
                float hoehe = 1.0 - smoothstep(0.55, 0.98, abs(d.y) * 2.0);

                float linien = aktiv * duenn * strich * seite * hoehe * _Streifen;
                farbe += linien * _StreifenFarbe.rgb * _StreifenFarbe.a;

                // --- Vignette ---
                farbe *= 1.0 - _Vignette * smoothstep(0.35, 1.0, r);

                return fixed4(farbe, tex2D(_MainTex, i.uv).a);
            }
            ENDCG
        }
    }
    Fallback Off
}
