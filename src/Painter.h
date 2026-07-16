#pragma once

#include "godot_cpp/classes/compositor_effect.hpp"
#include "godot_cpp/classes/render_data.hpp"
#include "godot_cpp/classes/rendering_device.hpp"
#include "godot_cpp/classes/wrapped.hpp"
#include "godot_cpp/variant/rid.hpp"
#include "godot_cpp/variant/vector2i.hpp"

namespace godot {
class PainterCpp : public CompositorEffect {
  GDCLASS(PainterCpp, CompositorEffect);

private:
  const String BRUSHPATH = "res://eponge.png";

  const String SHDERPATH = "uid://ctife0wj6cnkt";

  RenderingDevice *rd = nullptr;
  RID pipeline;
  RID shader_rid;
  RID frameBufferRid;
  Vector2i screen_size;

  RID textureRid;
  RID textureUniformSet;
  RID brushTextureRid;
  RID brushTextureUniformSet;
  RID samplerRid;

protected:
  static void _bind_methods();

public:
  void InitCompute();
  void _render_callback(int32_t p_effect_callback_type,
                        RenderData *p_render_data) override;
  void _notification(int p_what);

  PainterCpp();
  ~PainterCpp();
};
} // namespace godot
