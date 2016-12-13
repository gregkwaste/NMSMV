#version 330
#extension GL_ARB_explicit_uniform_location : enable
#extension GL_ARB_separate_shader_objects : enable
/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */
layout(location=0) in vec4 vPosition;
layout(location=1) in vec2 uvPosition0;
layout(location=2) in vec4 nPosition; //normals
layout(location=3) in vec4 tPosition; //tangents
layout(location=4) in vec4 bPosition; //bitangents
layout(location=5) in vec4 blendIndices;
layout(location=6) in vec4 blendWeights;

uniform vec3 theta, pan, light;
uniform int firstskinmat;
uniform int boneRemap[256];
uniform mat4 skinMats[128];
uniform bool matflags[64];
uniform int skinned;
uniform int id;
uniform mat4 look;
uniform mat4 proj;
uniform mat4 worldMat;

flat out int object_id;

void main()
{
	vec3 angles = radians( theta );
    vec3 c = cos( angles );
    vec3 s = sin( angles );
    vec4 light4 = vec4(light, 0.0);

    //Pas normal mapping related vectors
    //nvectors[0] = tPosition.xyz;
    //nvectors[1] = cross(tPosition.xyz, nPosition.xyz);
    //nvectors[2] = nPosition.xyz;
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

    //Check F02_SKINNED
    if (matflags[1]){

    	vec4 wPos=vec4(0.0, 0.0, 0.0, 0.0);
	    ivec4 index;

	    index.x = boneRemap[int(blendIndices.x)];
	    index.y = boneRemap[int(blendIndices.y)];
	    index.z = boneRemap[int(blendIndices.z)];
	    index.w = boneRemap[int(blendIndices.w)];

	    //Calculate wPos
	    wPos  = blendWeights.x * (skinMats[index.x] * vPosition);
	    wPos += blendWeights.y * (skinMats[index.y] * vPosition);
	    wPos += blendWeights.z * (skinMats[index.z] * vPosition);
	    wPos += blendWeights.w * (skinMats[index.w] * vPosition);

		gl_Position = proj * look * mviewMat * wPos;
        
    } else{
    	gl_Position = proj * look * mviewMat * worldMat * vPosition;
    }
    object_id = id;
}