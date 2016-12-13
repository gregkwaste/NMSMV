#version 330
/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */

layout(location=0) in vec4 vPosition;
layout(location=1) in vec3 vcolor;
uniform mat4 look, proj;
uniform vec3 theta;
uniform mat4 worldMat;

out vec3 color;

void main()
{
    //Set color
    color = vcolor;

    vec3 angles = radians( theta );
    vec3 c = cos( angles );
    vec3 s = sin( angles );
    
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
    
    //mat4 worldTransMat = mat4(1.0, 0.0, 0.0 , 0.0,
    //      0.0, 1.0, 0.0, 0.0,
    //      0.0, 0.0, 1.0, 0.0,
    //      worldTrans.x, worldTrans.y, worldTrans.z, 1.0);

    mat4 rotMat = rx*ry*rz;
    gl_Position = proj * look * rotMat * vPosition;
}