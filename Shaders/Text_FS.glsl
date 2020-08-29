/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */


//Includes
#include "/common.glsl"
#include "/common_structs.glsl"
#include "/common_lighting.glsl"


in vec2 uv;
uniform vec3 color;
//uniform float edge;
//uniform float width;
uniform CustomPerMaterialUniforms mpCustomPerMaterial;

//Deferred Shading outputs
out vec4 fragColor;



void main(){
	vec2 eff_uv = vec2(uv.x, uv.y);
	vec4 texColor = textureLod(mpCustomPerMaterial.gDiffuseMap, vec3(eff_uv, 0.0), 0.0);
	float dist = 1.0 - texColor.a;

	float width = 0.5;
	float edge = 0.2;
	
	float alpha = 1.0 - smoothstep(width, width + edge, dist);

	fragColor = vec4(color, alpha);
	//fragColor = vec4(1.0, 1.0, 0.5, 1.0);
}