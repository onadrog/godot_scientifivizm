#[compute]
#version 460

#define BLEED 2

#define CAM_POS_Z 3.0f

#define MIN_VISIBLE_BRUSH (0.5f / 255.0f)

layout(local_size_x = 16, local_size_y = 16) in;

layout(set = 0, binding = 0, rgba16f) restrict readonly uniform image2D framebuffer;

layout(set = 1, binding = 0, r8) restrict uniform image2D output_image;

layout(set = 2, binding = 0) uniform sampler2D brush_texture;

void main(void) {
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    ivec2 fb_size = imageSize(framebuffer);
    if (any(greaterThanEqual(id, fb_size))) return;

    vec4 clr = imageLoad(framebuffer, id);

    float dist = CAM_POS_Z - abs(clr.z);
    if (dist < 0.001f) return;

    vec2 brush_uv = (vec2(id) + 0.5f) / vec2(fb_size);
    float brush_clr = texture(brush_texture, brush_uv).r * min(dist, 1.0f);

    if (brush_clr < MIN_VISIBLE_BRUSH) return;

    ivec2 out_size = imageSize(output_image);
    ivec2 p = ivec2((clr.xy + 0.5f) * vec2(out_size));

    ivec2 lo = max(p - BLEED, ivec2(0));
    ivec2 hi = min(p + (BLEED - 1), out_size - 1);

    vec4 brush_value = vec4(brush_clr);
    for (int y = lo.y; y <= hi.y; ++y) {
        for (int x = lo.x; x <= hi.x; ++x) {
            ivec2 bleed_position = ivec2(x, y);
            if (imageLoad(output_image, bleed_position).r >= brush_clr) continue;
            imageStore(output_image, bleed_position, brush_value);
        }
    }
}
