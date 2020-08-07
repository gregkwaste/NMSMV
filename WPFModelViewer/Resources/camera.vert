/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */
layout(location=0) in vec4 vPosition;
layout(location=1) in vec4 nPosition; //normals
uniform mat4 self_mvp, mvp;

//Outputs
void main()
{
	vec4 inv_pos = inverse(self_mvp) * vPosition;

	inv_pos.z = min(inv_pos.z, 1000);

    gl_Position = mvp * inv_pos;

	//gl_Position = mvp * vPosition;
	gl_Position = gl_Position * 1.0f/gl_Position.w;
}