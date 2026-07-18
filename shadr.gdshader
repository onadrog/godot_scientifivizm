shader_type spatial;

const uint BRUSH_CAMERA_LAYER_MASK = 1u << 20u;

uniform sampler2D paintedTexture : source_color, filter_linear, repeat_disable;

void fragment() {
    if ((CAMERA_VISIBLE_LAYERS & BRUSH_CAMERA_LAYER_MASK) != 0u) {
        ALBEDO = vec3(0.0);
        SPECULAR = 0.0;
        EMISSION = vec3(UV - vec2(0.5), -VERTEX.z);
    } else {
        ALBEDO = texture(paintedTexture, UV).rrr;
    }
}
