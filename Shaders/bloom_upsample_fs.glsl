/*  Version and extension are added during preprocessing
 *  Bloom upsample: 3x3 tent filter of the smaller mip. Drawn with additive blending (One, One),
 *  so each mip accumulates the blurred mips below it. The last pass composites into the HDR frame
 *  with uIntensity as the bloom strength.
 */

uniform sampler2D srcTex;
uniform float uRadius;    //Tent radius in source texels
uniform float uIntensity; //1.0 inside the chain, bloom strength for the final composite

in vec2 uv0;
out vec4 fragColor;

void main()
{
    vec2 d = uRadius / vec2(textureSize(srcTex, 0));
    vec2 uv = uv0;

    vec3 s = texture(srcTex, uv + vec2(-d.x,  d.y)).rgb;
    s += texture(srcTex, uv + vec2( 0.0,  d.y)).rgb * 2.0;
    s += texture(srcTex, uv + vec2( d.x,  d.y)).rgb;
    s += texture(srcTex, uv + vec2(-d.x,  0.0)).rgb * 2.0;
    s += texture(srcTex, uv).rgb * 4.0;
    s += texture(srcTex, uv + vec2( d.x,  0.0)).rgb * 2.0;
    s += texture(srcTex, uv + vec2(-d.x, -d.y)).rgb;
    s += texture(srcTex, uv + vec2( 0.0, -d.y)).rgb * 2.0;
    s += texture(srcTex, uv + vec2( d.x, -d.y)).rgb;

    s *= uIntensity / 16.0;
    if (any(isnan(s)) || any(isinf(s)))
        s = vec3(0.0);

    //Alpha 0 so the additive blend leaves the destination alpha untouched
    fragColor = vec4(s, 0.0);
}
