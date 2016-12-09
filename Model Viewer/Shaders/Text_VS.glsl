/* Simple Quad Rendering Shader
 */
attribute vec4 vPosition;
attribute vec4 uvPosition;
//Outputs
varying vec2 uv0;
varying float dx, dy;
uniform mat4 projMat;
uniform float w, h;
//Text Transforms
uniform vec2 pos;
uniform float scale;



void main()
{
    //uv0 = vPosition.xy * vec2(0.5, 0.5) + vec2(0.5, 0.5);
    uv0 = uvPosition.xy;
    uv0.y = 1.0 - uv0.y;
    dx = 2.0/w;
    dy = 2.0/h;
    //Render to UV coordinate
    mat4 projmat = mat4(400.0/w, 0.0,    0.0, 0.0,
                        0.0,   400.0/h,  0.0, 0.0,
                        0.0,   0.0,    -1.0, 0.0,
                        0.0, 0.0,  0.0, 1.0);

    vec4 offset = vec4(-1.0, -1.0, 0.0, 0.0) + vec4(pos, 0.0, 0.0);
    gl_Position = offset + projmat * vec4(scale*vPosition.xyz, 1.0);
}