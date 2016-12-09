#version 330
/* Copies incoming vertex color without change.
 * Applies the transformation matrix to vertex position.
 */
attribute vec4 vPosition;
attribute vec4 nPosition; //normals
attribute vec4 tPosition; //tangents
attribute vec4 bPosition; //bitangents
attribute vec2 uvPosition0;
attribute vec4 blendWeights;
attribute vec4 blendIndices;
uniform vec3 theta, pan, light;
uniform int firstskinmat;
uniform int boneRemap[256];
uniform mat4 skinMats[128], worldMat;
uniform int skinned;
uniform bool matflags[64];
uniform float scale;
uniform mat4 look, proj;
//Outputs

//Output for geometry shader

out Vertex
{
  vec3 normal;
  vec3 tangent;
  vec3 bitangent;
  vec4 color;
} vertex;


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
    mat4 nMat;
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

		//wPos = BMs[int(tempI.x)]*vPosition;
		//gl_PointSize = 10.0;
	    
        //gl_Position = proj * look * mviewMat * wPos;
        mat4 nMat = transpose(inverse(look * mviewMat));
        gl_Position = wPos;
        vertex.color = vec4(1.0, 1.0, 0.0, 1.0);
	    
    } else{
    	//gl_Position = proj * look * mviewMat * worldMat * vPosition;
        mat4 nMat = transpose(inverse(look * mviewMat));
        gl_Position = vPosition.xyzw;
        vertex.color = vec4(1.0, 0.0, 0.0, 1.0);
    }

    //Construct TBN matrix
    //Nullify w components
    vec3 lLocalTangentVec3 = tPosition.xyz;
    vec3 lLocalBitangentVec3 = bPosition.xyz;
    vec3 lLocalNormalVec3 = normalize(nPosition.xyz);
    
    vec3 lWorldTangentVec3 = (vec4(lLocalTangentVec3, 1.0)).xyz;
    vec3 lWorldNormalVec3 =  (vec4(lLocalNormalVec3, 1.0)).xyz;
    vec3 lWorldBitangentVec3 = cross(lWorldNormalVec3, lWorldTangentVec3);

    //Handle Geometry Shader outputs
    vertex.normal = normalize(lWorldNormalVec3);
    vertex.tangent = normalize(lWorldTangentVec3);
    vertex.bitangent = normalize(lWorldBitangentVec3);

    
}