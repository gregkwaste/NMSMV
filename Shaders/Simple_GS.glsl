/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */


layout(triangles) in;

// Three lines will be generated: 6 vertices
layout(line_strip, max_vertices=6) out;

//Dedicated  Uniforms
uniform mat4 mvp, rotMat, worldMat;
uniform vec3 theta;

in Vertex
{
  vec3 normal;
  vec3 tangent;
  vec3 bitangent;
  vec4 color;
} vertex[];

out vec4 vertex_color;

void main()
{
    mat4 mviewMat = rotMat;

    //Handle Geometry
	int i;
    for (i=0;i<1;i++){
        vec4 P = gl_in[i].gl_Position;
        vec4 N = vec4(vertex[i].normal ,0.0);
        vec4 T = vec4(vertex[i].tangent ,0.0);
        vec4 B = vec4(vertex[i].bitangent ,0.0);

        //AddNormal
        gl_Position = mvp * vec4(P.xyz, 1.0);
        vertex_color = vec4(1.0, 0.0, 0.0, 1.0);
        EmitVertex();

        gl_Position = mvp * (worldMat * vec4(P.xyz, 1.0) + 0.05*N);
        vertex_color = vec4(1.0, 0.0, 0.0, 1.0);
        EmitVertex();
        
        EndPrimitive();

        //AddTangent
        gl_Position = mvp * worldMat * vec4(P.xyz, 1.0);
        vertex_color = vec4(0.0, 1.0, 0.0, 1.0);
        EmitVertex();

        gl_Position = mvp * (worldMat * vec4(P.xyz, 1.0) + 0.05*T);
        vertex_color = vec4(0.0, 1.0, 0.0, 1.0);
        EmitVertex();
        
        EndPrimitive();    

        //AddBiTangent
        gl_Position = mvp * worldMat * vec4(P.xyz, 1.0);
        vertex_color = vec4(0.0, 0.0, 1.0, 1.0);
        EmitVertex();

        gl_Position = mvp * (worldMat * vec4(P.xyz, 1.0) + 0.05*B);
        vertex_color = vec4(0.0, 0.0, 1.0, 1.0);
        EmitVertex();
        
        EndPrimitive();    

    }

    
}