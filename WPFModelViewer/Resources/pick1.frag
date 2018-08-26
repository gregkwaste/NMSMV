#version 330
#extension GL_ARB_explicit_uniform_location : enable
flat in int object_id;
//flat int object_id;
layout(location=0) out vec4 outcolor;

void main()
{	
	
	//Calculate fragcolor from object_id
	vec4 color = vec4(0.0, 0.0, 0.0, 1.0);

	color.r = float(object_id & 0xFF) /255.0;
	color.g = float((object_id>>8) & 0xFF) /255.0;
	color.b = 0.0;

	//pickColor = color;
	outcolor = color;
}