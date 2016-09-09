using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Diagnostics;

namespace Model_Viewer
{
    public static class Util
    {
        public static readonly Random randgen = new Random();
        //public static string dirpath = "J:\\Installs\\Steam\\steamapps\\common\\No Man's Sky\\GAMEDATA\\PCBANKS";
        public static string dirpath = "C:\\Users\\greg\\Source\\Repos\\nms-viewer\\Model Viewer\\Samples";
    }

    class ModelProcGen
    {
        //static Random randgen = new Random();

        public static List<Selector> parse_level(XmlNode level)
        {
            List<Selector> sel_list = new List<Selector>();
            Dictionary<string, opt_dict_val> opt_dict = new Dictionary<string, opt_dict_val>();
            string[] blacklist = new string[] {"COLLISION", "JOINT"};
            

            //Iterate in level
            foreach (XmlElement elem in level.ChildNodes)
            {
                XmlNode node = elem.SelectSingleNode(".//INFO/NAME");
                XmlNode typ = elem.SelectSingleNode(".//INFO/TYPE");

                if (blacklist.Contains(typ.InnerText)) continue;

                if (node.InnerText.StartsWith("_") & (!node.InnerText.Contains("Shape"))){
                    //Handle Unique Parts

                    string nam = node.InnerText.TrimStart('_').Split('_')[0];
                    string opt = node.InnerText.TrimStart('_').Split('_')[1];
                    //Debug.WriteLine(nam);
                    //Debug.WriteLine(opt);

                    //Check if name already in dictionary
                    if (!opt_dict.Keys.Contains(nam))
                        opt_dict.Add(nam, new opt_dict_val());

                    opt_dict[nam].opts.Add(opt);
                    opt_dict[nam].nodes.Add(elem);
                } else if (!node.InnerText.Contains("Shape")) 
                    //Handle endpoints
                {
                    string nam = node.InnerText;
                    if (!opt_dict.Keys.Contains(nam))
                        opt_dict.Add(nam, new opt_dict_val());

                    opt_dict[nam].nodes.Add(null);
                }
            }
            //Create Selector from dict
            foreach (string n in opt_dict.Keys)
            {
                Selector sel = new Selector(n);
                sel.opts = opt_dict[n].opts;
                //Iterate in opts and parse the descendants
                for (int i=0; i < sel.opts.Count; i++)
                {
                    string key = sel.opts[i];
                    XmlNode elem = opt_dict[n].nodes[i];
                    if (elem == null)
                    {
                        sel.endpoint = true;
                        continue;
                    }
                     
                    XmlNode children = elem.SelectSingleNode(".//CHILDREN");
                    if (children != null)
                        sel.subs[key] = parse_level(children);
                    
                }
                sel_list.Add(sel);
            }

            return sel_list;
            
        }

        public static void addToStr(ref List<string> parts,string entry)
        {
            if (!parts.Contains(entry))
                parts.Add(entry);
        }

        public static void parse_selector(Selector active, ref List<string> parts)
        {
            int v = -1;
            if (active.opts.Count == 0)
            {
                //Debug.WriteLine(path + '|' + active.name);
                addToStr(ref parts, active.name);
                return;
            } else if (active.opts.Count == 1)
                v = 0;
            else
            {
                v = Util.randgen.Next(0, active.opts.Count);
            }

            string vsub = active.opts[v];


            //Check for endpoint
            if (active.subs.Keys.Contains(vsub))
                for (int i = 0; i < active.subs[vsub].Count; i++)
                {
                    Selector newsel = active.subs[vsub][i];
                    //parse_selector(newsel, path + '|' + active.name + '_' + vsub);
                    addToStr(ref parts, '_'+active.name + '_' + vsub);
                    parse_selector(newsel, ref parts);
                }
            else
            {
                //Debug.WriteLine(path + '|' + active.name + '_' + vsub);
                addToStr(ref parts, '_' + active.name + '_' + vsub);
                return;
            }
                

        }

    }

    

    class opt_dict_val
    {
        public List<string> opts = new List<string>();
        public List<XmlElement> nodes = new List<XmlElement>();
    }

    class Selector
    {
        public string split = "_";
        public List<string> opts = new List<string>();
        public Dictionary<string, List<Selector>> subs = new Dictionary<string, List<Selector>>();
        public bool endpoint = false;
        public string name;

        public Selector(string nm)
        {
            this.name = nm;
        }

        public List<string> get_subnames(string key)
        {
            List<string> l = new List<string>();
            Debug.WriteLine(this.subs[key]);
            if (this.subs.Keys.Contains(key))
                throw new ApplicationException("Malakia Key");
            
            foreach (Selector sel in this.subs[key])
            {
                l.Add(sel.name);
            }

            return l;
        }
        
    }


}
