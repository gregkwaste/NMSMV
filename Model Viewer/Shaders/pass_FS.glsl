#version 440
/* Copies incoming fragment color without change. */
uniform sampler2D diffuseTex[8];
uniform sampler2D maskTex[8];


varying vec2 uv0;
varying vec3 color;
//layout(location=0) out vec3 tcolor;

void main()
{	
	//gl_FragColor = vec4(texture2D(diffuseTex[0], uv0).rgb, 1.0);
	//gl_FragColor = vec4(uv0, 0.0, 1.0);
	//tcolor = color;
	//gl_FragColor = vec4(color, 1.0);
	gl_FragColor = vec4(0.0, 1.0, 0.0, 1.0);
}