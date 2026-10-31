//PBR Functions
float DistributionGGX(vec3 N, vec3 H, float roughness)
{
    float a      = roughness*roughness;
    float a2     = a*a;
    float NdotH  = max(dot(N, H), 0.0);
    float NdotH2 = NdotH*NdotH;
    
    float num   = a2;
    float denom = (NdotH2 * (a2 - 1.0) + 1.0);
    denom = PI * denom * denom;
    
    return num / denom;
}

float GeometrySchlickGGX(float NdotV, float roughness)
{
    float r = (roughness + 1.0);
    float k = (r*r) / 8.0;

    float num   = NdotV;
    float denom = NdotV * (1.0 - k) + k;
    
    return num / denom;
}

float GeometrySmith(vec3 N, vec3 V, vec3 L, float roughness)
{
    float NdotV = max(dot(N, V), 0.0);
    float NdotL = max(dot(N, L), 0.0);
    float ggx2  = GeometrySchlickGGX(NdotV, roughness);
    float ggx1  = GeometrySchlickGGX(NdotL, roughness);
    
    return ggx1 * ggx2;
}

//pow() is undefined for a negative base, and cosTheta can come out as 1.0000001
//(e.g. the camera fill light, where H == V), which produced NaN pixels
vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    float m = clamp(1.0 - cosTheta, 0.0, 1.0);
    float m2 = m * m;
    return F0 + (1.0 - F0) * (m2 * m2 * m);
}  

vec3 fresnelSchlickRoughness(float cosTheta, vec3 F0, float roughness)
{
    return F0 + (max(vec3(1.0 - roughness), F0) - F0) * pow(clamp(1.0 - cosTheta, 0.0, 1.0), 5.0);
}

//Roughness 0 makes the GGX distribution a spike (fireflies / NaN highlights)
float clampRoughness(float roughness)
{
    return clamp(roughness, 0.045, 1.0);
}

float calcLightAttenuation(Light light, vec4 _fragPos){
    float attenuation = 0.0f;

    //General Configuration
    
    //New light system
    float lfLightIntensity = light.color.w;
    //float lfLightIntensity = sqrt(light.color.w);
    //float lfLightIntensity = log(light.color.w) / log(10.0);
    
    vec3 lightPos = light.position.xyz; 
    vec3 lightDir = normalize(_fragPos.xyz - lightPos);
    vec3 lPosToLight = lightPos - _fragPos.xyz;

    //vec3 lightDir = normalize(mpCommonPerFrame.cameraDirection);
    float l_distance = distance(lightPos, _fragPos.xyz); //Calculate distance of 
    float lfDistanceSquared = l_distance * l_distance; //Distance to light squared
    

    float lfFalloffType = light.falloff;
    float lfCutOff = 0.05;

    vec3 lspotDir = normalize(light.direction.xyz);

    //Point Lights
    if (light.type < 1.0){
        lspotDir = lightDir;
    }

    //SpotLights
    //Calculate attenuation
    if (lfFalloffType < 1.0)
    {
        // Quadratic Distance attenuation
        //attenuation = 1.0 / max(1.0, 0.5 * lfDistanceSquared * lfDistanceSquared);
        attenuation = 1.0 / max(1e-6, lfDistanceSquared);
    } else if (lfFalloffType < 2.0) {
        //Constant
        attenuation = 1.0;
    }
    else if (lfFalloffType < 3.0)
    {
        // Linear Distance attenuation
        attenuation = inversesqrt(lfDistanceSquared);
        attenuation = min( attenuation, 1.0 );
        //attenuation *= lfLightIntensity;
    }
    else
    {
        // Square root distance attenuation (LINEAR_SQRT)
        attenuation = min(inversesqrt(max(l_distance, 1e-6)), 1.0);
    }

    /*
    //Doing it the NMS way :D
    float lfLightFOV = cos(light.direction.w / 2.0);
    float lfConeAngle = dot(lspotDir, lightDir);
    float lfConeAttenuation   = saturate( (lfConeAngle - lfLightFOV) * 5.0 );

    attenuation *= lfConeAttenuation;
    if (lfConeAngle < lfLightFOV + lfCutOff)
    //if (attenuation <= (lfCutOff / (1.0 - lfCutOff)))
    {
        return 0.0;
    }
    */

    //Old working
    //if (lfConeAngle < lfLightFOV + lfCutOff) {
    //    return 0.0;
    //} 

    return attenuation;
}


//Cook-Torrance BRDF for one light. L points from the surface to the light, radiance is the incoming light
vec3 calcBRDFLighting(vec3 L, vec3 radiance, vec3 N, vec3 V,
            vec3 albedoColor, float lfMetallic, float lfRoughness)
{
    lfRoughness = clampRoughness(lfRoughness);
    vec3 F0 = mix(vec3(0.04), albedoColor, lfMetallic);
    vec3 H = normalize(V + L);
    
    // cook-torrance brdf
    float NDF = DistributionGGX(N, H, lfRoughness);        
    float G   = GeometrySmith(N, V, L, lfRoughness);      
    vec3 F    = fresnelSchlick(max(dot(H, V), 0.0), F0);       
    
    vec3 kS = F;
    vec3 kD = vec3(1.0) - kS;
    kD *= 1.0 - lfMetallic;   
    
    vec3 numerator    = NDF * G * F;
    float denominator = 4.0 * max(dot(N, V), 0.0) * max(dot(N, L), 0.0);
    vec3 specular     = numerator / max(denominator, 0.001);
    //specular = vec3(0.0);

    // add to outgoing radiance finalColor
    float NdotL = max(dot(N, L), 0.0);
    vec3 finalColor = (kD * albedoColor / PI + specular) * radiance * NdotL; 

    return finalColor;
}

//Incoming radiance below this is invisible after tone mapping, even at high exposure. The attenuation
//curves never reach zero, so without a cutoff every light would shade every pixel.
//LightClusters.cs sizes the light ranges with the same value
const float kLightCutoff = 3e-3;

//The clustered path already needs the light vector and squared distance for its cheap rejection tests.
//Accept them here so the expensive path does not calculate both values a second time.
vec3 calcLightingPrepared(Light light, vec3 toLight, float d2, vec3 fragNormal, vec3 V,
            vec3 albedoColor, float lfMetallic, float lfRoughness) {
    float d = sqrt(d2);
    float falloff = light.falloff;

    //Same curves as calcLightAttenuation
    float attenuation;
    if (falloff < 1.0)
        attenuation = 1.0 / max(1e-6, d2); //Quadratic
    else if (falloff < 2.0)
        attenuation = 1.0; //Constant
    else if (falloff < 3.0)
        attenuation = min(1.0 / max(d, 1e-6), 1.0); //Linear
    else
        attenuation = min(inversesqrt(max(d, 1e-6)), 1.0); //Square root

    //The UBO key light predates this clustered-light field and contains zero in the padding slot.
    //Treat zero as the neutral value; scene lights explicitly upload 1 or their LOD boundary gain.
    float lodScale = light.lodAttenuationScale > 0.0 ? light.lodAttenuationScale : 1.0;
    vec3 radiance = light.color.xyz * (light.color.w * attenuation * lodScale);
    if (max(radiance.r, max(radiance.g, radiance.b)) < kLightCutoff)
        return vec3(0.0);

    vec3 L = toLight / max(d, 1e-6);
    return calcBRDFLighting(L, radiance, fragNormal, V, albedoColor, lfMetallic, lfRoughness);
}

//General non-clustered entry point. V is the normalized vector from the surface to the camera.
vec3 calcLightingV(Light light, vec4 fragPos, vec3 fragNormal, vec3 V,
            vec3 albedoColor, float lfMetallic, float lfRoughness) {
    vec3 toLight = light.position.xyz - fragPos.xyz;
    if (dot(fragNormal, toLight) <= 0.0)
        return vec3(0.0);
    return calcLightingPrepared(light, toLight, dot(toLight, toLight), fragNormal, V,
                                albedoColor, lfMetallic, lfRoughness);
}

vec3 calcLighting(Light light, vec4 fragPos, vec3 fragNormal, vec3 cameraPos, vec3 cameraDir,
            vec3 albedoColor, float lfMetallic, float lfRoughness, float ao) {
    vec3 V = normalize(cameraPos - fragPos.xyz); //Calculate viewer vector based on camera position
    return calcLightingV(light, fragPos, fragNormal, V, albedoColor, lfMetallic, lfRoughness);
}


//CLUSTERED SCENE LIGHTS
//Every frame the engine (LightClusters.cs) splits the view into screen tiles and exponential depth
//slices and lists the scene lights that reach each cluster. Lights that reach the whole view come
//first in sceneLights and apply to every pixel. The key light (light 0) is not part of these.

layout (std430, binding=2) readonly buffer _SCENE_LIGHTS
{
    Light sceneLights[];
};

layout (std430, binding=3) readonly buffer _LIGHT_CLUSTERS
{
    uvec4 clusterGrid; //x, y: screen tiles, z: depth slices, w: number of global lights
    vec4 clusterParams; //x, w: tile width and height in pixels, y, z: depth slice scale and bias
    uvec2 clusterCells[]; //Offset in clusterLightIndices and light count of every cluster
};

layout (std430, binding=4) readonly buffer _LIGHT_CLUSTER_INDICES
{
    uint clusterLightIndices[];
};

//Scene-light upload normalizes direction.xyz and stores cos(half cone angle) in direction.w.
//Point lights are identified by type and deliberately bypass this test, including authored 360-degree lights.
bool outsideSpotCone(Light light, vec3 toLight)
{
    if (light.type < 1.0)
        return false;

    vec3 lightToFragment = -toLight;
    float distanceSquared = dot(lightToFragment, lightToFragment);
    if (distanceSquared <= 1e-12)
        return false;

    float axialDistance = dot(light.direction.xyz, lightToFragment);
    float coneCos = light.direction.w;
    //Direction normalization and cos(half FOV) are handled once during light upload. Keep the
    //per-pixel comparison branch-free; GPU reciprocal square root is cheaper than the prior
    //branch-heavy squared test and remains correct for both acute and obtuse authored cones.
    return axialDistance * inversesqrt(distanceSquared) < coneCos;
}

//Distance in front of the camera for a window space depth, as the clusters measure it
float viewDepthFromWindowZ(float windowZ, mat4 projMatInv)
{
    vec4 v = projMatInv * vec4(0.0, 0.0, 2.0 * windowZ - 1.0, 1.0);
    return -v.z / v.w;
}

vec3 calcSceneLighting(vec2 fragCoord, float viewDepth, vec4 fragPos, vec3 fragNormal, vec3 cameraPos, vec3 cameraDir,
            vec3 albedoColor, float lfMetallic, float lfRoughness, float ao)
{
    vec3 finalColor = vec3(0.0);
    if (clusterGrid.x == 0u)
        return finalColor;

    vec3 V = normalize(cameraPos - fragPos.xyz);

    //Lights that reach the whole view
    for (uint i = 0u; i < clusterGrid.w; ++i)
    {
        Light light = sceneLights[i];
        vec3 toLight = light.position.xyz - fragPos.xyz;
        //Reject authored spot cones before range, attenuation and BRDF work.
        if (outsideSpotCone(light, toLight))
            continue;
        //About half the candidates face away; reject them before distance, attenuation and BRDF work.
        if (dot(fragNormal, toLight) <= 0.0)
            continue;
        float d2 = dot(toLight, toLight);
        if (d2 > light.range * light.range)
            continue;
        finalColor += calcLightingPrepared(light, toLight, d2, fragNormal, V,
                                           albedoColor, lfMetallic, lfRoughness);
    }

    //Lights of this pixel's cluster
    ivec2 tile = clamp(ivec2(fragCoord / clusterParams.xw), ivec2(0), ivec2(clusterGrid.xy) - 1);
    int slice = clamp(int(floor(log(max(viewDepth, 1e-6)) * clusterParams.y + clusterParams.z)), 0, int(clusterGrid.z) - 1);
    uvec2 cell = clusterCells[(uint(slice) * clusterGrid.y + uint(tile.y)) * clusterGrid.x + uint(tile.x)];

    for (uint i = 0u; i < cell.y; ++i)
    {
        //A cluster is bigger than a pixel, so lights listed for it can still be out of range here.
        //The distance check is much cheaper than the attenuation and BRDF
        Light light = sceneLights[clusterLightIndices[cell.x + i]];
        vec3 toLight = light.position.xyz - fragPos.xyz;
        if (outsideSpotCone(light, toLight))
            continue;
        if (dot(fragNormal, toLight) <= 0.0)
            continue;
        float d2 = dot(toLight, toLight);
        if (d2 > light.range * light.range)
            continue;
        finalColor += calcLightingPrepared(light, toLight, d2, fragNormal, V,
                                           albedoColor, lfMetallic, lfRoughness);
    }

    return finalColor;
}


//ENVIRONMENT LIGHTING
//Scale independent viewport lighting: a directional key light, a camera fill light and a sky/ground
//hemisphere for ambient diffuse and specular. Nothing here depends on distances, so tiny and huge
//models are lit the same way. Scene lights loaded from models are added on top with calcLighting.

const vec3 kEnvSkyColourVec3 = vec3(0.60, 0.65, 0.75);
const vec3 kEnvGroundColourVec3 = vec3(0.30, 0.27, 0.24);
//Calibrated for the ACES tone curve at 0 EV: a mid grey surface under the key light lands around
//0.2-0.5 linear, leaving headroom for highlights
const float kEnvIntensity = 0.4;
const float kFillLightIntensity = 0.3;

vec3 EnvHemisphere(vec3 dir)
{
    return mix(kEnvGroundColourVec3, kEnvSkyColourVec3, dir.y * 0.5 + 0.5) * kEnvIntensity;
}

//Analytic approximation of the split sum environment BRDF (Karis, "Physically Based Shading on Mobile")
vec3 EnvBRDFApprox(vec3 F0, float roughness, float NoV)
{
    const vec4 c0 = vec4(-1.0, -0.0275, -0.572, 0.022);
    const vec4 c1 = vec4(1.0, 0.0425, 1.04, -0.04);
    vec4 r = roughness * c0 + c1;
    float a004 = min(r.x * r.x, exp2(-9.28 * NoV)) * r.x + r.y;
    vec2 AB = vec2(-1.04, 1.04) * a004 + r.zw;
    return F0 * AB.x + AB.y;
}

//keyLight is the default viewport light: only its direction from the scene origin and its color are used.
//keyShadow scales the key light only (1: lit, 0: in shadow)
vec3 calcEnvironmentLighting(Light keyLight, vec4 fragPos, vec3 N, vec3 cameraPos,
            vec3 albedoColor, float lfMetallic, float lfRoughness, float ao, float keyShadow)
{
    lfRoughness = clampRoughness(lfRoughness);
    vec3 V = normalize(cameraPos - fragPos.xyz);
    float NoV = max(dot(N, V), 1e-4);
    vec3 F0 = mix(vec3(0.04), albedoColor, lfMetallic);
    vec3 finalColor = vec3(0.0);

    //Key light
    if (keyLight.position.w >= 1.0)
    {
        vec3 keyDir = keyLight.position.xyz;
        keyDir = dot(keyDir, keyDir) > 1e-8 ? normalize(keyDir) : normalize(vec3(1.0));
        finalColor += keyShadow * calcBRDFLighting(keyDir, keyLight.color.rgb * keyLight.color.w, N, V, albedoColor, lfMetallic, lfRoughness);
    }

    //Fill light from the camera, so the side facing the viewer is never black
    finalColor += calcBRDFLighting(V, vec3(kFillLightIntensity), N, V, albedoColor, lfMetallic, lfRoughness);

    //Ambient diffuse and specular from the hemisphere
    vec3 F = fresnelSchlickRoughness(NoV, F0, lfRoughness);
    vec3 kD = (vec3(1.0) - F) * (1.0 - lfMetallic);
    vec3 ambientDiffuse = kD * albedoColor * EnvHemisphere(N);
    vec3 ambientSpecular = EnvHemisphere(reflect(-V, N)) * EnvBRDFApprox(F0, lfRoughness, NoV);
    finalColor += (ambientDiffuse + ambientSpecular) * ao;

    return max(finalColor, vec3(0.0));
}

