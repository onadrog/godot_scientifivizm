extends Camera3D

var fc: FrameCompositor
@onready var mesh_instance_3d: MeshInstance3D = %MeshInstance3D

func _ready() -> void:
    set_physics_process(false)
    for c :CompositorEffect in %Camera3D.compositor.compositor_effects:
        if c is FrameCompositor:
            fc = c
            break
    assert(fc != null)
    fc._on_rdy.connect(_on_rdy)
    RenderingServer.call_on_render_thread(fc.initCompute)
    %Camera3D.cull_mask = int(1) << int(20)
    mesh_instance_3d.layers |= int(1) << int(20)
    
func _on_rdy(rid: RID) -> void:
    var t: Texture2DRD = Texture2DRD.new()
    t.texture_rd_rid = rid
    (mesh_instance_3d.material_overlay as ShaderMaterial).set_shader_parameter("paintedTexture", t)
    set_physics_process(true)
    $"../TextureRect".texture = t

func _physics_process(delta: float) -> void:
    if Input.is_action_pressed("right"):
        mesh_instance_3d.rotate_y(1.7 * delta)
    elif Input.is_action_pressed("left"):
        mesh_instance_3d.rotate_y(-1.7 * delta)
    if Input.is_action_pressed("up"):
        mesh_instance_3d.rotate_x(-1.7 * delta)
    elif Input.is_action_pressed("down"):
        mesh_instance_3d.rotate_x(1.7 * delta)
    #var from: Vector3 = project_ray_origin(get_viewport().get_mouse_position())
    #var to : Vector3 = project_ray_normal(get_viewport().get_mouse_position())

    %Camera3D.look_at(project_position(get_viewport().get_mouse_position(), 5.0))
    $"../Label".text = "%d FPS" % Engine.get_frames_per_second()

    #var v: Vector2 =( get_viewport().get_mouse_position() / get_viewport().get_visible_rect().size) - Vector2(0.5,0.5)
    #%Camera3D.position.x = v.x
    #%Camera3D.position.y = 1 - v.y


func _on_button_pressed() -> void:
    get_tree().root.propagate_notification(NOTIFICATION_WM_CLOSE_REQUEST)
    get_tree().quit()
