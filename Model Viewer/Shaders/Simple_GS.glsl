/* First Geometry Shader :)
    It is supposed to emit vertex normals 
 */
#version 330
#extension GL_ARB_geometry_shader5 : enable
layout(triangles) in;

// Three lines will be generated: 6 vertices
layout(line_strip, max_vertices=6) out;

//Dedicated  Uniforms
uniform mat4 look, proj, worldMat;
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
    vec3 angles = radians( theta );
    vec3 c = cos( angles );
    vec3 s = sin( angles );
    
    // Remeber: thse matrices are column-major
    mat4 rx = mat4( 1.0,  0.0,  0.0, 0.0,
                    0.0,  c.x,  s.x, 0.0,
                    0.0, -s.x,  c.x, 0.0,
                    0.0,  0.0,  0.0, 1.0 );

    mat4 ry = mat4( c.y, 0.0, -s.y, 0.0,
            0.0, 1.0,  0.0, 0.0,
            s.y, 0.0,  c.y, 0.0,
            0.0, 0.0,  0.0, 1.0 );

    mat4 rz = mat4( c.z, -s.z, 0.0, 0.0,
            s.z,  c.z, 0.0, 0.0,
            0.0,  0.0, 1.0, 0.0,
            0.0,  0.0, 0.0, 1.0 );

    mat4 rotMat = rx*ry*rz;
    mat4 mviewMat = rotMat;

    //Handle Geometry
	int i;
    for (i=0;i<1;i++){
        vec4 P = gl_in[i].gl_Position;
        vec4 N = vec4(vertex[i].normal ,0.0);
        vec4 T = vec4(vertex[i].tangent ,0.0);
        vec4 B = vec4(vertex[i].bitangent ,0.0);

        //AddNormal
        gl_Position = proj * look * mviewMat * worldMat * vec4(P.xyz, 1.0);
        vertex_color = vec4(1.0, 0.0, 0.0, 1.0);
        EmitVertex();

        gl_Position = proj * look * mviewMat * (worldMat * vec4(P.xyz, 1.0) + 0.05*N);
        vertex_color = vec4(1.0, 0.0, 0.0, 1.0);
        EmitVertex();
        
        EndPrimitive();

        //AddTangent
        gl_Position = proj * look * mviewMat * worldMat * vec4(P.xyz, 1.0);
        vertex_color = vec4(0.0, 1.0, 0.0, 1.0);
        EmitVertex();

        gl_Position = proj * look * mviewMat * (worldMat * vec4(P.xyz, 1.0) + 0.05*T);
        vertex_color = vec4(0.0, 1.0, 0.0, 1.0);
        EmitVertex();
        
        EndPrimitive();    

        //AddBiTangent
        gl_Position = proj * look * mviewMat * worldMat * vec4(P.xyz, 1.0);
        vertex_color = vec4(0.0, 0.0, 1.0, 1.0);
        EmitVertex();

        gl_Position = proj * look * mviewMat * (worldMat * vec4(P.xyz, 1.0) + 0.05*B);
        vertex_color = vec4(0.0, 0.0, 1.0, 1.0);
        EmitVertex();
        
        EndPrimitive();    

    }

    
}