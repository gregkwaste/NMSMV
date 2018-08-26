#version 330
/* Copies incoming fragment color without change. */
uniform sampler2D diffuseTex;
in vec2 uv0;
in float dx,dy;

out vec4 outcolors[3];

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
	
	outcolors[0] = recolour * color;	
	outcolors[1] = vec4(1.0, 1.0, 1.0, 1.0);
	outcolors[2] = vec4(1.0, 1.0, 1.0, 1.0);
}