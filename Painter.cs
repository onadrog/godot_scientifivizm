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
	Vector2I screenSize;

	Rid textureRid;
	Rid textureUniformSet;
	Rid brushTextureRid;
	Rid brushTextureUniformSet;

	Rid samplerRid;

	RDUniform frameBufferUniform;
	Godot.Collections.Array<RDUniform> frameBufferUniforms;
	bool firstRenderLogged;
	int renderCount;

	public void Cleanup()
	{
		if (rd == null || !shaderRid.IsValid) return;
		if (textureRid.IsValid) rd.FreeRid(textureRid);
		if (brushTextureRid.IsValid) rd.FreeRid(brushTextureRid);
		if (samplerRid.IsValid) rd.FreeRid(samplerRid);
		rd.FreeRid(shaderRid);
		textureRid = default;
		brushTextureRid = default;
		samplerRid = default;
		shaderRid = default;
		pipeline = default;
		textureUniformSet = default;
		brushTextureUniformSet = default;
		frameBufferUniform = null;
		frameBufferUniforms = null;
		GD.Print($"[Painter] GPU resources freed after {renderCount} compute dispatches");
	}

	Painter()
	{
		Enabled = false;
	}

	public void InitCompute()
	{
		GD.Print("[Painter] InitCompute: building compute resources");
		rd = RenderingServer.GetRenderingDevice();
		Debug.Assert(rd != null);
		RDShaderFile f = GD.Load<RDShaderFile>(SHDERPATH);
		var spirV = f.GetSpirV();
		string compileError = spirV.GetStageCompileError(RenderingDevice.ShaderStage.Compute);
		if (!string.IsNullOrEmpty(compileError))
			GD.PrintErr($"[Painter] compute shader compile error: {compileError}");
		shaderRid = rd.ShaderCreateFromSpirV(spirV);

		Debug.Assert(shaderRid.IsValid);

		RDTextureFormat imageFormat = new()
		{
			Format = RenderingDevice.DataFormat.R8Unorm,
			Height = 1024,
			Width = 1024,
			UsageBits = RenderingDevice.TextureUsageBits.CanUpdateBit
						| RenderingDevice.TextureUsageBits.StorageBit
						| RenderingDevice.TextureUsageBits.SamplingBit
		};

		byte[] zeroData = new byte[1024 * 1024];
		textureRid = rd.TextureCreate(imageFormat, new(), [zeroData]);

		RDUniform imageUniform = new()
		{
			Binding = 0,
			UniformType = RenderingDevice.UniformType.Image,
		};
		imageUniform.AddId(textureRid);

		textureUniformSet = rd.UniformSetCreate([imageUniform], shaderRid, 1);
		Debug.Assert(textureUniformSet.IsValid);

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

		frameBufferUniform = new RDUniform
		{
			Binding = 0,
			UniformType = RenderingDevice.UniformType.Image,
		};
		frameBufferUniforms = [frameBufferUniform];

		EmitSignal(SignalName.OnRdy, textureRid);
		Enabled = true;
		GD.Print("[Painter] compute ready, effect enabled");
	}

	public override void _RenderCallback(int effectCallbackType, RenderData renderData)
	{
		if (!pipeline.IsValid) return;

		RenderSceneBuffersRD rsb = (RenderSceneBuffersRD)renderData.GetRenderSceneBuffers();
		screenSize = rsb.GetInternalSize();
		if (screenSize.X == 0 || screenSize.Y == 0) return;

		renderCount++;
		if (!firstRenderLogged)
		{
			firstRenderLogged = true;
			GD.Print($"[Painter] first render callback at internal size {screenSize}");
		}

		frameBufferUniform.ClearIds();
		frameBufferUniform.AddId(rsb.GetColorTexture());
		Rid frameBufferUniformSet = UniformSetCacheRD.GetCache(shaderRid, 0, frameBufferUniforms);

		long computeList = rd.ComputeListBegin();
		rd.ComputeListBindUniformSet(computeList, frameBufferUniformSet, 0);
		rd.ComputeListBindUniformSet(computeList, textureUniformSet, 1);
		rd.ComputeListBindUniformSet(computeList, brushTextureUniformSet, 2);
		rd.ComputeListBindComputePipeline(computeList, pipeline);
		rd.ComputeListDispatch(computeList, (uint)MathF.Ceiling(screenSize.X / 16.0f), (uint)MathF.Ceiling(screenSize.Y / 16.0f), 1);
		rd.ComputeListEnd();
	}

}
