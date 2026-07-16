shader_type spatial;

uniform sampler2D paintedTexture: source_color;

void fragment() {
    bool is_camera_brush = (CAMERA_VISIBLE_LAYERS & (1u << uint(20))) != 0u; // cull mask 21
    if(is_camera_brush){
        ALBEDO = vec3(0.0);
		SPECULAR = 0.0;
        EMISSION = vec3(UV - vec2(0.5), -VERTEX.z);
    } else {
        vec4 clr = texture(paintedTexture, UV);
        ALBEDO = clr.rrr;
    }
}
