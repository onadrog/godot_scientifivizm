#include "Painter.h"
#include "godot_cpp/classes/image.hpp"
#include "godot_cpp/classes/rd_sampler_state.hpp"
#include "godot_cpp/classes/rd_shader_file.hpp"
#include "godot_cpp/classes/rd_texture_format.hpp"
#include "godot_cpp/classes/rd_texture_view.hpp"
#include "godot_cpp/classes/rd_uniform.hpp"
#include "godot_cpp/classes/ref.hpp"
#include "godot_cpp/classes/render_scene_buffers_rd.hpp"
#include "godot_cpp/classes/rendering_server.hpp"
#include "godot_cpp/classes/resource_loader.hpp"
#include "godot_cpp/classes/uniform_set_cache_rd.hpp"
#include "godot_cpp/core/memory.hpp"
#include "godot_cpp/variant/array.hpp"
#include "godot_cpp/variant/packed_byte_array.hpp"
#include "godot_cpp/variant/typed_array.hpp"
#include "godot_cpp/variant/utility_functions.hpp"
#include <cmath>
using namespace godot;

void PainterCpp::_bind_methods() {
  ClassDB::bind_method(D_METHOD("InitCompute"), &PainterCpp::InitCompute);
  ADD_SIGNAL(MethodInfo("OnRdy", PropertyInfo(Variant::RID, "r")));
}

PainterCpp::PainterCpp() { set_enabled(false); }

PainterCpp::~PainterCpp() {}

void PainterCpp::InitCompute() {
  UtilityFunctions::print("InitCompute");
  rd = RenderingServer::get_singleton()->get_rendering_device();
  if (rd == nullptr)
    UtilityFunctions::push_error("rd = null");
  Ref<RDShaderFile> f = ResourceLoader::get_singleton()->load(SHDERPATH);
  shader_rid = rd->shader_create_from_spirv(f->get_spirv());
  if (!shader_rid.is_valid())
    UtilityFunctions::push_error("shader = null");

  Ref<Image> img =
      Image::create_empty(1024, 1024, false, Image::Format::FORMAT_R8);

  Ref<RDTextureFormat> image_format = memnew(RDTextureFormat);
  image_format->set_format(RenderingDevice::DataFormat::DATA_FORMAT_R8_UNORM);
  image_format->set_height(1024);
  image_format->set_width(1024);
  image_format->set_usage_bits(
      RenderingDevice::TextureUsageBits::TEXTURE_USAGE_CAN_UPDATE_BIT |
      RenderingDevice::TextureUsageBits::TEXTURE_USAGE_STORAGE_BIT |
      RenderingDevice::TextureUsageBits::TEXTURE_USAGE_SAMPLING_BIT);

  TypedArray<PackedByteArray> tmp_img;
  tmp_img.append(img->get_data());

  Ref<RDTextureView> view = memnew(RDTextureView);
  textureRid = rd->texture_create(image_format, view, tmp_img);

  Ref<RDUniform> image_uniform = memnew(RDUniform);
  image_uniform->set_binding(0);
  image_uniform->set_uniform_type(
      RenderingDevice::UniformType::UNIFORM_TYPE_IMAGE);
  image_uniform->add_id(textureRid);

  TypedArray<Ref<RDUniform>> tmp_img_uniform;
  tmp_img_uniform.append(image_uniform);
  textureUniformSet = rd->uniform_set_create(tmp_img_uniform, shader_rid, 1);

  if (!textureUniformSet.is_valid())
    UtilityFunctions::push_error("textureUniformSet = null");

  // -------------------- brush --------------------- //

  Ref<Image> brush = ResourceLoader::get_singleton()->load(BRUSHPATH);
  brush->clear_mipmaps();

  if (brush->get_format() != Image::FORMAT_R8) {
    brush->convert(Image::FORMAT_R8);
  }

  Ref<RDTextureFormat> brush_texture_format = memnew(RDTextureFormat);
  brush_texture_format->set_height(brush->get_height());
  brush_texture_format->set_width(brush->get_width());
  brush_texture_format->set_format(RenderingDevice::DATA_FORMAT_R8_UNORM);
  brush_texture_format->set_usage_bits(
      RenderingDevice::TextureUsageBits::TEXTURE_USAGE_SAMPLING_BIT);

  TypedArray<PackedByteArray> tmp_brush;
  tmp_brush.append(brush->get_data());

  brushTextureRid = rd->texture_create(brush_texture_format, view, tmp_brush);

  Ref<RDSamplerState> sampler_state = memnew(RDSamplerState);
  sampler_state->set_mag_filter(
      RenderingDevice::SamplerFilter::SAMPLER_FILTER_LINEAR);
  sampler_state->set_min_filter(
      RenderingDevice::SamplerFilter::SAMPLER_FILTER_LINEAR);
  sampler_state->set_repeat_u(
      RenderingDevice::SamplerRepeatMode::SAMPLER_REPEAT_MODE_CLAMP_TO_EDGE);
  sampler_state->set_repeat_v(
      RenderingDevice::SamplerRepeatMode::SAMPLER_REPEAT_MODE_CLAMP_TO_EDGE);

  samplerRid = rd->sampler_create(sampler_state);

  Ref<RDUniform> brush_uniform = memnew(RDUniform);
  brush_uniform->set_binding(0);
  brush_uniform->set_uniform_type(
      RenderingDevice::UNIFORM_TYPE_SAMPLER_WITH_TEXTURE);
  brush_uniform->add_id(samplerRid);
  brush_uniform->add_id(brushTextureRid);

  TypedArray<Ref<RDUniform>> tmp_brush_uniform;
  tmp_brush_uniform.append(brush_uniform);
  brushTextureUniformSet =
      rd->uniform_set_create(tmp_brush_uniform, shader_rid, 2);

  if (!brushTextureUniformSet.is_valid()) {
    UtilityFunctions::print("brush texture uniform null");
  }

  pipeline = rd->compute_pipeline_create(shader_rid);

  if (!pipeline.is_valid())
    UtilityFunctions::print("pipeline null");

  emit_signal("OnRdy", textureRid);
  set_enabled(true);
}

void PainterCpp::_render_callback(int32_t p_effect_callback_type,
                                  RenderData *p_render_data) {

  Ref<RenderSceneBuffersRD> rsb = p_render_data->get_render_scene_buffers();
  screen_size = rsb->get_internal_size();

  if (screen_size.x == 0 || screen_size.y == 0)
    return;

  frameBufferRid = rsb->get_color_texture();

  Ref<RDUniform> frameBufferUniform = memnew(RDUniform);
  frameBufferUniform->set_binding(0);
  frameBufferUniform->set_uniform_type(
      RenderingDevice::UniformType::UNIFORM_TYPE_IMAGE);
  frameBufferUniform->add_id(frameBufferRid);

  TypedArray<Ref<RDUniform>> tmp_frame;
  tmp_frame.append(frameBufferUniform);
  RID frameBufferUniformSet =
      UniformSetCacheRD::get_cache(shader_rid, 0, tmp_frame);

  long compute_list = rd->compute_list_begin();
  rd->compute_list_bind_uniform_set(compute_list, frameBufferUniformSet, 0);
  rd->compute_list_bind_uniform_set(compute_list, textureUniformSet, 1);
  rd->compute_list_bind_uniform_set(compute_list, brushTextureUniformSet, 2);
  rd->compute_list_bind_compute_pipeline(compute_list, pipeline);
  rd->compute_list_dispatch(compute_list, std::ceil(screen_size.x / 8.f),
                            std::ceil(screen_size.y / 8.f), 1);
  rd->compute_list_end();
}

void PainterCpp::_notification(int p_what) {
  if (p_what == NOTIFICATION_PREDELETE && rd != nullptr) {
    UtilityFunctions::print("brruh");

    if (textureRid.is_valid())
      rd->free_rid(textureRid);

    if (brushTextureRid.is_valid())
      rd->free_rid(brushTextureRid);

    if (samplerRid.is_valid())
      rd->free_rid(samplerRid);

    if (shader_rid.is_valid())
      rd->free_rid(shader_rid);
  }
}
