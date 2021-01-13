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

vec3 fresnelSchlick(float cosTheta, vec3 F0)
{
    return F0 + (1.0 - F0) * pow(1.0 - cosTheta, 5.0);
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


vec3 calcLighting(Light light, vec4 fragPos, vec3 fragNormal, vec3 cameraPos, vec3 cameraDir,
            vec3 albedoColor, float lfMetallic, float lfRoughness, float ao) {
    
    vec3 L;
    float attenuation;

    vec3 F0 = vec3(0.04); 
    vec3 N = fragNormal;
    F0 = mix(F0, albedoColor, lfMetallic);

    //ao = 1.0;
    //return vec3(lfRoughness, 0.0, 0.0);

    vec3 V = normalize(cameraPos - fragPos.xyz); //Calculate viewer vector based on camera position
    L = normalize(light.position.xyz - fragPos.xyz);    
    attenuation = calcLightAttenuation(light, fragPos);
    float distance  = length(light.position.xyz - fragPos.xyz);
    //attenuation = 0.001 * 1.0 / (distance * distance); //Default calculation

    vec3 radiance = light.color.xyz * light.color.w * attenuation;
    //vec3 radiance = light.color.xyz * light.color.w / (distance * distance);
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

