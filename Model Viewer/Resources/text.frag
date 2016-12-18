/* Copies incoming fragment color without change. */
uniform sampler2D diffuseTex;
varying vec2 uv0;
varying float dx,dy;

void main()
{	
	
	/*
		Character Maps are usually full white textures with
		black characters. The Shader should invert the colors
		and use the input color to recolour the letters
	*/

	vec4 color = textureLod(diffuseTex, uv0, 0.0);
	//color = vec4(vec3(1.0, 1.0, 1.0) - color.rgb, color.a);

	//color -= dFdx(color) * dx;
	//color -= dFdy(color) * dy;
	//color += dFdxFine(color) * dx;
	
	vec4 recolour = vec4(1.0, 1.0, 0.0, 1.0);
	//color = recolour * color;
	
	
	gl_FragColor = recolour * color;	
	//	gl_FragColor = vec4(recolour, 1.0);	

}