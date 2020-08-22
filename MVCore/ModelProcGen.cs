using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Diagnostics;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System.Reflection;
using System.IO;
using libMBIN.NMS.Toolkit;
using MVCore.Utils;
using System.Collections.ObjectModel;

namespace MVCore
{
    public class ModelProcGen
    {
        //static Random randgen = new Random();
        public static Dictionary<string, string> procDecisions = new Dictionary<string, string>();

        public static void addToStr(ref List<string> parts, string entry)
        {
            if (!parts.Contains(entry))
                parts.Add(entry);
        }

        public static void parse_descriptor(Random randgen, string dirpath, ref List<string> parts, XmlElement root)
        {
            foreach (XmlElement el in root.ChildNodes)
            {
                string TypeId = ((XmlElement)el.SelectSingleNode(".//Property[@name='TypeId']")).GetAttribute("value");
                //Debug.WriteLine(TypeId);
                //Select descriptors
                XmlElement descriptors = (XmlElement)el.SelectSingleNode(".//Property[@name='Descriptors']");

                //Select one descriptor
                int sel = randgen.Next(0, descriptors.ChildNodes.Count);
                XmlElement selNode = (XmlElement) descriptors.ChildNodes[sel];
                //Add selection to parts
                string partName = ((XmlElement)selNode.SelectSingleNode(".//Property[@name='Name']")).GetAttribute("value");
                addToStr(ref parts, partName);

                //Check for existing descriptors in the current element
                XmlElement refNode = (XmlElement) selNode.SelectSingleNode(".//Property[@name='ReferencePaths']");
                if (refNode.ChildNodes.Count > 0)
                {
                    for (int i = 0; i < refNode.ChildNodes.Count; i++)
                    {
                        XmlElement refChild = (XmlElement) refNode.ChildNodes[i];
                        string refPath = ((XmlElement)refChild.SelectSingleNode("Property[@name='Value']")).GetAttribute("value");
                        //Construct Descriptor Path
                        string[] split = refPath.Split('.');
                        string descrpath = "";
                        for (int j = 0; j < split.Length - 2; j++)
                            descrpath = Path.Combine(descrpath, split[j]);
                        descrpath += ".DESCRIPTOR.MBIN";
                        descrpath = Path.Combine(dirpath, descrpath);
                        string exmlPath = FileUtils.getExmlPath(descrpath);

                        //Check if descriptor exists at all
                        if (File.Exists(descrpath))
                        {
                            //Convert only if file does not exist
                            if (!File.Exists(exmlPath))
                            {
                                Debug.WriteLine("Exml does not exist, Converting...");
                                //Convert Descriptor MBIN to exml
                                FileUtils.MbinToExml(descrpath, exmlPath);
                            }

                            //Parse exml now
                            XmlDocument descrXml = new XmlDocument();
                            descrXml.Load(exmlPath);
                            XmlElement newRoot = (XmlElement)descrXml.ChildNodes[2].ChildNodes[0];
                            //Parse Descriptors from this object
                            parse_descriptor(randgen, dirpath, ref parts, newRoot);
                        }
                    }
                    

                }
                    
                //Get to children
                XmlElement children = (XmlElement)selNode.SelectSingleNode(".//Property[@name='Children']");

                if (children.ChildNodes.Count != 0)
                {
                    foreach (XmlElement child in children.ChildNodes[0].ChildNodes)
                        parse_descriptor(randgen, dirpath, ref parts, child);
                }
                

                //foreach (XmlElement d in descriptors.ChildNodes)
                //{
                //    string Id = ((XmlElement) d.SelectSingleNode(".//Property[@name='Id']")).GetAttribute("value");
                //    string Name = ((XmlElement)d.SelectSingleNode(".//Property[@name='Name']")).GetAttribute("value");
                //    Debug.WriteLine(Id + Name);
                //}

            }
        }


        public static GMDL.model get_procgen_parts(ref List<string> descriptors, GMDL.model root)
        {
            //Make deep copy of root 
            GMDL.model newRoot = ((GMDL.scene) root).Clone();
            root.procFlag = true; //Always keep the root node

            //PHASE 1
            //Flag Procgen parts
            get_procgen_parts_phase1(ref descriptors, newRoot);
            //PHASE 2
            //Save all candidates for removal
            List<string> childDelList = new List<string>();
            get_procgen_parts_phase2(ref childDelList, newRoot);
            //PHASE 3
            //Remove candidates
            get_procgen_parts_phase3(childDelList, newRoot);
            


            return newRoot;
        }

        public static void get_procgen_parts_phase1(ref List<string> descriptors, GMDL.model root)
        {
            //During phase one all procgen parts are flagged
            foreach (GMDL.model child in root.children)
            {
                //Identify Descriptors
                if (child.name.StartsWith("_"))
                {
                    for (int i = 0; i < descriptors.Count; i++)
                    {
                        if (child.name.Contains(descriptors[i]))
                        {
                            child.procFlag = true;
                            Debug.WriteLine("Setting Flag on " + child.name);
                            //iterate into Descriptor children
                            get_procgen_parts_phase1(ref descriptors, child);
                        }
                    }
                }
                //DO FLAG JOINTS
                else if (child.type == TYPES.JOINT)
                    continue;
                //Standard part, Endpoint as well
                else
                {
                    //Add part to partlist if not Joint, Light or Collision
                    if (child.type != TYPES.JOINT & child.type != TYPES.LIGHT & child.type != TYPES.COLLISION)
                    {
                        child.procFlag = true;
                        Debug.WriteLine("Setting Flag on " + child.name);
                        //Cover the case where endpoints have children as well
                        get_procgen_parts_phase1(ref descriptors, child);
                    }
                }
            }
        }

        public static void get_procgen_parts_phase2(ref List<string> dellist, GMDL.model root)
        {
            foreach (GMDL.model child in root.children)
            {
                if (!child.procFlag)
                    dellist.Add(child.name);
                else
                    get_procgen_parts_phase2(ref dellist, child);
            }   
        }

        public static void get_procgen_parts_phase3(List<string> dellist, GMDL.model root)
        {
            for (int i = 0; i < dellist.Count; i++)
            {
                string part_name = dellist[i];
                GMDL.model child;
                child = collectPart(root.children, part_name);

                if (child != null)
                {
                    GMDL.model parent = child.parent;
                    parent.children.Remove(child);
                }
                
            }
        }

        public static void parse_procTexture(ref List<TkProceduralTexture> parts, TkProceduralTextureList template, ref ResourceManager resMgr)
        {
            for (int i = 0; i < template.Layers.Length; i++)
            {
                TkProceduralTextureLayer layer = template.Layers[i];
                string layername = layer.Name;
                int layerid = i;
                List<TkProceduralTexture> textures = layer.Textures;
                //Debug.WriteLine("Texture Layer: " + layerid.ToString());

                parts[layerid] = null; //Init to null

                int sel;
                if (textures.Count > 0)
                {
                    //Select one descriptor at random
                    sel = Common.RenderState.randgen.Next(0, textures.Count);

                    TkProceduralTexture texture = textures[sel];
                    string partName = texture.Diffuse;
                    parts[layerid] = texture;
                    //addToStr(ref parts, partName);
                }
            }
        }

        public static GMDL.model collectPart(List<GMDL.model> coll, string name)
        {
            foreach (GMDL.model child in coll)
            {
                if (child.name == name)
                {
                    return child;
                }
                else
                {

                    GMDL.model ret = collectPart(child.children, name);
                    if (ret != null)
                        return ret;
                    else
                        continue;
                }
            }
            return null;
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
