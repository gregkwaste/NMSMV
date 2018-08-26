using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using libMBIN.Models.Structs;

namespace Model_Viewer
{
    public static class Palettes
    {
        //Palette
        //public static Dictionary<string, int> palette_NameToID = new Dictionary<string, int>();
        //public static Dictionary<int, string> palette_IDToName = new Dictionary<int, string>();

        //ColourAlt
        //public static Dictionary<string, int> colourAlt_NameToID = new Dictionary<string, int>();
        //public static Dictionary<int, string> colourAlt_IDToName = new Dictionary<int, string>();

        public static int activeID = -1;
        public static readonly float rbgFloat = 0.003921f;
        public static readonly List<Vector3> Paint = new List<Vector3> { Vector3.Multiply(new Vector3 (230, 230, 230) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (255, 223, 181) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (252, 242, 207) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (239, 245, 251) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (213, 226, 241) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (213, 226, 241) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (243, 243, 243) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (92, 91, 98) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (252, 133, 61) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (200, 89, 61) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (236, 105, 14) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (249, 118, 68) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (254, 124, 74) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (255, 187, 102) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (255, 160, 2) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (230, 197, 146) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (244, 185, 40) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (249, 236, 17) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (251, 200, 95) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (255, 172, 63) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (247, 208, 107) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (255, 242, 133) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (255, 243, 25) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (244, 185, 40) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (251, 107, 87) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (236, 95, 79) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (199, 75, 67) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (210, 72, 74) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (160, 79, 86) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (255, 147, 117) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (241, 96, 83) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (219, 86, 79) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (160, 58, 70) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (232, 112, 122) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (204, 104, 108) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (236, 97, 101) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (226, 126, 129) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (206, 88, 87) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (166, 79, 70) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (191, 115, 107) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (65, 110, 180) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (47, 122, 162) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (111, 141, 237) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (122, 158, 218) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (145, 196, 199) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (145, 196, 199) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (62, 143, 202) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (65, 110, 180) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (69, 96, 135) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (61, 135, 218) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (99, 156, 202) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (66, 114, 137) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (101, 173, 195) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (117, 125, 169) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (91, 119, 167) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (78, 85, 103) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (68, 121, 71) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (51, 130, 108) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (100, 121, 116) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (83, 113, 87) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (100, 178, 136) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (152, 200, 156) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (74, 114, 116) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (117, 150, 129) , rbgFloat) };

        public static readonly List<Vector3> Fur = new List<Vector3> { Vector3.Multiply(new Vector3 (50, 129, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (53, 139, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (62, 166, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (73, 191, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (13, 128, 116) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 135, 125) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 160, 147) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 172, 165) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (63, 60, 44) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (121, 118, 91) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (177, 172, 137) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (233, 227, 182) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 141, 160) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 166, 167) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (38, 127, 166) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (70, 137, 166) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (129, 169, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (133, 169, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (179, 189, 77) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (242, 255, 105) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (197, 79, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (234, 94, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (227, 136, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (174, 85, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (116, 157, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (97, 137, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (54, 115, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (127, 128, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (116, 39, 157) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (134, 91, 181) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (156, 101, 181) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (156, 101, 181) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 129, 77) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 139, 83) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 166, 101) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (0, 191, 113) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (150, 9, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (194, 62, 80) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (197, 79, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (234, 94, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (184, 79, 130) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (184, 59, 119) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (184, 95, 175) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (207, 59, 52) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (168, 39, 56) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (164, 0, 22) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (193, 36, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (227, 136, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (154, 0, 20) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (164, 0, 22) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (227, 136, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (158, 137, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (116, 39, 157) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (234, 94, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (191, 98, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (174, 85, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (116, 39, 157) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (134, 91, 181) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (156, 101, 181) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (118, 73, 149) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (180, 158, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (158, 137, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (138, 127, 0) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (120, 115, 0) , rbgFloat) };

        public static readonly List<Vector3> Rock = new List<Vector3> { Vector3.Multiply(new Vector3 (165, 165, 165) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (158, 143, 120) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (162, 158, 125) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (167, 170, 125) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (177, 167, 119) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (189, 148, 131) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (185, 168, 138) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (147, 158, 169) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (150, 150, 150) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (144, 130, 107) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (149, 144, 113) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (154, 156, 113) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (166, 155, 109) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (179, 137, 119) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (175, 158, 131) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (143, 154, 164) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (132, 132, 132) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (126, 112, 91) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (131, 127, 95) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (136, 139, 96) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (149, 138, 91) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (162, 120, 104) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (162, 145, 115) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (131, 142, 153) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (109, 110, 110) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (103, 90, 73) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (109, 104, 76) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (113, 117, 77) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (127, 115, 76) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (143, 98, 86) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (143, 125, 99) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (119, 131, 140) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (87, 87, 87) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (82, 70, 56) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (85, 81, 59) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (89, 91, 60) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (100, 90, 57) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (116, 78, 68) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (118, 103, 80) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (106, 116, 125) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (64, 64, 64) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (59, 51, 40) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (62, 59, 42) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (64, 66, 43) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (75, 65, 41) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (89, 58, 50) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (94, 81, 62) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (93, 101, 108) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (47, 47, 47) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (42, 36, 29) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (45, 42, 31) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (47, 48, 31) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (53, 48, 30) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (67, 44, 40) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (72, 62, 49) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (81, 87, 94) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (34, 34, 34) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (33, 27, 21) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (33, 33, 22) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (34, 35, 23) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (40, 35, 22) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (52, 36, 31) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (58, 50, 39) , rbgFloat),
                                                                        Vector3.Multiply(new Vector3 (73, 76, 83) , rbgFloat) };

        public static readonly List<Vector3> Underbelly = new List<Vector3> {Vector3.Multiply(new Vector3 (254, 204, 195) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (201, 172, 118) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 196, 98) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (201, 139, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (94, 64, 33) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (84, 45, 36) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (138, 84, 71) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (103, 77, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 217, 196) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (219, 126, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (227, 220, 196) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (183, 114, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (77, 44, 21) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (92, 102, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (100, 50, 14) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (168, 138, 64) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (235, 222, 149) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (246, 226, 171) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (234, 230, 205) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (239, 234, 204) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 68, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (155, 145, 115) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (136, 123, 102) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (98, 54, 28) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (241, 225, 180) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (171, 189, 107) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 174, 112) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (193, 195, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (66, 56, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (71, 61, 63) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (74, 62, 28) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (66, 70, 42) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (96, 45, 133) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (185, 1, 25) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (177, 17, 5) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (8, 157, 93) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (57, 124, 4) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (214, 243, 6) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (176, 176, 126) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (32, 40, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (91, 49, 116) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (207, 1, 30) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (101, 39, 18) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (12, 181, 104) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (125, 137, 4) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (234, 249, 9) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (228, 228, 168) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (86, 194, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (179, 157, 13) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (120, 42, 146) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (235, 157, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (144, 14, 6) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (7, 135, 154) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (187, 208, 166) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (2, 140, 160) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (34, 128, 154) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (158, 138, 13) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 45, 123) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 0, 25) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (222, 190, 164) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (6, 158, 160) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 134, 168) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 206, 190) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (7, 134, 166) , rbgFloat) };

        public static readonly List<Vector3> Feather = new List<Vector3>{   Vector3.Multiply(new Vector3 (162, 122, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (170, 95, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (61, 163, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (171, 80, 151) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 122, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (122, 97, 182) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (61, 163, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (170, 95, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (169, 99, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 117, 170) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 92, 182) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 82, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (169, 99, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 117, 170) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 92, 182) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 82, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (54, 119, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 99, 77) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 82, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 158, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (170, 95, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (163, 114, 72) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (135, 115, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 158, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 162, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (145, 96, 185) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (96, 98, 111) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 84, 84) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 162, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 84, 84) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 133, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (99, 100, 114) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 122, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 134, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (61, 163, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (168, 99, 78) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 122, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (170, 95, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (61, 163, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (171, 80, 151) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (169, 99, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 117, 170) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 92, 182) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 82, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (169, 99, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 117, 170) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (54, 119, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 82, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (156, 151, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (130, 95, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (173, 83, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 158, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (65, 107, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 162, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (164, 113, 74) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 158, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 162, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 103, 76) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 133, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (170, 95, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 162, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (76, 111, 143) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 133, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (170, 95, 80) , rbgFloat) };

        public static readonly List<Vector3> Scale = new List<Vector3> {    Vector3.Multiply(new Vector3 (3, 139, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (92, 174, 82) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 208, 78) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (16, 63, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 38, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 235, 250) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 217, 227) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 75, 165) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (13, 13, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (100, 100, 91) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (86, 118, 137) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (74, 54, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 10, 18) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 249, 250) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 227, 226) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 144, 165) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (138, 218, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (189, 248, 74) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 135, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (74, 18, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 1, 6) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 235, 250) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 217, 227) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 75, 165) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (103, 180, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (147, 228, 76) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (208, 255, 91) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (74, 74, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 74, 74) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 249, 250) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 227, 226) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 144, 165) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (20, 118, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (29, 165, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (83, 224, 225) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (136, 247, 247) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 101, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (198, 34, 34) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 131, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 150, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (142, 25, 25) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (198, 34, 34) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (217, 38, 38) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 101, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (244, 104, 143) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (186, 32, 32) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (243, 103, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 206, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (131, 23, 23) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (186, 32, 32) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (217, 38, 52) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 101, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (254, 253, 254) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (71, 38, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 157, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (245, 139, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (117, 24, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 22, 134) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 229, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 224, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (142, 255, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (232, 190, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (226, 207, 76) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 190, 79) , rbgFloat) };

        public static readonly List<Vector3> Leaf = new List<Vector3> {     Vector3.Multiply(new Vector3 (98, 125, 56) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (85, 101, 19) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (39, 137, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (39, 167, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (196, 102, 38) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (201, 131, 26) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (144, 86, 30) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (101, 98, 19) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (138, 179, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 123, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (44, 128, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (44, 150, 149) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 84, 23) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (142, 68, 51) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 98, 24) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (123, 115, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (129, 179, 43) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (140, 157, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (38, 109, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (38, 129, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (163, 140, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (172, 82, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (165, 99, 26) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (157, 137, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (57, 150, 44) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (159, 180, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (68, 105, 116) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (12, 132, 148) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (133, 122, 4) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (133, 119, 20) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (197, 137, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (180, 159, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (79, 166, 25) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 182, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (188, 198, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (112, 112, 16) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (25, 166, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (178, 36, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (172, 40, 58) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (106, 55, 99) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 139, 27) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 159, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (171, 185, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (56, 83, 32) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (27, 139, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (151, 40, 33) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (147, 42, 55) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (121, 54, 112) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (58, 118, 21) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (68, 142, 62) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (134, 169, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (91, 121, 16) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (21, 118, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (131, 34, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (127, 37, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 56, 108) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (57, 98, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (74, 126, 71) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (130, 169, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 150, 7) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (31, 98, 71) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (110, 44, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (108, 46, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (140, 56, 114) , rbgFloat)};

        public static readonly List<Vector3> Metal = new List<Vector3> {    Vector3.Multiply(new Vector3 (68, 68, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (72, 65, 65) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (65, 65, 65) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 62, 62) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (63, 63, 63) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (66, 60, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (62, 62, 62) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (65, 59, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (62, 69, 75) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (65, 65, 72) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (59, 66, 72) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (62, 62, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (57, 64, 70) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (60, 60, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (56, 63, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (59, 59, 65) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (58, 58, 58) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (61, 55, 55) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (56, 56, 56) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (59, 53, 53) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (53, 53, 53) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (56, 50, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (51, 51, 51) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (54, 49, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (52, 59, 65) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (55, 55, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (50, 57, 63) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (53, 53, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (47, 54, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (50, 50, 56) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (45, 52, 57) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (49, 49, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (48, 48, 48) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (51, 46, 46) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (46, 46, 46) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (49, 44, 44) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (43, 43, 43) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (46, 41, 41) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (42, 42, 42) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (45, 40, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (42, 49, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (46, 46, 51) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (40, 47, 52) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (44, 44, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (38, 44, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (41, 41, 46) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (37, 43, 48) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (40, 40, 45) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (39, 39, 39) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (42, 37, 37) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (37, 37, 37) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (40, 35, 35) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (34, 34, 34) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (37, 32, 32) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (31, 31, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (34, 29, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (34, 40, 45) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (37, 37, 42) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (32, 38, 43) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (35, 35, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (29, 35, 39) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (32, 32, 37) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (26, 32, 36) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (29, 29, 34) , rbgFloat)};

        public static readonly List<Vector3> Wood = new List<Vector3> {
                                                                            Vector3.Multiply(new Vector3 (150, 96, 76) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (163, 143, 107) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (161, 126, 96) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (115, 97, 82) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (80, 72, 65) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (120, 118, 84) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (136, 134, 120) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (136, 128, 120) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (152, 109, 94) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (161, 136, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (176, 137, 102) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (126, 106, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 121, 132) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 61, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (101, 101, 92) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (108, 104, 97) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (153, 116, 102) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (103, 91, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (217, 164, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (145, 119, 97) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (108, 114, 90) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (136, 66, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (228, 241, 214) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (184, 173, 159) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (154, 125, 115) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (124, 107, 75) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (109, 97, 75) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (109, 93, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (154, 168, 156) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (150, 83, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (101, 98, 77) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (108, 102, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (241, 225, 214) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (125, 112, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (82, 76, 64) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (153, 109, 100) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 114, 96) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (173, 91, 91) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (132, 103, 93) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (134, 125, 84) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (198, 127, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (91, 76, 70) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (81, 66, 62) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (188, 130, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (77, 82, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 79, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (120, 96, 86) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (157, 145, 93) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (168, 112, 91) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (108, 86, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (111, 81, 75) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (187, 120, 105) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (107, 91, 97) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (136, 95, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (89, 76, 70) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (218, 200, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (170, 125, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (124, 96, 86) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (125, 89, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (168, 110, 97) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (96, 103, 88) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (179, 117, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (208, 132, 105) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (220, 205, 141) , rbgFloat)};

        public static readonly List<Vector3> Stone = new List<Vector3> {
                                                                            Vector3.Multiply(new Vector3 (165, 165, 165) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (158, 143, 120) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 158, 125) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 170, 125) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (177, 167, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (189, 148, 131) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (185, 168, 138) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (147, 158, 169) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (150, 150, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (144, 130, 107) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 144, 113) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (154, 156, 113) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (166, 155, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (179, 137, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (175, 158, 131) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (143, 154, 164) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (132, 132, 132) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (126, 112, 91) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (131, 127, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (136, 139, 96) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 138, 91) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 120, 104) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 145, 115) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (131, 142, 153) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (109, 110, 110) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (103, 90, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (109, 104, 76) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 117, 77) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (127, 115, 76) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (143, 98, 86) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (143, 125, 99) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 131, 140) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (87, 87, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (82, 70, 56) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (85, 81, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (89, 91, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (100, 90, 57) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (116, 78, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (118, 103, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (106, 116, 125) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (64, 64, 64) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (59, 51, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (62, 59, 42) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (64, 66, 43) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (75, 65, 41) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (89, 58, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (94, 81, 62) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (93, 101, 108) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (47, 47, 47) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (42, 36, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (45, 42, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (47, 48, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (53, 48, 30) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (67, 44, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (72, 62, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (81, 87, 94) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (34, 34, 34) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (33, 27, 21) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (33, 33, 22) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (34, 35, 23) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (40, 35, 22) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (52, 36, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (58, 50, 39) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 76, 83) , rbgFloat) };

        public static readonly List<Vector3> Sand = new List<Vector3> {
                                                                            Vector3.Multiply(new Vector3 (145, 145, 145) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (133, 147, 161) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 139, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (168, 162, 127) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 148, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 153, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (143, 155, 125) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 161, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (129, 129, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (112, 129, 148) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 119, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (159, 151, 106) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 130, 93) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 138, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (130, 147, 105) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 149, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (109, 109, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (89, 111, 134) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 97, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (148, 138, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 112, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 121, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 135, 82) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 135, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (122, 122, 122) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (99, 125, 146) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (178, 109, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (157, 149, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (178, 125, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (178, 134, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (128, 148, 90) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (177, 145, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (135, 135, 135) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (115, 136, 153) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (175, 122, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (157, 154, 100) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (175, 136, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (175, 144, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (130, 149, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (175, 152, 82) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 149, 149) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (138, 150, 159) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (172, 142, 121) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (160, 158, 128) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (169, 147, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (172, 153, 121) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (145, 155, 128) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (172, 159, 121) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (127, 127, 127) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (114, 125, 134) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 117, 100) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (139, 136, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 125, 100) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 128, 100) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (127, 135, 110) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (150, 136, 102) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (99, 99, 99) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (91, 100, 108) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 94, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (111, 108, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 100, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 103, 80) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (100, 107, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (119, 108, 80) , rbgFloat)};
        public static readonly List<Vector3> Plant = new List<Vector3> {
                                                                            Vector3.Multiply(new Vector3 (57, 98, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (58, 118, 21) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 139, 27) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (79, 166, 25) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (57, 150, 44) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (129, 179, 43) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (138, 179, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (98, 125, 56) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (74, 126, 71) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (68, 142, 62) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 159, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 182, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (159, 180, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (140, 157, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 123, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (85, 101, 19) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (130, 169, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (134, 169, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (171, 185, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (188, 198, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (68, 105, 116) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (38, 109, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (44, 128, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (39, 137, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 150, 7) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (91, 121, 16) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (56, 83, 32) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (112, 112, 16) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (12, 132, 148) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (38, 129, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (44, 150, 149) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (39, 167, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (31, 98, 71) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (21, 118, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (27, 139, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (25, 166, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (133, 122, 4) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (163, 140, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 84, 23) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (196, 102, 38) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (110, 44, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (131, 34, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (151, 40, 33) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (178, 36, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (133, 119, 20) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (172, 82, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (142, 68, 51) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (201, 131, 26) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (108, 46, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (127, 37, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (147, 42, 55) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (172, 40, 58) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (197, 137, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (165, 99, 26) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 98, 24) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (144, 86, 30) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 76, 120) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (88, 71, 104) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (87, 70, 105) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (80, 67, 94) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (180, 159, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (157, 137, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (123, 115, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (101, 98, 19) , rbgFloat)};

        public static readonly List<Vector3> Crystal = new List<Vector3> {
                                                                            Vector3.Multiply(new Vector3 (61, 252, 138) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 158, 51) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 126, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (184, 60, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 60, 90) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (206, 255, 237) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (210, 59, 219) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (208, 250, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (124, 252, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 158, 51) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 126, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (184, 60, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 60, 90) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (32, 253, 172) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (181, 34, 189) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (32, 232, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (126, 227, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 169, 45) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 135, 74) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (197, 54, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 64, 81) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (28, 250, 181) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (160, 34, 159) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (28, 217, 252) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (103, 222, 25) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (254, 187, 36) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 148, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (214, 45, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 69, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (22, 246, 197) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (155, 27, 142) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (22, 193, 248) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (77, 216, 18) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (254, 206, 26) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 163, 57) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (234, 35, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 76, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (16, 241, 212) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 19, 123) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (16, 166, 243) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (52, 210, 11) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (253, 226, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 178, 48) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 25, 252) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 82, 34) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (9, 236, 229) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (143, 12, 104) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (9, 138, 238) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (29, 205, 5) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (252, 244, 6) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 191, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 17, 236) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 87, 21) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (4, 218, 232) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (138, 5, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (4, 114, 234) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (14, 202, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (252, 255, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 200, 35) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 59, 239) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (255, 91, 11) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 202, 229) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (135, 0, 76) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (0, 98, 231) , rbgFloat)};

        public static readonly List<Vector3> Undercoat = new List<Vector3> {
                                                                            Vector3.Multiply(new Vector3 (195, 197, 195) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (212, 212, 200) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (218, 215, 208) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (214, 209, 203) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (214, 207, 203) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (208, 195, 195) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (187, 179, 183) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 200, 208) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 197, 195) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (212, 212, 200) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (218, 215, 208) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (214, 209, 203) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (214, 207, 203) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (208, 195, 195) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (187, 179, 183) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 200, 208) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (183, 185, 183) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (204, 204, 190) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (210, 207, 198) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (206, 200, 193) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (206, 197, 193) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (198, 183, 183) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (174, 165, 170) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (184, 190, 199) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 169, 167) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (191, 191, 174) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (200, 196, 185) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (194, 186, 177) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (195, 184, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (184, 167, 167) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (153, 143, 149) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (168, 175, 185) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (146, 149, 146) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (177, 177, 156) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (188, 183, 170) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (180, 171, 160) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (180, 167, 160) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (168, 148, 148) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (131, 119, 126) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (149, 157, 169) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (127, 130, 127) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (163, 163, 138) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (176, 170, 154) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 156, 143) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (167, 151, 143) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (152, 129, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (109, 95, 103) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (129, 139, 152) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (111, 114, 111) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (151, 151, 122) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (166, 160, 141) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (155, 143, 128) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (155, 137, 128) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (138, 112, 112) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (88, 73, 81) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 124, 139) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (99, 102, 99) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (143, 143, 112) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (158, 151, 131) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (148, 134, 118) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (148, 128, 118) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (129, 101, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (75, 59, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (101, 113, 129) , rbgFloat)};
        public static readonly List<Vector3> Dirt = new List<Vector3> {
                                                                            Vector3.Multiply(new Vector3 (175, 175, 175) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (160, 114, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (169, 140, 119) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (143, 131, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (215, 147, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (214, 186, 99) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (147, 168, 181) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (154, 138, 97) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (162, 162, 162) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (141, 102, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (157, 129, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (131, 122, 138) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (201, 136, 81) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (201, 175, 93) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (140, 158, 172) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (130, 117, 81) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (145, 145, 145) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (128, 92, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (139, 114, 97) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (118, 108, 124) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (181, 122, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (180, 157, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (126, 143, 157) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 106, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (150, 150, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (118, 85, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (144, 116, 103) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (122, 113, 130) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (184, 119, 81) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (185, 155, 93) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (133, 152, 162) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (98, 93, 60) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (127, 127, 127) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (147, 77, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (122, 98, 87) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (103, 95, 110) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (159, 101, 69) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (159, 132, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (113, 130, 140) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (141, 154, 97) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (105, 105, 105) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (126, 65, 56) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (101, 83, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (87, 81, 91) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (134, 85, 61) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (135, 109, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 109, 115) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (118, 130, 81) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (87, 86, 86) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (111, 55, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (85, 69, 63) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 68, 77) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (112, 72, 53) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (112, 92, 58) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (80, 90, 96) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (97, 113, 68) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (74, 74, 74) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (100, 49, 43) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (73, 61, 57) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (63, 59, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (94, 63, 48) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (95, 79, 52) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (69, 78, 84) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3 (84, 98, 60) , rbgFloat) };

        public static readonly List<Vector3> Plastic = new List<Vector3> {Vector3.Multiply(new Vector3(206, 148, 157) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(228, 253, 158) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(255, 255, 159) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(224, 181, 241) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(172, 189, 239) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(196, 134, 128) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(195, 126, 143) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(247, 211, 183) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(204, 147, 173) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(165, 121, 83) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(253, 244, 157) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(241, 173, 241) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(180, 220, 244) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(236, 122, 106) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(195, 126, 156) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(134, 182, 237) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(204, 147, 198) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(135, 145, 134) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(245, 196, 137) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(247, 211, 148) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(192, 250, 250) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(239, 152, 114) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(130, 131, 134) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(176, 152, 235) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(202, 105, 232) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(103, 109, 116) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(239, 154, 124) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(179, 173, 238) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(184, 250, 208) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(240, 164, 117) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(214, 96, 233) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(139, 203, 152) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(143, 97, 229) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(152, 248, 198) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(206, 150, 149) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(240, 161, 165) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(199, 199, 198) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(245, 199, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(150, 81, 228) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(192, 128, 93) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(149, 145, 202) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(234, 200, 207) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(131, 138, 101) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(241, 167, 141) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(229, 253, 180) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(252, 241, 144) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(137, 136, 140) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(159, 137, 211) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(144, 156, 203) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(137, 151, 164) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(194, 250, 202) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(254, 252, 174) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(158, 204, 141) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(255, 255, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(219, 191, 172) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(174, 165, 146) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(248, 215, 213) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(132, 210, 241) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(255, 255, 255) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(234, 165, 116) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(177, 178, 179) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(237, 253, 147) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(104, 120, 130) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(124, 123, 111) , rbgFloat) };

        public static readonly List<Vector3> Grass = new List<Vector3> { Vector3.Multiply(new Vector3(57, 98, 31) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(58, 118, 21) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(69, 139, 27) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(79, 166, 25) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(57, 150, 44) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(129, 179, 43) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(138, 179, 73) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(98, 125, 56) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(74, 126, 71) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(68, 142, 62) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(73, 159, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(73, 182, 66) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(159, 180, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(140, 157, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(105, 123, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(85, 101, 19) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(130, 169, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(134, 169, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(171, 185, 67) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(188, 198, 59) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(68, 105, 116) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(38, 109, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(44, 128, 150) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(39, 137, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(113, 150, 7) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(91, 121, 16) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(56, 83, 32) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(112, 112, 16) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(12, 132, 148) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(38, 129, 129) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(44, 150, 149) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(39, 167, 178) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(31, 98, 71) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(21, 118, 79) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(27, 139, 95) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(25, 166, 109) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(133, 122, 4) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(163, 140, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(174, 84, 23) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(196, 102, 38) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(110, 44, 40) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(131, 34, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(151, 40, 33) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(178, 36, 29) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(133, 119, 20) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(172, 82, 50) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(142, 68, 51) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(201, 131, 26) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(108, 46, 54) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(127, 37, 49) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(147, 42, 55) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(172, 40, 58) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(197, 137, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(165, 99, 26) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(167, 98, 24) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(144, 86, 30) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(105, 76, 120) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(88, 71, 104) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(87, 70, 105) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(80, 67, 94) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(180, 159, 0) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(157, 137, 1) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(123, 115, 15) , rbgFloat),
                                                                            Vector3.Multiply(new Vector3(101, 98, 19) , rbgFloat)};

        public static readonly List<Vector3> Scientific = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 255) , rbgFloat)};

        public static readonly List<Vector3> ScientificAlt = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 255) , rbgFloat)};

        public static readonly List<Vector3> Trader = new List<Vector3>
        { Vector3.Multiply(new Vector3 (0, 255, 255) , rbgFloat)};

        public static readonly List<Vector3> TraderAlt = new List<Vector3>
        { Vector3.Multiply(new Vector3 (0, 255, 255) , rbgFloat)};

        public static readonly List<Vector3> Warrior = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 255, 0) , rbgFloat)};

        public static readonly List<Vector3> WarriorAlt = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 255, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Head = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Torso = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Chest_Armour = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Backpack = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Arms = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Hands = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Legs = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        public static readonly List<Vector3> Custom_Feet = new List<Vector3>
        { Vector3.Multiply(new Vector3 (255, 0, 0) , rbgFloat)};

        //Palette Selection
        public static Dictionary<string, Dictionary<string, Vector4>> paletteSel;

        //Methods
        public static List<Vector3> getPalette(string name)
        {
            Type t = typeof(Palettes);
            FieldInfo[] fields = t.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            foreach (FieldInfo f in fields)
            {
                if (f.Name == name)
                {
                    object ob = f.GetValue(null);
                    return (List<Vector3>)ob;
                }

            }
            throw new ApplicationException("Missing Pallete" + name);
        }

        public static Dictionary<string, Dictionary<string, Vector4>> createPalette()
        {
            Dictionary<string, Dictionary<string, Vector4>> newPal;
            newPal = new Dictionary<string, Dictionary<string, Vector4>>();

            //ColourAlt ids
            int primary = MVCore.Common.RenderState.randgen.Next(0, 64);
            int alternative1 = (primary + 1) % 64;
            int alternative2 = (alternative1 + 1) % 64;
            int alternative3 = (alternative2 + 1) % 64;
            int alternative4 = (alternative3 + 1) % 64;
            int unique = (alternative4 + 1) % 64;

            Type t = typeof(Palettes);
            FieldInfo[] fields = t.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            foreach (FieldInfo f in fields)
            {
                //Check field type
                if (f.FieldType != typeof(List<Vector3>))
                    continue;
                //Get palette
                List<Vector3> palette = (List<Vector3>)f.GetValue(null);

                //Add palette to dictionary
                newPal[f.Name] = new Dictionary<string, Vector4>();
                //Add None option
                newPal[f.Name]["None"] = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);

                try
                {
                    newPal[f.Name]["Primary"] = new Vector4(palette[primary], 1.0f);
                    newPal[f.Name]["Alternative1"] = new Vector4(palette[alternative1], 1.0f);
                    newPal[f.Name]["Alternative2"] = new Vector4(palette[alternative2], 1.0f);
                    newPal[f.Name]["Alternative3"] = new Vector4(palette[alternative3], 1.0f);
                    newPal[f.Name]["Alternative4"] = new Vector4(palette[alternative4], 1.0f);
                    newPal[f.Name]["Unique"] = new Vector4(palette[unique], 1.0f);
                }
                catch (ArgumentOutOfRangeException e)
                {
                    Console.WriteLine("Missing Options for Palette " + f.Name);
                    //Choose the first color in all cases that the palette files have not been properly imported
                    newPal[f.Name]["Primary"] = new Vector4(palette[0], 1.0f);
                    newPal[f.Name]["Alternative1"] = new Vector4(palette[0], 1.0f);
                    newPal[f.Name]["Alternative2"] = new Vector4(palette[0], 1.0f);
                    newPal[f.Name]["Alternative3"] = new Vector4(palette[0], 1.0f);
                    newPal[f.Name]["Alternative4"] = new Vector4(palette[0], 1.0f);
                    newPal[f.Name]["Unique"] = new Vector4(palette[0], 1.0f);
                }
            }
            return newPal;
        }

        public static Dictionary<string, Dictionary<string, Vector4>> createPalettefromBasePalettes()
        {
            Dictionary<string, Dictionary<string, Vector4>> newPal;
            newPal = new Dictionary<string, Dictionary<string, Vector4>>();

            string filepath = "I:\\SteamLibrary1\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS\\METADATA\\SIMULATION\\SOLARSYSTEM\\COLOURS\\BASECOLOURPALETTES.MBIN";

            libMBIN.MBINFile mbinf = new libMBIN.MBINFile(filepath);
            mbinf.Load();
            GcPaletteList template = (GcPaletteList) mbinf.GetData();

            TkPaletteTexture tkpt = new TkPaletteTexture();

            GcPaletteData gcpd = new GcPaletteData();
            string[] numColorValues = gcpd.NumColoursValues();


            for (int i = 0; i < template.Palettes.Length; i++)
            {
                string pal_name = ((TkPaletteTexture.PaletteEnum) i).ToString();
                Console.WriteLine("Palette {0} NumColors {1}", pal_name, numColorValues[template.Palettes[i].NumColours]);
                newPal[pal_name] = new Dictionary<string, Vector4>();

                //Generate Bitmap for palette
                Bitmap bmp = new Bitmap(64, 1);

                for (int j = 0; j < template.Palettes[i].Colours.Length; j++)
                {
                    Colour colour = template.Palettes[i].Colours[j];

                    //Console.WriteLine("Color {0} {1} {2} {3} {4}",
                    //j, colour.R, colour.G, colour.B, colour.A);

                    //bmp.SetPixel(j, 0, Color.FromArgb((int)(colour.A * 255),
                    //                                    (int)(colour.R * 255),
                    //                                    (int)(colour.G * 255),
                    //                                    (int)(colour.B * 255)));
                }

                Vector4 primary, alt1, alt2, alt3, alt4, matchg, unique, none;
                int index = 0;
                int index1 = 0;
                int index2 = 0;
                int index3 = 0;
                int index4 = 0;
                int unique_index = 0;
                none = new Vector4(1.0f, 1.0f, 1.0f, 0.0f);

                switch (template.Palettes[i].NumColours) {
                    case 0: //Inactive
                        //Test by not saving anything
                        break;
                    case 1: //1 Color - All colors should be the same
                        index = 0; 
                        index1 = index;
                        index2 = index;
                        index3 = index;
                        index4 = index;
                        unique_index = 0;
                        break;
                    case 2: //4 Color
                    case 3: //8 Color NOTE: Not enough samples for that
                        index = get_active_palette_index(4);
                        index1 = index + 1;
                        index2 = index + 2;
                        index3 = index + 3;
                        unique_index = get_active_palette_index(1);
                        break;
                    case 4: //16 Color
                        //Align to groups of 2
                        index = get_active_palette_index(2);
                        index1 = index + 1;
                        index2 = get_active_palette_index(2);
                        index3 = index2 + 1;
                        index4 = get_active_palette_index(2);
                        unique_index = get_active_palette_index(1);
                        break;
                    case 5: //All
                        index = get_active_palette_index(1);
                        index1 = get_active_palette_index(1);
                        index2 = get_active_palette_index(1);
                        index3 = get_active_palette_index(1);
                        index4 = get_active_palette_index(1);
                        unique_index = get_active_palette_index(1);
                        break;
                }

                //Set Colors
                primary = colour_to_vec4(template.Palettes[i].Colours[index]);
                alt1 = colour_to_vec4(template.Palettes[i].Colours[index1]);
                alt2 = colour_to_vec4(template.Palettes[i].Colours[index2]);
                alt3 = colour_to_vec4(template.Palettes[i].Colours[index3]);
                alt4 = colour_to_vec4(template.Palettes[i].Colours[index4]);
                matchg = primary;
                unique = colour_to_vec4(template.Palettes[i].Colours[unique_index]);

                //save the colors to the dictionary
                newPal[pal_name]["Primary"] = primary;
                newPal[pal_name]["Alternative1"] = alt1;
                newPal[pal_name]["Alternative2"] = alt2;
                newPal[pal_name]["Alternative3"] = alt3;
                newPal[pal_name]["Alternative4"] = alt4;
                newPal[pal_name]["Unique"] = unique;
                newPal[pal_name]["None"] = none;


                //bmp.Save("Temp\\" + pal_name + ".bmp", ImageFormat.Bmp);
            }
            
            return newPal;
        }

        private static int get_active_palette_index(int grouping)
        {
            return MVCore.Common.RenderState.randgen.Next(0, 64 / grouping) * grouping;
        }

        private static Vector4 colour_to_vec4(Colour col)
        {
            return new Vector4(col.R, col.G, col.B, col.A);
        }

        public static void set_palleteColors()
        {
            //Initialize the palette everytime this is called
            paletteSel = new Dictionary<string, Dictionary<string, Vector4>>();
            //paletteSel = createPalette();
            paletteSel = createPalettefromBasePalettes();
        }

    }

}
