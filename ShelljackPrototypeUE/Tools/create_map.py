import unreal

asset_tools = unreal.AssetToolsHelpers.get_asset_tools()
path = "/Game/Prototype"
material_path = path + "/M_Prototype"
if not unreal.EditorAssetLibrary.does_asset_exist(material_path):
    mat = asset_tools.create_asset("M_Prototype", path, unreal.Material, unreal.MaterialFactoryNew())
    color = unreal.MaterialEditingLibrary.create_material_expression(mat, unreal.MaterialExpressionVectorParameter, -300, 0)
    color.set_editor_property("parameter_name", "Tint")
    color.set_editor_property("default_value", unreal.LinearColor(0.5, 0.6, 0.3, 1))
    unreal.MaterialEditingLibrary.connect_material_property(color, "", unreal.MaterialProperty.MP_BASE_COLOR)
    rough = unreal.MaterialEditingLibrary.create_material_expression(mat, unreal.MaterialExpressionConstant, -300, 180)
    rough.set_editor_property("r", 0.85)
    unreal.MaterialEditingLibrary.connect_material_property(rough, "", unreal.MaterialProperty.MP_ROUGHNESS)
    unreal.MaterialEditingLibrary.recompile_material(mat)
    unreal.EditorAssetLibrary.save_loaded_asset(mat)

level = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
actors = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
if not unreal.EditorAssetLibrary.does_asset_exist(path + "/Island"):
    level.new_level(path + "/Island")
else:
    level.load_level(path + "/Island")
    for actor in actors.get_all_level_actors():
        if actor.get_actor_label().startswith("Shell_"):
            actors.destroy_actor(actor)

sun = actors.spawn_actor_from_class(unreal.DirectionalLight, unreal.Vector(0, 0, 1800), unreal.Rotator(-45, -35, 0))
sun.set_actor_label("Shell_Sun")
sun.light_component.set_editor_property("intensity", 3.0)
sun.light_component.set_editor_property("mobility", unreal.ComponentMobility.MOVABLE)
sky = actors.spawn_actor_from_class(unreal.SkyLight, unreal.Vector(0, 0, 1800))
sky.set_actor_label("Shell_Skylight")
sky.light_component.set_editor_property("mobility", unreal.ComponentMobility.MOVABLE)
sky.light_component.set_editor_property("intensity", 1.0)
sky.light_component.set_editor_property("real_time_capture", True)
atmosphere = actors.spawn_actor_from_class(unreal.SkyAtmosphere, unreal.Vector(0, 0, -300))
atmosphere.set_actor_label("Shell_Atmosphere")
fog = actors.spawn_actor_from_class(unreal.ExponentialHeightFog, unreal.Vector(0, 0, -300))
fog.set_actor_label("Shell_Fog")
fog.get_component_by_class(unreal.ExponentialHeightFogComponent).set_editor_property("fog_density", 0.006)
sea = actors.spawn_actor_from_class(unreal.StaticMeshActor, unreal.Vector(0, 0, -110))
sea.set_actor_label("Shell_Sea_Backdrop")
sea.static_mesh_component.set_static_mesh(unreal.load_asset("/Engine/BasicShapes/Cube"))
sea.set_actor_scale3d(unreal.Vector(1000, 1000, 0.1))
sea.static_mesh_component.set_collision_enabled(unreal.CollisionEnabled.NO_COLLISION)
mi_path = path + "/MI_Sea"
mi = unreal.load_asset(mi_path)
if not mi:
    mi = asset_tools.create_asset("MI_Sea", path, unreal.MaterialInstanceConstant, unreal.MaterialInstanceConstantFactoryNew())
unreal.MaterialEditingLibrary.set_material_instance_parent(mi, unreal.load_asset(material_path))
unreal.MaterialEditingLibrary.set_material_instance_vector_parameter_value(mi, "Tint", unreal.LinearColor(0.035, 0.24, 0.30, 1))
unreal.EditorAssetLibrary.save_loaded_asset(mi)
sea.static_mesh_component.set_material(0, mi)
level.save_current_level()
unreal.log("SHELLJACK_MAP_READY")
