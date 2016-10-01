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
uniform mat4 worldMat;
uniform mat4 skinMats[128];
uniform int skinned;
uniform float scale;
uniform mat4 look, proj, worldRot;
//Outputs
varying vec3 E,N;
varying vec2 uv0;
varying float bColor;

void main()
{
	vec3 angles = radians( theta );
    vec3 c = cos( angles );
    vec3 s = sin( angles );
    vec4 light4 = vec4(light, 0.0);

    //Pass uv
    uv0 = uvPosition0;
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

    if (skinned==1){
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
		bColor = blendIndices.x/255.0;
	    
	    //gl_PointSize = 10.0;
	    gl_Position = proj * look * mviewMat * wPos;
	    
    } else{
    	gl_Position = proj * look * mviewMat * worldMat * vPosition;
    }
    
}