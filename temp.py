import bpy
import tempfile
import zipfile
from bpy.types import UIList

class BSP_OP_ToggleTexturePreview(bpy.types.Operator):
    bl_idname = "bsp.select_preview"
    bl_label = "Preview"
    bl_description = "Preview"
    bl_options = {'REGISTER'}
        
    def execute(self, context):
        sc = context.scene

        sc.texture_preview.image = sc.texture_group[sc.texture_selected].image
        return {'FINISHED'}
    @classmethod
    def poll(cls, context):
        return True

def current_updated(scene, _context):
    sc = bpy.context.scene
    scene.texture_preview.image = scene.texture_group[scene.texture_selected].image

class BSP_UL_texslots(UIList):

    def draw_item(self, _context, layout, _data, item, icon, _active_data, _active_propname, _index):
        # assert(isinstance(item, bpy.types.MaterialSlot)
        # ob = data
        slot = item
        ma = slot.image

#        layout.context_pointer_set("asd", ma)
        # print(type(layout))
        # print(dir(layout))
        if ma:
            icon = ma.preview.icon_id


        if self.layout_type in {'DEFAULT', 'COMPACT'}:
            if ma:
                layout.prop(ma, "name", text="", emboss=False, icon_value=icon)
            else:
                layout.label(text="", icon_value=icon)
        elif self.layout_type == 'GRID':
            if ma:
                layout.alignment = 'CENTER'
                col = layout.column()
                col.label(text="", icon_value=icon)
                col.prop(ma, "name", text="", emboss=False)
            else:
                layout.label(text="", icon_value=icon)
                
    def filter_items(self, context, scene, propname):
#        vgroups = getattr(data, propname)
        helper_funcs = bpy.types.UI_UL_list
        flt_flags = [self.bitflag_filter_item if self.filter_name == "" or i.name.startswith(self.filter_name) else 0 for i in scene.texture_group]
        flt_neworder = list(range(len(scene.texture_group)))

        return flt_flags, flt_neworder

class SetupDirectory(bpy.types.Operator):
    bl_idname = "bsp.setup"
    bl_label = "Select Executable"
    bl_description = "Select quake executable location"
    bl_options = {'REGISTER'}
    
    filepath: bpy.props.StringProperty(
        name="File Path", 
        description="Filepath used for importing txt files",
        maxlen= 1024,
        default= ""
        )
#    files: bpy.props.CollectionProperty(
#        name="File Path",
#        type=bpy.types.OperatorFileListElement,
#        )
        
    def execute(self, context):
        bpy.context.scene["MyString"] = self.properties.filepath

        tg0 = bpy.context.scene.texture_groups.add()
        tg0.name = "vislo"
        tg1 = bpy.context.scene.texture_groups.add()
        tg1.name = "posla"

#        print("*************SELECTED FILES ***********")
#        for file in self.files:
#            print(file.name)

        print("Selected file is %s"%self.properties.filepath)#display the file name and current path        
        return {'FINISHED'}
    
    def draw(self, context):
        self.layout.operator('file.select_all')        
    def invoke(self, context, event):
        wm = context.window_manager
        wm.fileselect_add(self)
        return {'RUNNING_MODAL'}


class SelectTextureGroup(bpy.types.Operator):
    bl_idname = "bsp.select_texturegroup"
    bl_label = "Select TextureGroup"
    bl_description = "Select"
    bl_options = {'REGISTER'}
    
#    files: bpy.props.CollectionProperty(
#        name="File Path",
#        type=bpy.types.OperatorFileListElement,
#        )
        
    def execute(self, context):
        sc = bpy.context.scene
        
#        if len(sc.texture_group) == 0:
        t0 = sc.texture_group.add()
        t1 = sc.texture_group.add()
        t0.image = bpy.data.images["tx0"]
        t0.name = "tx0"
        t1.image = bpy.data.images["tx1"]
        t1.name = "tx1"


#        print("*************SELECTED FILES ***********")
#        for file in self.files:
#            print(file.name)

        print("ADDED!")#display the file name and current path        
        return {'FINISHED'}
    
class BspPanel:
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "texture"
    
class BspPanelOld:
    bl_space_type = "VIEW_3D"
    bl_region_type = "UI"
    bl_label = "BspTools"
    bl_category = "MapPanel"
    bl_context = "texture"
    
class MapPanel(bpy.types.Panel):
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "scene"
    bl_label = "BSP"
    
#    image: bpy.props.PointerProperty(type=bpy.types.Image)
    texture: bpy.props.PointerProperty(type=bpy.types.Texture)
    @classmethod
    def poll(cls, context):
        return True
    
    def draw(self, context):
        layout = self.layout
        
        row = layout.row()
        row.operator(SetupDirectory.bl_idname, text="Setup")
        sc = context.scene
        
        #layout.prop_enum(sc, "texture_groups", text="Collection")
        layout.operator(SelectTextureGroup.bl_idname, text="Test")
        
        # col.template_image(self.texture, "image", self.texture.image_user, compact=False, multiview=True)
        rows = 8
        layout.template_list("BSP_UL_texslots", "", sc, "texture_group", sc, "texture_selected", rows=rows)
        layout.operator(BSP_OP_ToggleTexturePreview.bl_idname, text="Preview")
#        img = bpy.data.textures['ttx0']
#        col.
        #self.texture.image = sc.texture_group[sc.texture_selected]
        layout.template_preview(sc.texture_preview, show_buttons=True)
        #if sc.texture_selected < len(sc.texture_group):
        #    bpy.data.textures['.bspPreview'].image = sc.texture_group[sc.texture_selected].image
        #    layout.template_preview(sc.texture_preview)
#        row.prop(

class ImageProperty(bpy.types.PropertyGroup):
    name: bpy.props.StringProperty()
    image: bpy.props.PointerProperty(type=bpy.types.Image)
    
class TextruesGroup(bpy.types.PropertyGroup):
    name: bpy.props.StringProperty() 
    textures:bpy.props.CollectionProperty(type=ImageProperty)

classes = (MapPanel,
    SetupDirectory,
    BSP_UL_texslots,
    BSP_OP_ToggleTexturePreview,
    ImageProperty,
    TextruesGroup,
    SelectTextureGroup,)
def register():
    for cls in classes:
        bpy.utils.register_class(cls)
        
#    setattr(bpy.types.Scene, "texture_groups", bpy.props.CollectionProperty(type=bpy.types.Image))
    setattr(bpy.types.Scene, "texture_groups", bpy.props.CollectionProperty(type=TextruesGroup))
    setattr(bpy.types.Scene, "texture_group", bpy.props.CollectionProperty(type=ImageProperty))
    setattr(bpy.types.Scene, "texture_selected", bpy.props.IntProperty(update=current_updated))
    setattr(bpy.types.Scene, "texture_preview", bpy.props.PointerProperty(type=bpy.types.Texture))
    if ".bspPreview" not in bpy.data.textures:
        bpy.context.scene.texture_preview = bpy.data.textures.new(name=".bspPreview", type="IMAGE")
        print("AAAAAAAAAAAAAAAAAAAAA")
    else:
        bpy.context.scene.texture_preview = bpy.data.textures['.bspPreview']
        print("BBBBBBBBBBBBBBBBBBBBB")
    

def unregister():
    for cls in classes:
        clst = cls.__bases__[0].bl_rna_get_subclass_py(cls.__name__)
        if clst:
            bpy.utils.unregister_class(clst)
            
#    if "texture_groups" in dir(bpy.types.Scene):
#        delattr(bpy.types.Scene, "texture_groups")
#        #bpy.utils.unregister_class(bpy.types.Panel.bl_rna_get_subclass_py('MapPanel'))
    if "texture_groups" in dir(bpy.types.Scene):
        delattr(bpy.types.Scene, "texture_groups") 
    if "texture_group" in dir(bpy.types.Scene):
        delattr(bpy.types.Scene, "texture_group") 
    if "texture_selected" in dir(bpy.types.Scene):
        delattr(bpy.types.Scene, "texture_selected") 
    if "texture_preview" in dir(bpy.types.Scene):
        delattr(bpy.types.Scene, "texture_preview") 

if __name__ == "__main__":
    unregister()
    register()


entity_params = {
    "Ammo": {},
    "Func": {},
    "Holdable": {},
    "Info": {},
    "Item": {},
    "Misc": {},
    "Shooter": {},
    "Target": {},
    "Team": {},
    "Trigger": {},
    "Weapon": {},
}

entities = {
    'Ammo': {
        "BFG": entity_params["Ammo"],
        "Bullets": entity_params["Ammo"],
        "Cells": entity_params["Ammo"],
        "Grenades": entity_params["Ammo"],
        "Lightning": entity_params["Ammo"],
        "Rockets": entity_params["Ammo"],
        "Shells": entity_params["Ammo"],
        "Slugs": entity_params["Ammo"],
    },
    'Func': {
        "Bobbing": entity_params["Func"],
        "Button": entity_params["Func"],
        "Door": entity_params["Func"],
        "Group": entity_params["Func"],
        "Pendulum": entity_params["Func"],
        "Plat": entity_params["Func"],
        "Rotating": entity_params["Func"],
        "Static": entity_params["Func"],
        "Timer": entity_params["Func"],
        "Train": entity_params["Func"],
    },
    'Holdable': {
        "Medkit": entity_params["Holdable"],
        "Teleporter": entity_params["Holdable"],
    },
    'Info': {
        "Camp": entity_params["Info"],
        "Null": entity_params["Info"],
        "NotNull": entity_params["Info"],
        "Player Deathmatch": entity_params["Info"],
        "Player Intermission": entity_params["Info"],
        "Player Start": entity_params["Info"],
    },
    'Item': {
        "Armor Body": entity_params["Item"],
        "Armor Combat": entity_params["Item"],
        "Armor Shard": entity_params["Item"],
        "BotRoam": entity_params["Item"],
        "Enviro": entity_params["Item"],
        "Flight": entity_params["Item"],
        "Haste": entity_params["Item"],
        "Health": entity_params["Item"],
        "Health Large": entity_params["Item"],
        "Health Mega": entity_params["Item"],
        "Health Small": entity_params["Item"],
        "Invis": entity_params["Item"],
        "Quad": entity_params["Item"],
        "Regen": entity_params["Item"],
    },
    'Misc': {
        "Model": entity_params["Misc"],
        "Portal Camera": entity_params["Misc"],
        "Portal Surface": entity_params["Misc"],
        "Teleporter Dest": entity_params["Misc"],
    },
    'Path': {
        "Corner": {},
    },
    'Shooter': {
        "Grenade": entity_params["Shooter"],
        "Grenade TargetPlayer": entity_params["Shooter"],
        "Rocket": entity_params["Shooter"],
        "Rocket TargetPlayer": entity_params["Shooter"],
        "Plasma": entity_params["Shooter"],
        "Plasma TargetPlayer": entity_params["Shooter"],
    },
    'Target': {
        "Checkpoint": entity_params["Target"],
        "Delay": entity_params["Target"],
        "FragsFilter": entity_params["Target"],
        "Give": entity_params["Target"],
        "Init": entity_params["Target"],
        "Kill": entity_params["Target"],
        "Location": entity_params["Target"],
        "Multimanager": entity_params["Target"],
        "Position": entity_params["Target"],
        "Print": entity_params["Target"],
        "Push": entity_params["Target"],
        "Relay": entity_params["Target"],
        "Remove Powerups": entity_params["Target"],
        "Score": entity_params["Target"],
        "SmallPrint": entity_params["Target"],
        "Speaker": entity_params["Target"],
        "Speed": entity_params["Target"],
        "StartTimer": entity_params["Target"],
        "StopTimer": entity_params["Target"],
        "Teleporter": entity_params["Target"],
    },
    'Team': {
        "CTF BlueFlag": entity_params["Team"],
        "CTF BluePlayer": entity_params["Team"],
        "CTF BlueSpawn": entity_params["Team"],
        "CTF RedFlag": entity_params["Team"],
        "CTF RedPlayer": entity_params["Team"],
        "CTF RedSpawn": entity_params["Team"],
    },
    'Trigger': {
        "Always": entity_params["Trigger"],
        "Hurt": entity_params["Trigger"],
        "Multiple": entity_params["Trigger"],
        "Push": entity_params["Trigger"],
        "Push Velocity": entity_params["Trigger"],
        "Teleport": entity_params["Trigger"],
    },
    'Weapon': {
        "BFG": entity_params["Weapon"],
        "Gauntlet": entity_params["Weapon"],
        "GrapplingHook": entity_params["Weapon"],
        "GrenadeLauncher": entity_params["Weapon"],
        "Lighting": entity_params["Weapon"],
        "MachineGun": entity_params["Weapon"],
        "PlasmaGun": entity_params["Weapon"],
        "RailGun": entity_params["Weapon"],
        "RocketLauncher": entity_params["Weapon"],
        "Shotgun": entity_params["Weapon"],
    },
}
