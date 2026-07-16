#include "Cam.h"
#include "godot_cpp/classes/accessibility_server.hpp"
#include "godot_cpp/classes/compositor.hpp"
#include "godot_cpp/classes/compositor_effect.hpp"
#include "godot_cpp/classes/engine.hpp"
#include "godot_cpp/classes/input.hpp"
#include "godot_cpp/classes/mesh_instance3d.hpp"
#include "godot_cpp/classes/ref.hpp"
#include "godot_cpp/classes/rendering_server.hpp"
#include "godot_cpp/classes/scene_tree.hpp"
#include "godot_cpp/classes/shader_material.hpp"
#include "godot_cpp/classes/texture2d.hpp"
#include "godot_cpp/classes/texture2drd.hpp"
#include "godot_cpp/classes/texture_rect.hpp"
#include "godot_cpp/classes/viewport.hpp"
#include "godot_cpp/core/memory.hpp"
#include "godot_cpp/variant/string_name.hpp"
#include "godot_cpp/variant/utility_functions.hpp"
#include <cstdio>
#include <godot_cpp/core/class_db.hpp>
#include <string>

using namespace godot;

void Cam::_bind_methods() {
  ClassDB::bind_method(D_METHOD("OnQuitPressed"), &Cam::OnQuitPressed);
  ClassDB::bind_method(D_METHOD("OnRdy"), &Cam::OnRdy);
}

Cam::Cam() { std::printf("bjr je suis la cam"); }

Cam::~Cam() {}

void Cam::_ready() {
  button = get_node<Button>("%Button");
  label = get_node<Label>("%Label");
  cam = get_node<Camera3D>("%Camera3D");
  meshInstance = get_node<MeshInstance3D>("%MeshInstance3D");
  texture = get_node<TextureRect>("%TextureRect");
  set_physics_process(false);

  const Ref<Compositor> comp = cam->get_compositor();
  for (Ref<CompositorEffect> ce : comp->get_compositor_effects()) {
    if (ce->is_class("PainterCpp")) {
      pe = ce;
      break;
    }
  }

  if (pe == nullptr)
    UtilityFunctions::push_error("painterCpp is null");
  pe->connect("OnRdy", Callable(this, "OnRdy"));
  RenderingServer::get_singleton()->call_on_render_thread(
      Callable(*pe, "InitCompute"));
  cam->set_cull_mask(1 << 20);
  meshInstance->set_layer_mask(meshInstance->get_layer_mask() | 1 << 20);
  button->connect("pressed", Callable(this, "OnQuitPressed"));
}

void Cam::_physics_process(double delta) {
  if (Input::get_singleton()->is_action_pressed("right")) {
    meshInstance->rotate_y(1.7f * delta);
  } else if (Input::get_singleton()->is_action_pressed("left")) {
    meshInstance->rotate_y(-1.7f * delta);
  }
  if (Input::get_singleton()->is_action_pressed("up")) {
    meshInstance->rotate_x(1.7f * delta);
  } else if (Input::get_singleton()->is_action_pressed("down")) {
    meshInstance->rotate_x(-1.7f * delta);
  }
  const Viewport *v = get_viewport();
  cam->look_at(project_position(v->get_mouse_position(), 5.0f));
  std::string s =
      std::to_string(Engine::get_singleton()->get_frames_per_second());
  label->set_text(s.c_str());
}

void Cam::OnRdy(RID r) {
  UtilityFunctions::print("roger");
  Ref<Texture2DRD> t = memnew(Texture2DRD);
  t->set_texture_rd_rid(r);

  Ref<ShaderMaterial> sm = meshInstance->get_material_overlay();
  sm->set_shader_parameter("paintedTexture", t);
  set_physics_process(true);
}

void Cam::OnQuitPressed() {
  propagate_notification(NOTIFICATION_WM_CLOSE_REQUEST);
  get_tree()->quit();
}
