import bpy
import mathutils
import bmesh
import numpy as np
try:
    import bsp_file as bsp_lib
except:
    bsp_lib = bpy.data.texts["bsp.py"].as_module()
import struct

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
                vert.lm_uv = (*(f.loops[i][lm].uv * width),)
                vert.normal = (*v.normal, )
                vert.color = (*((np.array((0.,0.,0.,1.) if col is None else f.loops[i][col]) + 1/512) * 255).astype('byte'), )
                verts.append(vert)
            for ind in range(len(f.verts)-2):
                # triangle fan
                inds += [0, ind+1, ind+2]
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