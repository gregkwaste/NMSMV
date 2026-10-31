in vec2 uv;
uniform sampler2D fontAtlas;
uniform vec3 textColor;
out vec4 fragColor;

void main()
{
    float distanceValue = 1.0 - texture(fontAtlas, uv).a;
    float alpha = 1.0 - smoothstep(0.50, 0.70, distanceValue);
    fragColor = vec4(textColor, alpha);
}
