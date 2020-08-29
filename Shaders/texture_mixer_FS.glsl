/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */
 

//Diffuse Textures
uniform sampler2DArray mainTex[8];
uniform sampler2DArray alphaTex[8];
uniform float lbaseLayersUsed[8];
uniform vec4 lRecolours[8];
uniform vec4 lAverageColors[8];

uniform float recolor_flag;
uniform float use_alpha_textures;
uniform int baseLayerIndex;

in vec2 uv0;
in vec3 color;

#include "/common.glsl"

out vec4 gl_FragColor;

vec3
Recolour(
    vec3  lOriginalColourVec3,
    vec3  lAverageColourVec3,
    vec3  lRecolourVec3,
    float lfMix )
{
    vec3 lOriginalHSVVec3 = RGBToHSV( fixColorGamma(lOriginalColourVec3 ) );
    vec3 lAverageHSVVec3  = RGBToHSV( lAverageColourVec3 );
    vec3 lRecolourHSVVec3 = RGBToHSV( lRecolourVec3 );

    //Adjust Hue
    //lOriginalHSVVec3.r = fract( lOriginalHSVVec3.r - lAverageHSVVec3.r + lRecolourHSVVec3.r );
    lOriginalHSVVec3.r = mix(lRecolourHSVVec3.r, lOriginalHSVVec3.r, 0.2);
    
    //Adjust Saturation
    lOriginalHSVVec3.g = min( lOriginalHSVVec3.g, lRecolourHSVVec3.g );
	
	//Adjust Value
	//lOriginalHSVVec3.b = saturate( lOriginalHSVVec3.b + sin( 3.14 * lOriginalHSVVec3.b ) * ( lRecolourHSVVec3.b - lAverageHSVVec3.b ) );
	lOriginalHSVVec3.b = pow(10.0, (-10.0 * (lOriginalHSVVec3.b - 0.5)*(lOriginalHSVVec3.b - 0.5) ) ) * (0.5 * ( lRecolourHSVVec3.b - lAverageHSVVec3.b )) + lOriginalHSVVec3.b;

    lOriginalHSVVec3 = GammaCorrectInput( saturate( HSVToRGB( lOriginalHSVVec3 ) ) );
    lOriginalHSVVec3 = mix( GammaCorrectInput( lOriginalColourVec3 ), lOriginalHSVVec3, lfMix );
    
    return lOriginalHSVVec3;
}

vec3 toGrayScale(vec3 color){
	float avg = (color.r + color.g + color.b) / 3.0;
	return vec3(avg, avg, avg);
}

//Diffuse Color Mixing
vec4 MixTextures(){
	//Constants
	float gBaseAlphaLayerXVec4[8];
	gBaseAlphaLayerXVec4[0] = 0.0;
	gBaseAlphaLayerXVec4[1] = 0.0;
	gBaseAlphaLayerXVec4[2] = 0.0;
	gBaseAlphaLayerXVec4[3] = 0.0;
	gBaseAlphaLayerXVec4[4] = 0.0;
	gBaseAlphaLayerXVec4[5] = 0.0;
	gBaseAlphaLayerXVec4[6] = 0.0;
	gBaseAlphaLayerXVec4[7] = 0.0;

	//Storage Arrays
	vec4 lLayerXVec4[8];
	float lfAlpha[8];

	//Fetch Diffuse Colors
	for (int i=0; i<8; i++){
		lLayerXVec4[i] = texture(mainTex[i], vec3(uv0, 0.0));
		if (use_alpha_textures > 0.0) {
			lfAlpha[i] = texture(alphaTex[i], vec3(uv0, 0.0)).a;
		}
		 else {
			lfAlpha[i] = lLayerXVec4[i].a;	
		}
	}

	//My input
	//Set Base layer
 	//gBaseAlphaLayerXVec4[baseLayerIndex] = 1.0f;


	
	//Set the lowest alpha layer to fully opaque
	gBaseAlphaLayerXVec4[baseLayerIndex] = 1.0;
	for (int i=0; i<8; i++) {
	   	lfAlpha[i] = mix(lfAlpha[i], 1.0, gBaseAlphaLayerXVec4[i]);
	}
	
	//Set the alpha for any layer which is not used to 0
	for (int i=0; i<8; i++) {
		lfAlpha[i] *= lbaseLayersUsed[i];
	}

	//RECOLOURING HAPPENS HERE
	// vec4 iColour[8];
	// for (int i=0; i<8; i++){
	// 	//iColour[i] = mix( lLayerXVec4[i] * lRecolours[i], lRecolours[i], lfAlpha[i] );
	// 	iColour[i] = mix(lRecolours[i], lLayerXVec4[i] * lRecolours[i], lfAlpha[i]);	
	// }
	
	if (recolor_flag > 0.0) {
		// Original Color Mix
		for (int i=0; i<8; i++){
			//Maintain original color
			//lLayerXVec4[i].rgb = lLayerXVec4[i].rgb;
			
			//Recoloring Modes
			lLayerXVec4[i].rgb = Recolour(lLayerXVec4[i].rgb, lAverageColors[i].rgb, lRecolours[i].rgb, lRecolours[i].a);
			
			//my way
			//lLayerXVec4[i].rgb = lRecolours[i].rgb * toGrayScale(lLayerXVec4[i].rgb);
			//lLayerXVec4[i].rgb = lRecolours[i].rgb;
		}
	}
	

	//Blend Layers together
	//Blend the opposite way
	vec4 lFinalDiffColor = vec4(0.0, 0.0, 0.0, 0.0);
	for (int i=7; i>=0; i--) {
		lFinalDiffColor = mix(lFinalDiffColor, lLayerXVec4[i], lfAlpha[i]);
	}

	//Output Color
	//return lLayerXVec4[4];
	return lFinalDiffColor;
}


void main()
{
	gl_FragColor = MixTextures();
	//gl_FragColor = vec4(1.0, 1.0, 0.0, 1.0);
}
