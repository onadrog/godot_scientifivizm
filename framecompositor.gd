class_name FrameCompositor
extends CompositorEffect

signal _on_rdy(rid: RID)

#const brush : Image = preload("uid://rxhh6ls35v6o") # brusjh_tecture
const brush : Image = preload("res://eponge.png") # eponge

const shader: RDShaderFile = preload("uid://ctife0wj6cnkt")

var rd: RenderingDevice
var pipeline: RID
var shader_rid: RID
var frameUniformSetRID: RID
var framebuffa_rid: RID
var screen_size: Vector2i


var texture_rid: RID
var textureUniformSet: RID
var brush_texture_rid: RID
var brush_texture_uniform_set_rid: RID

var sampler_rid: RID

func _init() -> void:
    enabled = false

func initCompute() -> void:
    print("initcompute")
    rd = RenderingServer.get_rendering_device()
    assert(rd != null)
    shader_rid = rd.shader_create_from_spirv(shader.get_spirv())
    assert(shader_rid != null)
    
    var img: Image = Image.create_empty(1024,1024, false, Image.FORMAT_R8)
    var imageFormat: RDTextureFormat = RDTextureFormat.new()
    imageFormat.format = RenderingDevice.DATA_FORMAT_R8_UNORM
    imageFormat.height = 1024
    imageFormat.width = 1024
    imageFormat.usage_bits = RenderingDevice.TEXTURE_USAGE_CAN_UPDATE_BIT \
                            | RenderingDevice.TEXTURE_USAGE_STORAGE_BIT \
                            | RenderingDevice.TEXTURE_USAGE_SAMPLING_BIT 
    
    texture_rid = rd.texture_create(imageFormat, RDTextureView.new(), [img.get_data()])
    
    var imageUniform: RDUniform = RDUniform.new()
    imageUniform.binding = 0
    imageUniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
    imageUniform.add_id(texture_rid)
    
    textureUniformSet = rd.uniform_set_create([imageUniform], shader_rid, 1)
    assert(textureUniformSet.is_valid())
    
    
    # --------------- brush ------------- #
    
    if brush.get_format() != Image.Format.FORMAT_R8:
        brush.convert(Image.Format.FORMAT_R8)
    
    var brush_texture_format: RDTextureFormat = RDTextureFormat.new()
    brush_texture_format.height = brush.get_height()
    brush_texture_format.width = brush.get_width()
    brush_texture_format.format = RenderingDevice.DATA_FORMAT_R8_UNORM
    brush_texture_format.usage_bits = RenderingDevice.TEXTURE_USAGE_SAMPLING_BIT
    
    brush_texture_rid = rd.texture_create(brush_texture_format, RDTextureView.new(), [brush.get_data()])
    
    var samplerState: RDSamplerState = RDSamplerState.new()
    samplerState.mag_filter = RenderingDevice.SAMPLER_FILTER_LINEAR
    samplerState.min_filter = RenderingDevice.SAMPLER_FILTER_LINEAR
    samplerState.repeat_u = RenderingDevice.SAMPLER_REPEAT_MODE_CLAMP_TO_EDGE
    samplerState.repeat_v = RenderingDevice.SAMPLER_REPEAT_MODE_CLAMP_TO_EDGE
    sampler_rid = rd.sampler_create(samplerState)
    
    var brush_uniform: RDUniform = RDUniform.new()
    brush_uniform.binding = 0
    brush_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_SAMPLER_WITH_TEXTURE
    brush_uniform.add_id(sampler_rid)
    brush_uniform.add_id(brush_texture_rid)
    
    brush_texture_uniform_set_rid = rd.uniform_set_create([brush_uniform], shader_rid, 2)
    assert(brush_texture_uniform_set_rid.is_valid())
    
    pipeline = rd.compute_pipeline_create(shader_rid)
    assert(pipeline.is_valid())
    _on_rdy.emit(texture_rid)
    enabled = true

func _render_callback(_effect_callback_type: int, render_data: RenderData) -> void:
    #if rd == null || !pipeline.is_valid():
        #return

    var rsb :RenderSceneBuffersRD = render_data.get_render_scene_buffers()
    framebuffa_rid = rsb.get_color_layer(0)
    
    screen_size = rsb.get_internal_size()
    
    if screen_size.x == 0 || screen_size.y == 0 :
        return
        
    var frameUniform : RDUniform = RDUniform.new()
    frameUniform.binding = 0
    frameUniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
    frameUniform.add_id(framebuffa_rid)
    
    frameUniformSetRID = UniformSetCacheRD.get_cache(shader_rid, 0, [frameUniform])
    
    rd.draw_command_begin_label("bjr", Color.DARK_ORCHID)

    var compute_list: int = rd.compute_list_begin()
    rd.compute_list_bind_uniform_set(compute_list, frameUniformSetRID, 0)
    rd.compute_list_bind_uniform_set(compute_list, textureUniformSet, 1)
    rd.compute_list_bind_uniform_set(compute_list, brush_texture_uniform_set_rid, 2)
    rd.compute_list_bind_compute_pipeline(compute_list, pipeline)
    rd.compute_list_dispatch(compute_list, ceili(screen_size.x / 8.0), ceili(screen_size.y/8.0), 1)
    rd.compute_list_end()
    
    rd.draw_command_end_label()

func _notification(what: int) -> void:
    print("brruh")
    if what == NOTIFICATION_PREDELETE && rd != null:
        #if framebuffa_rid.is_valid():
            #rd.free_rid(framebuffa_rid)
        if texture_rid.is_valid():
            rd.free_rid(texture_rid)
        if brush_texture_rid.is_valid():
            rd.free_rid(brush_texture_rid)
        if sampler_rid.is_valid():
            rd.free_rid(sampler_rid)
        if shader_rid.is_valid():
            rd.free_rid(shader_rid)
        
        
        #rd.free()
