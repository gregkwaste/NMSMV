#version 450

//One workgroup handles one cluster. The CPU-built list already contains only lights whose range
//sphere intersects the cluster; this pass conservatively removes authored spotlights whose cone
//cannot intersect a sphere enclosing the cluster. Exact per-pixel cone checks remain in the lighting
//shader, so boundary cases can safely stay in the list.
layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;

uniform mat4 projMatInv;
uniform mat4 viewMat;
uniform float cameraNear;
uniform float cameraFar;

struct Light
{
    vec4 position;
    vec4 color;
    vec4 direction;
    float falloff;
    float type;
    float range;
    float lodAttenuationScale;
};

layout(std430, binding = 2) readonly buffer SceneLights
{
    Light sceneLights[];
};

layout(std430, binding = 3) readonly buffer InputClusters
{
    uvec4 clusterGrid;
    vec4 clusterParams;
    uvec2 clusterCells[];
};

layout(std430, binding = 4) readonly buffer InputIndices
{
    uint clusterLightIndices[];
};

layout(std430, binding = 6) buffer OutputClusters
{
    uvec4 outputGrid;
    vec4 outputParams;
    uvec2 outputCells[];
};

layout(std430, binding = 7) writeonly buffer OutputIndices
{
    uint outputLightIndices[];
};

vec3 unproject(float x, float y, float z)
{
    vec4 p = projMatInv * vec4(x, y, z, 1.0);
    return p.xyz / p.w;
}

void clusterSphere(ivec2 tile, int slice, out vec3 center, out float radius)
{
    float depth0 = cameraNear * pow(cameraFar / cameraNear, float(slice) / float(clusterGrid.z));
    float depth1 = cameraNear * pow(cameraFar / cameraNear, float(slice + 1) / float(clusterGrid.z));
    float t0 = (depth0 - cameraNear) / (cameraFar - cameraNear);
    float t1 = (depth1 - cameraNear) / (cameraFar - cameraNear);
    float x0 = 2.0 * float(tile.x) / float(clusterGrid.x) - 1.0;
    float x1 = 2.0 * float(tile.x + 1) / float(clusterGrid.x) - 1.0;
    float y0 = 2.0 * float(tile.y) / float(clusterGrid.y) - 1.0;
    float y1 = 2.0 * float(tile.y + 1) / float(clusterGrid.y) - 1.0;

    vec3 minimum = vec3(3.402823e38);
    vec3 maximum = vec3(-3.402823e38);
    for (int corner = 0; corner < 4; ++corner)
    {
        float x = (corner & 1) == 0 ? x0 : x1;
        float y = (corner & 2) == 0 ? y0 : y1;
        vec3 nearPoint = unproject(x, y, -1.0);
        vec3 farPoint = unproject(x, y, 1.0);
        vec3 p0 = mix(nearPoint, farPoint, t0);
        vec3 p1 = mix(nearPoint, farPoint, t1);
        minimum = min(minimum, min(p0, p1));
        maximum = max(maximum, max(p0, p1));
    }

    center = 0.5 * (minimum + maximum);
    //Inflate the enclosing sphere slightly so floating-point error at tile/depth boundaries retains
    //the candidate. False positives cost only an exact per-pixel test; false negatives lose light.
    radius = length(0.5 * (maximum - minimum));
    radius += max(radius * 1e-5, 1e-5);
}

bool sphereOutsideSpotCone(Light light, vec3 center, float radius)
{
    //Point lights bypass cone filtering. Authored 360-degree lights are uploaded as points as well.
    if (light.type < 1.0)
        return false;

    float coneCos = clamp(light.direction.w, -1.0, 1.0);
    if (coneCos <= -0.999999)
        return false;

    vec3 apex = (viewMat * vec4(light.position.xyz, 1.0)).xyz;
    vec3 axis = normalize(mat3(viewMat) * light.direction.xyz);
    vec3 toCenter = center - apex;
    float distanceSquared = dot(toCenter, toCenter);
    if (distanceSquared <= radius * radius)
        return false;

    float distanceToCenter = sqrt(distanceSquared);
    float sinDelta = clamp(radius / distanceToCenter, 0.0, 1.0);
    float cosDelta = sqrt(max(0.0, 1.0 - sinDelta * sinDelta));
    float sinTheta = sqrt(max(0.0, 1.0 - coneCos * coneCos));

    //For an obtuse cone, theta + the sphere's angular radius may cover the full opposite direction.
    if (coneCos < 0.0 && sinDelta >= sinTheta)
        return false;

    float cosThetaPlusDelta = coneCos * cosDelta - sinTheta * sinDelta;
    float centerCos = dot(axis, toCenter) / distanceToCenter;
    return centerCos < cosThetaPlusDelta - 2e-5;
}

void main()
{
    ivec2 tile = ivec2(gl_WorkGroupID.xy);
    int slice = int(gl_WorkGroupID.z);
    uint cluster = (uint(slice) * clusterGrid.y + uint(tile.y)) * clusterGrid.x + uint(tile.x);
    uvec2 inputCell = clusterCells[cluster];

    vec3 center;
    float radius;
    clusterSphere(tile, slice, center, radius);

    uint outputCount = 0u;
    for (uint i = 0u; i < inputCell.y; ++i)
    {
        uint lightIndex = clusterLightIndices[inputCell.x + i];
        if (sphereOutsideSpotCone(sceneLights[lightIndex], center, radius))
            continue;

        outputLightIndices[inputCell.x + outputCount] = lightIndex;
        ++outputCount;
    }
    //Stable compaction leaves point-only and 360-degree-light lists byte-for-byte unchanged.
    outputCells[cluster] = uvec2(inputCell.x, outputCount);
}
