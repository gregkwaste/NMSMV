#version 330
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
uniform mat4 skinMats[128], rotMat;
uniform bool matflags[64];
uniform float scale;
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
	    
        gl_Position = wPos;
        
    } else{
    	gl_Position = vPosition.xyzw;
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
    //Normalized proper vectors
    vertex.color = vec4(1.0, 0.0, 0.0, 1.0);
    vertex.normal = normalize(lWorldNormalVec3);
    vertex.tangent = normalize(lWorldTangentVec3);
    vertex.bitangent = normalize(lWorldBitangentVec3);
    

    //Raw vectors
    //vertex.normal = nPosition.xyz;
    //vertex.tangent = tPosition.xyz;
    //vertex.bitangent = normalize(cross(nPosition.xyz, tPosition.xyz));

    
}