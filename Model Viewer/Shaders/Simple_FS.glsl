/* Copies incoming fragment color without change. */
uniform vec3 color;
//Diffuse Textures
uniform int diffTexCount;
uniform sampler2D diffuseTex[8];
uniform vec3 palColors[8];
//Normal Texture
uniform sampler2D normalTex;
uniform float diffuseFlag;

varying vec3 E,N;
varying vec2 uv0;
varying float bColor;
void main()
{	
	float kd = max(dot(E, N), 0.0);

	vec4 diffTexColor=vec4(1.0, 1.0, 1.0, 1.0);
	//vec4 diffTexColor=vec4(color, 1.0);
	bool init = false;
	for (int i=diffTexCount-1;i>=0;i--){
		vec4 texColor = texture2D(diffuseTex[i], uv0);
		vec4 palColor = vec4(palColors[i], 1.0);
	// 	//vec4 t0 = vec4(palColors[i], 1.0) * texture2D(diffuseTex[i], uv0);
	 	//vec4 iColor = mix(palColor, texColor, texColor.a);
		vec4 iColor = mix(palColor, palColor * texColor, texColor.a);
	 	if (!init){
	 		diffTexColor = mix(palColor, texColor * palColor, texColor.a);
	 		//diffTexColor = vec4(palColor.rgb * texColor.rgb * (1.0-texColor.a), 1.0);
	 		init = true;
	 	} else{
	 		diffTexColor = mix(diffTexColor, iColor, texColor.a);
	 	}
 	// 	diffTexColor *= iColor;
	// 	//diffTexColor *= vec4(texColor.a * texColor.rgb + (1.0 - texColor.a) * palColor.rgb, 1.0);
	}
	
	vec3 diff = diffuseFlag * diffTexColor.xyz + (1.0-diffuseFlag)*color;
    
    vec3 normal = 2.0 * texture(normalTex, uv0) .rgb - 1.0;
	normal = normalize (normal);
	float shininess = pow (max (dot (E, normal), 0.0), 2.0);
    
    //vec3 diff = texture(diffuseTex, uv0);
    float intense = 10.0;
    
    //gl_FragColor = intense*vec4((kd+1.0)* diff, 1.0);	
    gl_FragColor = vec4((kd+1.0)*diff.xyz, 1.0);	
    //gl_FragColor = vec4(0,0,bColor,1.0);
    
}