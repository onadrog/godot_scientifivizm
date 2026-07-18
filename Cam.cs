using Godot;

public partial class Cam : Camera3D
{
	static readonly StringName ActionRight = "right";
	static readonly StringName ActionLeft = "left";
	static readonly StringName ActionUp = "up";
	static readonly StringName ActionDown = "down";
	static readonly StringName PaintedTextureParam = "paintedTexture";

	const float ROTATE_SPEED = 1.7f;
	const float LOOK_AT_DEPTH = 5.0f;
	const double LABEL_REFRESH_SECONDS = 0.25;

	MeshInstance3D _meshInstance;

	ShaderMaterial _overlayMaterial;

	Painter pe;

	TextureRect _texture;

	Camera3D _cam;

	Label label;

	Button _button;

	Viewport _viewport;

	SubViewport _subViewport;

	Rid _viewportRid;

	Rid _subViewportRid;

	Texture2Drd _paintedTexture;

	Vector2 _lastMousePos = new(float.NaN, float.NaN);

	double _fpsAccum;

	public override void _Ready()
	{
		_button = GetNode<Button>("%Button");
		label = GetNode<Label>("%Label");
		_cam = GetNode<Camera3D>("%Camera3D");
		_subViewport = _cam.GetParent<SubViewport>();
		_meshInstance = GetNode<MeshInstance3D>("%MeshInstance3D");
		_overlayMaterial = _meshInstance.MaterialOverlay as ShaderMaterial;
		_texture = GetNode<TextureRect>("%TextureRect");
		_viewport = GetViewport();
		_viewportRid = _viewport.GetViewportRid();
		RenderingServer.ViewportSetMeasureRenderTime(_viewportRid, true);
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

		if (LeakSentry.Instance != null)
		{
			LeakSentry.Instance.LeakSuspected += OnLeakSuspected;
			GD.Print("[Cam] leak_sentry armed: shift+L overlay, shift+G report, shift+D dump");
		}

		pe.OnRdy += OnRdy;
		RenderingServer.CallOnRenderThread(Callable.From(pe.InitCompute));
		_cam.CullMask = 1 << 20;
		_meshInstance.Layers |= 1 << 20;

		_button.Pressed += OnQuitPressed;
	}

	public void OnRdy(Rid r)
	{
		_paintedTexture = new Texture2Drd { TextureRdRid = r };
		LeakSentry.Track(_paintedTexture, "painted-texture");
		_overlayMaterial?.SetShaderParameter(PaintedTextureParam, _paintedTexture);
		_texture.Texture = _paintedTexture;
		if (_subViewport != null)
		{
			_subViewportRid = _subViewport.GetViewportRid();
			_subViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
			RenderingServer.ViewportSetMeasureRenderTime(_subViewportRid, true);
			GD.Print("[Cam] brush viewport gated on input (renders only on rotate/mouse-move)");
		}
		else
		{
			GD.PrintErr("[Cam] SubViewport not found, brush renders every frame (no gating)");
		}
		SetPhysicsProcess(true);
		GD.Print("[Cam] painted texture bound to mesh overlay, input enabled");
	}

	void OnLeakSuspected(string metric, double growthPerSecond)
	{
		GD.PrintErr($"[Cam] leak_sentry suspects {metric} growing at {growthPerSecond:0.00}/s");
	}

	public override void _ExitTree()
	{
		if (LeakSentry.Instance != null)
			LeakSentry.Instance.LeakSuspected -= OnLeakSuspected;

		if (pe == null)
			return;
		_overlayMaterial?.SetShaderParameter(PaintedTextureParam, default);
		_paintedTexture = null;
		RenderingServer.CallOnRenderThread(Callable.From(pe.Cleanup));
		GD.Print("[Cam] requested Painter GPU cleanup on render thread");
	}

	public override void _PhysicsProcess(double delta)
	{
		float step = ROTATE_SPEED * (float)delta;
		bool changed = false;

		if (Input.IsActionPressed(ActionRight))
		{
			_meshInstance.RotateY(step);
			changed = true;
		}
		else if (Input.IsActionPressed(ActionLeft))
		{
			_meshInstance.RotateY(-step);
			changed = true;
		}

		if (Input.IsActionPressed(ActionUp))
		{
			_meshInstance.RotateX(step);
			changed = true;
		}
		else if (Input.IsActionPressed(ActionDown))
		{
			_meshInstance.RotateX(-step);
			changed = true;
		}

		Vector2 mouse = _viewport.GetMousePosition();
		bool mouseMoved = mouse != _lastMousePos;

		if (mouseMoved)
		{
			_cam.LookAt(ProjectPosition(mouse, LOOK_AT_DEPTH));
			_lastMousePos = mouse;
		}

		if (_subViewport != null && (changed || mouseMoved))
			_subViewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;

		_fpsAccum += delta;
		if (_fpsAccum < LABEL_REFRESH_SECONDS)
			return;
		_fpsAccum = 0.0;


		double gpuMs = RenderingServer.ViewportGetMeasuredRenderTimeGpu(_viewportRid);
		double cpuMs = RenderingServer.ViewportGetMeasuredRenderTimeCpu(_viewportRid);
		if (_subViewportRid.IsValid)
		{
			gpuMs += RenderingServer.ViewportGetMeasuredRenderTimeGpu(_subViewportRid);
			cpuMs += RenderingServer.ViewportGetMeasuredRenderTimeCpu(_subViewportRid);
		}
		string gpuStr =
			gpuMs > 0.001
				? $"GPU {gpuMs:0.00}ms -> ~{(int)(1000.0 / gpuMs)} FPS reel"
				: "GPU n/a (Metal, lance sur Linux)";
		label.Text = $"{Engine.GetFramesPerSecond()} FPS ecran | {gpuStr} | CPU {cpuMs:0.00}ms";
	}

	void OnQuitPressed()
	{
		GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
		GetTree().Quit();
	}
}
