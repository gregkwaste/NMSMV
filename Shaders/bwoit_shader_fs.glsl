//Blender Weighted Order Independent Transparency Composite Shader

//Diffuse Textures
uniform sampler2D in1Tex;
uniform sampler2D in2Tex;

out vec4 fragColour; 

void main()
{
	vec4 accum = texelFetch(in1Tex, ivec2(gl_FragCoord.xy), 0);
    float reveal = texelFetch(in2Tex, ivec2(gl_FragCoord.xy), 0).r;

    fragColour = vec4(accum.rgb/max(accum.a, 1e-5), reveal);
}
