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
uniform float scale;
uniform mat4 mvp, rotMat, worldMat;

//Outputs
out vec3 E;
out vec3 N;
out vec2 uv0;
out float l_distance;
out mat3 TBN;
out float bColor;
out vec4 finalPos;

void main()
{
	vec4 light4 = vec4(light, 0.0);
    //Pass uv
    uv0 = uvPosition0;
    //Pas normal mapping related vectors
    //nvectors[0] = tPosition.xyz;
    //nvectors[1] = cross(tPosition.xyz, nPosition.xyz);
    //nvectors[2] = nPosition.xyz;
	// Remeber: thse matrices are column-major
    
    mat4 nMat = transpose(inverse(rotMat));
    //mat4 nMat = rotMat;
    
    N = normalize(nMat * vec4(nPosition.xyz, 0.0)).xyz;
    
    //gl_FrontColor = gl_Color;
    E = (rotMat * (light4 - vPosition)).xyz; //Light vector
    l_distance = distance(vPosition.xyz, light);
    //E = - ((vPosition-light4)).xyz; //Light vector
    E = normalize(E);
    //E = - rotMat(vPosition-light4).xyz; //Light vector
    
    //Construct TBN matrix
    //Nullify w components
    vec4 lLocalTangentVec4 = tPosition;
    vec4 lLocalBitangentVec4 = vec4(bPosition.xyz, 0.0);
    vec4 lLocalNormalVec4 = nPosition;
    
    vec4 lWorldTangentVec4 = nMat * lLocalTangentVec4;
    vec4 lWorldNormalVec4 = nMat * lLocalNormalVec4;
    vec4 lWorldBitangentVec4 = vec4( cross(lWorldNormalVec4.xyz, lWorldTangentVec4.xyz), 0.0);
    
    TBN = mat3( normalize(lWorldTangentVec4.xyz),
                normalize(lWorldBitangentVec4.xyz),
                normalize(lWorldNormalVec4.xyz) );

    //TBN = transpose(TBN);

    //Check F02_SKINNED
    if (matflags[1]) {
        vec4 wPos = vec4(0.0, 0.0, 0.0, 0.0);
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
		bColor = blendIndices.x/255.0;
	    
	    //gl_PointSize = 10.0;
	    finalPos = wPos;
        gl_Position = mvp * wPos;
        //gl_Position = mvp * worldMat * vPosition;
        
    } else {
        finalPos = worldMat * vPosition;
    	gl_Position = mvp * finalPos;
    }
    
}