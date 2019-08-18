//Light struct
struct Light //locations:5
{
    vec3 position;
    vec3 color;
    float intensity;
    int fov;
    int falloff;
    float renderable; 
};

//Common Per Mesh Struct
struct CommonPerMeshUniforms
{
    mat4 worldMat;
    mat4 nMat;
    mat4 skinMats[80];
    vec3 color; //Mesh Default Color
    float skinned;
    float selected; //Selected
};

//Custom Per Frame Struct
struct CommonPerFrameUniforms
{
    float diffuseFlag; //Enable Textures //floats align to 16 bytes
    float use_lighting; //Enable lighting
    
    //Rendering Options
    mat4 rotMat;
    mat4 mvp;
    vec3 cameraPosition;
    vec3 cameraDirection;
    int light_number;
};

//Custom Per Frame Struct
struct CustomPerMaterialUniforms  //locations:73
{
    float matflags[64];
    
    sampler2DArray gDiffuseMap;
    sampler2DArray gMasksMap;
    sampler2DArray gNormalMap;

    vec4 gMaterialColourVec4;
    vec4 gMaterialParamsVec4;
    vec4 gMaterialSFXVec4;
    vec4 gMaterialSFXColVec4;
    vec4 gDissolveDataVec4;
    vec4 gUserDataVec4;
};