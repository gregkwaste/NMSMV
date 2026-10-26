/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */



#include "/common.glsl"
#include "/common_structs.glsl"


#define FXAA_SPAN_MAX 16.0
#define FXAA_REDUCE_MUL   (1.0 / FXAA_SPAN_MAX)
#define FXAA_REDUCE_MIN   (1.0 / 128.0)


//Diffuse Textures
uniform sampler2D diffuseTex;
out vec4 finalColor;

//Uniform Blocks
layout (std140, binding=0) uniform _COMMON_PER_FRAME
{
    CommonPerFrameUniforms mpCommonPerFrame;
};



vec4 fxaa(vec2 uv, vec2 u_texelStep)
{
    vec3 rgbM = texture(diffuseTex, uv).rgb;
    vec4 fragColor = vec4(vec3(0.0), 1.0);
    //Edge thresholds (FXAA 3.11 "quality" presets): relative to the local maximum, with an absolute
    //floor so tiny differences in dark areas are not treated as edges
    const float kEdgeThreshold = 0.125;
    const float kEdgeThresholdMin = 0.0312;

    // Sampling neighbour texels. Offsets are adapted to OpenGL texture coordinates. 
    vec3 rgbNW = textureOffset(diffuseTex, uv, ivec2(-1, 1)).rgb;
    vec3 rgbNE = textureOffset(diffuseTex, uv, ivec2(1, 1)).rgb;
    vec3 rgbSW = textureOffset(diffuseTex, uv, ivec2(-1, -1)).rgb;
    vec3 rgbSE = textureOffset(diffuseTex, uv, ivec2(1, -1)).rgb;

    // see http://en.wikipedia.org/wiki/Grayscale
    const vec3 toLuma = vec3(0.299, 0.587, 0.114);
    
    // Convert from RGB to luma.
    float lumaNW = dot(rgbNW, toLuma);
    float lumaNE = dot(rgbNE, toLuma);
    float lumaSW = dot(rgbSW, toLuma);
    float lumaSE = dot(rgbSE, toLuma);
    float lumaM = dot(rgbM, toLuma);

    // Gather minimum and maximum luma.
    float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
    float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));
    
    // If contrast is lower than a maximum threshold ...
    if (lumaMax - lumaMin < max(kEdgeThresholdMin, lumaMax * kEdgeThreshold))
    {
        return vec4(rgbM, 1.0);
    }  
    
    // Sampling is done along the gradient.
    vec2 samplingDirection; 
    samplingDirection.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
    samplingDirection.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));
    
    // Sampling step distance depends on the luma: The brighter the sampled texels, the smaller the final sampling step direction.
    // This results, that brighter areas are less blurred/more sharper than dark areas.  
    float samplingDirectionReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * 0.25 * FXAA_REDUCE_MUL, FXAA_REDUCE_MIN);

    // Factor for norming the sampling direction plus adding the brightness influence. 
    float minSamplingDirectionFactor = 1.0 / (min(abs(samplingDirection.x), abs(samplingDirection.y)) + samplingDirectionReduce);
    
    // Calculate final sampling direction vector by reducing, clamping to a range and finally adapting to the texture size. 
    samplingDirection = clamp(samplingDirection * minSamplingDirectionFactor, vec2(-FXAA_SPAN_MAX), vec2(FXAA_SPAN_MAX)) * u_texelStep;
    
    // Inner samples on the tab.
    vec3 rgbSampleNeg = texture(diffuseTex, uv + samplingDirection * (1.0/3.0 - 0.5)).rgb;
    vec3 rgbSamplePos = texture(diffuseTex, uv + samplingDirection * (2.0/3.0 - 0.5)).rgb;

    vec3 rgbTwoTab = (rgbSamplePos + rgbSampleNeg) * 0.5;  

    // Outer samples on the tab.
    vec3 rgbSampleNegOuter = texture(diffuseTex, uv + samplingDirection * (0.0/3.0 - 0.5)).rgb;
    vec3 rgbSamplePosOuter = texture(diffuseTex, uv + samplingDirection * (3.0/3.0 - 0.5)).rgb;
    
    vec3 rgbFourTab = (rgbSamplePosOuter + rgbSampleNegOuter) * 0.25 + rgbTwoTab * 0.5;   
    
    // Calculate luma for checking against the minimum and maximum value.
    float lumaFourTab = dot(rgbFourTab, toLuma);
    
    // Are outer samples of the tab beyond the edge ... 
    if (lumaFourTab < lumaMin || lumaFourTab > lumaMax)
    {
        // ... yes, so use only two samples.
        fragColor = vec4(rgbTwoTab, 1.0); 
    }
    else
    {
        // ... no, so use four samples. 
        fragColor = vec4(rgbFourTab, 1.0);
    }

    return fragColor;
}

void main()
{
    vec2 uv2 = gl_FragCoord.xy / mpCommonPerFrame.frameDim;
    vec2 texelSize = 1.0 / mpCommonPerFrame.frameDim;

    finalColor = fxaa(uv2, texelSize);
}
