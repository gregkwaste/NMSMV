static const vec3 kGammaOutVec3 = vec3( 1.0 / 2.2 );
static const vec3 kGammaInVec3  = vec3( 2.2 );
static const vec4 RGBToHSV_K    = vec4( 0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0 );
static const vec4 HSVToRGB_K    = vec4( 1.0,  2.0 / 3.0, 1.0 / 3.0,  3.0 );


vec3 
GammaCorrectInput(
    in vec3 lColourVec3 )
{
    vec3 lCorrectColourVec3;

    lCorrectColourVec3 = pow( lColourVec3, kGammaInVec3 );

    return lCorrectColourVec3;
}