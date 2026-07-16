using System;
using System.Diagnostics;
using Godot;

[GlobalClass]
public partial class Painter : CompositorEffect
{
    [Signal]
    public delegate void OnRdyEventHandler(Rid rid);

    const string BRUSHPATH = "res://eponge.png";

    const string SHDERPATH = "uid://ctife0wj6cnkt";

    RenderingDevice rd;
    Rid pipeline;
    Rid shaderRid;
    Rid frameBuffaRid;
    Vector2I screenSize;


    Rid textureRid;
    Rid textureUniformSet;
    Rid brushTextureRid;
    Rid brushTextureUniformSet;

    Rid samplerRid;

    Painter()
    {
        Enabled = false;
    }

    public void InitCompute()
    {
        GD.Print("InitCompupte");
        rd = RenderingServer.GetRenderingDevice();
        Debug.Assert(rd != null);
        RDShaderFile f = GD.Load<RDShaderFile>(SHDERPATH);
        shaderRid = rd.ShaderCreateFromSpirV(f.GetSpirV());

        Debug.Assert(shaderRid.IsValid);

        Image img = Image.CreateEmpty(1024, 1024, false, Image.Format.R8);

        RDTextureFormat imageFormat = new()
        {
            Format = RenderingDevice.DataFormat.R8Unorm,
            Height = 1024,
            Width = 1024,
            UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit
                        | RenderingDevice.TextureUsageBits.StorageBit
                        | RenderingDevice.TextureUsageBits.SamplingBit
        };

        textureRid = rd.TextureCreate(imageFormat, new(), [img.GetData()]);

        RDUniform imageUniform = new()
        {
            Binding = 0,
            UniformType = RenderingDevice.UniformType.Image,
        };
        imageUniform.AddId(textureRid);

        textureUniformSet = rd.UniformSetCreate([imageUniform], shaderRid, 1);
        Debug.Assert(textureUniformSet.IsValid);

        //----------- brush

        Image brush = GD.Load<Image>(BRUSHPATH);
        brush.ClearMipmaps();

        if (brush.GetFormat() != Image.Format.R8)
        {
            brush.Convert(Image.Format.R8);
        }

        RDTextureFormat brushtextureFormat = new()
        {
            Height = (uint)brush.GetHeight(),
            Width = (uint)brush.GetWidth(),
            Format = RenderingDevice.DataFormat.R8Unorm,
            UsageBits = RenderingDevice.TextureUsageBits.SamplingBit
        };

        brushTextureRid = rd.TextureCreate(brushtextureFormat, new(), [brush.GetData()]);

        RDSamplerState samplerState = new()
        {
            MagFilter = RenderingDevice.SamplerFilter.Linear,
            MinFilter = RenderingDevice.SamplerFilter.Linear,
            RepeatU = RenderingDevice.SamplerRepeatMode.ClampToEdge,
            RepeatV = RenderingDevice.SamplerRepeatMode.ClampToEdge,
        };

        samplerRid = rd.SamplerCreate(samplerState);

        RDUniform brushUniform = new()
        {
            Binding = 0,
            UniformType = RenderingDevice.UniformType.SamplerWithTexture
        };
        brushUniform.AddId(samplerRid);
        brushUniform.AddId(brushTextureRid);

        brushTextureUniformSet = rd.UniformSetCreate([brushUniform], shaderRid, 2);
        Debug.Assert(brushTextureRid.IsValid);

        pipeline = rd.ComputePipelineCreate(shaderRid);
        Debug.Assert(pipeline.IsValid);
        EmitSignal(SignalName.OnRdy, textureRid);
        Enabled = true;
    }

    public override void _RenderCallback(int effectCallbackType, RenderData renderData)
    {
        RenderSceneBuffersRD rsb = (RenderSceneBuffersRD)renderData.GetRenderSceneBuffers();

        screenSize = rsb.GetInternalSize();

        if (screenSize.X == 0 || screenSize.Y == 0) return;

        frameBuffaRid = rsb.GetColorTexture();


        RDUniform frameBufferUniform = new()
        {
            Binding = 0,
            UniformType = RenderingDevice.UniformType.Image,
        };
        frameBufferUniform.AddId(frameBuffaRid);

        Rid frameBufferUniformSet = UniformSetCacheRD.GetCache(shaderRid, 0, [frameBufferUniform]);

        long computeList = rd.ComputeListBegin();
        rd.ComputeListBindUniformSet(computeList, frameBufferUniformSet, 0);
        rd.ComputeListBindUniformSet(computeList, textureUniformSet, 1);
        rd.ComputeListBindUniformSet(computeList, brushTextureUniformSet, 2);
        rd.ComputeListBindComputePipeline(computeList, pipeline);
        rd.ComputeListDispatch(computeList, (uint)MathF.Ceiling(screenSize.X / 8.0f), (uint)MathF.Ceiling(screenSize.Y / 8.0f), 1);
        rd.ComputeListEnd();
    }

    public override void _Notification(int what)
    {
        GD.Print("brruh");
        if (what == NotificationPredelete && rd != null)
        {
            if (textureRid.IsValid) rd.FreeRid(textureRid);
            if (brushTextureRid.IsValid) rd.FreeRid(brushTextureRid);
            if (samplerRid.IsValid) rd.FreeRid(samplerRid);
            if (shaderRid.IsValid) rd.FreeRid(shaderRid);
        }
    }

}
