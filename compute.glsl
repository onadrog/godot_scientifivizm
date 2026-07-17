#[compute]
#version 460

#define BLEED 2

#define CAM_POS_Z 3.0f

layout(local_size_x = 16, local_size_y = 16) in;

layout(set = 0, binding = 0, rgba16f) restrict readonly uniform image2D framebuffer;

layout(set = 1, binding = 0, r8) restrict uniform image2D output_image;

layout(set = 2, binding = 0) uniform sampler2D brush_texture;

void main(void) {
    ivec2 id = ivec2(gl_GlobalInvocationID.xy);

    ivec2 fb_size = imageSize(framebuffer);
    if (id.x >= fb_size.x || id.y >= fb_size.y) return;

    vec4 clr = imageLoad(framebuffer, id);
    clr.xy = clr.xy + vec2(0.5f);

    float dist = CAM_POS_Z - abs(clr.z);

    if (dist < 0.001f) return;

    vec2 brush_uv = (vec2(id) + vec2(0.5f)) / vec2(fb_size);
    float brush_clr = texture(brush_texture, brush_uv).r * clamp(dist, 0.0f, 1.0f);

    if (brush_clr == 0.0f) return;

    ivec2 out_size = imageSize(output_image);
    ivec2 p = ivec2(clr.xy * vec2(out_size));
    for (int y = -BLEED; y < BLEED; ++y) {
        for (int x = -BLEED; x < BLEED; ++x) {
            ivec2 bleed_position = p + ivec2(x, y);
            if (bleed_position.x < 0 || bleed_position.y < 0 || bleed_position.x >= out_size.x || bleed_position.y >= out_size.y) continue;
            if (imageLoad(output_image, bleed_position).r > brush_clr) continue;
            imageStore(output_image, bleed_position, vec4(brush_clr));
        }
    }
}
