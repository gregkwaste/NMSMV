/*  Version and extension are added during preprocessing
 *  Copies incoming vertex color without change.
 *  Applies the transformation matrix to vertex position.
 */

in vec4 vertex_color;
out vec4 Out_Color;
void main()
{
  Out_Color = vertex_color;
}