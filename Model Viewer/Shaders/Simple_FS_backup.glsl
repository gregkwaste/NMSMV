/* Copies incoming fragment color without change. */
uniform vec3 color;
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
varying mat3 TBN;
varying vec3 nvectors[3];
varying vec2 uv0;
varying float bColor;
void main()
{	
	float kd = max(dot(normalize(TBN * E), N), 0.0);

	vec4 diffTexColor=vec4(1.0, 1.0, 1.0, 1.0);
	//vec4 diffTexColor=vec4(color, 1.0);
	bool init = false;
	for (int i=diffTexCount-1;i>=0;i--){
		vec4 texColor = texture2D(diffuseTex[i], uv0);
		vec4 palColor = vec4(palColors[i], 1.0);
	// 	//vec4 t0 = vec4(palColors[i], 1.0) * texture2D(diffuseTex[i], uv0);
	 	vec4 iColor;
	 	float alpha;
	 	
	 	if (maskFlags[i]){
	 		vec4 maskColor = texture2D(maskTex[i],uv0);
	 		iColor = mix(palColor, texColor * palColor, maskColor.r);	
	 		if (!procFlag) iColor = vec4(texColor.rgb, maskColor.g);		
	 	} else {
	 		iColor = mix(palColor, texColor * palColor, texColor.a);	
	 		if (!procFlag) iColor = texColor;		
	 	}

	 	//Explicit check for non proc models
	 	if (!procFlag &  maskFlags[0]) alpha = maskColor.g;
	 	if (!procFlag & !maskFlags[0]) alpha = texColor.a;


	 	if (!init){
	 		diffTexColor = iColor;
			//diffTexColor = vec4(palColor.rgb * texColor.rgb * (1.0-texColor.a), 1.0);
	 		init = true;
 		} else{
			diffTexColor = mix(diffTexColor, iColor, texColor.a);
		}
 	// 	diffTexColor *= iColor;
	// 	//diffTexColor *= vec4(texColor.a * texColor.rgb + (1.0 - texColor.a) * palColor.rgb, 1.0);
	}
	
	vec3 diff = diffuseFlag * diffTexColor.xyz + (1.0-diffuseFlag)*color;
    
    vec3 normal = 2.0 * texture(normalTex[0], uv0) .rgb - 1.0;
	normal = normalize (normal);
	E = normalize(E);
	//TBN = transpose(TBN);
	float shininess = pow (max (dot (E, TBN * normal), 0.0), 2.0);
    
    //vec3 diff = texture(diffuseTex, uv0);
    float intense = 3.0;
    vec3 ambient = vec3(0.1, 0.1, 0.1);
    //gl_FragColor = intense*vec4((kd+1.0)* diff, 1.0);	
    gl_FragColor = vec4(ambient + intense * shininess * diff.xyz, 1.0);	
    //gl_FragColor = vec4(nvectors[1], 1.0);
    
}