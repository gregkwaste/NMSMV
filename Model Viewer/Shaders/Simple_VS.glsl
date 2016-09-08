/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */

attribute vec4 vPosition;
attribute vec4 nPosition;
uniform vec3 theta, pan, light;
uniform float scale;
uniform mat4 look, proj;
out vec3 E,N;

void main()
{
	vec3 angles = radians( theta );
    vec3 c = cos( angles );
    vec3 s = sin( angles );
    vec4 light4 = vec4(light, 0.0);

	// Remeber: thse matrices are column-major
    mat4 rx = mat4( 1.0,  0.0,  0.0, 0.0,
            		0.0,  c.x,  s.x, 0.0,
            		0.0, -s.x,  c.x, 0.0,
            		0.0,  0.0,  0.0, 1.0 );

    mat4 ry = mat4( c.y, 0.0, -s.y, 0.0,
            0.0, 1.0,  0.0, 0.0,
            s.y, 0.0,  c.y, 0.0,
            0.0, 0.0,  0.0, 1.0 );

    mat4 rz = mat4( c.z, -s.z, 0.0, 0.0,
            s.z,  c.z, 0.0, 0.0,
            0.0,  0.0, 1.0, 0.0,
            0.0,  0.0, 0.0, 1.0 );

    mat4 panning = mat4(1.0, 0.0, 0.0 , 0.0,
          0.0, 1.0, 0.0, 0.0,
          0.0, 0.0, 1.0, 0.0,
          pan.x, pan.y, pan.z, 1.0);
    //      pan.x*(scale+1.0), pan.y*(scale+1.0), 0.0, 1.0);


    mat4 rotMat = rx*ry*rz;
    mat4 mviewMat = rotMat;
    mat4 nMat = transpose(inverse(rotMat));
    //gl_FrontColor = gl_Color;
    E = - (rotMat * (vPosition-light4)).xyz;
    vec4 nPos = vec4(nPosition.xyz, 0.0);
    N = normalize(nMat * nPos).xyz;

	//gl_PointSize = 10.0;
    gl_Position = proj * look * mviewMat * vPosition;
}