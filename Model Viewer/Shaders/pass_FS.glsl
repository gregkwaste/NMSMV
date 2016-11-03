/* Copies incoming fragment color without change. */
//Diffuse Textures
uniform int diffTexCount;
uniform sampler2D diffuseTex[8];
uniform sampler2D maskTex[8];
uniform bool maskFlags[8];
uniform sampler2D normalTex[8];
uniform bool normalFlags[8];
//uniform vec3 palColors[8];

uniform float diffuseFlag;
uniform bool procFlag;

varying vec2 uv0;
varying vec3 color;
//layout(location=0) out vec3 tcolor;


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

	//Storage Arrays
	vec4 lLayerXVec4[8];
	float lfAlpha[8];

	//Output Color
	vec4 lFinalDiffColor = vec4(1.0, 1.0, 1.0, 0.0);

	//Fetch Diffuse Colors
	for (int i=0; i< diffTexCount; i++){
		lLayerXVec4[i] = texture2D(diffuseTex[i], uv0);
		if (!maskFlags[i]) lfAlpha[i] = lLayerXVec4[i].a;
		else lfAlpha[i] = texture2D(maskTex[i], uv0).r;
	}

	for (int i=0; i<= diffTexCount; i++)
		lFinalDiffColor = mix(lFinalDiffColor, lLayerXVec4[i], lfAlpha[i]);

	//Calculate Alpha Channel
	for (int i=0;i<diffTexCount;i++)
		lFinalDiffColor.a = mix(lFinalDiffColor.a, lLayerXVec4[i].a, gBaseAlphaLayerXVec4[i]);


	return lFinalDiffColor;
}

void main()
{	
	//gl_FragColor = vec4(texture2D(diffuseTex[0], uv0).rgb, 1.0);
	gl_FragColor = MixDiffuseMaps();
	//gl_FragColor = vec4(uv0, 0.0, 1.0);
	//tcolor = color;
	//gl_FragColor = vec4(color, 1.0);
	//gl_FragColor = vec4(0.0, 1.0, 0.0, 1.0);
}