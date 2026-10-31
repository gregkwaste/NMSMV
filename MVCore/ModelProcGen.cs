using System;
using System.Collections.Generic;
using libMBIN.NMS.Toolkit;

namespace MVCore
{
    public static class ModelProcGen
    {
        public static void parse_procTexture(List<TkProceduralTexture> parts, TkProceduralTextureList template)
        {
            Dictionary<string, int> selections = new Dictionary<string, int>();
            for (int i = 0; i < template.Layers.Length; i++)
            {
                TkProceduralTextureLayer layer = template.Layers[i];
                List<TkProceduralTexture> textures = layer.Textures;

                parts[i] = null;

                if (textures == null || textures.Count == 0)
                    continue;

                if (i > 0 && Common.RenderState.randgen.NextDouble() >= layer.Probability)
                    continue;

                int selection;
                if (!string.IsNullOrEmpty(layer.LinkedLayer) &&
                    selections.TryGetValue(layer.LinkedLayer, out int linked) &&
                    linked < textures.Count)
                {
                    selection = linked;
                }
                else
                {
                    selection = weightedPick(textures);
                }

                if (!string.IsNullOrEmpty(layer.Name))
                    selections[layer.Name] = selection;
                parts[i] = textures[selection];
            }
        }

        private static int weightedPick(List<TkProceduralTexture> textures)
        {
            float total = 0.0f;
            foreach (TkProceduralTexture texture in textures)
                total += Math.Max(texture.Probability, 0.0f);

            if (total <= 0.0f)
                return Common.RenderState.randgen.Next(0, textures.Count);

            double selection = Common.RenderState.randgen.NextDouble() * total;
            for (int i = 0; i < textures.Count; i++)
            {
                selection -= Math.Max(textures[i].Probability, 0.0f);
                if (selection < 0.0)
                    return i;
            }

            return textures.Count - 1;
        }
    }
}
