//#extension GL_ARB_shading_language_include : require
//Imports
#include "/common.glsl"

/* Copies incoming fragment color without change. */
uniform vec3 color;
uniform float intensity;
//Diffuse Textures
uniform int diffTexCount;
uniform sampler2D diffuseTex[8];
uniform sampler2D maskTex[8];
uniform bool maskFlags[8];
uniform sampler2D normalTex[8];
uniform bool normalFlags[8];
uniform vec3 palColors[8];
//Normal Texture
uniform float diffuseFlag;
uniform bool procFlag;

varying vec3 E,N;
varying float l_distance;
varying mat3 TBN;
varying vec3 nvectors[3];
varying vec2 uv0;
varying float bColor;

//Normal Decode Function
vec3 DecodeNormalMap(vec4 lNormalTexVec4){
	lNormalTexVec4 = (lNormalTexVec4 * 2.0) - 1.0;

	return normalize(vec3(lNormalTexVec4.a, lNormalTexVec4.g, 
					 (1.0 - lNormalTexVec4.a * lNormalTexVec4.a) * \
					 (1.0 - lNormalTexVec4.g * lNormalTexVec4.g) ));
}


void main()
{	
	//Final Light/Normal vector calculations
	
	
	//Colors
	vec4 diffTexColor;
	vec3 lightColor = vec3(0.8, 0.8, 0.8);
	vec3 ambient;

	float shininess;
	float bshininess;
	//vec4 diffTexColor=vec4(color, 1.0);
	bool init = false;
	
	for (int i=diffTexCount-1;i>=0;i--){
		vec4 texColor = texture2D(diffuseTex[i], uv0);
		vec4 palColor = vec4(palColors[i], 1.0);
	// 	//vec4 t0 = vec4(palColors[i], 1.0) * texture2D(diffuseTex[i], uv0);
	 	vec4 iColor;
	 	float alpha;
	 	float shininess;
	 	//Mask Checks
	 	if (maskFlags[i]){
	 		alpha = texture2D(maskTex[i], uv0).r;
 		} else {
	 		alpha = texColor.a;
	 	}
	 	//Normal Checks
	 	vec3 normal;
	 	if (normalFlags[i]){
	 		normal = DecodeNormalMap(texture2D(normalTex[i], uv0));
	 		bshininess = pow(max (dot (E, normalize(TBN * normal)), 0.0), 2.0);
	 		//shininess = pow(max (dot (E, N), 0.0), 2.0);
		} else {
			normal = N;
			bshininess = pow(max (dot (E, N), 0.0), 2.0);	
			//bshininess = 1.0;
		}
	 	shininess = pow(max (dot (E, N), 0.0), 2.0);
	 	//shininess = 1.0;
	 	
	 	iColor = mix(palColor, texColor * palColor, alpha);	

	 	//Explicit check for non proc models
	 	if (!procFlag &&  maskFlags[0]) iColor = vec4(texColor.rgb , alpha);
	 	if (!procFlag && !maskFlags[0]) iColor = texColor;

	 	if (!init){
	 		ambient = iColor.rgb;
	 		diffTexColor = (0.0 + bshininess) * iColor;
	 		
			//diffTexColor = vec4(palColor.rgb * texColor.rgb * (1.0-texColor.a), 1.0);
	 		init = true;
 		} else {
			ambient = mix(diffTexColor, iColor, texColor.a).rgb;
			diffTexColor = mix(diffTexColor, (0.0 + bshininess) * iColor, texColor.a);
		}
 	
 	}
	
	ambient = 0.8 * ambient;
	vec3 diff = diffuseFlag * diffTexColor.xyz + (1.0-diffuseFlag)*color;
	diff = intensity * lightColor * diff.xyz; //(l_distance*l_distance);
        
    //gl_FragColor = vec4(ambient + (intense + 1.0) * diff.xyz, 1.0);	
    gl_FragColor = vec4(ambient + diff.xyz, 1.0);	
    //gl_FragColor = vec4(nvectors[1], 1.0);
    
}