using System;
using System.Collections.Generic;
using DevGame.Utility;
using UnityEngine;
[Serializable]
public class SDConfig
{
    [StringArray(new []{"Euler a",
        "Euler",
        "LMS",
        "Heun",
        "DPM2",
        "DPM2 a",
        "DPM++ 2S a",
        "DPM++ 2M",
        "DPM++ SDE",
        "DPM fast",
        "DPM adaptive",
        "LMS Karras",
        "DPM2 Karras",
        "DPM2 a Karras",
        "DPM++ 2S a Karras",
        "DPM++ 2M Karras",
        "DPM++ SDE Karras",
        "DDIM",
        "PLMS"})] public string sampler_name = "DPM++ 2M Karras";

    public string scheduler = "Karras";
    [TextArea] public string prompt;
    [TextArea] public string negative_prompt;
    public int seed;
    [Range(1, 150)] public int steps = 20;
    [Range(1, 30f)] public float cfg_scale = 7;
    public int width = 512;
    public int height = 512;
    public bool restore_faces;
    public int batch_size = 1;

    public SDConfig(SDConfig source)
    {
        sampler_name = source.sampler_name;
        scheduler = source.scheduler;
        prompt = source.prompt;
        negative_prompt = source.negative_prompt;
        seed = source.seed;
        steps = source.steps;
        cfg_scale = source.cfg_scale;
        width = source.width;
        height = source.height;
        restore_faces = source.restore_faces;
        batch_size = source.batch_size;
    }
}
[Serializable]
public class RembgConfig
{
    public string model = "isnet-anime";
    public string input_image = "";

    public bool return_mask = false;
    public bool alpha_matting = false;

    public int alpha_matting_foreground_threshold = 240;
    public int alpha_matting_background_threshold = 10;
    public int alpha_matting_erode_size = 10;
}

// [Serializable]
// public class ImgToImgConfig : SDConfig
// {
//     public List<string> init_images;
//     [Range(0, 1f)] public double denoising_strength = 0.7f;
// }

[Serializable]
public class ImgToImgResponse
{
    public string[] images;
    public int width;
    public int height;
}

[Serializable]
public class RembgResponse
{
    public string image;
}
