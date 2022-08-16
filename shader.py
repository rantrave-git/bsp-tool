import bpy
import bgl
import zipfile
import json
import os
import re
try:
    import qsl
except ImportError:
    qsl = bpy.data.texts["qsl.py"].as_module()

def add_target_arrow():
    import gpu
    import mathutils as m
    from gpu_extras.batch import batch_for_shader
    vertex_shader = '''
        uniform mat4 viewProjectionMatrix;
        uniform vec3 beg;
        uniform vec3 end;
        uniform vec3 cam;
        in vec3 position;
        in vec4 color;
        void main()
        {
            vec3 pos = mix(beg, end, position);
            vec3 l = normalize(end - beg);
            vec3 c = (beg + end) * 0.5f;
            vec3 o = normalize(cross(c - cam, l));
            vec3 arr = c - l * 0.5f + color.y * o; 
            gl_Position = viewProjectionMatrix * vec4(mix(arr, pos, color.x), 1.0f);
        }
    '''
    fragment_shader = '''
        uniform vec4 color;
        out vec4 FragColor;
        void main(){FragColor = color;}
    '''
    handle_name = "__arrow_draw_handle"

    coords = [(0,0,0), (1,1,1), (0.5,0.5,0.5), (0, 0, 0), (0.5, 0.5, 0.5), (0, 0, 0)]
    colors = [(1,0,0,0), (1,0,0,0), (1,0,0,0), (0,0.5,0,20),(1,0,0,0), (0,-0.5,0,20)]
    shader = gpu.types.GPUShader(vertex_shader, fragment_shader)
    batch = batch_for_shader(shader, 'LINES', {"position": coords, "color": colors})
    def draw():
        shader.bind()
        matrix = bpy.context.region_data.perspective_matrix
        cam = m.Matrix.inverted(bpy.context.region_data.view_matrix) @ m.Vector((0,0,0,1))
        shader.uniform_float("viewProjectionMatrix", matrix)
        shader.uniform_float("cam", cam.xyz)
        bgl.glEnable(bgl.GL_BLEND)
        bgl.glEnable(bgl.GL_LINE_SMOOTH)
        drawn = set()
        for ob in bpy.context.selected_objects:
            if ob.target:
                tloc = ob.target.location
                shader.uniform_float("beg", ob.location)
                shader.uniform_float("end", tloc)
                shader.uniform_float("color", (0.2, 0.95, 0.1, 1))
                batch.draw(shader)
                drawn.add((ob.name, ob.target.name))
                
        for ob in bpy.context.selected_objects:
            for i in bpy.data.objects:
                if i.target != ob: continue
                if (i.name, ob.name) in drawn: continue # already drawn
                tloc = i.location
                shader.uniform_float("beg", tloc)
                shader.uniform_float("end", ob.location)
                shader.uniform_float("color", (0.0, 0.4, 0.0, 1))
                batch.draw(shader)
        bgl.glDisable(bgl.GL_BLEND)
        bgl.glDisable(bgl.GL_LINE_SMOOTH)
    try:
        if handle_name in bpy.app.driver_namespace:
            bpy.types.SpaceView3D.draw_handler_remove(bpy.app.driver_namespace[handle_name], "WINDOW")
    except:
        pass
    handle = bpy.types.SpaceView3D.draw_handler_add(draw, (), 'WINDOW', 'POST_VIEW')
    bpy.app.driver_namespace[handle_name] = handle

def on_target_update(object, context):
    if object.target == object: # self reference
        object.target = None

def filter_target(self, object):
    return self != object
        
tx_preview = ".bsp_tex_preview"
tx_material_preview = ".bsp_material_tex_preview"

def to_uint(i):
    return -(i & 0x7fffffff)-1 if (i & 0x80000000) != 0 else i

class MaterialHelper:
    def __init__(self, material, scene):
        self.mat = material
        self.scene = scene
        self.root_pos = [0, 0]
        self.grid = [250, 300]
    def link(self, n_out, n_in):
        l = self.mat.node_tree.links.new(n_out, n_in)
        nx = n_out.node.location[0]
        n_out.node.location[0] = max(nx, n_in.node.location[0] + self.grid[0])
        n_out.node.location[1] = n_in.node.location[1]
        return l
    def set_input(self, node, index, value):
        if value is None: return
        if isinstance(value, bpy.types.NodeSocket):
            self.link(node.inputs[index], value)
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
            else: print("WTF0!")
                        
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
            else: print("WTF1!")
        elif node.inputs[index].type == 'VALUE':
            if isinstance(value, int) or isinstance(value,float):
                node.inputs[index].default_value = value
            elif isinstance(value, list) or isinstance(value, tuple):
                if len(value) > 0:
                    node.inputs[index].default_value = value[0]
            else: print("WTF2!")
        else: print("WTF5555!")
        
    def set_root_pos(self):
        for i in self.mat.node_tree.nodes:
            if self.root_pos[1] < i.location[1] + self.grid[1] // 2:
                self.root_pos[1] = i.location[1]
        self.root_pos[1] += self.grid[1]
    def node(self, type, ins=[], ins_dict={}, **parms):
        self.set_root_pos()
        n = self.mat.node_tree.nodes.new(type=type)
        for k, v in parms.items():
            setattr(n, k, v)
        n.location = self.root_pos
        for i, inp in enumerate(ins):
            self.set_input(n, i, inp)
        for i, inp in ins_dict.items():
            self.set_input(n, i, inp)
        return n
    
    def tc_gen(self, uv=None, uv_s=[1,0,0], uv_t=[0,1,0]):
        uv_type = uv.lower() if uv else None
        uv_s = [float(x) for x in uv_s]
        uv_t = [float(x) for x in uv_t]
        if uv_type == 'lightmap':
            n = self.node('ShaderNodeUVMap', uv_map='Lightmap', from_instancer=False)
            return n.outputs['UV']
        if uv_type == 'environment':
            n = self.node('ShaderNodeTexCoord', from_instancer=False)
            return n.outputs['Reflection']
        if uv_type == 'vector':
            n = self.node('ShaderNodeNewGeometry')
            u = self.node('ShaderNodeVectorMath', [n.outputs['Position'], uv_s], operation='DOT_PRODUCT')
            v = self.node('ShaderNodeVectorMath', [n.outputs['Position'], uv_t], operation='DOT_PRODUCT')
            
            uv_n = self.node('ShaderNodeCombineXYZ', [u.outputs['Value'], v.outputs['Value']])
            return uv_n.outputs['Vector']
            
#        if uv is None or uv == 'base':
        # uv from first channel
        n = self.node('ShaderNodeUVMap', uv_map='UVMap', from_instancer=False)
        return n.outputs['UV']

    def map(self, image, uv=None, clamp=False, **parms):
        n = self.node('ShaderNodeTexImage', image=image, **parms)
        if uv:
            self.link(uv, n.inputs['Vector'])
        if clamp:
            n.extension = 'CLIP'
        return (n.outputs['Color'], n.outputs['Alpha'])

    def mix(self, fac, m0, m1, type='MIX'):
        n = self.node('ShaderNodeMixRGB', [fac, m0, m1], blend_type=type)
        return n.outputs['Color']
    
    def math(self, op, vs, **parms):
        n = self.node('ShaderNodeMath', vs, operation=op, **parms)
        return n.outputs['Value']
    def vmath(self, op, vs, **parms):
        n = self.node('ShaderNodeVectorMath', vs, operation=op, **parms)
        if op in ['DOT_PRODUCT', 'DISTANCE', 'LENGTH']:
            out = 'Value'
        else:
            out = 'Vector'
        return n.outputs[out]
    
    def wave(self, v, style, b, a, ph, clamp=False):
        b = float(b)
        a = float(a)
        ph = float(ph)
        if style == 'noise':
            n = self.node('ShaderNodeTexNoise', [self.math('ADD', [v, ph]), 1, 5, 1, 0], noise_dimensions='1D').outputs['Fac']
            n = self.math('MULTIPLY_ADD', [n, 2, -1])
            n = self.math('MULTIPLY_ADD', [n, a, b], use_clamp=clamp)
        if style == 'sin':
            n = self.math('MULTIPLY_ADD', [v, 6.28319, ph * 6.28319])
            n = self.math('SINE', [n])
            n = self.math('MULTIPLY_ADD', [n, a, b], use_clamp=clamp)
        if style == 'triangle':
            n = self.math('ADD', [v, ph + 0.25])
            n = self.math('PINGPONG', [n, 0.5])
            n = self.math('MULTIPLY_ADD', [n, 2, -0.5])
            n = self.math('MULTIPLY_ADD', [n, a, b], use_clamp=clamp)
        if style == 'sawtooth':
            n = self.math('ADD', [v, ph])
            n = self.math('FRACT', [n])
            n = self.math('MULTIPLY_ADD', [n, a, b], use_clamp=clamp)
        if style == 'inversesawtooth':
            n = self.math('ADD', [v, ph])
            n = self.math('FRACT', [n])
            n = self.math('SUBTRACT', [1, n])
            n = self.math('MULTIPLY_ADD', [n, a, b], use_clamp=clamp)
        if style == 'square':
            n = self.math('ADD', [v, ph])
            n = self.math('FRACT', [n])
            n = self.math('GREATER_THAN', [n, 0.5])
            n = self.math('MULTIPLY_ADD', [n, 2, -1])
            n = self.math('MULTIPLY_ADD', [n, a, b], use_clamp=clamp)
        return n
    
    def animate(self, speed):
        s = float(speed)
        v = self.node('ShaderNodeValue').outputs[0]
        v.default_value = 0
        v.keyframe_insert('default_value', frame=self.scene.frame_start)
        v.default_value = speed * (self.scene.frame_end - self.scene.frame_start) / self.scene.render.fps
        v.keyframe_insert('default_value', frame=self.scene.frame_end)
        for kf in self.mat.node_tree.animation_data.action.fcurves[-1].keyframe_points:
            kf.interpolation = 'LINEAR'
        return v
    
    def rgb_gen(self, m, mode, *params):
        mode_t = mode.lower() if mode else None
        if mode_t == "vertex":
            att = self.node('ShaderNodeAttribute', attribute_name='Col')
            return self.mix(1, m[0], att.outputs['Color'], type='MULTIPLY'), m[1]
        if mode_t == "oneminusvertex":
            att = self.node('ShaderNodeAttribute', attribute_name='Col')
            return self.mix(1, m[0], self.mix(1, [1.0, 1.0, 1.0, 1.0], att.outputs['Color'], type='SUBTRACT'), type='MULTIPLY'), m[1]
        if mode_t == "const":
            return self.mix(1, m[0], params[0], type='MULTIPLY'), m[1]
        if mode_t == "wave":
            style, b, a, ph, T = params
            style = style.lower()
            w = self.wave(self.animate(float(T)), style, b, a, ph)
            return self.mix(1, m[0], w, 'MULTIPLY'), m[1]
        return m

    def alpha_gen(self, m, mode, *params):
        mode_t = mode.lower() if mode else None
        if mode_t == "vertex":
            att = self.node('ShaderNodeAttribute', attribute_name='Col')
            return (m[0], self.math('MULTIPLY', [m[1], att.outputs['Alpha']]))
        if mode_t == "oneminusvertex":
            att = self.node('ShaderNodeAttribute', attribute_name='Col')
            return (m[0], self.math('MULTIPLY', [m[1], self.math('SUBTRACT', [1, att.outputs['Alpha']])]))
        if mode_t == "wave":
            style, b, a, ph, T = params
            style = style.lower()
            return (m[0], self.math('MULTIPLY', [m[1], self.wave(self.animate(float(T)), style, b, a, ph)]))
        return m
    
    def vcombine(self, x, y, z=0):
        return self.node('ShaderNodeCombineXYZ', [x, y, z]).outputs['Vector']
            
    def tc_mod(self, uv, mode, *params):
        mode_t = mode.lower() if mode else None
        if mode_t == 'rotate':
            if len(params) < 1: raise KeyError(f"[tc_mod {mode}] Missing required parameter 'angle speed'")
            v = self.animate(float(params[0]) / 57.2957)
            return self.node('ShaderNodeVectorRotate', [uv, [0.5, 0.5, 0], [0, 0, 1], v]).outputs['Vector']
        if mode_t == 'scale':
            if len(params) < 2: raise KeyError(f"[tc_mod {mode}] Not enough arguments. Got {len(params)}, expected: {2}")
            return self.vmath('ADD', [[0.5, 0.5, 0.0], self.vmath('MULTIPLY', [self.vmath('ADD', [[-0.5, -0.5, 0.0], uv]), [float(params[0]), float(params[1]), 1]])])
        if mode_t == 'scroll':
            if len(params) < 2: raise KeyError(f"[tc_mod {mode}] Not enough arguments. Got {len(params)}, expected: {2}")
            v = self.animate(1.0)
            v = self.vmath('MULTIPLY', [v, [float(params[0]), -float(params[1]), 1]])
            return self.vmath('ADD', [uv, v])
        if mode_t == 'stretch':
            if len(params) < 5: raise KeyError(f"[tc_mod {mode}] Not enough arguments. Got {len(params)}, expected: {5}")
            style, b, a, ph, T = params
            v = self.wave(self.animate(float(T)), style, b, a, ph)
            return self.vmath('ADD', [[0.5, 0.5, 0.0], self.vmath('MULTIPLY', [self.vmath('ADD', [[-0.5, -0.5, 0.0], uv]), v])])
        if mode_t == 'turb':
            if len(params) > 4:
                params = params[-4:]
            if len(params) < 4: raise KeyError(f"[tc_mod {mode}] Not enough arguments. Got {len(params)}, expected: {4}")
            _, a, ph, T = [float(x) for x in params]
            v = self.animate(float(T))
            p = self.math('ADD', [v, ph])
            p_sh = self.math('MULTIPLY', [p, 0.99])
            sn = self.math('SINE', [p])
            co = self.math('COSINE', [p])
            sn_sh = self.math('SINE', [p_sh])
            co_sh = self.math('COSINE', [p_sh])
            x0 = self.vmath('MULTIPLY', [self.vcombine(sn, co_sh), 0.5 * a])
            x1 = self.vmath('MULTIPLY_ADD', [self.vcombine(co, sn_sh), 0.5 * a, [1,1,0]])
            sc = self.vmath('SUBTRACT', [x1, x0])
            return self.vmath('MULTIPLY_ADD', [uv, sc, x0])
        if mode_t == 'transform':
            if len(params) < 6: raise KeyError(f"[tc_mod {mode}] Not enough arguments. Got {len(params)}, expected: {6}")
            m00, m01, m10, m11, t0, t1 = [float(x) for x in params]
            u = self.vmath('DOT_PRODUCT', [uv, [m00, m01, 0]])
            v = self.vmath('DOT_PRODUCT', [uv, [m00, m01, 0]])
            return self.vmath('ADD', [self.vcombine(u, v, 0), [t0, t1, 0]])
        raise KeyError(f'[tc_mod] Unknown instruction: {mode_t}')
        
    def blend(self, src, dst, src_f, dst_f=None):
        src_f = src_f.lower()
        if dst_f is None:
            if src_f == 'blend':
                src_f = 'gl_src_alpha'
                dst_f = 'gl_one_minus_src_alpha'
            elif src_f == 'add':
                src_f = 'gl_one'
                dst_f = 'gl_one'
            elif src_f == 'filter':
                src_f = 'gl_dst_color'
                dst_f = 'gl_zero'
            else: raise KeyError(f'[blend] Unknown instruction: {src_f}')
        dst_f = dst_f.lower()
        blends = {
            'gl_one': lambda: 1.0,
            'gl_zero': lambda: 0.0,
            'gl_dst_color': lambda: dst[0],
            'gl_one_minus_dst_color': lambda: self.mix(1.0, 1.0, dst[0], 'SUBTRACT'),
            'gl_src_color': lambda: src[0],
            'gl_one_minus_src_color': lambda: self.mix(1.0, 1.0, src[0], 'SUBTRACT'),
            'gl_dst_alpha': lambda: dst[1],
            'gl_one_minus_dst_alpha': lambda: self.math('SUBTRACT', [1, dst[1]]),
            'gl_src_alpha': lambda: src[1],
            'gl_one_minus_src_alpha': lambda: self.math('SUBTRACT', [1, src[1]]),
        }
        src_b = {**blends, 'gl_src_color': blends['gl_one'], 'gl_one_minus_src_color': blends['gl_zero']}
        dst_b = {**blends, 'gl_dst_color': blends['gl_one'], 'gl_one_minus_dst_color': blends['gl_zero']}
        s = self.mix(1, src[0], src_b[src_f.lower()](), 'MULTIPLY')
        d = self.mix(1, dst[0], dst_b[dst_f.lower()](), 'MULTIPLY')
        return self.mix(1, s, d, 'ADD'), src[1]
    
    def lightmap(self, lm_images):
        lm_uv = self.tc_gen('lightmap')
        lms = [self.map(x, lm_uv, extension='CLIP')[0] for x in lm_images]
        for i, lm in enumerate(lms):
            lm.node.name = f'Lightmap.{i:03}'
        att = self.node('ShaderNodeAttribute', attribute_name='LM')
        p2 = self.math('GREATER_THAN', [self.math('MODULO', [att.outputs['Fac'], 0.2499]), 0.1245])
        p4 = self.math('GREATER_THAN', [self.math('MODULO', [att.outputs['Fac'], 0.2495]), 0.4999])
        p8 = self.math('GREATER_THAN', [att.outputs['Fac'], 0.4995])
        
        m0 = self.mix(p4, self.mix(p2, lms[0], lms[1]), self.mix(p2, lms[2], lms[3]))
        m1 = self.mix(p4, self.mix(p2, lms[4], lms[5]), self.mix(p2, lms[6], lms[7]))
        return self.mix(p8, m0, m1), 1.0
    
    def missing(self, uv):
        return self.node('ShaderNodeTexChecker', [uv, [1.0, 0.0, 1.0, 1.0], [0.0, 0.0, 0.0, 1.0], 5]).outputs[0], 1.0
    
    def alpha(self, map, func):
        func_t = func.lower()
        if func_t == 'gt0':
            return map[0], self.math('GREATER_THAN', [map[1], 0])
        if func_t == 'lt128':
            return map[0], self.math('LESS_THAN', [map[1], .5])
        if func_t == 'ge128':
            return map[0], self.math('SUBTRACT', [1.0, self.math('LESS_THAN', [map[1], .5])])
        raise KeyError(f'[alphafunc] Unknown instruction: {func_t}')
    
    @staticmethod
    def update_material(mat, context):
        mat.use_nodes = True
        h = MaterialHelper(mat, bpy.context.scene)
        # [TODO] handle sky
        tex = mat.texture
        mat.node_tree.nodes.clear()
        if tex.type == 'IMAGE':
            lm = h.lightmap([x.image if x.image else 1.0 for x in bpy.context.scene.bsp_lightmaps])
            img = BSP_Texture.find_image(tex.pak, tex.path, context)
            if img:
                map = h.map(img)
            else:
                map = h.missing(None)
            dst = h.mix(0.0 if context.scene.bsp_disable_lightmap else 1.0, map[0], lm[0], type='MULTIPLY'), map[1]
            dst[0].node.name = 'LightmapOutput'
        elif tex.type == 'SHADER':
            dst = (0.0, 0.0)
            sh = json.loads(tex.shader)
            for ps in sh.get('pass', []):
                # get uv
                tc_gen = []
                for i in ps:
                    if 'tcgen' in i:
                        tc_gen = i['tcgen']
                        break
                # get map (use first map in shader or white)
                is_lm_pass = False 
                map = (1.0, 1.0)
                for i in ps:
                    if 'map' in i or 'clampmap' in i:
                        v = i.get('map', i.get('clampmap', None))
                        if v[0] == '$whitemap':
                            map = (1.0, 1.0)
                            break
                        if v[0] == '$lightmap':
                            map = h.lightmap([x.image if x.image else 1.0 for x in bpy.context.scene.bsp_lightmaps])
                            is_lm_pass = True
                            break
                        uv = h.tc_gen(*tc_gen)
                        for j in ps:
                            if 'tcmod' in j:
                                uv = h.tc_mod(uv, *j['tcmod'])
                        img = BSP_Texture.find_image(None, v[0], context)
                        if img:
                            map = h.map(img, uv, clamp='clampmap' in i)
                        else:
                            map = h.missing(uv)
                        break
                    if 'animmap' in i:
                        v = i['animmap']
                        # [TODO] make an animation
                        uv = h.tc_gen(*tc_gen)
                        for j in ps:
                            if 'tcmod' in j:
                                uv = h.tc_mod(uv, *j['tcmod'])
                        img = BSP_Texture.find_image(None, v[1], context)
                        if img:
                            map = h.map(img, uv)
                        else:
                            map = h.missing(uv)
                        break
                # get resulting src color for blend pass
                src = map
                for i in ps:
                    if 'rgbgen' in i:
                        src = h.rgb_gen(src, *i['rgbgen'])
                        break
                alphafunc = False
                for i in ps:
                    if 'alphafunc' in i:
                        alphafunc = True
                        src = h.alpha(src, *i['alphafunc'])
                        break
                if is_lm_pass:
                    src = h.mix(0.0 if context.scene.bsp_disable_lightmap else 1.0, 1.0, src[0]), src[1]
                    src[0].node.name = 'LightmapOutput'
                blended = False
                for i in ps:
                    if 'blendfunc' in i:
                        blended = True
                        if dst is None:
                            dst = h.blend(src, dst, *i['blendfunc'])
                        else:
                            dst = h.blend(src, dst, *i['blendfunc'])
                        break
                if not blended:
                    if not alphafunc:
                        dst = h.blend(src, dst, 'gl_one', 'gl_zero')
                    else:
                        dst = h.blend(src, dst, 'blend')
        else: return
#        h.node('ShaderNodeOutputMaterial', [h.node('ShaderNodeBsdfPrincipled', [], {'Base Color': dst[0], 'Alpha': dst[1]}).outputs[0]])
        h.node('ShaderNodeOutputMaterial', [h.node('ShaderNodeBsdfDiffuse', [dst[0]]).outputs[0]])
        
    @staticmethod
    def set_lightmap_disabled(mat, disabled):
        if mat.node_tree and 'LightmapOutput' in mat.node_tree.nodes:
            for n in mat.node_tree.nodes:
                if n.name.startswith('LightmapOutput'):
                    n.inputs['Fac'].default_value = 0.0 if disabled else 1.0

image_name_re = re.compile(r'([^:]*):(.*)')

class BSP_Texture(bpy.types.PropertyGroup):
    pak: bpy.props.StringProperty()
    path: bpy.props.StringProperty()
    contents: bpy.props.IntProperty()
    flags: bpy.props.IntProperty()
    shader: bpy.props.StringProperty()
    image: bpy.props.PointerProperty(type=bpy.types.Image)
    type: bpy.props.EnumProperty(
        description="Image or shader",
        items=[
            ('IMAGE', 'Image', "Image of texture"),
            ('SHADER', 'Shader', "Shader text"),
        ]
    )
    
    @classmethod
    def load_pak(cls, pak_path, context):
        cls.cleanup(context)
        wm = context.window_manager
        with zipfile.ZipFile(pak_path, 'r') as pak:
            tex = qsl.pk3_parse_textures(pak)
            shs = qsl.pk3_parse_shaders(pak)
            wm.progress_begin(0, len(tex) + len(shs))
            try:
                for i, tx in enumerate(tex):
                    cls.load_image(pak, tx, context)
                    wm.progress_update(i)
                for i, sh in enumerate(shs):
                    cls.load_shader(pak, sh, context)
                    wm.progress_update(i + len(tex))
            finally:
                wm.progress_end()
    
    @classmethod
    def list_groups(cls, context):
        return list(set([y for y in [qsl.get_group(x.path) for x in context.scene.bsp_textures] if y is not None]))
    @classmethod
    def list_textures(cls, group, context):
        for i in context.scene.bsp_textures:
            if qsl.match_group(group, i.path):
                yield i
        for m in bpy.data.materials:
            # print(group, m.texture.path)
            if qsl.match_group(group, m.texture.path):
                yield m.texture
    
    @classmethod
    def pak_path(cls, pak, context):
        if not context.scene.bsp_executable_path:
            return pak.filename
        return os.path.relpath(pak.filename, os.path.dirname(context.scene.bsp_executable_path))
    
    @classmethod
    def load_image(cls, pak, path, context):
        filename = pak.extract(path, bpy.app.tempdir)
        pak_path = cls.pak_path(pak, context)
        fn = f"{pak_path}:{path}" # qsl.to_name(path)
        if fn in bpy.data.images:
            limg = bpy.data.images[fn]
            
            limg.unpack()
            limg.filepath = filename
            limg.filepath_raw = filename
            limg.reload()
        else:
            limg = bpy.data.images.load(filename, check_existing=False) # load anyway
            limg.name = fn
        # which is right?
#        limg.colorspace_settings.name = 'Linear'
        limg.pack()
        os.unlink(filename)
        
        tx = cls.get_texture(pak_path, path, context)
        if tx is None:
            tx = context.scene.bsp_textures.add()
        tx.type = 'IMAGE'
        tx.pak = pak_path
        tx.path = path
        tx.image = limg
        tx.contents, tx.flags = qsl.build_surfparam([])
        return tx
    
    @classmethod
    def load_shader(cls, pak, shader, context):
        tx = cls.get_shader(pak, shader['name'], context)
        if tx is None:
            tx = context.scene.bsp_textures.add()

        pak_path = cls.pak_path(pak, context)
        tx.type = 'SHADER'
        tx.pak = pak_path
        tx.path = shader['name']
        tx.shader = json.dumps(shader)
        c, f = qsl.build_surfparam(shader['properties'])
        tx.contents, tx.flags = to_uint(c), to_uint(f)
        
        icon = qsl.get_image(shader)
        if icon is None:
            return tx
        #print(f'[{shader["name"]}] {pak_path}')
        imgpath = qsl.get_image(shader)
        if imgpath is None: return tx
        img = cls.find_image(pak_path, imgpath, context)
        if img is None: return tx
        tx.image = img
        return tx
    
    @classmethod
    def create_texture(cls, name, tex_type, image, shader, context):
        tx = context.scene.bsp_textures.add()
        tx.set_name(name)
        tx.type = tex_type
        tx.image = image
        tx.shader = shader
        return tx
        
    @staticmethod
    def __p(p):
        if p[-4:].lower() in ['.jpg', '.tga']:
            return p[:-4].lower()
        return p.lower()
    
    @classmethod
    def get_texture(cls, pak, path, context):
        for i in context.scene.bsp_textures:
            if i.type == 'IMAGE' and i.image and (pak is None or i.pak == pak) and BSP_Texture.__p(i.path) == BSP_Texture.__p(path):
                return i
        
        return None
    
    @classmethod
    def get_shader(cls, pak, path, context):
        for i in context.scene.bsp_textures:
            if i.type == 'SHADER' and i.path == path:
                return i
        return None

    @classmethod
    def find_image(cls, pak_path, path, context):
        if pak_path:
            pname = f"{pak_path}:{path}"
            if pname in bpy.data.images:
                return bpy.data.images[pname]
            print(f"WARNING! Texture '{path}' couldn't be located at pak '{pak_path}'.")    
        if path in bpy.data.images:
            return bpy.data.images[path]
        
        nores = '.'.join(path.split('.')[:-1])
        for i in bpy.data.images:
            m = image_name_re.match(i.name)
            if m:
                if m[2] == path:
                    print(f"WARNING! Texture '{path}' couldn't be located. '{i.name}' used.")
                    return i
                
                if m[2][:-4] == nores:
                    print(f"WARNING! Texture '{path}' couldn't be located with requested resolution. '{i.name}' used.")
                    return i
    
    @classmethod
    def find_texture(cls, image, context):
        for i in context.scene.bsp_textures:
            if i.type == 'IMAGE' and i.image == image:
                return i
        return None
    
    @classmethod
    def cleanup(cls, context):
        wm = context.window_manager
        context.scene.bsp_selected_texture = -1
        to_delete = list([i for i,v in enumerate(context.scene.bsp_textures) if v.type == 'IMAGE' and v.image == None or v.is_custom()])
        for i in to_delete[::-1]:
            context.scene.bsp_textures.remove(i)
        for m in bpy.data.materials:
            if not m.texture.is_custom() or not m.texture.is_valid():
                continue
            m.texture.copy_to(context.scene.bsp_textures.add())
        wm.progress_begin(0, len(context.scene.bsp_textures))
        try:
            for prog, i in enumerate(context.scene.bsp_textures):
                wm.progress_update(prog)
                if i.type != 'SHADER' or i.image != None:
                    continue
                imgpath = qsl.get_image(json.loads(i.shader))
                if imgpath is None: continue
                img = cls.find_image(i.pak, imgpath, context)
                if img is None: continue
                i.image = img
        finally:
            wm.progress_end()
            
    @classmethod
    def remove_unused(cls, context):
        used_materials = set()
        used_images = set()
        for m in bpy.data.materials:
            t = m.texture
            used_materials.add((t.pak, t.path))
            for i in m.texture.deps(context):
                if i:
                    used_images.add((i.name))
        
        inds = []
        for i,t in enumerate(context.scene.bsp_textures):
            if (t.pak, t.path) in used_materials or (t.type == 'IMAGE' and t.image in used_images):
                continue
            inds.append(i)
        for i in inds[::-1]:
            tx = context.scene.bsp_textures[i]
            if tx.type == 'IMAGE' and tx.image and tx.image.name in bpy.data.images and tx.image.name not in used_images:
                bpy.data.images.remove(tx.image)
            context.scene.bsp_textures.remove(i)
            
        cls.cleanup(context)
        
    def is_valid(self):
        if self.type == 'SHADER':
            return self.shader and self.path
        return self.image and self.path
        
    def set_name(self, name):
        self.pak = ''
        self.path = f'textures/<custom>/{name}'
        
    def get_name(self):
        s = self.path.split('/')
        if len(s) < 2:
            return self.path
        return '/'.join(s[2:])
    
    def get_descriptor(self):
        return self.pak
        
    def is_custom(self):
        return not self.pak
    
    def copy_to(self, tex):
        tex.type = self.type
        tex.pak = self.pak
        tex.path = self.path
        tex.contents = self.contents
        tex.flags = self.flags
        tex.shader = self.shader
        tex.image = self.image
    
    def create_material(self):
        mat = bpy.data.materials.new(name=self.get_name())
        self.copy_to(mat.texture)
        mat.translucent = (self.contents & qsl.Q_CONT_TRANSLUCENT) == qsl.Q_CONT_TRANSLUCENT
        mat.slick = (self.flags & qsl.Q_SURF_SLICK) == qsl.Q_SURF_SLICK
        mat.noimpact = (self.flags & qsl.Q_SURF_NOIMPACT) == qsl.Q_SURF_NOIMPACT
        mat.nomarks = (self.flags & qsl.Q_SURF_NOMARKS) == qsl.Q_SURF_NOMARKS
        mat.nodamage = (self.flags & qsl.Q_SURF_NODAMAGE) == qsl.Q_SURF_NODAMAGE
        mat.dust = (self.flags & qsl.Q_SURF_DUST) == qsl.Q_SURF_DUST
        mat.noob = (self.flags & qsl.Q_SURF_NOOB) == qsl.Q_SURF_NOOB
        if self.flags & qsl.Q_SURF_NOSTEPS:
            mat.surface_type = 'NOSOUND'
        elif self.flags & qsl.Q_SURF_METALSTEPS:
            mat.surface_type = 'METAL'
        elif self.flags & qsl.Q_SURF_FLESH:
            mat.surface_type = 'FLESH'
        return mat
    
    def make_custom(self, name, context):
        if not self.is_custom():
            self.set_name(name)
            tx = context.scene.bsp_textures.add()
            self.copy_to(tx)
    
    def find_material(self):
        for i in bpy.data.materials:
            if self.pak == i.texture.pak and self.path == i.texture.path:
                return i
        return None
        
    def deps(self, context):
        if self.type == 'IMAGE':
            yield self.image
            return
        sh = json.loads(self.shader)
        for ref in qsl.get_references(sh):
            rt = BSP_Texture.find_image(self.pak, ref, context)
            if rt:
                yield rt
        
    def maps(self):
        if self.type == 'IMAGE':
            yield (self.image, [])
            return
        sh = json.loads(self.shader)
        for map in sh['pass']:
            m = [x for x in map if 'map' in x]
            if len(m) < 1: continue
            m = m[0]['map']
            if m.startswith('$'):
                yield (None, map)
                continue
            rt = BSP_Texture.find_image(self.pak, m, context)
            yield (rt, map)

    
# ============================================================================================
#
#   TEXTURE PANEL
#
# ============================================================================================

class BSP_UL_TextureImage(bpy.types.UIList):
    def draw_item(self, _context, layout, _data, item, icon, _active_data, _active_propname, _index):
        slot = item
        img = slot.image
        name = slot.get_name()
        pak = slot.get_descriptor()
        parms = {}
        icoparms = {'icon': 'MATERIAL' if slot.type == 'SHADER' else 'TEXTURE'}
        if img:
            img.preview_ensure()
            parms = {'icon_value': img.preview.icon_id }
        else:
            parms = {'icon': 'ERROR'}
        if self.layout_type in {'DEFAULT', 'COMPACT'}:
            row = layout.row(align=True)
            row.label(text='', **icoparms)
            row.label(text=name, **parms)
            row.separator()
            row.label(text=f'[{pak}]')
#            row.prop(img, "name", text="", emboss=False, icon_value=img.preview.icon_id)
        elif self.layout_type == 'GRID':
            layout.alignment = 'CENTER'
            col = layout.column(align=True)
            col.label(text='', **parms)
            col.label(text=name)
            col.label(text=f'[{pak}]')
    def filter_items(self, context, scene, propname):
#        vgroups = getattr(data, propname)
        helper_funcs = bpy.types.UI_UL_list
        group = BSP_MT_TextureGroupSelectionMenu.get_selected_text()
        textures = scene.bsp_textures
        flt_flags = [self.bitflag_filter_item if qsl.match_group(group, i.path) and (self.filter_name == "" or self.filter_name.lower() in i.get_name().lower()) else 0 for i in textures]
        flt_neworder = list(range(len(textures)))
        return flt_flags, flt_neworder

class BSP_Lightmap(bpy.types.PropertyGroup):
    image: bpy.props.PointerProperty(type=bpy.types.Image)
    
    def init(self, size, index):
        if self.image is None:
            self.image = bpy.data.images.new(name=f'bsp-lightmap-{index}', width=size, height=size)
    
class BSP_UL_LightmapImage(bpy.types.UIList):
    def draw_item(self, _context, layout, _data, item, icon, _active_data, _active_propname, _index):
        slot = item
        img = slot.image
        parms = {}
        if img:
            img.preview_ensure()
            name = img.name
            parms = {'icon_value': img.preview.icon_id }
        else:
            name = '<image missing>'
            parms = {'icon': 'ERROR'}
        if self.layout_type in {'DEFAULT', 'COMPACT'}:
            row = layout.row(align=True)
            row.label(text=name, **parms)
        elif self.layout_type == 'GRID':
            layout.alignment = 'CENTER'
            col = layout.column(align=True)
            col.label(text='', **parms)
            col.label(text=name)
    def filter_items(self, context, scene, propname):
        helper_funcs = bpy.types.UI_UL_list
        group = BSP_MT_TextureGroupSelectionMenu.get_selected_text()
        flt_flags = [self.bitflag_filter_item if self.filter_name == "" or i.image is None or self.filter_name.lower() in i.image.name.lower() else 0 for i in scene.bsp_lightmaps]
        flt_neworder = list(range(len(scene.bsp_lightmaps)))
        return flt_flags, flt_neworder

class BSP_MT_TextureGroupSelectionMenu(bpy.types.Operator):
    bl_idname = "bsp.group_selection"
    bl_label = "Select group"
    def avail_objects(self,context):
        bpy.app.driver_namespace['.bsp_texture_group_cache'] = sorted([(x, x, x) for x in BSP_Texture.list_groups(context)])
        return bpy.app.driver_namespace['.bsp_texture_group_cache']
    items: bpy.props.EnumProperty(items = avail_objects, name = "Texture group")
    @classmethod
    def poll(cls, context):
        return context.scene is not None
    @classmethod
    def get_selected_text(cls):
        dn = bpy.app.driver_namespace
        return dn.get('.bsp_selected_group', '<select>')
        
    def execute(self,context):
        bpy.app.driver_namespace['.bsp_selected_group'] = self.items
        context.scene.bsp_selected_texture = -1
        return {'FINISHED'}
    
class BSP_OP_TexturesRemoveUnused(bpy.types.Operator):
    bl_idname = "bsp.remove_unused"
    bl_label = "Remove unused"
    bl_description = "Remove unused images and shaders"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        BSP_Texture.remove_unused(context)
        if '.bsp_selected_group' in bpy.app.driver_namespace:
            del bpy.app.driver_namespace['.bsp_selected_group']
        return {'FINISHED'}
class BSP_OP_TexturesCleanup(bpy.types.Operator):
    bl_idname = "bsp.cleanup"
    bl_label = "Cleanup resources"
    bl_description = "Remove invalid unused textures"
    bl_options = {'REGISTER'}
    def execute(self, context):
        BSP_Texture.cleanup(context)
        return {'FINISHED'}
    
class BSP_OP_LoadPaks(bpy.types.Operator):
    bl_idname = "bsp.add_paks"
    bl_label = "Add paks"
    bl_description = "Add resources from pk3 files"
    bl_options = {'REGISTER', 'INTERNAL'}
    filepath: bpy.props.StringProperty(
        maxlen= 1024,
        default= "")
    files: bpy.props.CollectionProperty(
        name="File Path",
        type=bpy.types.OperatorFileListElement,
        )
    def execute(self, context):
        sc = context.scene
        for i, f in enumerate(self.files):
            BSP_Texture.load_pak(os.path.join(os.path.dirname(self.filepath), f.name), context)
        return {'FINISHED'}
    def draw(self, context):
        self.layout.operator('file.select_all')        
    def invoke(self, context, event):
        wm = context.window_manager
        wm.fileselect_add(self)
        return {'RUNNING_MODAL'}

class BSP_OP_SetupDirectory(bpy.types.Operator):
    bl_idname = "bsp.setup"
    bl_label = "Select Executable"
    bl_description = "Select quake executable location"
    bl_options = {'REGISTER', 'INTERNAL'}
    filepath: bpy.props.StringProperty(
        name="File Path", 
        description="Quake game executable path",
        maxlen= 1024,
        default= "")
    def execute(self, context):
        sc = context.scene
        sc.bsp_executable_path = self.properties.filepath
        #sh, tx = load_paks(sc.executable_path)
        return {'FINISHED'}
    def draw(self, context):
        self.layout.operator('file.select_all')        
    def invoke(self, context, event):
        wm = context.window_manager
        wm.fileselect_add(self)
        return {'RUNNING_MODAL'}

def run_assign(context):
    sc = context.scene
    tex = sc.bsp_textures[sc.bsp_selected_texture]
    m = tex.find_material()
    if m is None:
        m = sc.bsp_textures[sc.bsp_selected_texture].create_material()
    MaterialHelper.update_material(m, context)
    
    def find_slot(o):
        if m.name in o.data.materials:
            slot = [i for i, x in enumerate(o.material_slots) if x.material == m][0]
        else:
            slot = len(o.material_slots)
            o.data.materials.append(m)
        return slot
    mats = {o.name: find_slot(o) for o in context.selected_objects if o.type == 'MESH'}
    all = context.mode == 'OBJECT'
    bpy.ops.object.mode_set(mode = 'OBJECT')
    bpy.ops.object.mode_set(mode = 'EDIT')
    selected = [(y.name, i) for y in context.selected_objects for i,x in enumerate(y.data.polygons) if y.type == 'MESH' and (x.select or all)]
    bpy.ops.object.mode_set(mode = 'OBJECT')
    for n, i in selected:
        bpy.data.objects[n].data.polygons[i].material_index = mats[n]
    bpy.ops.object.mode_set(mode = 'EDIT')
    if all:
        bpy.ops.object.mode_set(mode = 'OBJECT')

class BSP_OP_TextureAssignMaterial(bpy.types.Operator):
    bl_idname = "bsp.assign_material"
    bl_label = "Assign material"
    bl_description = "Create material with selected texture or shader"
    bl_options = {'REGISTER', 'UNDO'}
    def execute(self, context):
        run_assign(context)
        return {'FINISHED'}
        
    @classmethod
    def poll(cls, context):
        sc = context.scene
        return (context.mode == 'OBJECT' or context.mode == 'EDIT_MESH') and sc and sc.bsp_selected_texture >= 0 and sc.bsp_selected_texture < len(sc.bsp_textures)

class BSP_OP_ResizeLightmap(bpy.types.Operator):
    bl_idname = "bsp.resize_lightmap"
    bl_label = "Resize"
    bl_description = "Resize selected lightmap"
    bl_options = {'REGISTER', 'INTERNAL', 'UNDO'}
    size: bpy.props.IntProperty(default=1024)
    @classmethod
    def poll(cls, context):
        sc = context.scene
        return sc is not None and len(sc.bsp_lightmaps) > 0 and sc.bsp_selected_lightmap < len(sc.bsp_lightmaps)
    def execute(self, context):
        sc = context.scene
        lm = sc.bsp_lightmaps[sc.bsp_selected_lightmap]
        if lm.image:
            bpy.data.images.remove(lm.image)
        lm.image = bpy.data.images.new(name=f'lightmap', width=self.size, height = self.size)
        return {'FINISHED'}
    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)
    def draw(self, context):
        row = self.layout
        layout = self.layout
        row = layout.row(align=True)
        row.label(text='Image size')
        row.prop(self, 'size', text='')
        layout.separator()
class BSP_OP_AssignLightmap(bpy.types.Operator):
    bl_idname = "bsp.assign_lightmap"
    bl_label = "Assign"
    bl_description = "Assign selected lightmap"
    bl_options = {'REGISTER', 'INTERNAL', 'UNDO'}
    @classmethod
    def poll(cls, context):
        sc = context.scene
        obs = context.selected_objects
        return context.mode=='OBJECT' and len(obs) > 0 and sc is not None and len(sc.bsp_lightmaps) > 0 and sc.bsp_selected_lightmap < len(sc.bsp_lightmaps)
    def execute(self, context):
        import bmesh
        sc = context.scene
        obs = context.selected_objects
        lm = sc.bsp_lightmaps[sc.bsp_selected_lightmap]
        m = bmesh.new()
        try:
            for o in obs:
                if o.type != 'MESH': continue
                # add required uv
                if 'Lightmap' not in o.data.uv_layers:
                    l = o.data.uv_layers.new(name='Lightmap')
                    l.active = True
                # set currently used lightmap
                o.bsp_selected_lightmap = sc.bsp_selected_lightmap
                m.from_mesh(o.data)
                m.faces.ensure_lookup_table()
                if 'LM' in m.faces.layers.int:
                    m.faces.layers.int.remove(m.faces.layers.int['LM'])
                if 'LM' not in m.faces.layers.float:
                    m.faces.layers.float.new('LM')
                l = m.faces.layers.float['LM']
                for f in m.faces:
                    f[l] = sc.bsp_selected_lightmap * 0.125
                m.to_mesh(o.data)
                m.clear()
        finally:
            m.free()
        return {'FINISHED'}
class BSP_OP_SelectLightmap(bpy.types.Operator):
    bl_idname = "bsp.select_lightmap"
    bl_label = "Select"
    bl_description = "Select all objects with specified lightmap and it's image on materials"
    bl_options = {'REGISTER', 'INTERNAL', 'UNDO'}
    @classmethod
    def poll(cls, context):
        sc = context.scene
        obs = context.selected_objects
        return context.mode=='OBJECT' and sc is not None and len(sc.bsp_lightmaps) > 0 and sc.bsp_selected_lightmap < len(sc.bsp_lightmaps)
    def execute(self, context):
        import bmesh
        sc = context.scene
        obs = bpy.data.objects
        for o in obs:
            if o.type != 'MESH' or o.bsp_selected_lightmap != sc.bsp_selected_lightmap:
                o.select_set(False)
                continue
            if 'Lightmap' in o.data.uv_layers:
                l.active = True
            o.select_set(True)
            for m in o.material_slots:
                mat = m.material
                lmname = f'Lightmap.{sc.bsp_selected_lightmap:03}'
                if mat and mat.use_nodes and lmname in mat.node_tree.nodes:
                    an = mat.node_tree.nodes[lmname]
                    mat.node_tree.nodes.active = an
        return {'FINISHED'}
class BSP_OP_BakeLightmap(bpy.types.Operator):
    bl_idname = "bsp.bake_lightmap"
    bl_label = "Bake"
    bl_description = "Bake lightmap layer"
    bl_options = {'REGISTER', 'INTERNAL'}
    @classmethod
    def poll(cls, context):
        sc = context.scene
        obs = context.selected_objects
        return (context.mode=='OBJECT' and sc is not None and len(sc.bsp_lightmaps) > 0 and
            sc.bsp_selected_lightmap < len(sc.bsp_lightmaps) and
            sc.bsp_lightmaps[sc.bsp_selected_lightmap].image)
    def execute(self, context):
        import bmesh
        sc = context.scene
        obs = bpy.data.objects
        lm = sc.bsp_lightmaps[sc.bsp_selected_lightmap]
        for o in obs:
            if o.type != 'MESH' or o.bsp_selected_lightmap != sc.bsp_selected_lightmap:
                o.select_set(False)
                continue
            o.select_set(True)
            if 'Lightmap' in o.data.uv_layers:
                o.data.uv_layers['Lightmap'].active = True
        obs = context.selected_objects
        if len(obs) == 0:
            self.report({'ERROR'}, "Lightmap is not assigned to any mesh")
            return {'FINISHED'}
        context.view_layer.objects.active = obs[0]
        bpy.ops.uv.lightmap_pack()
        for o in obs:
            assigned = False
            for m in o.material_slots:
                mat = m.material
                lmname = f'Lightmap.{sc.bsp_selected_lightmap:03}'
                if mat and mat.use_nodes and lmname in mat.node_tree.nodes:
                    an = mat.node_tree.nodes[lmname]
                    mat.node_tree.nodes.active = an
                    assigned = True
            if not assigned:
                o.select_set(False)
        obs = context.selected_objects
        if len(obs) == 0:
            self.report({'ERROR'}, "No target lightmap found. Assign at least one lightmapped material.")
            return {'FINISHED'}
        context.view_layer.objects.active = obs[0]
        bpy.ops.uv.lightmap_pack()
        
        lmdis = sc.bsp_disable_lightmap
        sc.bsp_disable_lightmap = True
        bpy.ops.object.bake(type='SHADOW', width=lm.image.size[0], height=lm.image.size[1], use_clear=True)
        sc.bsp_disable_lightmap = lmdis
        return {'FINISHED'}

class BSP_OP_MaterialUpdatePreview(bpy.types.Operator):
    bl_idname = "bsp.update_preview"
    bl_label = "Update"
    bl_description = "Update preview image."
    bl_options = {'REGISTER', 'INTERNAL'}
    @classmethod
    def poll(cls, context):
        return context.material and context.material.texture.image
    def execute(self, context):
        if context.material:
            set_preview_image(context.scene, context.material.texture.image)
        return {'FINISHED'}
        
def preview(texture, st, col):
    row = col.row(align=True)
    b = None
    if texture and texture.image:
        row.template_preview(texture, show_buttons=True)
        b = row.column(align=True)
        row = col.row(align=True)
        row.prop(texture, 'use_alpha')
        row.prop(texture.image, 'alpha_mode', text='', )
    if st:
        # info
        if st.type == 'IMAGE':
            r = col.row(align=True)
            r.label(text="Texture image")
            r.label(text=st.path)
            if st.pak:
                r = col.row(align=True)
                r.label(text="Pak")
                r.label(text=st.pak)
            if st.image:
                r = col.row(align=True)
                if b:
                    b.operator(BSP_OP_MaterialUpdatePreview.bl_idname, icon='FILE_REFRESH', text='')
                r = col.row(align=True)
                r.label(text="Image")
                r.label(text=st.image.name)
                r = col.row(align=True)
                r.label(text="Size")
                r.label(text=f"{st.image.size[0]}x{st.image.size[1]}")
            else:
                col.label(text=f"Image is not loaded. Reload containing pak or do cleanup!", icon='ERROR')
        else:
            col.label(text=f"Shader: '{st.path}'")
            col.label(text=f"Pak: {st.pak}")
            box = col.box()
            c = box.column(align=True)
            s = qsl.shader_formatted_print(json.loads(st.shader))
            for i in s.split('\n'):
                c.label(text=i)

class BSP_PT_ScenePanel(bpy.types.Panel):
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "scene"
    bl_label = "BSP"
    
    def draw(self, context):
        layout = self.layout
        sc = context.scene
        col = layout.column()
        row = col.row(align=True)
        row.label(text="Game executable")
        row.separator()
        row.prop(sc, "bsp_executable_path", emboss=False, text='')
        row.operator(BSP_OP_SetupDirectory.bl_idname, text='', icon='FILE_FOLDER')
        
        if not os.path.isfile(sc.bsp_executable_path):
            layout.label(text="Select quake executable path first", icon='ERROR')
            return
        row = col.row(align=True)
        row.operator(BSP_OP_LoadPaks.bl_idname, icon='COLLECTION_NEW')
        row.operator(BSP_OP_TexturesCleanup.bl_idname, text='', icon='RESTRICT_INSTANCED_OFF')
        row.operator(BSP_OP_TexturesRemoveUnused.bl_idname, text='', icon='TRASH')
        
        col.separator()
        col.label(text="Texture management:")
        
        row = col.row(align=True)
        row.label(text="Texture group")
        row.separator()
        text = BSP_MT_TextureGroupSelectionMenu.get_selected_text()
        row.operator_menu_enum(BSP_MT_TextureGroupSelectionMenu.bl_idname, "items", text=text)
        
        row = col.row(align=True)
        row.template_list(BSP_UL_TextureImage.__name__, "", sc, "bsp_textures", sc, "bsp_selected_texture", rows=5)
        
        row = col.row(align=True)
        row.operator(BSP_OP_TextureAssignMaterial.bl_idname, icon='SHADING_RENDERED')
        if sc.bsp_selected_texture >= 0 and sc.bsp_selected_texture < len(sc.bsp_textures):
            st = sc.bsp_textures[sc.bsp_selected_texture]
            preview(sc.bsp_preview, st, col)
        
        col.separator()
        col.label(text="Lightmap management:")
        row = col.row()
        row.template_list(BSP_UL_LightmapImage.__name__, "", sc, "bsp_lightmaps", sc, "bsp_selected_lightmap", rows=3)
        
        row = col.row(align=True)
        row.operator(BSP_OP_AssignLightmap.bl_idname)
        row.operator(BSP_OP_SelectLightmap.bl_idname, icon='BORDERMOVE')
        row.operator(BSP_OP_ResizeLightmap.bl_idname, icon='IMAGE_PLANE')
        row = col.row(align=True)
        row.operator(BSP_OP_BakeLightmap.bl_idname, icon='RENDER_STILL')
        if sc.bsp_selected_lightmap >= 0 and sc.bsp_selected_lightmap < len(sc.bsp_lightmaps):
            row = col.row(align=True)
            lm = sc.bsp_lightmaps[sc.bsp_selected_lightmap]
            if lm.image:
                row.label(text=f"Size: {lm.image.size[0]}x{lm.image.size[1]}")
            else:
                row.label(text=f"Select image...")
            row = col.row(align=True)
            row.label(text='Image')
            row.prop(lm, 'image', text='')
        row = col.row(align=True)
        row.label(text='')
        row.prop(sc, 'bsp_disable_lightmap', text='Disable lightmaps')
        

class BSP_PT_ObjectPanel(bpy.types.Panel):
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "object"
    bl_label = "BSP Object"
    def draw(self, context):
        ob = context.object
        layout = self.layout
        col = layout.column()
        row = col.row(align=True)
        row.prop(ob, "object_type", expand=True)
        col.separator()
        if ob.object_type == 'ENTITY':
            row = col.row(align=True)
            row.label(text='Entity group')
            row.prop(ob, "entity_group", text="")
            s = ob.entity_group
            row = col.row(align=True)
            row.label(text='Entity type')
            row.prop(ob, f"{s.lower()}_type", text="")
            
        if ob.object_type == 'VOLUME':
            row = col.row(align=True)
            row.prop(ob, "volume_type", text=ob.volume_type)
            
            if ob.volume_type == 'FOG':
                row = col.row(align=True)
                row.label(text="Fog color")
                row.prop(ob, "fog_color", text="")
                row = col.row(align=True)
                row.label(text="Fog distance")
                row.prop(ob, "fog_distance", text="")
        if ob.object_type == 'TRIGGER':
            row = col.row(align=True)
            row.label(text="Target")
            row.prop(ob, "target", text="")
        

# ============================================================================================
#
#   MATERIAL PANEL
#
# ============================================================================================

class ArgList:
    def __init__(self, v):
        if v is None:
            return
        elif isinstance(v, str):
            self.type = v
            self.args = []
        elif isinstance(v, list):
            self.type = "list"
            self.args = [ArgList(x) for x in v]
        elif isinstance(v, dict):
            self.type = "enum"
            self.args = {k: ArgList(x) for k,x in v.items()}
        elif isinstance(v, ArgList):
            self.type = v.type
            self.args = v.args
        else:
            raise ValueError(f"Unknown type of argument '{v.__class__}'")
    
    @staticmethod
    def create(type, args):
        a = ArgList(None)
        a.type = type
        a.args = args
        return a
    
    @staticmethod
    def array(type, min=1, max=8):
        return ArgList.create("array", {"type": type, "min_size": min, "max_size": max})
    
    def validate(self, arg, context):
        # print(f"validating {self.type}: {arg}")
        if self.type == 'float':
            try:
                x = float(arg[0])
                return 1
            except:
                return f"Expected float number; got: '{arg}'"
        if self.type == 'int':
            try:
                x = int(arg[0])
                return 1
            except:
                return f"Expected integer number, got; '{arg}'"
        if self.type == 'vector3':
            try:
                for i in range(3):
                    x = float(arg[0][i])
                return 1
            except:
                return f"Expected vector of 3 float numbers; got: '{arg}'"
        if self.type == 'enum':
            # print(f"enum: {list(self.args.keys())}")
            if not isinstance(arg[0], str) or arg[0].lower() not in self.args:
                return f"Expected one of: " + ','.join([f"'{x}'" for x in self.args.keys()]) + f"; got: '{arg}'"
            r = self.args[arg[0].lower()].validate(arg[1:], context)
            if isinstance(r, str):
                return f"{arg[0]}: {r}"
            return r + 1
        if self.type == 'list':
            total = 0
            for i, a in enumerate(self.args):
                r = a.validate(arg[total:], context)
                if isinstance(r, str):
                    return f"Element {i}: {r}"
                total += r
            return total
        if self.type == 'texture':
            if not isinstance(arg[0], str):
                return f"Expected image path; got: {arg[0]}"
            if arg[0].startswith('$'):
                if arg[0] in ['$whitemap', '$lightmap']:
                    return 1
                return f"Expected map identifier; got {arg[0]}"
            i = BSP_Texture.find_image(None, arg[0], context)
            if i is None:
                return f"Expected image at '{arg[0]}'."
            return 1
        if self.type == 'skybox':
            if not isinstance(arg[0], str):
                return f"Expected skybox path; got: {arg[0]}"
            for i in ['bk', 'dn', 'ft', 'lf', 'rt' 'up']:
                n = arg[0] + f'_{i}'
                img = BSP_Texture.find_image(None, n, context)
                if img is None:
                    return f"Expected image at '{n}'."
            return 1
        if self.type == 'array':
            mn = self.args['min_size']
            mx = self.args['max_size']
            tp = self.args['type']
            #if len(arg) > mx and len(arg) < mn:
            #    return f"Expected {mn} to {mx} arguments of type '{tp}'"
            atp = ArgList.create(tp, None)
            total = 0
            for i, a in enumerate(arg):
                if i >= mx: return total
                r = atp.validate(i, context)
                if isinstance(r, str):
                    if i < mn:
                        return f"Array: {r}"
                    return total
                total += r
            return total
        if self.type == 'union':
            res = ""
            for i in self.args:
                r = i.validate(arg[0])
                if not isinstance(r, str): return r
                res += f"{r}\n"
            return f"Union: {res}"
        return f"Not implemented argument type '{self.type}'"
    
class ShaderEditor:
    syntax_wave_type = {
        "sin": [],
        "triangle": [],
        "square": [],
        "sawtooth": [],
        "inversesawtooth": []
    }
    syntax_general = {
        "skyparms": ['skybox', 'float', 'skybox'],
        "cull": {"front": [], "back": [], "disable": [], "none": []},
        "deformvertexes": [{
            "wave": [
                'float', # div
                syntax_wave_type,
                'float', 'float', 'float', 'float',
            ],
            "normal": [
                'float',
                syntax_wave_type,
                'float', 'float', 'float',
            ],
            "bulge": ['float', 'float', 'float'],
            "move": [
                'float', 'float', 'float',
                syntax_wave_type,
                'float', 'float', 'float', 'float',
            ],
            "autosprite": [],
            "autosprite2": []
        }],
        "fogparms": ['vector3', 'float'],
        "nopicmip": [],
        "nomipmaps": [],
        "polygonoffset": [],
        "portal": [],
        "sort": 'int',
        "surfaceparm": [{k: [] for k in qsl.surfparms}]
    }
    syntax_blend_mode = {
        'gl_one': [],
        'gl_zero': [],
        'gl_dst_color': [],
        'gl_one_minus_dst_color': [],
        'gl_src_color': [],
        'gl_one_minus_src_color': [],
        'gl_dst_alpha': [],
        'gl_one_minus_dst_alpha': [],
        'gl_src_alpha': [],
        'gl_one_minus_src_alpha': [],
    }
    syntax_pass = {
        "map": "texture",
        "clampmap": "texture",
        "animmap": ["float", ArgList.array("texture", 1, 8)],
        "blendfunc": [syntax_blend_mode, syntax_blend_mode],
        "rgbgen": {
            "identitylighting": [],
            "identity": [],
            "overbright": [],
            "wave": [syntax_wave_type, "float", "float", "float", "float"],
            "entity": [],
            "oneminusentity": [],
            "vertex": [],
            "oneminusvertex": [],
            "lightingdiffuse": [],
            "const": "vector3"
        },
        "alphagen": {
            "portal": ArgList.array("int", 0, 1),
            "specular": [],
            "wave": [syntax_wave_type, "float", "float", "float", "float"],
            "entity": [],
            "oneminusentity": [],
            "vertex": [],
            "oneminusvertex": [],
        },
        "tcgen": {
            "base": [],
            "lightmap": [],
            "envinroment": [],
            "vector": ["vector3", "vector3"],
        },
        "tcmod": {
            "rotate": ["float", "float"],
            "scale": ["float", "float"],
            "scroll": ["float", "float"],
            "transform": ["float", "float", "float", "float", "float", "float"],
            "stretch": [syntax_wave_type,
                "float", "float", "float", "float"],
            "turb": ["float", "float", "float", "float"],
        },
        "depthfunc": {
            "lequal": [],
            "equal": []
        },
        "depthwrite": [],
        "detail": [],
        "alphafunc": {
            "gt0": [],
            "lt128": [],
            "ge128": [],
        }
    }
    @staticmethod
    def parse(shader_code, mode='QSL'):
        if mode == 'QSL':
            shs = list(qsl.parse_shader(shader_code, False))
            if len(shs) < 1:
                raise ValueError("No shaders parsed")
            return shs[0]
        elif mode == 'JSON':
            return json.loads(shader_code)
        raise KeyError(f'Unknown mode {mode}')
        
    @staticmethod
    def shader_validate(sh, context):
        st_prop = ArgList(ShaderEditor.syntax_general)
        errors = []
        for p in sh.get("properties", []):
            for k,v in p.items():
                if k.startswith('qer_') or k.startswith('q3map_'):
                    continue
                r = st_prop.validate([k, *v], context)
                if isinstance(r, str):
                    errors.append(r)
                    continue
        
        st_pass = ArgList(ShaderEditor.syntax_pass)
        for i, m in enumerate(sh.get("pass", [])):
            map_defined = False
            singles = set()
            for p in m:
                for k, v in p.items():
                    r = st_pass.validate([k, *v], context)
                    if isinstance(r, str):
                        errors.append(f'[Pass {i}] {r}')
                        continue
                
                    if k in ['map', 'clampmap', 'animmap']:
                        if map_defined:
                            errors.append(f"More than one map defined at pass {i}")
                        map_defined = True
                        continue
                    if k in ['blendfunc', 'alphafunc', 'rgbgen', 'alphagen', 'tcgen', 'depthwrite', 'depthfunc', 'detail']:
                        if k in singles:
                            errors.append(f"More than one '{k}' defined at pass {i}")
                        singles.add(k)
                        continue
                    if k == 'tcmod':
                        if len(v) > 0 and v[0] in ["rotate","scale","scroll","transform","stretch","turb"]:
                            if v[0] in singles:
                                errors.append(f"More than one '{k}' of '{v[0]}' type defined at pass {i}")
                            singles.add(v[0])
                        continue
                    errors.append(f"Unknown property '{k}' defined at pass {i}")
        return errors
    

class BSP_OP_CreateMaterialShader(bpy.types.Operator):
    bl_idname = "bsp.material_create_shader"
    bl_label = "Create shader"
    bl_description = "Create material as shader"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        BSP_Texture.create_texture(context.material.name, 'SHADER', None, '{\n}', context).copy_to(context.material.texture)
        set_preview_image(context.scene, context.material.texture.image)
        MaterialHelper.update_material(context.material, context)
        return {'FINISHED'}
class BSP_OP_CreateMaterialImage(bpy.types.Operator):
    bl_idname = "bsp.material_create_image"
    bl_label = "Create image"
    bl_description = "Create material as image"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        tt = BSP_Texture.find_texture(tex.image, context)
        if tt:
            tt.copy_to(tex)
        else:
            BSP_Texture.create_texture(context.material.texture.image.name, 'IMAGE', context.material.texture.image, '{\n}', context).copy_to(context.material.texture)
        set_preview_image(context.scene, context.material.texture.image)
        MaterialHelper.update_material(context.material, context)
        return {'FINISHED'}
    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)
    def draw(self, context):
        row = self.layout.row(align=True)
        row.label(text="Image")
        row.template_ID(context.material.texture, 'image', text='')
    @classmethod
    def poll(cls, context):
        return context.material and True
class BSP_OP_EditImage(bpy.types.Operator):
    bl_idname = "bsp.material_edit_image"
    bl_label = "Create image"
    bl_description = "Create material as image"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        tex = context.material.texture
        tt = BSP_Texture.find_texture(tex.image, context)
        if tt:
            tt.copy_to(tex)
        else:
            if tex.image:
                tex.path = tex.image.name
                tex.pak = ''
        set_preview_image(context.scene, tex.image)
        MaterialHelper.update_material(context.material, context)
        return {'FINISHED'}
    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)
    def draw(self, context):
        row = self.layout.row(align=True)
        row.label(text="Image")
        row.template_ID(context.material.texture, 'image', text='')

class BSP_OP_EditShader(bpy.types.Operator):
    bl_idname = "bsp.edit_shader"
    bl_label = "Edit"
    bl_description = "Edit shader text"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        from uuid import uuid4
        mat = context.material
        if not mat.texture.is_custom():
             mat.texture.make_custom(mat.name, context)
             
        is_new = False
        for i in bpy.data.texts:
            if i.bsp_material == mat:
                t = i
                break
        else:
            n = str(f"{mat.name}-{uuid4()}.qsl")
            t = bpy.data.texts.new(name=n)
            t.bsp_material = mat
            is_new = True
        n = t.name
        sh = json.loads(mat.texture.shader)
        sh['name'] = mat.texture.path
        text = t.as_string().replace(' ', '')
        stext = qsl.shader_formatted_print(sh)
        if not is_new and text != stext.replace(' ', ''):
            self.report({'ERROR'}, f"Shader text changed. Apply or reset changes first.")
        else:
            t.from_string(stext)
        te = [x for x in bpy.context.screen.areas if x.type == "TEXT_EDITOR"]
        if len(te) == 0:
            self.report({'INFO'}, f"Open '{n}' in text editor to edit shader")
            return {'FINISHED'}
        
        te[0].spaces[0].text = t
        return {'FINISHED'}
    
    @classmethod
    def poll(cls, context):
        return context.material and context.material.texture.type == 'SHADER' and context.material.texture.shader

class BSP_PT_MaterialPanel(bpy.types.Panel):
    bl_space_type = 'PROPERTIES'
    bl_region_type = 'WINDOW'
    bl_context = "material"
    bl_label = "BSP Material"
    def draw(self, context):
        layout = self.layout
        mat = context.material
        if mat.texture.path:
            row = layout.row()
            row.label(text=f'Type is {mat.texture.type}')
            row = layout.row(align=True)
            if mat.texture.type == 'SHADER':
                row.operator(BSP_OP_EditShader.bl_idname, text='Edit shader')
            else:
                row.operator(BSP_OP_EditImage.bl_idname, text='Edit image')
            col = layout.column()
            preview(context.scene.bsp_material_preview, mat.texture, col)
        else:
            row = layout.row(align=True)
            row.operator(BSP_OP_CreateMaterialImage.bl_idname)
            row.operator(BSP_OP_CreateMaterialShader.bl_idname)
    @classmethod
    def poll(cls, context):
        return context.material is not None

class BSP_OP_ValidateShader(bpy.types.Operator):
    bl_idname = "bsp.validate_shader"
    bl_label = "Validate"
    bl_description = "Validate shader text"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        tx = context.area.spaces[0].text
        text = tx.as_string()
        try:
            shader = ShaderEditor.parse(text)
        except ValueError as e:
            errors.append(str(e))
        errors = ShaderEditor.shader_validate(shader, context)
        if len(errors) > 0:
            for e in errors:
                self.report({'ERROR'}, e)
            return {'FINISHED'}
        
        self.report({'INFO'}, "Shader is valid")
        return {'FINISHED'}
    @classmethod
    def poll(cls, context):
        tx = context.area.spaces[0].text
        return tx.bsp_material and tx.bsp_material.texture.type == 'SHADER'
    
class BSP_OP_SaveShader(bpy.types.Operator):
    bl_idname = "bsp.save_shader"
    bl_label = "Save shader"
    bl_description = "Compile and save shader into material"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        tx = context.area.spaces[0].text
        # [TODO] validate!
        text = tx.as_string()
        try:
            shader = ShaderEditor.parse(text)
        except ValueError as e:
            errors.append(str(e))
        errors = ShaderEditor.shader_validate(shader, context)
        if len(errors) > 0:
            for e in errors:
                self.report({'ERROR'}, e)
            return {'FINISHED'}
        tx.bsp_material.texture.shader = json.dumps(shader)
        self.report({'INFO'}, f"Shader 'textures/<custom>/{tx.bsp_material.texture.path}' saved")
        MaterialHelper.update_material(tx.bsp_material, context)
        return {'FINISHED'}
    @classmethod
    def poll(cls, context):
        tx = context.area.spaces[0].text
        return tx.bsp_material and tx.bsp_material.texture.type == 'SHADER'

class BSP_OP_ResetShader(bpy.types.Operator):
    bl_idname = "bsp.reset_shader"
    bl_label = "Reset shader"
    bl_description = "Reset shader to saved at material"
    bl_options = {'REGISTER', 'INTERNAL'}
    def execute(self, context):
        tx = context.area.spaces[0].text
        mat = tx.bsp_material
        sh = json.loads(mat.texture.shader)
        sh['name'] = mat.texture.path
        tx.from_string(qsl.shader_formatted_print(sh))
        return {'FINISHED'}
    def invoke(self, context, event):
        return context.window_manager.invoke_props_dialog(self)
    def draw(self, context):
        self.layout.label(text="Are you sure want to reset shader?")
    @classmethod
    def poll(cls, context):
        tx = context.area.spaces[0].text
        return tx.bsp_material and tx.bsp_material.texture.type == 'SHADER'

class BSP_PT_ShaderEditing(bpy.types.Panel):
    bl_space_type = "TEXT_EDITOR"
    bl_region_type = "UI"
    bl_label = "Shader Editor"
    bl_category = "BSP"
    bl_options = {'DEFAULT_CLOSED'}
    
    def draw(self, context):
        self.layout.operator(BSP_OP_ValidateShader.bl_idname)
        pass

def add_shader_editor_selection():
    handle_name = "__shader_editor_func"
    if handle_name in bpy.app.driver_namespace:
        bpy.types.TEXT_HT_header.remove(bpy.app.driver_namespace[handle_name])
        del bpy.app.driver_namespace[handle_name]
        
    def use_as_shader_editor_draw(self, context):
        area = context.area
        self.layout.separator()
        row = self.layout.row(align=True)
        row.label(text='QSL')
        row.operator(BSP_OP_SaveShader.bl_idname, text='', icon='FILE_TICK')
        row.operator(BSP_OP_ResetShader.bl_idname, text='', icon='FILE_REFRESH')
#        self.layout.operator(BSP_OP_SelectAsShaderEditor.bl_idname, depress=context.scene.bsp_shader_editor == area, text='', icon='SHADING_RENDERED')
    bpy.app.driver_namespace[handle_name] = use_as_shader_editor_draw
    bpy.types.TEXT_HT_header.append(bpy.app.driver_namespace[handle_name])
    
# ============================================================================================
#
#   HANDLERS
#
# ============================================================================================

def add_active_material_update_tracker():
    from bpy.app.handlers import persistent
    handle_name = "__material_updated_func"
    if handle_name in bpy.app.driver_namespace:
        try:
            bpy.app.timers.unregister(bpy.app.driver_namespace[handle_name])
        except ValueError:
            pass
        del bpy.app.driver_namespace[handle_name]
    @persistent
    def active_material_updated():
        if not bpy.context.object:
            return
        material = bpy.context.object.active_material
        update_material_preview_image(bpy.context.scene, material)

        # check again once every second.
        return 0.5
    bpy.app.driver_namespace[handle_name] = active_material_updated
    bpy.app.timers.register(bpy.app.driver_namespace[handle_name])
    
def add_referenced_objects_cleanup():
    from bpy.app.handlers import persistent
    handle_name = "__referenced_objects_cleanup_func"
    if handle_name in bpy.app.driver_namespace:
        try:
            bpy.app.timers.unregister(bpy.app.driver_namespace[handle_name])
        except ValueError:
            pass
        del bpy.app.driver_namespace[handle_name]
    @persistent
    def referenced_objects_cleanup():
        to_del = []
        for o in bpy.data.objects:
            if bpy.context.scene.objects.get(o.name) is not None:
                continue
            to_del.append(o)
        for o in to_del:
            bpy.data.objects.remove(o)
        return 1.0
    bpy.app.driver_namespace[handle_name] = referenced_objects_cleanup
    bpy.app.timers.register(bpy.app.driver_namespace[handle_name])
    

def set_preview_image(sc, image):
    key = ".bsp_preview_texture"
    if key not in bpy.data.textures:
        sc.bsp_preview = bpy.data.textures.new(name=key, type="IMAGE")
        sc.bsp_preview.extension='CLIP'
    else:
        sc.bsp_preview = bpy.data.textures[key]
    sc.bsp_preview.image = image
    

def update_material_preview_image(sc, material):
    key = ".bsp_material_preview_texture"
    if key not in bpy.data.textures:
        sc.bsp_material_preview = bpy.data.textures.new(name=key, type="IMAGE")
        sc.bsp_material_preview.extension='CLIP'
    else:
        sc.bsp_material_preview = bpy.data.textures[key]
    
    if sc.bsp_material_preview.image != material.texture.image:
        sc.bsp_material_preview.image = material.texture.image

def on_selected_texture_updated(sc, _context):
    if sc.bsp_selected_texture < 0 or sc.bsp_selected_texture >= len(sc.bsp_textures):
        return
    set_preview_image(sc, sc.bsp_textures[sc.bsp_selected_texture].image)

def on_disable_lightmap_updated(sc, _content):
    for m in bpy.data.materials:
        MaterialHelper.set_lightmap_disabled(m, sc.bsp_disable_lightmap)
classes = [
    BSP_OP_ResizeLightmap,
    BSP_OP_AssignLightmap,
    BSP_OP_SelectLightmap,
    BSP_OP_BakeLightmap,
    BSP_Lightmap,
    BSP_UL_LightmapImage,
    BSP_Texture,
    BSP_UL_TextureImage,
    BSP_MT_TextureGroupSelectionMenu,
    BSP_OP_TextureAssignMaterial,
    BSP_OP_TexturesRemoveUnused,
    BSP_OP_TexturesCleanup,
    BSP_OP_LoadPaks,
    BSP_OP_SetupDirectory,
    BSP_PT_ScenePanel,
    BSP_PT_ObjectPanel,
    BSP_PT_MaterialPanel,
    BSP_OP_MaterialUpdatePreview,
    BSP_OP_EditImage,
    BSP_OP_CreateMaterialShader,
    BSP_OP_CreateMaterialImage,
    BSP_OP_ValidateShader,
    BSP_OP_EditShader,
    BSP_OP_SaveShader,
    BSP_OP_ResetShader,
    BSP_PT_ShaderEditing,
]

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
oprops = {
    f"{k.lower()}_type": bpy.props.EnumProperty(items=[
        (x.replace(' ', '_').upper(), x, "") for x, _ in v.items()
    ]) for k,v in entities.items()
}
props = [
    (bpy.types.Scene, {
        "bsp_textures": bpy.props.CollectionProperty(type=BSP_Texture),
        "bsp_selected_texture": bpy.props.IntProperty(update=on_selected_texture_updated),
        "bsp_lightmaps": bpy.props.CollectionProperty(type=BSP_Lightmap),
        "bsp_selected_lightmap": bpy.props.IntProperty(),
        "bsp_disable_lightmap": bpy.props.BoolProperty(update=on_disable_lightmap_updated),
        "bsp_preview": bpy.props.PointerProperty(type=bpy.types.Texture),
        "bsp_material_preview": bpy.props.PointerProperty(type=bpy.types.Texture),
        "bsp_executable_path": bpy.props.StringProperty(), # update=on_executable_changed), # not allow to edit as text
    }),
    (bpy.types.Text, {
        "bsp_material": bpy.props.PointerProperty(type=bpy.types.Material),
    }),
    (bpy.types.Material, {
        "material_type": bpy.props.EnumProperty(
            description="Special type of surface",
            items = [
                ('DETAIL', 'Detail', "Detail face without collision", 1),
                ('STRUCTURAL', 'Structural', "Structural face, that affects bsp tree and visibility", 2),
                ('PORTAL', 'Portal', "Face that only affects bsp, may be used to separate zones", 3),
            ]
        ),
        # default face properties
        "texture":      bpy.props.PointerProperty(type=BSP_Texture),
        "translucent":  bpy.props.BoolProperty(),
        "slick":        bpy.props.BoolProperty(),
        "noimpact":     bpy.props.BoolProperty(),
        "nomarks":      bpy.props.BoolProperty(),
        "nodamage":     bpy.props.BoolProperty(),
        "dust":         bpy.props.BoolProperty(),
        "noob":         bpy.props.BoolProperty(),
        "surface_type": bpy.props.EnumProperty(
            description = "Steps sound",
            items = [('DEFAULT', 'Default', "Default sound"),
                    ('METAL', 'Metal', "Metal steps"),
                    ('FLESH', 'Flesh', "Flesh sound"),
                    ('NOSOUND', 'NoSound', "No sound")]
        ),
        # portal properties
        "portal_type": bpy.props.EnumProperty(
            description = "Portal type",
            items = [('HINT', 'Hint', "Prioritized at bsp subdivision"),
                    ('ZONEPORTAL', 'ZonePortal', "Split zones with this plane"),]
        )
    }),
    (bpy.types.Object, {
        **oprops,
        # service params
        "bsp_selected_lightmap": bpy.props.IntProperty(),
        # common params
        "object_type":  bpy.props.EnumProperty(
            description="Object properties",
            items=[
                ('GEOMETRY', 'Geo', "Default geometry of the level"),
                ('ENTITY', 'Entity', "Entity (door, lift, etc)"),
                ('VOLUME', 'Volume', "Special type of block"),
                ('TRIGGER', 'Trigger', "Triggers an event"),
                ('ZONEINFO', 'Zone', "Marks areas")
            ]
        ),
        # [TODO] entity params
        "entity_group": bpy.props.EnumProperty(
            items=[(x.replace(' ', '_').upper(), x, "") for x in entities.keys()]
        ),
        # volume params
        "volume_type":    bpy.props.EnumProperty(
            items=[
                ('PLAYERCLIP', 'PlayerClip', "Blocks player, pass shots"),
                ('FULLCLIP', 'FullClip', "Blocks everything"),
                ('WATER', 'Water', "Swimmable water"),
                ('LAVA', 'Lava', "Lava"),
                ('SLIME', 'Slime', "Slime"),
                ('FOG', 'Fog', "Fog"),
                ('NODROP', 'NoDrop', "No drops are inside"),
                # [TODO] more
            ]
        ),
        "fog_color":    bpy.props.FloatVectorProperty(subtype='COLOR', size=3, default=[0.3,0.3,0.3]),
        "fog_distance": bpy.props.IntProperty(min=0, max=10000),

        # trigger params
        "target": bpy.props.PointerProperty(type=bpy.types.Object, update=on_target_update, poll=filter_target),
        # zoneinfo params
    })
]


def register():
    for cls in classes:
        bpy.utils.register_class(cls)
    
    for tp, d in props:
        for k, v in d.items():
            setattr(tp, k, v)
        
    if tx_preview not in bpy.data.textures:
        bpy.data.textures.new(name=tx_preview, type="IMAGE")
    if tx_material_preview not in bpy.data.textures:
        bpy.data.textures.new(name=tx_material_preview, type="IMAGE")

    while len(bpy.context.scene.bsp_lightmaps) < 8:
        bpy.context.scene.bsp_lightmaps.add()
        
    for i, lm in enumerate(bpy.context.scene.bsp_lightmaps):
        lm.init(1024, i)
    add_shader_editor_selection()
    add_active_material_update_tracker()
    add_referenced_objects_cleanup()
    add_target_arrow()

def unregister():
    for cls in classes:
        clst = cls.__bases__[0].bl_rna_get_subclass_py(cls.__name__)
        if clst:
            bpy.utils.unregister_class(clst)

    for tp, d in props:
        for k in d:
            if hasattr(tp, k):
                delattr(tp, k)
    if tx_preview in bpy.data.textures:
        bpy.data.textures.remove(bpy.data.textures[tx_preview])
    if tx_material_preview in bpy.data.textures:
        bpy.data.textures.remove(bpy.data.textures[tx_material_preview])

if __name__ == '__main__':
    unregister()
    register()