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

layout(location=7) uniform mat4 mvp;
layout(location=8) uniform mat4 nMat;
layout(location=9) uniform mat4 rotMat;
layout(location=10) uniform mat4 worldMat;

uniform vec3 theta, pan, light;

layout(location=11) uniform bool matflags[64];
layout(location=78) uniform mat4 skinMats[128];

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

	    index.x = int(blendIndices.x);
        index.y = int(blendIndices.y);
        index.z = int(blendIndices.z);
        index.w = int(blendIndices.w);

        wPos =  blendWeights.x * skinMats[index.x] * vPosition;
        wPos += blendWeights.y * skinMats[index.y] * vPosition;
        wPos += blendWeights.z * skinMats[index.z] * vPosition;
        wPos += blendWeights.w * skinMats[index.w] * vPosition;

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