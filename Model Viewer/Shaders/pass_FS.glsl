#version 330
/* Copies incoming fragment color without change. */
//Diffuse Textures
uniform sampler2DArray diffuseTex[8];
uniform sampler2DArray maskTex[8];
uniform sampler2DArray normalTex[8];
uniform float d_lbaseLayersUsed[8];
uniform float m_lbaseLayersUsed[8]; //Masks
uniform float n_lbaseLayersUsed[8]; //Normals
uniform float lalphaLayersUsed[8];
uniform vec4 lRecolours[8];
uniform vec4 lAverageColors[8];

//uniform vec3 palColors[8];

uniform bool recolour;
uniform float hasAlphaChannel;
uniform int mode;
uniform int baseLayerIndex;

varying vec2 uv0;
varying vec3 color;
//layout(location=0) out vec3 tcolor;

#include "/common.glsl"

out vec4 outcolors[3];

vec3
Recolour(
    vec3  lOriginalColourVec3,
    vec3  lAverageColourVec3,
    vec3  lRecolourVec3,
    float lfMix )
{
    vec3 lOriginalHSVVec3 = RGBToHSV( lOriginalColourVec3 );
    vec3 lAverageHSVVec3  = RGBToHSV( lAverageColourVec3 );
    vec3 lRecolourHSVVec3 = RGBToHSV( lRecolourVec3 );

    lOriginalHSVVec3.r = fract( lOriginalHSVVec3.r - lAverageHSVVec3.r + lRecolourHSVVec3.r );
    //lOriginalHSVVec3.r = lRecolourHSVVec3.r;
    lOriginalHSVVec3.g = min( lOriginalHSVVec3.g, lRecolourHSVVec3.g );
    //lOriginalHSVVec3.b = saturate( lOriginalHSVVec3.b + sin( 3.14 * lOriginalHSVVec3.b ) * ( lRecolourHSVVec3.b - lAverageHSVVec3.b ) );
    lOriginalHSVVec3.b = pow(10.0, (-10.0 * (lOriginalHSVVec3.b-0.5)*(lOriginalHSVVec3.b-0.5) ) ) * (0.5 * ( lRecolourHSVVec3.b - lAverageHSVVec3.b )) + lOriginalHSVVec3.b;

    lOriginalHSVVec3 = GammaCorrectInput( saturate( HSVToRGB( lOriginalHSVVec3 ) ) );


    lOriginalHSVVec3 = mix( GammaCorrectInput( lOriginalColourVec3 ), lOriginalHSVVec3, lfMix );
     
    return lOriginalHSVVec3;
}

vec3 toGrayScale(vec3 color){
	float avg = (color.r + color.g + color.b) / 3.0;
	return vec3(avg, avg, avg);
}

vec4 MixMaskMaps(){
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

	vec4 lLayerXVec4[8];
	float lfAlpha[8];

	//Output Color
	vec4 lFinalDiffColor = vec4(0.0, 0.0, 0.0, 0.0);
	
	for (int i=0; i<8; i++){
		lLayerXVec4[i] = texture(maskTex[i], vec3(uv0, 0.0));
		lfAlpha[i] = texture(diffuseTex[i], vec3(uv0, 0.0)).a;
	}

	//My input
	//Set Base layer
 	gBaseAlphaLayerXVec4[baseLayerIndex] = 1.0f;

	//Set the lowest alpha layer to fully opaque
	for (int i=0; i<8; i++){
	   	lfAlpha[i] = mix(lfAlpha[i], 1.0, gBaseAlphaLayerXVec4[i]);
	}

	//Set the alpha for any layer which is not used to 0
	for (int i=0; i<8; i++){
		lfAlpha[i] *= m_lbaseLayersUsed[i];
	}

	//Blend Layers together
	//Blend the opposite way
	for (int i=7; i>=0; i--) {
		lFinalDiffColor = mix(lFinalDiffColor, lLayerXVec4[i], lfAlpha[i]);
	}
	
	return lFinalDiffColor;
}

//Diffuse Color Mixing
vec4 MixDiffuseMaps(){
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

	//Output Color
	vec4 lFinalDiffColor = vec4(0.0, 0.0, 0.0, 0.0);

	//Fetch Diffuse Colors
	for (int i=0; i<8; i++){
		lLayerXVec4[i] = texture(diffuseTex[i], vec3(uv0, 0.0));
		lfAlpha[i] = lLayerXVec4[i].a;
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
		lfAlpha[i] *= d_lbaseLayersUsed[i];
	}

	//RECOLOURING HAPPENS HERE
	// vec4 iColour[8];
	// for (int i=0; i<8; i++){
	// 	//iColour[i] = mix( lLayerXVec4[i] * lRecolours[i], lRecolours[i], lfAlpha[i] );
	// 	iColour[i] = mix(lRecolours[i], lLayerXVec4[i] * lRecolours[i], lfAlpha[i]);	
	// }
	
	// Original Color Mix
	for (int i=0; i<8; i++){
		//Maintain original color
		//lLayerXVec4[i].rgb = lLayerXVec4[i].rgb;
		
		//Recoloring Modes
		lLayerXVec4[i].rgb = Recolour(lLayerXVec4[i].rgb, lAverageColors[i].rgb, lRecolours[i].rgb, lRecolours[i].a);
		
		//my way
		//lLayerXVec4[i].rgb = lRecolours[i].rgb * toGrayScale(lLayerXVec4[i].rgb);
	}

	//Blend Layers together
	//Blend the opposite way
	bool init = false;
	for (int i=7; i>=0; i--) {
		lFinalDiffColor.rgb = mix(lFinalDiffColor.rgb, lLayerXVec4[i].rgb, lfAlpha[i]);
	}

	//Set Final Alpha
	//if (hasAlphaChannel > 0.0)
		//for (int i=0; i<8; i++)
			//lFinalDiffColor.a = mix(lFinalDiffColor.a, lLayerXVec4[i].a, gBaseAlphaLayerXVec4[i]);
	
	for (int i=0; i<8; i++) {
		lFinalDiffColor.a = max(lFinalDiffColor.a, lLayerXVec4[i].a);
	}
	
	//MAIN
	//return vec4(lFinalDiffColor.rgb, 1.0);
	return vec4(lFinalDiffColor.rgba);
	//return vec4(lRecolours[0].rgb, 1.0);
	//return gRecolourXVec4[0];
	//return vec4(lFinalDiffColor.rgba);
}

//Diffuse Color Mixing
vec4 MixNormalMaps(){
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

	//Output Color
	vec4 lFinalDiffColor = vec4(0.0, 0.0, 0.0, 0.0);

	//Fetch Normal Colors
	for (int i=0; i<8; i++){
		lLayerXVec4[i] = texture(normalTex[i], vec3(uv0, 0.0));
		lfAlpha[i] = texture(diffuseTex[i], vec3(uv0, 0.0)).a;
	}

	//My input
	//Set Base layer
 	gBaseAlphaLayerXVec4[baseLayerIndex] = 1.0f;

	//Set the lowest alpha layer to fully opaque
	for (int i=0; i<8; i++){
	   	lfAlpha[i] = mix(lfAlpha[i], 1.0, gBaseAlphaLayerXVec4[i]);
	}

	//Set the alpha for any layer which is not used to 0
	for (int i=0; i<8; i++){
		lfAlpha[i] *= n_lbaseLayersUsed[i];
	}

	//Blend Layers together
	bool init = false;
	for (int i=7; i >= 0; i--){
		lFinalDiffColor = mix(lFinalDiffColor, lLayerXVec4[i], lfAlpha[i]);
	}
	
	return lFinalDiffColor;
}

void main()
{
	//gl_FragColor = vec4(texture2D(diffuseTex[0], uv0).rgb, 1.0);
	//gl_FragColor = MixDiffuseMaps();
	outcolors[0] = MixDiffuseMaps();
	outcolors[1] = MixMaskMaps();
	outcolors[2] = MixNormalMaps();

}
