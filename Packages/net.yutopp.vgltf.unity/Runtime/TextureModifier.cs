//
// Copyright (c) 2022- yutopp (yutopp@gmail.com)
//
// Distributed under the Boost Software License, Version 1.0. (See accompanying
// file LICENSE_1_0.txt or copy at  https://www.boost.org/LICENSE_1_0.txt)
//

using UnityEngine;

namespace VGltf.Unity
{
    public static class TextureModifier
    {
        // TODO: non-blocking version
        // glTF -> Unity
        public static void OverwriteGltfOcclusionTexToUnity(Texture2D tex)
        {
            var pixels = tex.GetPixels();
            for (var i = 0; i < pixels.Length; ++i)
            {
                pixels[i] = ValueConv.ConvertGltfOcclusionPixelToUnity(pixels[i]);
            }
            tex.SetPixels(pixels);
            tex.Apply();
        }

        // TODO: non-blocking version
        // Unity -> glTF
        public static void OverwriteUnityOcclusionTexToGltf(Texture2D tex)
        {
            var pixels = tex.GetPixels();
            for (var i = 0; i < pixels.Length; ++i)
            {
                pixels[i] = ValueConv.ConvertUnityOcclusionPixelToGltf(pixels[i]);
            }
            tex.SetPixels(pixels);
            tex.Apply();
        }

        // --

        // TODO: non-blocking version
        // glTF -> Unity
        public static void OverriteRoughnessMapToGlossMap(Texture2D tex, float metallic, float roughness)
        {
            var pixels = tex.GetPixels();
            for (var i = 0; i < pixels.Length; ++i)
            {
                pixels[i] = ValueConv.RoughnessPixelToGlossPixel(pixels[i], metallic, roughness);
            }
            tex.SetPixels(pixels);
            tex.Apply();
        }

        // TODO: non-blocking version
        // Unity -> glTF
        public static void OverriteToGlossMapToRoughnessMap(Texture2D tex, float metallic, float smoothness)
        {
            var pixels = tex.GetPixels();
            for (var i = 0; i < pixels.Length; ++i)
            {
                pixels[i] = ValueConv.GlossPixelToRoughnessPixel(pixels[i], metallic, smoothness);
            }
            tex.SetPixels(pixels);
            tex.Apply();
        }
    }
}
