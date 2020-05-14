/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

//Extract brightness based on hardwired threshold

#include "/common.glsl"

//Diffuse Textures
uniform sampler2D inTex;

out vec4 fragColour; 

vec3 Threshold(
    in vec3 lColour,
    in float lfThreshold,      
    in float lfGain )
{
    vec3 lumcoeff = vec3(0.299,0.587,0.114);
    
    float lum = dot(lColour.rgb, lumcoeff);

    float thresh = max((lum - lfThreshold) * lfGain, 0.0);
    return mix( vec3(0.0), lColour, thresh );
}

void oldmain()
{
	//const vec3 brightness_threshold =  vec3(0.2126, 0.7152, 0.0722); //Brightness Threshold
	const vec3 brightness_threshold =  vec3(0.2126, 0.7152, 0.0722); //Brightness Threshold
	
	vec3 color = texelFetch(inTex, ivec2(gl_FragCoord.xy), 0).rgb;
	float brightness = dot(color, brightness_threshold);
	
	// check whether fragment output is higher than threshold, if so output as brightness color    
    if (brightness > 1.0)
        fragColour = vec4(color, 1.0);
    else
        fragColour = vec4(0.0, 0.0, 0.0, 1.0);
}


void main()
{
    vec3 lBrightColourVec3;
    lBrightColourVec3 = texelFetch(inTex, ivec2(gl_FragCoord.xy), 0).rgb;

	float lfGlowAlpha = 1.0;

    vec4 gHDRParamsVec4
	// a - Exposure (higher values make scene brighter)
	// b - Brightpass threshold (intensity where blooming begins)
	// c - BrightPass offset (smaller values produce stronger blooming) 
	= {2, 0.6, 0.06, 0};

    lBrightColourVec3 = GammaCorrectInput( lBrightColourVec3 );

    lBrightColourVec3.xyz = TonemapKodak(lBrightColourVec3.xyz) / TonemapKodak( vec3(1.0,1.0,1.0) );

    lBrightColourVec3 = GammaCorrectOutput( lBrightColourVec3 );

    lBrightColourVec3 = Threshold(  lBrightColourVec3, 
                                    min( gHDRParamsVec4.y, lfGlowAlpha),  // Threshold
                                    	 gHDRParamsVec4.z );// Offset

    lBrightColourVec3 = clamp( lBrightColourVec3, 0.0, 1.0 );
    fragColour = vec4( lBrightColourVec3, 1.0 );
}
