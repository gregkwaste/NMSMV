/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

layout(location = 0) in vec4 vPosition;

out vec2 uv0;

void main()
{
	uv0 = vPosition.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);
    uv0.x = 1.0 - uv0.x; //Mirror on Y
    //Render to UV coordinate
    float w,h;
    w = 1.0;
    h = 1.0;
    mat4 projMat = mat4(1.0/w, 0.0,  0.0, 0.0,
                        0.0, 1.0/h,  0.0, 0.0,
                        0.0, 0.0, -1.0/1.0, 0.0,
                        0.0, 0.0,  0.0, 1.0);

    gl_Position = vec4(vPosition.xyz, 1.0);

}