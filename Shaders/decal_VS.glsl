#version 330
/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */

layout(location=0) in vec4 vPosition;
layout(location=1) in vec2 uvPosition0;
layout(location=2) in vec4 nPosition; //normals
layout(location=3) in vec4 tPosition; //tangents
layout(location=4) in vec4 bPosition; //bitangents

uniform mat4 mvp, proj, look, worldMat;

out vec2 uv0, uv1;
out vec4 clipPos;
out vec4 viewPos;



void main()
{
	uv0 = vPosition.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);
    uv1 = uvPosition0;
    //Render to UV coordinate
    viewPos = look * worldMat * vec4(vPosition.xyz, 1.0);
    clipPos = proj * viewPos;
    gl_Position = mvp * worldMat * vec4(vPosition.xyz, 1.0);
}