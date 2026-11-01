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

uniform float lHasMap[8];   //Masks/normals: 0 when the layer has a diffuse but no file of this kind
uniform vec4 lMissingValue; //Used for those layers (flat normal), so they still cover the layers below

uniform float recolor_flag;
uniform float use_alpha_textures;
uniform int baseLayerIndex;

in vec2 uv0;
in vec3 color;

#include "/common.glsl"

out vec4 FragColor;

vec3
Recolour(
    vec3  lOriginalColourVec3,
    vec3  lAverageColourVec3,
    vec3  lRecolourVec3,
    float lfMix )
{
    //The textures are sampled linear (sRGB formats), the recolouring works on the stored (gamma) values
    vec3 lOriginalGammaVec3 = fixColorGamma( lOriginalColourVec3 );
    vec3 lOriginalHSVVec3 = RGBToHSV( lOriginalGammaVec3 );
    vec3 lAverageHSVVec3  = RGBToHSV( fixColorGamma( lAverageColourVec3 ) );
    vec3 lRecolourHSVVec3 = RGBToHSV( lRecolourVec3 );

    //Shift the hue by the difference between the palette colour and the texture's average colour,
    //so the texture's own hue variation is kept
    lOriginalHSVVec3.r = fract( lOriginalHSVVec3.r - lAverageHSVVec3.r + lRecolourHSVVec3.r );
    
    //Saturation can only go down to the palette's
    lOriginalHSVVec3.g = min( lOriginalHSVVec3.g, lRecolourHSVVec3.g );
	
	//Move the value by the same offset, mostly in the mid tones
	lOriginalHSVVec3.b = saturate( lOriginalHSVVec3.b + sin( PI * lOriginalHSVVec3.b ) * ( lRecolourHSVVec3.b - lAverageHSVVec3.b ) );

    vec3 lRecolouredVec3 = saturate( HSVToRGB( lOriginalHSVVec3 ) );
    return GammaCorrectInput( mix( lOriginalGammaVec3, lRecolouredVec3, lfMix ) );
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

	vec3 lAverageXVec3[8];

	//Fetch Diffuse Colors
	for (int i=0; i<8; i++){
		lLayerXVec4[i] = texture(mainTex[i], vec3(uv0, 0.0));
		//The texture's average colour is its smallest mip, unless the descriptor overrides it (negative = not set)
		if (lAverageColors[i].r < 0.0)
			lAverageXVec3[i] = textureLod(mainTex[i], vec3(0.5, 0.5, 0.0), float(textureQueryLevels(mainTex[i]) - 1)).rgb;
		else
			lAverageXVec3[i] = lAverageColors[i].rgb;
		if (use_alpha_textures > 0.0) {
			if (lHasMap[i] < 0.5)
				lLayerXVec4[i] = lMissingValue;
			lfAlpha[i] = texture(alphaTex[i], vec3(uv0, 0.0)).a;
		}
		 else {
			lfAlpha[i] = lLayerXVec4[i].a;	
		}
	}

	//My input
	//Set Base layer
 	//gBaseAlphaLayerXVec4[baseLayerIndex] = 1.0f;


	
	//The base layer's own alpha is the material's opacity, keep it for the output
	float lfBaseAlpha = lLayerXVec4[baseLayerIndex].a;

	//The base layer (highest used index) covers everything, overlays use their alpha as coverage
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
			lLayerXVec4[i].rgb = Recolour(lLayerXVec4[i].rgb, lAverageXVec3[i], lRecolours[i].rgb, lRecolours[i].a);
			
			//my way
			//lLayerXVec4[i].rgb = lRecolours[i].rgb * toGrayScale(lLayerXVec4[i].rgb);
			//lLayerXVec4[i].rgb = lRecolours[i].rgb;
		}
	}
	

	//Descriptors list overlays first: blend backwards, from the base up to the topmost overlay
	vec4 lFinalDiffColor = vec4(0.0, 0.0, 0.0, 0.0);
	for (int i=7; i>=0; i--) {
		lFinalDiffColor = mix(lFinalDiffColor, lLayerXVec4[i], lfAlpha[i]);
	}
	//Masks and normals blend all four channels. The diffuse alpha is the opacity, not coverage, so it comes from the base
	if (use_alpha_textures <= 0.0)
		lFinalDiffColor.a = lfBaseAlpha;

	//Output Color
	//return lLayerXVec4[4];
	return lFinalDiffColor;
}


void main()
{
	FragColor = MixTextures();
	//gl_FragColor = vec4(1.0, 1.0, 0.0, 1.0);
}
