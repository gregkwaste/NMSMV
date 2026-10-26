/*  Version and extension are added during preprocessing
 *  Bloom downsample: 13-tap filter (Jimenez, "Next Generation Post Processing in Call of Duty").
 *  The first pass reads the HDR frame, removes fireflies with a Karis average and applies the
 *  bright threshold. Everything stays linear and unclamped.
 */

uniform sampler2D srcTex;
uniform float uPrefilter; //1.0 on the first pass (full res HDR frame -> first mip)
uniform float uThreshold; //Linear brightness where bloom starts
uniform float uKnee;      //Width of the soft transition around the threshold

in vec2 uv0;
out vec4 fragColor;

vec3 fetch(vec2 uv)
{
    //Guard against NaN/inf and values beyond half float range. clamp() alone is not enough: min/max with
    //NaN are undefined in GLSL and some drivers return the NaN, which the blur then spreads into blocks
    vec3 c = texture(srcTex, uv).rgb;
    if (any(isnan(c)) || any(isinf(c)))
        return vec3(0.0);
    return clamp(c, vec3(0.0), vec3(65000.0));
}

float karisWeight(vec3 c)
{
    return 1.0 / (1.0 + max(c.r, max(c.g, c.b)));
}

vec3 prefilter(vec3 c)
{
    float brightness = max(c.r, max(c.g, c.b));
    float soft = clamp(brightness - uThreshold + uKnee, 0.0, 2.0 * uKnee);
    soft = soft * soft / (4.0 * uKnee + 1e-5);
    float contribution = max(soft, brightness - uThreshold) / max(brightness, 1e-5);
    return c * contribution;
}

void main()
{
    vec2 t = 1.0 / vec2(textureSize(srcTex, 0));
    vec2 uv = uv0;

    vec3 a = fetch(uv + t * vec2(-2.0,  2.0));
    vec3 b = fetch(uv + t * vec2( 0.0,  2.0));
    vec3 c = fetch(uv + t * vec2( 2.0,  2.0));
    vec3 d = fetch(uv + t * vec2(-2.0,  0.0));
    vec3 e = fetch(uv);
    vec3 f = fetch(uv + t * vec2( 2.0,  0.0));
    vec3 g = fetch(uv + t * vec2(-2.0, -2.0));
    vec3 h = fetch(uv + t * vec2( 0.0, -2.0));
    vec3 i = fetch(uv + t * vec2( 2.0, -2.0));
    vec3 j = fetch(uv + t * vec2(-1.0,  1.0));
    vec3 k = fetch(uv + t * vec2( 1.0,  1.0));
    vec3 l = fetch(uv + t * vec2(-1.0, -1.0));
    vec3 m = fetch(uv + t * vec2( 1.0, -1.0));

    vec3 result;
    if (uPrefilter > 0.5)
    {
        //Karis average: weight each 2x2 group by its inverse brightness so single very bright
        //pixels cannot flicker in the bloom
        vec3 g0 = (j + k + l + m) * 0.25;
        vec3 g1 = (a + b + d + e) * 0.25;
        vec3 g2 = (b + c + e + f) * 0.25;
        vec3 g3 = (d + e + g + h) * 0.25;
        vec3 g4 = (e + f + h + i) * 0.25;
        float w0 = 0.5   * karisWeight(g0);
        float w1 = 0.125 * karisWeight(g1);
        float w2 = 0.125 * karisWeight(g2);
        float w3 = 0.125 * karisWeight(g3);
        float w4 = 0.125 * karisWeight(g4);
        result = (g0 * w0 + g1 * w1 + g2 * w2 + g3 * w3 + g4 * w4) / (w0 + w1 + w2 + w3 + w4);
        result = prefilter(result);
    }
    else
    {
        result = e * 0.125;
        result += (a + c + g + i) * 0.03125;
        result += (b + d + f + h) * 0.0625;
        result += (j + k + l + m) * 0.125;
    }

    fragColor = vec4(result, 1.0);
}
