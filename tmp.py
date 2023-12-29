import bpy
import math
import mathutils
import os
import subprocess as sp
import bmesh
import numpy as np
from bpy_extras.io_utils import ExportHelper, ImportHelper
from bpy.props import StringProperty, BoolProperty, EnumProperty, IntProperty
from bpy.types import Operator

GLOBAL_SCALE=64

def get_linked_sets(obj):
    vtof = {}
    ftov = {}
    
    for p in obj.data.polygons:
        ftov[p.index] = list([x for x in p.vertices])
        for v in p.vertices:
            if v in vtof:
                vtof[v].append(p.index)
            else:
                vtof[v] = [p.index]
                
    marked = set()
    subsets = []
    for p in obj.data.polygons:
        if p.index in marked:
            continue
        
        subs = set()
        subs_polys = set([p.index])
        while len(subs_polys) > 0:
            pp = subs_polys.pop()
            subs.add(pp)
            marked.add(pp)
            for vv in ftov[pp]:
                for ppp in vtof[vv]:
                    if ppp in marked:
                        continue
                    subs_polys.add(ppp)
                    
        subsets.append(subs)
        
    return subsets


class bsp_extensions:
    @staticmethod
    def read_face(bsp, face):
        return {
            "texture": bsp.textures[face.texture],
            "effect": None if face.effect < 0 else bsp.effects[face.effect],
            "type": face.type,
            "verts": bsp.vertexs[face.vertex:face.vertex+face.n_vertex],
            "inds": bsp.meshverts[face.meshvert:face.meshvert+face.n_meshvert],
            "lm_index": face.lm_index,
            "normal": face.normal,
            "size": face.vsize,
        }

    @staticmethod
    def read_model(bsp, model_index):
        model = bsp.models[model_index]
        return {
            "min": model.mins,
            "max": model.maxs,
            "faces": [bsp_extensions.read_face(bsp, bsp.faces[x]) for x in range(model.face,model.face+model.n_face)],
            "brushs": bsp.brushs[model.brush:model.brush+model.n_brush],
        }

        return verts, faces, lm_images

    @staticmethod
    def read_models(bsp):
        return [bsp_extensions.read_model(bsp, i) for i in range(len(bsp.models))]
        
    @staticmethod
    def write_models(bsp, models):
        def dict_setdefault(d, lst, val, key):
            if val is None: return -1
            ind = d.setdefault(key(val), len(lst))
            if ind >= len(lst):
                lst.append(val)
            return ind
        verts = []
        meshverts = []
        faces = []
        brushs = []
        resmodels = []
        leaffaces = []
        leafbrushs = []
        textures = []
        textures_dict = {}
        effects = []
        effects_dict = {}
        leafs = []

        texkey = lambda x: (x.name, x.flags, x.contents)
        fxkey = lambda x: (x.name, x.brush, x.unknown)
        brushkey = lambda x: (x.brushside, x.n_brushside, x.texture)
        # [TODO] probably better to illiminate unused ones
        textures_remap = {i: dict_setdefault(textures_dict, textures, v, texkey) for i, v in enumerate(bsp.textures)}
        for v in enumerate(bsp.effects):
            dict_setdefault(effects_dict, effects, v, fxkey)

        # fix indices
        for b in bsp.brushs:
            b.texture = textures_remap[b.texture]
        for b in bsp.brushsides: b.texture = textures_remap[b.texture]

        planes = [mathutils.Vector((*x.normal, x.dist)) for x in bsp.planes]
        for i, model in enumerate(models):
            faces_co = []
            resmodel = bsp_lib.model()
            resmodel.mins = model["min"]
            resmodel.maxs = model["max"]
            resmodel.face = len(faces)
            resmodel.n_face = len(model["faces"])
            for face in model["faces"]:
                vx = face["verts"]
                mvx = face["inds"]
                resface = bsp_lib.face()
                resface.type = face["type"]
                resface.texture = dict_setdefault(textures_dict, textures, face["texture"], texkey)
                resface.effect = dict_setdefault(effects_dict, effects, face["effect"], fxkey)
                resface.vertex = len(verts)
                resface.n_vertex = len(vx)
                resface.meshvert = len(meshverts)
                resface.n_meshvert = len(mvx)
                resface.lm_index = face["lm_index"]
                resface.normal = face["normal"]
                resface.wsize = face["size"]
                verts += vx
                meshverts += mvx
                faces.append(resface)
                faces_co.append([mathutils.Vector((*x.co, -1.0)) for x in vx])
            brushs_dict = {}
            resmodel.brush = len(brushs)
            if i == 0:
                leafs.append(bsp_lib.leaf()) # add empty model leaf
                for v in model["brushs"]:
                    dict_setdefault(brushs_dict, brushs, v, brushkey)
                # print(brushs_remap)
                resmodel.n_brush = len(brushs)
                leaf_faces = {}
                bsp_lib.classify_faces(bsp, 0, planes, faces_co, list(range(len(faces_co))), leaf_faces)
                mn = min(leaf_faces)
                mx = max(leaf_faces)
                for leaf_idx in range(mn, mx+1):
                    curfaces = leaf_faces.get(leaf_idx, [])
                    leaf = bsp_lib.leaf()
                    oldleaf = bsp.leafs[leaf_idx]
                    leaf.cluster = oldleaf.cluster
                    leaf.area = oldleaf.area
                    leaf.mins = oldleaf.mins
                    leaf.maxs = oldleaf.maxs
                    curbrushs = []
                    for x in range(oldleaf.leafbrush, oldleaf.leafbrush + oldleaf.n_leafbrush):
                        b = bsp.brushs[bsp.leafbrushs[x]]
                        if brushkey(b) in brushs_dict:
                            ind = dict_setdefault(brushs_dict, brushs, b, brushkey)
                            if ind >= resmodel.n_brush:
                                print(f"Warning: brush ({bsp.leafbrushs[x]}) was not found: {b}")
                            else:
                                curbrushs.append(ind)
                        else:
                            print(f"Warning: skipping brush {bsp.leafbrushs[x]}")
                    leaf.leafface = len(leaffaces)
                    leaf.n_leafface = len(curfaces)
                    leaf.leafbrush = len(leafbrushs)
                    leaf.n_leafbrush = len(curbrushs)
                    leaffaces += curfaces
                    leafbrushs += curbrushs
                    leafs.append(leaf)
            else:
                brushs += model["brushs"]
                resmodel.n_brush = len(model["brushs"])
                leaf = bsp_lib.leaf()
                curfaces = [x for x in range(resmodel.face, resmodel.face + resmodel.n_face)]
                curbrushs = [x for x in range(resmodel.brush, resmodel.brush + resmodel.n_brush)]
                leaf.leafface = len(leaffaces)
                leaf.n_leafface = len(curfaces)
                leaf.leafbrush = len(leafbrushs)
                leaf.n_leafbrush = len(curbrushs)
                leaffaces += curfaces
                leafbrushs += curbrushs
                leafs.append(leaf)

            resmodels.append(resmodel)
        
        bsp.textures = textures
        bsp.effects = effects
        bsp.brushs = brushs
        bsp.vertexs = verts
        bsp.faces = faces
        bsp.leaffaces = leaffaces
        bsp.leafbrushs = leafbrushs
        bsp.leafs = leafs
        bsp.meshverts = meshverts
        bsp.models = resmodels

    @staticmethod
    def obj_to_face_list(obj, scale):
        m = bmesh.new()
        m.from_mesh(obj.data)
        m.verts.ensure_lookup_table()
        m.faces.ensure_lookup_table()
        lm = m.loops.layers.uv.get("Lightmap")
        col = m.loops.layers.float_color[0] if len(m.loops.layers.float_color) > 0 else None
        if lm is None:
            raise KeyError(f"Unable to find lightmap UV layer ('Lightmap'), got: {m.loops.layers.uv.keys()}")
        
        otheruvs = [x for x in m.loops.layers.uv.keys() if x != 'Lightmap']
        if len(otheruvs) == 0:
            # raise Error(f"Unable to find base UV layer ('Lightmap'), got: {m.loops.layers.uv.keys()}")
            tx = None
        else:
            tx = m.loops.layers.uv.get("UV", m.loops.layers.uv.get("UVMap", m.loops.layers.uv[otheruvs[0]]))
        faces = []
        # obj.bsp_lm_grid
        width = getattr(obj, "bsp_lm_grid", 8)
        faces = []
        # if any((f.material_index not in lm_images for f in m.faces)):
        #     raise KeyError(f"Some materials has no lightmap image defined")
        has_lm = {}
        for i, slot in enumerate(obj.material_slots):
            has_lm[i] = False
            for node in slot.material.node_tree.nodes:
                if node.type != "UVMAP" or node.uv_map != 'Lightmap': continue
                for link in node.outputs['UV'].links:
                    if link.to_node.type == 'TEX_IMAGE':
                        if link.to_node.image:
                            has_lm[i] = True
        for f in m.faces:
            inds = []
            verts = []
            lm_coord = (np.array([x[lm].uv for x in f.loops]).mean(axis=0) * width).astype('int')
            mat = obj.material_slots[f.material_index].material
            if has_lm[f.material_index]:
                lm_index = width * lm_coord[1] + lm_coord[0]
            else:
                lm_index = -1
            for i,v in enumerate(f.verts):
                vert = bsp_lib.vertex()
                vert.co = (*(v.co * scale),) # GLOBAL_SCALE
                vert.uv = (0.0,0.0) if tx is None else (*f.loops[i][tx].uv,)
                vert.lm_uv = tuple((x % 1.0 for x in (f.loops[i][lm].uv * width)))
                vert.normal = (*v.normal, )
                vert.color = (*((np.array((0.,0.,0.,1.) if col is None else f.loops[i][col]) + 1/512) * 255).clip(0, 255).astype('ubyte'), )
                verts.append(vert)
            for ind in range(len(f.verts)-2):
                # triangle fan
                inds += [0, ind+2, ind+1]
            texture = bsp_lib.texture()
            texture.name = getattr(mat, "bsp_texture_name", "common/caulk")
            faces.append({
                "texture": texture,
                "effect": None,
                "type": 1,
                "verts": verts,
                "inds": inds,
                "lm_index": lm_index,
                "normal": (*f.normal,),
                "size": (0, 0),
            })
        return faces
    
def write_obj(bsp, obj):
    image = bpy.data.images.get(f"__map_lightmap_{obj.name}", None)
    models = bsp_extensions.read_models(bsp)
    
    faces = bsp_extensions.obj_to_face_list(obj, GLOBAL_SCALE)
    models[0]["faces"] = faces
    lm_slots = set((f["lm_index"] for f in faces))
    lm_slots.discard(-1)
    width = getattr(obj, "bsp_lm_grid", 8)
    
    bsp_extensions.write_models(bsp, models)

    lightmaps = [bsp_lib.lightmap() for _ in range(max(lm_slots) + 1)]
    if image is not None:
        allm = np.array(image.pixels).reshape((*image.size,4))
        tx = image.size[0] // width
        ty = image.size[1] // width
        for i in lm_slots:
            x = i // width
            y = i % width
            lightmaps[i].map = [*((allm[tx * x:tx * (x+1),ty * y:ty*(y+1),0:3] + 1/512) * 255).clip(0, 255).astype('ubyte').reshape((-1))]
        bsp.lightmaps = lightmaps
        

def r(x):
    return int(x * 100000 * GLOBAL_SCALE) / 100000

def build_parents():
    tindex = 0
    for i in bpy.data.objects:
        if i.type == 'MESH' or i.type == 'EMPTY':
            for j in i.children:
                if j.type == 'MESH' or j.type == 'EMPTY':
                    tag = f"t{tindex}"
                    i["target"] = tag
                    j["targetname"] = tag
            tindex = tindex + 1

def to_brush(obj, default_texture='common/caulk'):
    if 'classname' in obj:
        classname = obj['classname']
    else:
        classname = 'worldspawn'
    
    result = ""
    mt = obj.matrix_world
    
    if classname in ['worldspawn', 'trigger_hurt', 'trigger_multiple', 
        'trigger_push', 'trigger_push_velocity', 'trigger_teleport']:
        result += f'// Entity {obj.name}\n'
        result += '{\n"classname" "'+classname+'"\n'
        for k, v in obj.items():
            if k in ["classname", "origin", "angle"]: continue
            if isinstance(v, str):
                result+=f'"{k}" "{v.strip()}"\n'
        for idx, sub in enumerate(get_linked_sets(obj)):
            result += '{\n'+ f'// Brushsub: {idx}' +'\n'
            for p in sub:
                v0, v1 = [mt @ obj.data.vertices[x].co for x in obj.data.polygons[p].vertices[:2]]
                n = mt.to_3x3() @ obj.data.polygons[p].normal
                v2 = (v1 - v0).cross(n) + v0
                #v0, v1, v2 = [obj.data.vertices[x].co for x in obj.data.polygons[p].vertices[:3]]
                #
                #if (v1 - v0).cross(v2 - v0).dot(n) < 0:
                    
                for v in [v0, v1, v2]:
                    result += f'( {r(v.x)} {r(v.y)} {r(v.z)} ) '
                result += f'{default_texture} '
                result += '0 0 0 0.5 0.5 0 4 0'
                result += '\n'
            result+='}\n'
        result+='}\n'
    else:
        result = '{\n'
        result += f'// Entity {obj.name}\n'
        result += '"classname" "'+classname+'"\n'
        if obj.rotation_euler[2] > 0.001 or obj.rotation_euler[2] < -0.001:
            result+=f'"angle" "{int(obj.rotation_euler[2] / math.pi * 180)}"\n'
            
        l = obj.location
        result+=f'"origin" "{r(l.x)} {r(l.y)} {r(l.z)}"\n'
        
        # custom properties
        for k, v in obj.items():
            if k in ["classname", "origin", "angle"]: continue
            if isinstance(v, str):
                result+=f'"{k}" "{v.strip()}"\n'
        
        result+='}\n'
    return result


def write_some_data(context, filepath, build_map, q3map2, texture):
    print("Exporting map...")
    build_parents()
    rr = ""
    for i in bpy.data.objects:
        if i.type == 'MESH' or i.type == 'EMPTY':
            rr += to_brush(i, texture)
            
    with open(filepath, 'w', encoding='utf-8') as f:
        f.write(rr)

    print("Saved!")
    
    if build_map and os.path.isfile(q3map2):
        print("Building bsp...")
        sp.run([q3map2, filepath], cwd=os.path.dirname(q3map2))
        print("Done!")
    
    return {'FINISHED'}


class ExportToMapFile(Operator, ExportHelper):
    """This appears in the tooltip of the operator and in the generated docs"""
    bl_idname = "export_menu.map_file"  # important since its how bpy.ops.import_test.some_data is constructed
    bl_label = "Export MapFile"

    # ExportHelper mixin class uses this
    filename_ext = ".map"

    filter_glob: StringProperty(
        default="*.map",
        options={'HIDDEN'},
        maxlen=255,  # Max internal buffer length, longer would be clamped.
    )

    # List of operator properties, the attributes will be assigned
    # to the class instance from the operator settings before calling.
    build_map: BoolProperty(
        name="Build",
        description="Build map",
        default=False,
    )
    q3map2_location: StringProperty(
        name="Q3Map2",
        description="Q3Map2 executable path",
        default=""
    )
    default_texture: StringProperty(
        name="Texture",
        description="Default texture of all brushes",
        default="base_floor/concrete"
    )


    def execute(self, context):
        return write_some_data(context, self.filepath, self.build_map, self.q3map2_location, self.default_texture)


bsp_lib = bpy.data.texts["bsp.py"].as_module()


def mat_link(mat, n_out, n_in):
    return mat.node_tree.links.new(n_out, n_in)

def mat_set_input(mat, node, index, value):
    if value is None: return
    if isinstance(value, bpy.types.NodeSocket):
        mat_link(mat, node.inputs[index], value)
        return
    if node.inputs[index].type == 'RGBA':
        if isinstance(value, int) or isinstance(value,float):
            node.inputs[index].default_value = [value, value, value, value]
        elif isinstance(value, list) or isinstance(value, tuple):
            if len(value) == 4:
                node.inputs[index].default_value = value
            else:
                for i, v in enumerate(value):
                    if i > 3: break
                    node.inputs[index].default_value[i] = float(v)
                    
    elif node.inputs[index].type == 'VECTOR':
        if isinstance(value, int) or isinstance(value,float):
            node.inputs[index].default_value = [value, value, value]
        elif isinstance(value, list) or isinstance(value, tuple):
            if len(value) == 3:
                node.inputs[index].default_value = value
            else:
                for i, v in enumerate(value):
                    if i > 2: break
                    node.inputs[index].default_value[i] = v
    elif node.inputs[index].type == 'VALUE':
        if isinstance(value, int) or isinstance(value,float):
            node.inputs[index].default_value = value
        elif isinstance(value, list) or isinstance(value, tuple):
            if len(value) > 0:
                node.inputs[index].default_value = value[0]

def mat_node(mat, type, ins=[], ins_dict={}, **parms):
    n = mat.node_tree.nodes.new(type=type)
    for k, v in parms.items():
        setattr(n, k, v)
    for i, inp in enumerate(ins):
        mat_set_input(mat, n, i, inp)
    for i, inp in ins_dict.items():
        mat_set_input(mat, n, i, inp)
    return n


def mat_map(mat, image, uv=None, clamp=False, **parms):
    n = mat_node(mat, 'ShaderNodeTexImage', image=image, **parms)
    if uv:
        mat_link(mat, uv, n.inputs['Vector'])
    if clamp:
        n.extension = 'CLIP'
    return (n.outputs['Color'], n.outputs['Alpha'])

def mat_mix(mat, fac, m0, m1, type='MIX'):
    n = mat_node(mat, 'ShaderNodeMixRGB', [fac, m0, m1], blend_type=type)
    return n.outputs['Color']

def setup_mat(mat, lightmap):
    mat.use_nodes = True
    mat.node_tree.nodes.clear()

    uv = mat_node(mat, 'ShaderNodeUVMap', uv_map='Lightmap', from_instancer=False)

    mat_node(mat, 'ShaderNodeOutputMaterial', [
        mat_node(mat, 'ShaderNodeBsdfDiffuse', [
            mat_map(mat, lightmap, uv.outputs['UV'], True)[0]
        ]).outputs[0]
    ])

def assign_material(obj, m):
    def find_slot(o):
        if m.name in o.data.materials:
            slot = [i for i, x in enumerate(o.material_slots) if x.material == m][0]
        else:
            slot = len(o.material_slots)
            o.data.materials.append(m)
        return slot
    mats = {obj.name: find_slot(obj)}
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode = 'OBJECT')
    bpy.ops.object.mode_set(mode = 'EDIT')
    selected = [(y.name, i) for y in [obj] for i,x in enumerate(y.data.polygons) if y.type == 'MESH']
    bpy.ops.object.mode_set(mode = 'OBJECT')
    for n, i in selected:
        bpy.data.objects[n].data.polygons[i].material_index = mats[n]
    bpy.ops.object.mode_set(mode = 'EDIT')
    bpy.ops.object.mode_set(mode = 'OBJECT')
    
def make_obj(bsp, name, lm_index):
    vxmap = {}
    last_v = 0
    for f in bsp.faces:
        if f.lm_index >= 0:
            if f.lm_index != lm_index: continue
        if f.type != 1 and f.type != 3: continue
        for v in range(f.vertex, f.vertex + f.n_vertex):
            vxmap[v] = last_v
            last_v += 1
    
    m = bmesh.new()
    tx = m.loops.layers.uv.new("UV")
    lm = m.loops.layers.uv.new("Lightmap")
    for bsp_v in vxmap:
        v = bsp.vertexs[bsp_v]
        m.verts.new((v.co[0] / GLOBAL_SCALE, v.co[1] / GLOBAL_SCALE, v.co[2] / GLOBAL_SCALE))
    m.verts.ensure_lookup_table()
    for f in bsp.faces:
        if f.lm_index >= 0:
            if f.lm_index != lm_index: continue
        if f.type != 1 and f.type != 3: continue
        for tri in range(0, f.n_meshvert // 3):
            face = m.faces.new([m.verts[vxmap[f.vertex + bsp.meshverts[x]]] for x in range(f.meshvert + 3 * tri, f.meshvert + 3 * tri + 3)])
            for i, v in enumerate(range(f.meshvert + 3 * tri, f.meshvert + 3 * tri + 3)):
                face.loops[i][tx].uv = bsp.vertexs[f.vertex + bsp.meshverts[v]].uv
                face.loops[i][lm].uv = bsp.vertexs[f.vertex + bsp.meshverts[v]].lm_uv
    m.faces.ensure_lookup_table()
    mesh = bpy.data.meshes.new(name)
    m.to_mesh(mesh)
    obj = bpy.data.objects.new(name, mesh)
    obj.bsp_lm_index = lm_index
    bpy.context.collection.objects.link(obj)
    
    if lm_index >= 0:
        img = bpy.data.images.new(f'lm_{lm_index}', width = 128, height = 128)
        img.pixels[:] = np.pad(np.array(bsp.lightmaps[lm_index].map).reshape((128, 128, 3)) / 255., ((0,0),(0,0),(0,1)), mode="constant", constant_values=1.).reshape((len(img.pixels),))

        mat = bpy.data.materials.new(f'mat_{lm_index}')
        setup_mat(mat, img)
        assign_material(obj, mat)
    return obj

class BSP_OP_ImportBspFile(Operator, ImportHelper):
    """Imports bsp file"""
    bl_idname = "import_menu.bsp_file"
    bl_label = "Import *.bsp"

    filename_ext = ".bsp"

    filter_glob: StringProperty(
        default="*.bsp",
        options={'HIDDEN'},        maxlen=255,
    )

    def execute(self, context):
        name = os.path.basename(self.filepath)
        with open(self.filepath, "rb") as f:
            bsp_data = f.read()

        bsp = bsp_lib.bspfile()
        bsp.read(bsp_data)
        if len(bsp.lightmaps) > 0:
            for i, l in enumerate(bsp.lightmaps):
                make_obj(bsp, f"{name}_{i}", i)
        else:
            make_obj(bsp, name, -1)
        return {'FINISHED'}


class BSP_OP_ExportBspModel(Operator, ExportHelper):
    """Replaces faces and lightmap at target bsp with selected object"""
    bl_idname = "export_menu.bsp_model"  # important since its how bpy.ops.import_test.some_data is constructed
    bl_label = "Write BSP model"
    filename_ext = ".bsp"
    filter_glob: StringProperty(
        default="*.bsp",
        options={'HIDDEN'},
        maxlen=255,
    )
    bsp_version: EnumProperty(
        name="Bsp version",
        description="Rewrite header to support the game",
        items=(
            ('OPT_Q3', "Quake 3", ""),
            ('OPT_QL', "Quake Live", ""),
        ),
        default='OPT_Q3',
    )
    
    def execute(self, context):
        with open(self.filepath, "rb") as f:
            bsp_data = f.read()
        bsp = bsp_lib.bspfile()
        bsp.read(bsp_data)
        
        if context.active_object:    
            print("Writing object")
            write_obj(bsp, context.active_object)
        else:
            print("Just overwrite version")
            
        version = 47 if self.bsp_version == 'OPT_QL' else 46
        print(f"Version override: {self.bsp_version} {version}")
        bsp.header.version = version
        with open(self.filepath[:-4]+'-copy.bsp', "wb") as f:
            f.write(bsp.to_bytes())
        
        return {"FINISHED"}

class BSP_OP_BuildLightmap(bpy.types.Operator):
    bl_idname = "bsp.build_lightmap"
    bl_label = "Build LM"
    bl_description = "Make splittable lightmap"
    bl_options = {'REGISTER', 'INTERNAL', 'UNDO'}
    size: bpy.props.IntProperty(default=1024)
    max_layers: bpy.props.IntProperty(default=32)
    margin: bpy.props.FloatProperty(default=0.1)
    density: bpy.props.FloatProperty(default=16)
    @classmethod
    def poll(cls, context):
        return context.mode == 'OBJECT' and context.object is not None
    def execute(self, context):
        obj = context.object
        if 'Lightmap' not in obj.data.uv_layers:
            bpy.ops.mesh.uv_texture_add()
            newuvlayer = obj.data.uv_layers[-1]
            newuvlayer.active = True
            newuvlayer.active_render = True
            newuvlayer.name = 'Lightmap'
            
        lmname = f"__map_lightmap_{obj.name}"
        if lmname in bpy.data.images:
            bpy.data.images.remove(bpy.data.images[lmname])
        image = bpy.data.images.new(name=lmname, width=self.size, height = self.size)
        for slot in obj.material_slots:
            mat = slot.material
            mat.use_nodes = True
            for node in slot.material.node_tree.nodes:
                node.select = False
            has_lm = False
            for node in slot.material.node_tree.nodes:
                if node.type != "UVMAP" or node.uv_map != 'Lightmap': continue
                for link in node.outputs['UV'].links:
                    if link.to_node.type == 'TEX_IMAGE':
                        link.to_node.image = image
                        has_lm = True
                        node.select = True
                        break
            if has_lm: continue
        
            uv = mat_node(mat, 'ShaderNodeUVMap', uv_map='Lightmap', from_instancer=False)
            node = mat_map(mat, image, uv.outputs['UV'], True)[0].node
            node.select = True
        # split polys
        bpy.ops.object.mode_set(mode = 'OBJECT')
        bpy.ops.object.mode_set(mode = 'EDIT')
        bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.mesh.edge_split(type='EDGE')
        bpy.ops.mesh.select_all(action='DESELECT')
        me = context.edit_object.data
        bm = bmesh.from_edit_mesh(me)
        bsp_lm_detail = bm.faces.layers.float.get("bsp_lm_detail")
        # subdivide according density
        meanarea = np.array([x.calc_area() for x in bm.faces]).mean()
        print(meanarea)
        bm.verts.ensure_lookup_table()
        bm.faces.ensure_lookup_table()
#        print(int(1.0 / 2.0 * ((bm.faces[3].calc_area() / meanarea) ** 0.5)))
#        return {'FINISHED'}
        
        srcfaces = [x for x in bm.faces]
        if self.max_layers < 0:
            layers_num = (self.size // 128) ** 2
        else:
            layers_num = max(1, min(self.max_layers, (self.size // 128) ** 2))
        groups = [[] for x in range(layers_num)]
        wm = context.window_manager
        progress = 0
        wm.progress_begin(0, len(srcfaces) + len(groups) + len(bm.faces))
        
        try:
            for f in srcfaces:
                progress += 1
                wm.progress_update(progress)
                f.select = True
#                print(f"select {f.index}")
                if bsp_lm_detail:
                    v = max(0.0001, f[bsp_lm_detail])
                    if v < 0.9 or v > 1.1:
#                        print(f"scale {f[bsp_lm_detail]} {v}")
                        bpy.ops.transform.resize(value=(v,v,v))
                else: v = 1.0
                ratio = int(v / 2.0 * ((f.calc_area() / meanarea) ** 0.5))
                if ratio > 0:
#                    print(f"split {f.index} {v} {ratio}")
                    bpy.ops.mesh.subdivide(number_cuts=ratio)
#                print(f"deselect {f.index}")
                bpy.ops.mesh.select_all(action='DESELECT')
                for ff in bm.faces:
                    f.select = False
#            return {'FINISHED'}
            # split into groups
            print("Faces Done")
            curfaces = sorted(list(enumerate(bm.faces)), key=lambda x: -x[1].calc_area())
            curgroup = 0
            if len(groups) > 1:
                for ind, f in curfaces:
                    groups[curgroup].append(ind)
                    curgroup = (curgroup + 1) % len(groups)
            else:
                for ind, f in curfaces:
                    groups[0].append(ind)
#            print("Groups split")
            
            width = max(self.size // 128, 1)
            obj.bsp_lm_grid = width
            for gind, g in enumerate(groups):
                progress += 1
                wm.progress_update(progress)
                bm.faces.ensure_lookup_table()
                for f in g:
                    bm.faces[f].select = True
                bpy.ops.uv.lightmap_pack(PREF_CONTEXT='SEL_FACES', PREF_PACK_IN_ONE=True, PREF_NEW_UVLAYER=False, PREF_BOX_DIV=12, PREF_MARGIN_DIV=self.margin)
                
#                print(f"Packed {gind}")
                # locate and move uv
                shift = mathutils.Vector((gind % width, gind // width))
                bm = bmesh.from_edit_mesh(me)
                lmuv = bm.loops.layers.uv['Lightmap']
                bm.faces.ensure_lookup_table()
                for f in g:
                    for l in bm.faces[f].loops:
                        l[lmuv].uv = (l[lmuv].uv + shift) / width
                
                for ff in bm.faces:
                    ff.select = False
            bmesh.update_edit_mesh(me)

#        bpy.ops.mesh.select_all(action='DESELECT')
            bsp_lm_detail = bm.faces.layers.float.get("bsp_lm_detail")
            used_faces = set()
            for ind, f in enumerate(bm.faces):
                progress += 1
                wm.progress_update(progress)
                if ind in used_faces: continue
                f.select = True
                bpy.ops.mesh.select_linked(delimit=set())
                for ii, ff in enumerate(bm.faces):
                    if ff.select:
                        used_faces.add(ii)
                if bsp_lm_detail:
                    v = max(0.0001, f[bsp_lm_detail])
                    if v < 0.9 or v > 1.1:
                        bpy.ops.transform.resize(value=(1/v,1/v,1/v))
#                print(f"Face {f.index}")
                for ff in bm.faces:
                    ff.select = False
        finally:
            wm.progress_end()

        bpy.ops.object.mode_set(mode = 'OBJECT')
        return {'FINISHED'}
    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)
    def draw(self, context):
        row = self.layout
        layout = self.layout
        row = layout.row(align=True)
        row.label(text='Image size')
        row.prop(self, 'size', text='')
        row = layout.row(align=True)
        row.label(text='Max layers')
        row.prop(self, 'max_layers', text='')
        row = layout.row(align=True)
        row.label(text='Margin')
        row.prop(self, 'margin', text='')
        layout.separator()
        
import socket
import struct

vert_t = '3fi'
vert_t = (vert_t, struct.calcsize(vert_t))
corn_t = '2f2f4fi'
corn_t = (corn_t, struct.calcsize(corn_t))
face_t = 'l3ii'
face_t = (face_t, struct.calcsize(face_t))

def mesh_to_buffer(msh, content):
    msh.verts.index_update()
    msh.faces.index_update()
    msh.verts.ensure_lookup_table()
    msh.faces.ensure_lookup_table()
    nverts = len(msh.verts)
    ncorns = sum(len(f.loops) for f in msh.faces)
    nfaces = len(msh.faces)
    flags = msh.loops.layers.int[0] if len(msh.loops.layers.int) > 0 else None
    col = msh.loops.layers.color[0] if len(msh.loops.layers.color) > 0 else None
    if len(msh.loops.layers.uv) > 0:
        lm = msh.loops.layers.uv["Lightmap"] if "Lightmap" in msh.loops.layers.uv else None
        uvs = [x for x in msh.loops.layers.uv.values() if x.name != "Lightmap"]
        uv = uvs[0] if len(uvs) > 0 else None
    else:
        lm = None
        uv = None
    yield struct.pack('i', nverts)
    for v in range(nverts):
        yield struct.pack(vert_t[0], *msh.verts[v].co, 0)
    
    yield struct.pack('i', ncorns)
    for f in range(nfaces):
        for l in msh.faces[f].loops:
            uv0 = l[uv].uv if uv is not None else (0.0, 0.0)
            uv1 = l[lm].uv if lm is not None else (0.0, 0.0)
            color = l[col] if col is not None else (1.0, 1.0, 1.0, 1.0)
            yield struct.pack(corn_t[0], *uv0, *uv1, *color, l.vert.index)

    yield struct.pack('i', nfaces)

    flg = bm.faces.layers.int[0] if len(bm.faces.layers.int) > 0 else None
    shft = 0
    for f in range(nfaces):
        face = msh.faces[0]
        l = len(face.loops)
        yield struct.pack(face_t[0], face[flg] if flg is not None else 0, shft, l, face.material_index, 0)
        shft += l

    yield struct.pack('l', content)

def recv_array(sock, size):
    l, *_ = struct.unpack('i', sock.recv(4))
    return sock.recv(l * size)

def mesh_from_stream(sock):
    m = bmesh.new()
    uv = m.loops.layers.uv.new("UVMap")
    lm = m.loops.layers.uv.new("Lightmap")
    col = m.loops.layers.color.new("Color")
    flags = m.faces.layers.int.new("Flags")
    verts = recv_array(sock, vert_t[1])
    corns = recv_array(sock, corn_t[1])
    faces = recv_array(sock, face_t[1])
    context, *_ = struct.unpack('l', sock.recv(8))

    for i in range(len(verts) // vert_t[1]):
        *co, flg = struct.unpack(vert_t[0], verts[i * vert_t[1]:i * vert_t[1] + vert_t[1]])
        m.verts.new(co)
    m.verts.ensure_lookup_table()

    for i in range(len(faces) // face_t[1]):
        fl, ls, le, mat, *_ = struct.unpack(face_t[0], faces[i * face_t[1]:i * face_t[1] + face_t[1]])
        uv0s = []
        uv1s = []
        colors = []
        vxs = []
        for j in range(ls, ls + le):
            uv0x, uv0y, uv1x, uv1y, r, g, b, a, vx = struct.unpack(corn_t[0], corns[j * corn_t[1]: j * corn_t[1] + corn_t[1]])
            uv0s.append((uv0x, uv0y))
            uv1s.append((uv1x, uv1y))
            colors.append((r,g,b,a))
            vxs.append(vx)
        f = m.faces.new([m.verts[x] for x in vxs])
        f[flags] = fl
        f.material_index = mat
        for uv0, uv1, c, loop in zip(uv0s, uv1s, colors, f.loops):
            loop[uv].uv = uv0
            loop[lm].uv = uv1
            loop[col] = c
    return m


def console_progress(done, full):
    print(f"Progress: {done} / {full}")
def console_error(error):
    print(f"Error: {error}")

class requester:
    def __init__(self, addr, on_progress=None, on_error=None):
        self.__addr = addr
        self.__on_progress = on_progress if on_progress else console_progress
        self.__on_error = on_error if on_error else console_error

    @staticmethod
    def echo_mesh_send(sock, msh: bmesh.types.BMesh):
        def recv(sock):
            return mesh_from_stream(sock)
        for b in mesh_to_buffer(msh, 1):
            sock.send(b)
        return recv
    @staticmethod
    def mesh_split_send(sock, msh: bmesh.types.BMesh):
        def recv(sock):
            num, *_ = struct.unpack('i', sock.recv(4))
            return [mesh_from_stream(sock) for i in range(num)]
        for b in mesh_to_buffer(msh, 1):
            sock.send(b)
        return recv
    def make_request(self, code, func):
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as sock:
            sock.connect(self.__addr)
            print(f"command = {code}")
            sock.send(struct.pack('i', code))
            recv = func(sock)
            while True:
                data = sock.recv(4)
                if not data:
                    print("none")
                    break
                res_code, *_ = struct.unpack('i', data)
                print(res_code)
                if res_code == 0x10000000:
                    d, a = struct.unpack('2i', sock.recv(8))
                    self.__on_progress(d, a)
                    continue
                if res_code == 0x0:
                    return recv(sock)
                else:
                    l, *_ = struct.unpack('i', sock.recv(4))
                    self.__on_error(sock.recv(l).decode('utf-8'))
                    break
    
    def echo_mesh(self, mesh):
        return self.make_request(0x1, lambda s: requester.echo_mesh_send(s, mesh))

    def split_mesh(self, mesh):
        return self.make_request(0x2, lambda s: requester.mesh_split_send(s, mesh))


class BSP_OP_SubdivideToConvex(bpy.types.Operator):
    bl_idname = "bsp.subdivide_brush"
    bl_label = "Split to convex"
    bl_description = "Splits mesh to convex subparts"
    bl_options = {'REGISTER', 'UNDO'}
#    max_layers: bpy.props.IntProperty(default=32)
    @classmethod
    def poll(cls, context):
        return context.mode == 'EDIT'
    
    def on_error(self, msg):
        self.report({'ERROR', msg})
    def execute(self, context):
        me = context.edit_object.data
        bm = bmesh.from_edit_mesh(me)
        rq = requester(("localhost", "2232"), on_error=lambda msg: self.on_error(msg))
        meshes = rq.split_mesh(bm)
        if meshes is None:
            return {'CANCELLED'}
        for i, m in enumerate(meshes):
            name = context.edit_object.name + f"_{i}"
            mesh = bpy.data.meshes.new(name)
            m.to_mesh(mesh)
            obj = bpy.data.objects.new(name, mesh)
            bpy.context.collection.objects.link(obj)
        return {'FINISHED'}


class BP_PT_BspObjectLightmap(bpy.types.Panel):
    bl_label = "BSP"
    bl_idname = "OBJECT_PT_bsp_object_lightmap"
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_category = "object"
    
    @classmethod
    def poll(cls, context):
        return context.mode == 'OBJECT'

    def draw(self, context):
        layout = self.layout
        col = layout.column()
        row = col.row(align=True)
        row.operator(BSP_OP_BuildLightmap.bl_idname, text='Build lightmap UV', icon='GROUP_UVS')
        
class BSP_OP_SplitLongEdges(bpy.types.Operator):
    bl_idname = "bsp.split_long_edges"
    bl_label = "Split long edges"
    bl_description = "Adds custom lightmap detail property"
    bl_options = {'REGISTER', 'UNDO'}
#    max_layers: bpy.props.IntProperty(default=32)
#    @classmethod
#    def poll(cls, context):
#        return context.mode == 'EDIT'
    
    def execute(self, context):
        me = context.edit_object.data
        bm = bmesh.from_edit_mesh(me)
        sf = [f for f in bm.faces if f.select]
        if len(sf) == 0:
            sf = bm.faces
        for f in bm.faces:
            f.select = False
        for e in bm.edges:
            e.select = False
        for f in sf:
            lens = np.array([e.calc_length() for e in f.edges])
            meanlen = lens.min()
            for i, e in enumerate(f.edges):
                if lens[i] > 2.0 * meanlen:
                    print(i)
                    e.select = True
        bpy.ops.mesh.subdivide(number_cuts=1)
        bmesh.update_edit_mesh(me)
            
        return {'FINISHED'}
#    def draw(self, context):
#        row = self.layout
#        layout = self.layout
#        row = layout.row(align=True)
#        row.label(text='Image size')
#        row.prop(self, 'size', text='')
#        layout.separator()
        
class BSP_OP_AddLmDetail(bpy.types.Operator):
    bl_idname = "bsp.add_lm_detail"
    bl_label = "Add lightmap detail"
    bl_description = "Adds custom lightmap detail property"
    bl_options = {'REGISTER'}
    def execute(self, context):
        me = context.edit_object.data
        if not "bsp_lm_detail" in me.polygon_layers_float:
            bpy.ops.geometry.attribute_add(name="bsp_lm_detail", domain='FACE', data_type='FLOAT')
            bm = BP_PT_FaceEditor.get_bm(me)
            bsp_lm_detail = bm.faces.layers.float.get("bsp_lm_detail")
            if not bsp_lm_detail: raise Error("Invalid configuration. Code: 124")
            for f in bm.faces:
                f[bsp_lm_detail] = 1.0
            bmesh.update_edit_mesh(me)
            
        return {'FINISHED'}

class BP_PT_FaceEditor(bpy.types.Panel):
    bl_label = "BSP"
    bl_idname = "OBJECT_PT_bsp_face_editor"
    bl_space_type = 'VIEW_3D'
    bl_region_type = 'UI'
    bl_category = "Tool"
    bl_options = {'DEFAULT_CLOSED'}
    ebm = dict()
    
    @classmethod
    def get_bm(cls, me):
        bm = BP_PT_FaceEditor.ebm.setdefault(me.name, bmesh.from_edit_mesh(me))
        if not bm.is_valid:
            cls.ebm.clear()
            bm = BP_PT_FaceEditor.ebm.setdefault(me.name, bmesh.from_edit_mesh(me))
        return bm
    
    @classmethod
    def poll(cls, context):
        if context.mode == 'EDIT_MESH':
            return True

        cls.ebm.clear()
        return False

    def draw(self, context):
        layout = self.layout
        me = context.edit_object.data
        col = layout.column()
        row = col.row(align=True)
        fl = me.polygon_layers_float.get("bsp_lm_detail")

        if fl:
            BP_PT_FaceEditor.get_bm(me)
            row.prop(me, "bsp_lm_detail")
        else:
            row.operator(BSP_OP_AddLmDetail.bl_idname, text='Add LM detail', icon='TEXTURE_DATA')
            
        row = col.row(align=True)
        row.operator(BSP_OP_SplitLongEdges.bl_idname, text='Squarize', icon='MESH_GRID')
        row = col.row(align=True)
        row.operator(BSP_OP_SubdivideToConvex.bl_idname, text='Subdivide', icon='MESH_ICOSPHERE')
       
class BP_PT_BspMaterial(bpy.types.Panel):
    bl_label = "BSP"
    bl_idname = "OBJECT_PT_bsp_material"
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_category = "material"
    
    @classmethod
    def poll(cls, context):
        return context.material is not None

    def draw(self, context):
        layout = self.layout
        col = layout.column()
        row = col.row(align=True)
        row.label(text='Q3 texture')
        row.prop(context.material, "bsp_texture_name", text="")

def set_float(self, value):
    bm = BP_PT_FaceEditor.get_bm(self)
    bsp_lm_detail = bm.faces.layers.float.get("bsp_lm_detail")
    for f in bm.faces:
        if not f.select: continue
        f[bsp_lm_detail] = max(0.0, value)
    bmesh.update_edit_mesh(self)

def get_float(self):
    if bpy.context.mode != 'EDIT_MESH': return 1.0
    bm = BP_PT_FaceEditor.get_bm(self)
    bsp_lm_detail = bm.faces.layers.float.get("bsp_lm_detail")
    af = bm.faces.active
    if af:
        return(af[bsp_lm_detail])
    return 1.0

def menu_func_export(self, context):
    self.layout.operator(ExportToMapFile.bl_idname, text="Q3Radiant map (.map)")
def menu_func_import(self, context):
    self.layout.operator(BSP_OP_ImportBspFile.bl_idname, text="Quake 3 BSP (.bsp)")
def menu_func_export_model(self, context):
    self.layout.operator(BSP_OP_ExportBspModel.bl_idname, text="Export model to BSP (.bsp)")


def register():
    bpy.utils.register_class(BSP_OP_BuildLightmap)
    bpy.utils.register_class(BP_PT_BspObjectLightmap)
    bpy.utils.register_class(BSP_OP_AddLmDetail)
    bpy.utils.register_class(BP_PT_FaceEditor)
    bpy.utils.register_class(BP_PT_BspMaterial)
    bpy.utils.register_class(BSP_OP_SplitLongEdges)
    bpy.utils.register_class(BSP_OP_SubdivideToConvex)
    
    bpy.types.Mesh.bsp_lm_detail = bpy.props.FloatProperty(name="bsp_lm_detail", default=1.0, get=get_float, set=set_float)

    setattr(bpy.types.Object, "bsp_lm_index", bpy.props.IntProperty())
    setattr(bpy.types.Object, "bsp_lm_grid", bpy.props.IntProperty())
    setattr(bpy.types.Material, "bsp_texture_name", bpy.props.StringProperty(default="common/caulk"))
    # default=1.0, min=0.0, max=100, step=0.01,precision=3,options=[]
#    setattr(bpy.types.MeshPolygon, "bsp_lm_detail", bpy.props.IntProperty())

    bpy.utils.register_class(BSP_OP_ImportBspFile)
    bpy.types.TOPBAR_MT_file_import.append(menu_func_import)
    bpy.utils.register_class(ExportToMapFile)
    bpy.types.TOPBAR_MT_file_export.append(menu_func_export)
    bpy.utils.register_class(BSP_OP_ExportBspModel)
    bpy.types.TOPBAR_MT_file_export.append(menu_func_export_model)


def unregister():
    if hasattr(bpy.types.Object, "bsp_lm_index"):
        delattr(bpy.types.Object, "bsp_lm_index")
    if hasattr(bpy.types.Object, "bsp_lm_grid"):
        delattr(bpy.types.Object, "bsp_lm_grid")
#    bpy.utils.unregister_class(BspFaceEditorPanel)
#    bpy.utils.unregister_class(ImportBspFile)
#    bpy.types.TOPBAR_MT_file_import.remove(menu_func_import)
#    bpy.utils.unregister_class(ExportToMapFile)
#    bpy.types.TOPBAR_MT_file_export.remove(menu_func_export)
#    bpy.utils.unregister_class(ExportBspLightmap)
#    bpy.types.TOPBAR_MT_file_export.remove(menu_func_export_lightmaps)
    
if __name__ == "__main__":
    unregister()
    register()