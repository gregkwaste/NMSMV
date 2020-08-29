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
using libMBIN.NMS.Toolkit;
using libMBIN.NMS.GameComponents;
using libMBIN.NMS;
using MVCore;
using MVCore.Common;
using MVCore.Utils;

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
                //Add MatchGround option
                newPal[f.Name]["MatchGround"] = new Vector4(0.5f, 0.427f, 0.337f, 0.0f);

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
                    CallBacks.Log("Missing Options for Palette " + f.Name);
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

            GcPaletteList template;
            
            try {
                 template = NMSUtils.LoadNMSTemplate("METADATA\\SIMULATION\\SOLARSYSTEM\\COLOURS\\BASECOLOURPALETTES.MBIN",
                    ref MVCore.Common.RenderState.activeResMgr) as GcPaletteList;
            } catch (Exception ex) {
                CallBacks.Log("Using Default Palettes");
                return createPalette();
            }
            
            TkPaletteTexture tkpt = new TkPaletteTexture();
            GcPaletteData gcpd = new GcPaletteData();
            
            for (int i = 0; i < template.Palettes.Length; i++)
            {
                string pal_name = ((TkPaletteTexture.PaletteEnum) i).ToString();
                CallBacks.Log(string.Format("Palette {0} NumColors {1}", pal_name, template.Palettes[i].NumColours));
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
                    case GcPaletteData.NumColoursEnum.Inactive: //Inactive
                        //Test by not saving anything
                        break;
                    case GcPaletteData.NumColoursEnum._1: //1 Color - All colors should be the same
                        index = 0; 
                        index1 = index;
                        index2 = index;
                        index3 = index;
                        index4 = index;
                        unique_index = 0;
                        break;
                    case GcPaletteData.NumColoursEnum._4: //4 Color
                    case GcPaletteData.NumColoursEnum._8: //8 Color NOTE: Not enough samples for that
                        index = get_active_palette_index(4);
                        index1 = index + 1;
                        index2 = index + 2;
                        index3 = index + 3;
                        unique_index = get_active_palette_index(1);
                        break;
                    case GcPaletteData.NumColoursEnum._16: //16 Color
                        //Align to groups of 2
                        index = get_active_palette_index(2);
                        index1 = index + 1;
                        index2 = get_active_palette_index(2);
                        index3 = index2 + 1;
                        index4 = get_active_palette_index(2);
                        unique_index = get_active_palette_index(1);
                        break;
                    case GcPaletteData.NumColoursEnum.All: //All
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
                none = primary;

                //save the colors to the dictionary
                newPal[pal_name]["Primary"] = primary;
                newPal[pal_name]["Alternative1"] = alt1;
                newPal[pal_name]["Alternative2"] = alt2;
                newPal[pal_name]["Alternative3"] = alt3;
                newPal[pal_name]["Alternative4"] = alt4;
                newPal[pal_name]["Unique"] = unique;
                //Add MatchGround option
                newPal[pal_name]["MatchGround"] = new Vector4(0.5f, 0.427f, 0.337f, 0.0f);
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
            paletteSel = createPalettefromBasePalettes();
        }

    }

}
