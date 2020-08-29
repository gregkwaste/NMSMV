/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

//Diffuse Textures
uniform sampler2D diffuseTex;

out vec4 fragColor;

vec3 gaussianBlur(){
	float offset = 3.9;  

	vec2 offsets[9] = vec2[](
        vec2(-offset,  offset), // top-left
        vec2( 0.0f,    offset), // top-center
        vec2( offset,  offset), // top-right
        vec2(-offset,  0.0f),   // center-left
        vec2( 0.0f,    0.0f),   // center-center
        vec2( offset,  0.0f),   // center-right
        vec2(-offset, -offset), // bottom-left
        vec2( 0.0f,   -offset), // bottom-center
        vec2( offset, -offset)  // bottom-right    
    );

    float kernel[9] = float[](
	    1.0 / 16, 2.0 / 16, 1.0 / 16,
	    2.0 / 16, 4.0 / 16, 2.0 / 16,
	    1.0 / 16, 2.0 / 16, 1.0 / 16  
	);

	vec3 result = vec3(0.0);
    for (int i=0; i<9; i++) { 
		result += kernel[i] * texelFetch(diffuseTex, ivec2(gl_FragCoord.xy + offsets[i]), 0).rgb;
	}
	return result;
}

void main()
{
	fragColor = vec4(gaussianBlur(), 1.0);
}
