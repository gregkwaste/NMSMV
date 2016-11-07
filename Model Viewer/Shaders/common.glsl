//static const vec3 kGammaOutVec3 = vec3( 1.0 / 2.2 );
//static const vec3 kGammaInVec3  = vec3( 2.2 );
//static const vec4 RGBToHSV_K    = vec4( 0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0 );
//static const vec4 HSVToRGB_K    = vec4( 1.0,  2.0 / 3.0, 1.0 / 3.0,  3.0 );


vec3 GammaCorrectInput(
    in vec3 lColourVec3 )
{
    vec3 kGammaOutVec3 = vec3( 1.0 / 2.2 );
    vec3 kGammaInVec3  = vec3( 2.2 );
    vec3 lCorrectColourVec3;

    lCorrectColourVec3 = pow( lColourVec3, kGammaInVec3 );

    return lCorrectColourVec3;
}

//Saturate Function
vec3 saturate(in vec3 color){
	return min(max(color, 0.0), 1.0);
}

//-----------------------------------------------------------------------------
///
///     RGBToHSV
///
//-----------------------------------------------------------------------------
vec3 
RGBToHSV(
    vec3 lRGB )
{
    vec4 RGBToHSV_K    = vec4( 0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0 );
    //vec4 p = mix( vec4(lRGB.bg, RGBToHSV_K.wz), vec4(lRGB.gb, RGBToHSV_K.xy), step(lRGB.b, lRGB.g) );
    //vec4 q = mix( vec4(p.xyw, lRGB.r), vec4(lRGB.r, p.yzx), step(p.x, lRGB.r) );
    // This variant is faster, since it generates conditional moves
    vec4 p = lRGB.g < lRGB.b ? vec4(lRGB.bg, RGBToHSV_K.wz) : vec4(lRGB.gb, RGBToHSV_K.xy);
    vec4 q = lRGB.r < p.x ? vec4(p.xyw, lRGB.r) : vec4(lRGB.r, p.yzx);    
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
vec3 
HSVToRGB(
    vec3 lHSV )
{
    vec4 HSVToRGB_K    = vec4( 1.0,  2.0 / 3.0, 1.0 / 3.0,  3.0 );
    vec3 p = abs(fract(lHSV.xxx + HSVToRGB_K.xyz) * 6.0 - HSVToRGB_K.www);
    return lHSV.z * mix(HSVToRGB_K.xxx, saturate(p - HSVToRGB_K.xxx), lHSV.y);
}


