//Light struct
struct Light //locations:5
{
    vec4 position; //w is renderable
    vec4 color; //w is intensity
    vec4 direction; //w is fov
    float falloff;
    int type;
};

//Common Per Mesh Struct
struct CommonPerMeshUniforms
{
    mat4 nMat;
    mat4 skinMats[80];
    vec4 gUserDataVec4;
    vec3 color; //Mesh Default Color
    float skinned;
    float selected; //Selected
    mat4 worldMats[300]; //World Matrices
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
    float cameraFarPlane;
    vec3 cameraDirection;
    int light_count;
    Light lights[32];
};

struct CommonPerFrameSamplers
{
    samplerCube depthMap; //Depth Map for shadow calculation
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
    vec4 gCustomParams01Vec4;
};