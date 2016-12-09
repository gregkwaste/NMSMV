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

	vec3 color = texture2D(diffuseTex, uv0);
	color = vec3(1.0, 1.0, 1.0) - color;

	color -= dFdx(color) * dx;
	color -= dFdy(color) * dy;
	//color += dFdxFine(color) * dx;
	
	vec3 recolour = vec3(1.0, 1.0, 0.0);
	color = recolour * color;
	
	if ((color.x<0.01) && (color.y<0.01) && (color.z<0.01))
		discard;
	else 
		gl_FragColor = vec4(recolour * color, 1.0);	
	//	gl_FragColor = vec4(recolour, 1.0);	

}