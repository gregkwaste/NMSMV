/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

 
//Diffuse Textures
uniform sampler2D depthTex;
uniform sampler2D decalTex;
uniform mat4 mvp, invProj, invView, decalInvMat, worldMat;

in vec2 uv0, uv1;
in vec4 clipPos;
in vec4 viewPos;


vec4 worldfromDepth()
{
	vec4 world;
	world.xy = clipPos.xy / clipPos.w;
	vec2 depth_uv = 0.5 * world.xy + 0.5;
	world.z = 2.0 * texture2D(depthTex, depth_uv).r - 1.0;
	//world.xy = 2.0 * uv0 - 1.0;
	//world.xy = clipPos.xy / clipPos.w;
	world.w = 1.0f;

	//My way
	//world = inverse(mvp) * world;
	//world /= world.w;
	world = invProj * world;
	world /= world.w;
	world = invView * world;

	return world;
}

vec2 clipToUV(vec2 test){
	return 0.5 * test + 0.5;
}

vec2 UVToClip(vec2 test){
	return 2.0 * test - 1.0;
}


void clip(float test) { if (test < 0.0) discard; }

void main()
{
	//vec4 world = worldfromDepth();
	//Use positions texture

	vec2 screenPos = clipPos.xy / clipPos.w;
	vec2 texCoord = clipToUV(screenPos);
	vec4 world = texture2D(depthTex, texCoord);

	world /= world.w;

	vec4 localPos = inverse(worldMat) * world;
	localPos /= localPos.w;

	//Clip
	clip(0.5 - abs(localPos.x));
	clip(0.5 - abs(localPos.y));
	clip(0.5 - abs(localPos.z));
	
	//clip(0.5 - localPos.z);

	// if (dist0 < 0.0)
	// 	discard;
	// 	//gl_FragColor = vec4(0.0, 1.0, 0.0, 1.0);
	// else if (dist1 <0.0)
	// 	gl_FragColor = vec4(1.0, 0.0, 0.0, 1.0);
	// else
	//gl_FragColor = vec4(0.0, 0.0, 1.0, 1.0);

	vec4 color = texture2D(decalTex, uv1);
	gl_FragColor = vec4(color.rgb, color.a);
	//gl_FragColor = localPos;
}
