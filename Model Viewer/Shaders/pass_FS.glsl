/* Copies incoming fragment color without change. */
//Diffuse Textures
uniform sampler2D diffuseTex[8];
uniform sampler2D maskTex[8];
uniform sampler2D normalTex[8];
uniform float lbaseLayersUsed[8];
uniform float lalphaLayersUsed[8];
uniform vec4 lRecolours[8];

//uniform vec3 palColors[8];

uniform bool recolour;
uniform bool hasAlphaChannel;
uniform int mode;

varying vec2 uv0;
varying vec3 color;
//layout(location=0) out vec3 tcolor;

#include "/common.glsl"

//Recolour function
// vec3 Recolour(vec3 lOriginalColourVec3,
// 			  vec3 lAverageColourVec3,
// 			  vec3 lRecolourVec3,
// 			  float lfMix){

// 	vec3 lOriginalHSVVec3 = RGBToHSV(lOriginalColourVec3);
// 	vec3 lAverageHSVVec3 = RGBToHSV(lAverageColourVec3);
// 	vec3 lRecolourHSVVec3 = RGBToHSV(lRecolourVec3);
	
// 	lOriginalHSVVec3.r = fract((lOriginalHSVVec3.r - lAverageHSVVec3.r) + lRecolourHSVVec3.r);
// 	lOriginalHSVVec3.g = min( lOriginalHSVVec3.g, lRecolourHSVVec3.g);
// 	lOriginalHSVVec3.b = pow(10.0, (-10.0 *(lOriginalHSVVec3.b - 0.5) *(lOriginalHSVVec3.b -0.5))) * (0.5* (lRecolourHSVVec3.b - lAverageHSVVec3.b)) + lOriginalHSVVec3.b;

// 	lOriginalHSVVec3 = GammaCorrectInput(saturate(HSVToRGB(lOriginalHSVVec3)));
// 	lOriginalHSVVec3 = mix(GammaCorrectInput(lOriginalColourVec3), lOriginalHSVVec3, lfMix);

// 	return lOriginalHSVVec3;
// }

out vec3 outcolors[2];

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

    lOriginalHSVVec3.r = fract( (  lOriginalHSVVec3.r - lAverageHSVVec3.r ) + lRecolourHSVVec3.r );
    lOriginalHSVVec3.g = min(      lOriginalHSVVec3.g, lRecolourHSVVec3.g );
    //lOriginalHSVVec3.b = saturate( lOriginalHSVVec3.b + sin( 3.14 * lOriginalHSVVec3.b ) * ( lRecolourHSVVec3.b - lAverageHSVVec3.b ) );
    lOriginalHSVVec3.b = pow(10.0, (-10.0 * (lOriginalHSVVec3.b-0.5)*(lOriginalHSVVec3.b-0.5) ) ) * (0.5 * ( lRecolourHSVVec3.b - lAverageHSVVec3.b )) + lOriginalHSVVec3.b;

    lOriginalHSVVec3 = GammaCorrectInput( saturate( HSVToRGB( lOriginalHSVVec3 ) ) );
    //lOriginalHSVVec3 = saturate( HSVToRGB( lOriginalHSVVec3 ) );

    lOriginalHSVVec3 = mix( GammaCorrectInput( lOriginalColourVec3 ), lOriginalHSVVec3, lfMix );
	//lOriginalHSVVec3 = mix( lOriginalColourVec3 , lOriginalHSVVec3, lfMix );
     
    return lOriginalHSVVec3;
}

vec4 MixMaskMaps(){
	vec4 lLayerXVec4[8];
	float lfAlpha[8];
	vec4 lFinalDiffColor;
	lFinalDiffColor.r = 0.0;
	
	for (int i=0; i<8; i++){
		lLayerXVec4[i] = texture2D(maskTex[i], uv0);
		lfAlpha[i] = lLayerXVec4[i].a;
	}

	for (int i=0; i<8; i++)
		lFinalDiffColor.r = max(lFinalDiffColor.r, (1.0- lLayerXVec4[i].r) * lfAlpha[i]);

	lFinalDiffColor.r = 1.0 - lFinalDiffColor.r;

	return lFinalDiffColor;
}

//Diffuse Color Mixing
vec4 MixDiffuseMaps(){
	//Constants
	float gBaseAlphaLayerXVec4[8];
	gBaseAlphaLayerXVec4[0] = 0.0;
	gBaseAlphaLayerXVec4[1] = 0.0;
	gBaseAlphaLayerXVec4[2] = 0.0;
	gBaseAlphaLayerXVec4[3] = 1.0;
	gBaseAlphaLayerXVec4[4] = 0.0;
	gBaseAlphaLayerXVec4[5] = 0.0;
	gBaseAlphaLayerXVec4[6] = 0.0;
	gBaseAlphaLayerXVec4[7] = 1.0;

	vec4 gAverageColourXVec4[8];
	gAverageColourXVec4[0] = vec4(0.5, 0.5, 0.5, 1.0);
	gAverageColourXVec4[1] = vec4(0.5, 0.5, 0.5, 1.0);
	gAverageColourXVec4[2] = vec4(0.5, 0.5, 0.5, 1.0);
	gAverageColourXVec4[3] = vec4(0.5, 0.5, 0.5, 1.0);
	gAverageColourXVec4[4] = vec4(0.5, 0.5, 0.5, 1.0);
	gAverageColourXVec4[5] = vec4(0.5, 0.5, 0.5, 1.0);
	gAverageColourXVec4[6] = vec4(0.5, 0.5, 0.5, 1.0);
	gAverageColourXVec4[7] = vec4(0.5, 0.5, 0.5, 1.0);

	//Storage Arrays
	vec4 lLayerXVec4[8];
	float lfAlpha[8];

	//Output Color
	vec4 lFinalDiffColor = vec4(1.0, 1.0, 1.0, 0.0);

	//Fetch Diffuse Colors

	for (int i=0; i< 8; i++){
		lLayerXVec4[i] = texture2D(diffuseTex[i], uv0);
		lfAlpha[i] = lLayerXVec4[i].a;
	}

	 
 	for (int i=0;i<8;i++)
 		lfAlpha[i] = min(lfAlpha[i], texture2D(maskTex[i], uv0).r);
	//  if (!hasAlphaChannel)
	// 	for (int i=0;i<8;i++)
	// 		lfAlpha[i] = mix(1.0, 1.0 - texture2D(maskTex[i], uv0).r, lalphaLayersUsed[i]);

	//Set the lowest alpha layer to fully opaque
	//for (int i=0;i<8;i++)
	//	lfAlpha[i] = mix(lfAlpha[i], 1.0, gBaseAlphaLayerXVec4[i]);

	//Set the alpha for any layer which is not used to 0
	for (int i=0; i<8; i++)
		lfAlpha[i] *= lbaseLayersUsed[i];

	//RECOLOURING HAPPENS HERE
	//for (int i=0;i<8;i++)
	//	lLayerXVec4[i].rgb = Recolour(lLayerXVec4[i].rgb, gAverageColourXVec4[i].rgb, lRecolours[i].rgb, 1.0);

	//Blend Layers together
	for (int i=0; i<8; i++)
		lFinalDiffColor = mix(lFinalDiffColor, lLayerXVec4[i], lfAlpha[i]);

	//Set Final Alpha
	//if (hasAlphaChannel)
	//	for (int i=0; i<8; i++)
	//		lFinalDiffColor.a = mix(lFinalDiffColor.a, lLayerXVec4[i].a, gBaseAlphaLayerXVec4[i]);
	
	return vec4(lFinalDiffColor.rgb, 1.0);
}

void main()
{
	//gl_FragColor = vec4(texture2D(diffuseTex[0], uv0).rgb, 1.0);
	//gl_FragColor = MixDiffuseMaps();
	outcolors[0] = MixDiffuseMaps();
	outcolors[1] = MixMaskMaps();
	//outcolors[2] = MixNormalMaps();

}
