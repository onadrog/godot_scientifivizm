#pragma once

#include "Painter.h"
#include <godot_cpp/classes/camera3d.hpp>
#include <godot_cpp/classes/mesh_instance3d.hpp>
#include <godot_cpp/classes/texture_rect.hpp>
#include <godot_cpp/classes/label.hpp>
#include <godot_cpp/classes/button.hpp>

namespace godot {

	class Cam : public Camera3D {
		GDCLASS(Cam, Camera3D);

            private:
                MeshInstance3D *meshInstance = nullptr;
                Ref<PainterCpp> pe = nullptr;
                TextureRect *texture = nullptr;
                Camera3D *cam = nullptr;
                Label *label = nullptr;
                Button *button = nullptr;
                void OnRdy(RID r);
                void OnQuitPressed();

		protected:
			static void _bind_methods();

		public:
			Cam();
			~Cam();
                       
            void _ready() override;
            void _physics_process(double delta) override;
	};
}
