/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

layout(location = 0) in vec4 vPosition;
layout(location = 1) in vec3 vColor;

out vec2 uv0;
out vec3 color;

void main()
{
	color = vColor.xyz;
    uv0 = vPosition.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);
    
    gl_Position = vec4(vPosition.xyz, 1.0);
}