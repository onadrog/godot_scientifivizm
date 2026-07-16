using System.Diagnostics;
using Godot;

public partial class Cam : Camera3D
{
    MeshInstance3D _meshInstance;

    Painter pe;

    TextureRect _texture;

    Camera3D _cam;

    Label label;

    Button _button;

    public override void _Ready()
    {

        _button = GetNode<Button>("%Button");
        label = GetNode<Label>("%Label");
        _cam = GetNode<Camera3D>("%Camera3D");
        _meshInstance = GetNode<MeshInstance3D>("%MeshInstance3D");
        _texture = GetNode<TextureRect>("%TextureRect");
        SetPhysicsProcess(false);
        foreach (CompositorEffect ce in _cam.Compositor.CompositorEffects)
        {
            if (ce is Painter p)
            {
                pe = p;
                break;
            }
        }

        Debug.Assert(pe != null);
        pe.OnRdy += OnRdy;
        RenderingServer.CallOnRenderThread(Callable.From(pe.InitCompute));
        _cam.CullMask = 1 << 20;
        _meshInstance.Layers |=  1 << 20;

        _button.Pressed += OnQuitPressed;
    }
    public void OnRdy(Rid r)
    {
        Texture2Drd t = new()
        {
            TextureRdRid = r,
        };
        (_meshInstance.MaterialOverlay as ShaderMaterial).SetShaderParameter("paintedTexture", t);
        SetPhysicsProcess(true);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Input.IsActionPressed("right"))
        {
            _meshInstance.RotateY(1.7f * (float)delta);
        }
        else if (Input.IsActionPressed("left"))
        {
            _meshInstance.RotateY(-1.7f * (float)delta);
        }

        if (Input.IsActionPressed("up"))
        {
            _meshInstance.RotateX(1.7f * (float)delta);
        }
        else if (Input.IsActionPressed("down"))
        {
            _meshInstance.RotateX(-1.7f * (float)delta);
        }

        _cam.LookAt(ProjectPosition(GetViewport().GetMousePosition(), 5.0f));
        label.Text = $"{Engine.GetFramesPerSecond()} FPS";
    }

    void OnQuitPressed()
    {
        GetTree().Root.PropagateNotification((int)NotificationWMCloseRequest);
        GetTree().Quit();
    }
}
