//Light struct
struct Light //locations:5
{
    vec4 position; //w is renderable
    vec4 color; //w is intensity
    vec4 direction; //w is fov
    float falloff;
    int type;
};

struct MeshInstance
{
    mat4 worldMat;
    mat4 normalMat;
    float isOccluded;
    float isSelected;
};

//Common Per Mesh Struct
struct CommonPerMeshUniforms
{
    vec4 gUserDataVec4;
    vec3 color; //Mesh Default Color
    float skinned;
    MeshInstance instanceData[300]; //Instance world matrices, normal matrices, occlusion and selection status
};

//Custom Per Frame Struct
struct CommonPerFrameUniforms
{
    float diffuseFlag; //Enable Textures //floats align to 16 bytes
    float use_lighting; //Enable lighting
    float gfTime; //Time
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
    sampler2DArray gDiffuse2Map;
    sampler2DArray gMasksMap;
    sampler2DArray gNormalMap;
    samplerBuffer skinMatsTex;

    vec4 gMaterialColourVec4;
    vec4 gMaterialParamsVec4;
    vec4 gMaterialSFXVec4;
    vec4 gMaterialSFXColVec4;
    vec4 gUVScrollStepVec4;
    vec4 gDissolveDataVec4;
    vec4 gCustomParams01Vec4;
};