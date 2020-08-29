using System;
using System.Collections.Generic;
using System.Text;

namespace MVCore.Text
{
    public class TextManager
    {
        public enum Semantic
        {
            FPS = 0x0,
            OCCLUDED_COUNT,
            VERT_COUNT,
            TRIS_COUNT,
            TEXTURE_COUNT,
            CTRL_ID
        }

        public List<Text> texts = new List<Text>();
        public Dictionary<Semantic, Text> textMap = new Dictionary<Semantic, Text>();
        public TextManager()
        {

        }

        public Text getText(Semantic sem)
        {
            if (!textMap.ContainsKey(sem))
                return null;
            return textMap[sem];
        }

        public void addText(Text t, Semantic sem)
        {
            if (t != null)
            {
                texts.Add(t);
                if (textMap.ContainsKey(sem))
                    textMap[sem].Dispose();
                textMap[sem] = t;
            }

        }

        ~TextManager()
        {
            texts.Clear();
        }

        public void cleanup()
        {
            foreach (Text t in texts)
                t.Dispose();
            texts.Clear();
        }

    }
}
