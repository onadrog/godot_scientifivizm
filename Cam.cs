using Godot;

public partial class Cam : Camera3D
{
	MeshInstance3D _meshInstance;

	Painter pe;

	TextureRect _texture;

	Camera3D _cam;

	Label label;

	Button _button;

	Viewport _viewport;

	SubViewport _subViewport;

	Vector2 _lastMousePos;

	double _fpsAccum;

	public override void _Ready()
	{
		Engine.MaxFps = 0;
		DisplayServer.WindowSetVsyncMode(DisplayServer.VSyncMode.Disabled);

		_button = GetNode<Button>("%Button");
		label = GetNode<Label>("%Label");
		_cam = GetNode<Camera3D>("%Camera3D");
		_subViewport = _cam.GetParent<SubViewport>();
		_meshInstance = GetNode<MeshInstance3D>("%MeshInstance3D");
		_texture = GetNode<TextureRect>("%TextureRect");
		_viewport = GetViewport();
		RenderingServer.ViewportSetMeasureRenderTime(_viewport.GetViewportRid(), true);
		SetPhysicsProcess(false);

		foreach (CompositorEffect ce in _cam.Compositor.CompositorEffects)
		{
			if (ce is Painter p)
			{
				pe = p;
				break;
			}
		}

		if (pe == null)
		{
			GD.PrintErr("[Cam] Painter compositor effect not found on _cam.Compositor");
			return;
		}
		GD.Print("[Cam] Painter effect found, requesting compute init");

		pe.OnRdy += OnRdy;
		RenderingServer.CallOnRenderThread(Callable.From(pe.InitCompute));
		_cam.CullMask = 1 << 20;
		_meshInstance.Layers |= 1 << 20;

		_button.Pressed += OnQuitPressed;
	}
	public void OnRdy(Rid r)
	{
		Texture2Drd t = new()
		{
			TextureRdRid = r,
		};
		(_meshInstance.MaterialOverlay as ShaderMaterial).SetShaderParameter("paintedTexture", t);
		if (_subViewport != null)
		{
			_lastMousePos = _viewport.GetMousePosition();
			_subViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			RenderingServer.ViewportSetMeasureRenderTime(_subViewport.GetViewportRid(), true);
			GD.Print("[Cam] brush viewport gated on input (renders only on rotate/mouse-move)");
		}
		else
		{
			GD.PrintErr("[Cam] SubViewport not found, brush renders every frame (no gating)");
		}
		SetPhysicsProcess(true);
		GD.Print("[Cam] painted texture bound to mesh overlay, input enabled");
	}

	public override void _ExitTree()
	{
		if (pe == null) return;
		(_meshInstance.MaterialOverlay as ShaderMaterial)?.SetShaderParameter("paintedTexture", default);
		RenderingServer.CallOnRenderThread(Callable.From(pe.Cleanup));
		GD.Print("[Cam] requested Painter GPU cleanup on render thread");
	}

	public override void _PhysicsProcess(double delta)
	{
		bool changed = false;

		if (Input.IsActionPressed("right"))
		{
			_meshInstance.RotateY(1.7f * (float)delta);
			changed = true;
		}
		else if (Input.IsActionPressed("left"))
		{
			_meshInstance.RotateY(-1.7f * (float)delta);
			changed = true;
		}

		if (Input.IsActionPressed("up"))
		{
			_meshInstance.RotateX(1.7f * (float)delta);
			changed = true;
		}
		else if (Input.IsActionPressed("down"))
		{
			_meshInstance.RotateX(-1.7f * (float)delta);
			changed = true;
		}

		Vector2 mouse = _viewport.GetMousePosition();
		_cam.LookAt(ProjectPosition(mouse, 5.0f));

		if (_subViewport != null && (changed || mouse != _lastMousePos))
			_subViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
		_lastMousePos = mouse;

		_fpsAccum += delta;
		if (_fpsAccum >= 0.25)
		{
			double gpuMs = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewport.GetViewportRid());
			double cpuMs = RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewport.GetViewportRid());
			if (_subViewport != null)
			{
				Rid subVp = _subViewport.GetViewportRid();
				gpuMs += RenderingServer.ViewportGetMeasuredRenderTimeGpu(subVp);
				cpuMs += RenderingServer.ViewportGetMeasuredRenderTimeCpu(subVp);
			}
			string gpuStr = gpuMs > 0.001
				? $"GPU {gpuMs:0.00}ms -> ~{(int)(1000.0 / gpuMs)} FPS reel"
				: "GPU n/a (Metal, lance sur Linux)";
			label.Text = $"{Engine.GetFramesPerSecond()} FPS ecran | {gpuStr} | CPU {cpuMs:0.00}ms";
			_fpsAccum = 0.0;
		}
	}

	void OnQuitPressed()
	{
		GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
		GetTree().Quit();
	}
}
