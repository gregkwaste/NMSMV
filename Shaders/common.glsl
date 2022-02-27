//const vec3 kGammaOutVec3 = vec3( 1.0 / 2.2 );
//const vec3 kGammaInVec3  = vec3( 2.2 );
//static const vec4 RGBToHSV_K    = vec4( 0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0 );
//static const vec4 HSVToRGB_K    = vec4( 1.0,  2.0 / 3.0, 1.0 / 3.0,  3.0 );

#define PI 3.14159265359


//These defines are used for quick reference on the material flags.
//For ease of use all the includes will be the same flags with an underscore 

#define _F01_DIFFUSEMAP 0
#define _F02_SKINNED 1
#define _F03_NORMALMAP 2
#define _F07_UNLIT 6
#define _F08_ 7
#define _F09_TRANSPARENT 8
#define _F11_ALPHACUTOUT 10
#define _F14_UVSCROLL 13
#define _F16_DIFFUSE2MAP 15
#define _F17_MULTIPLYDIFFUSE2MAP 16
#define _F21_VERTEXCOLOUR 20
#define _F22_TRANSPARENT_SCALAR 21
#define _F24_AOMAP 23
#define _F25_ROUGHNESS_MASK 24
#define _F28_VBSKINNED 27
#define _F29_VBCOLOUR 28
#define _F31_DISPLACEMENT 30
#define _F34_GLOW 33
#define _F35_GLOW_MASK 34
#define _F37_RECOLOUR 36
#define _F39_METALLIC_MASK 38
#define _F40_SUBSURFACE_MASK 39
#define _F41_DETAIL_DIFFUSE 40
#define _F42_DETAIL_NORMAL 41
#define _F43_NORMAL_TILING 42
#define _F51_DECAL_DIFFUSE 50
#define _F52_DECAL_NORMAL 51
#define _F53_COLOURISABLE 52
#define _F55_MULTITEXTURE 54
#define _F62_DETAIL_ALPHACUTOUT 61




float max3(vec3 vec){
    return max(max(vec.r, vec.g), vec.b);
}

vec3 
GammaCorrectInput(
    in vec3 lColourVec3 )
{
    vec3 lCorrectColourVec3;
    lCorrectColourVec3 = lColourVec3 * ( lColourVec3 * ( lColourVec3 * vec3( 0.305306011 ) + vec3( 0.682171111 ) ) + vec3( 0.012522878 ) );
    return lCorrectColourVec3;
}

vec3 fixColorGamma(vec3 color){
    float gamma = 2.2;
    return pow(color.rgb, vec3(1.0 / gamma));
}

vec3 
GammaCorrectOutput(
    in vec3 lColourVec3 )
{
    vec3 kGammaOutVec3 = vec3( 1.0 / 2.2 );

    vec3 lCorrectColourVec3;
    lCorrectColourVec3 = pow( lColourVec3, kGammaOutVec3 );
    return lCorrectColourVec3;
}

//Saturate Function
vec3 saturate(in vec3 color){
	return min(max(color, 0.0), 1.0);
}

float saturate(in float val){
    return min(max(val, 0.0), 1.0);
}

//TONEMAPPING FUNCTIONS
vec3 
TonemapKodak(
    in vec3 x )
{
    return (( x * ( 0.22 * x + 0.10 * 0.30 ) + 0.20 * 0.01 ) / ( x * ( 0.22 * x + 0.30 ) + 0.20 * 0.30 ) ) - 0.01 / 0.30 ;
}


//-----------------------------------------------------------------------------
///
///     RGBToHSV
///
//-----------------------------------------------------------------------------
vec3 RGBToHSV(vec3 c)
{
    vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    vec4 p = mix(vec4(c.bg, K.wz), vec4(c.gb, K.xy), step(c.b, c.g));
    vec4 q = mix(vec4(p.xyw, c.r), vec4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    float e = 1.0e-10;
    return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}


//-----------------------------------------------------------------------------
///
///     HSVToRGB
///
///     @brief      http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
///
//-----------------------------------------------------------------------------
vec3 HSVToRGB(vec3 c)
{
    vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
    vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
    return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}


//-----------------------------------------------------------------------------
///
///     GetUpperValue
///
//-----------------------------------------------------------------------------
float 
GetUpperValue( 
    float lValue )
{
    int a = int( lValue * 255 );
    return ( float(a >> 4) / 16.0 );
}

//-----------------------------------------------------------------------------
///
///     GetLowerValue
///
///
//-----------------------------------------------------------------------------
float 
GetLowerValue( 
    float lValue )
{
    int a = int( lValue * 255 );
    float lReturn = float( a & 0xf ) / 16.0;
    lReturn = clamp( lReturn - GetUpperValue( lValue ), 0.0, 1.0 );
    return lReturn;
}