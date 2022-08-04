from colorsys import yiq_to_rgb
from typing import IO, Optional
import re
import os

Q_CONT_SOLID = 1                # /* an eye is never valid in a solid */
Q_CONT_LAVA = 0x8
Q_CONT_SLIME = 0x10
Q_CONT_WATER = 0x20
Q_CONT_FOG = 0x40
Q_CONT_AREAPORTAL = 0x8000
Q_CONT_PLAYERCLIP = 0x10000
Q_CONT_MONSTERCLIP = 0x20000
Q_CONT_TELEPORTER = 0x40000
Q_CONT_JUMPPAD = 0x80000
Q_CONT_CLUSTERPORTAL = 0x100000
Q_CONT_DONOTENTER = 0x200000
Q_CONT_BOTCLIP = 0x400000
Q_CONT_ORIGIN = 0x1000000       # /* removed before bsping an entity */
Q_CONT_BODY = 0x2000000         # /* should never be on a brush, only in game */
Q_CONT_CORPSE = 0x4000000
Q_CONT_DETAIL = 0x8000000       # /* brushes not used for the bsp */
Q_CONT_STRUCTURAL = 0x10000000  # /* brushes used for the bsp */
Q_CONT_TRANSLUCENT = 0x20000000 # /* don't consume surface fragments inside */
Q_CONT_TRIGGER = 0x40000000
Q_CONT_NODROP = 0x80000000      # /* don't leave bodies or items (death fog, lava) */
Q_SURF_NODAMAGE = 0x1           # /* never give falling damage */
Q_SURF_SLICK = 0x2              # /* effects game physics */
Q_SURF_SKY = 0x4                # /* lighting from environment map */
Q_SURF_LADDER = 0x8
Q_SURF_NOIMPACT = 0x10          # /* don't make missile explosions */
Q_SURF_NOMARKS = 0x20           # /* don't leave missile marks */
Q_SURF_FLESH = 0x40             # /* make flesh sounds and effects */
Q_SURF_NODRAW = 0x80            # /* don't generate a drawsurface at all */
Q_SURF_HINT = 0x100             # /* make a primary bsp splitter */
Q_SURF_SKIP = 0x200             # /* completely ignore, allowing non-closed brushes */
Q_SURF_NOLIGHTMAP = 0x400       # /* surface doesn't need a lightmap */
Q_SURF_POINTLIGHT = 0x800       # /* generate lighting info at vertexes */
Q_SURF_METALSTEPS = 0x1000      # /* clanking footsteps */
Q_SURF_NOSTEPS = 0x2000         # /* no footstep sounds */
Q_SURF_NONSOLID = 0x4000        # /* don't collide against curves with this set */
Q_SURF_LIGHTFILTER = 0x8000     # /* act as a light filter during q3map -light */
Q_SURF_ALPHASHADOW = 0x10000    # /* do per-pixel light shadow casting in q3map */
Q_SURF_NODLIGHT = 0x20000       # /* don't dlight even if solid (solid lava, skies) */
Q_SURF_DUST = 0x40000           # /* leave a dust trail when walking on this surface */
Q_SURF_NOOB = 0x80000           # /* no overbounce surface */

Q_SURF_VERTEXLIT = ( Q_SURF_POINTLIGHT | Q_SURF_NOLIGHTMAP )

C_SOLID = 0x1
C_DETAIL = 0x2
C_TRANSLUCENT = 0x4

surfparms = {
    "default": (Q_CONT_SOLID, -1, 0, -1), 							# default
    "origin": (Q_CONT_ORIGIN, Q_CONT_SOLID, 0, 0), 					# [TODO] what teh heck?
    "structural": (Q_CONT_STRUCTURAL, 0, 0, 0), 					# object flag (flag: structural)
    "detail": (Q_CONT_DETAIL, 0, 0, 0), 							# object flag (flag: !structural)
    "nonsolid": (0, Q_CONT_SOLID, Q_SURF_NONSOLID, 0), 				# object flag (flag: !structural)
    "trans": (Q_CONT_TRANSLUCENT, 0, 0, 0), 						# material property (flag: translucent) [TODO] need a check
    "slick": (0, 0, Q_SURF_SLICK, 0),								# material property (flag: slick)
    "noimpact": (0, 0, Q_SURF_NOIMPACT, 0),							# material property (flag: noimpact)
    "nomarks": (0, 0, Q_SURF_NOMARKS, 0),							# material property (flag: nomarks)
    "nodamage": (0, 0, Q_SURF_NODAMAGE, 0),							# material property (flag: nodamage)
    "metalsteps": (0, 0, Q_SURF_METALSTEPS, 0),						# material property (surface_type: metal)
    "flesh": (0, 0, Q_SURF_FLESH, 0),								# material property (surface_type: flesh)
    "nosteps": (0, 0, Q_SURF_NOSTEPS, 0),							# material property (surface_type: empty)
    "dust": (0, 0, Q_SURF_DUST, 0),									# material property (flag: dust)
    "areaportal": (Q_CONT_AREAPORTAL, Q_CONT_SOLID, 0, 0), 			# material property (type: portal, flag: zone_border)
    "hint": (0, 0, Q_SURF_HINT, 0), 								# material property (type: portal, flag: !zone_border)
    "trigger": (Q_CONT_TRIGGER, Q_CONT_SOLID, 0, 0), 				# as special object (volume)
    "playerclip": (Q_CONT_PLAYERCLIP, Q_CONT_SOLID, 0, 0), 			# as special object (volume)
    "water": (Q_CONT_WATER, Q_CONT_SOLID, 0, 0), 					# as special object (volume)
    "slime": (Q_CONT_SLIME, Q_CONT_SOLID, 0, 0), 					# as special object (volume)
    "lava": (Q_CONT_LAVA, Q_CONT_SOLID, 0, 0), 						# as special object (volume)
    "nodrop": (Q_CONT_NODROP, Q_CONT_SOLID, 0, 0), 					# as special object (volume)
    "fog": (Q_CONT_FOG, Q_CONT_SOLID, 0, 0), 						# as special object (volume)
    "monsterclip": (Q_CONT_MONSTERCLIP, Q_CONT_SOLID, 0, 0), 		# unused
    "lightgrid": (0, 0, 0, 0),										# unused
    "antiportal": (0, 0, 0, 0),										# unused
    "skip": (0, 0, 0, 0),											# unused
    "nodraw": (0, 0, Q_SURF_NODRAW, 0), 							# unused
    "alphashadow": (0, 0, Q_SURF_ALPHASHADOW, 0), 					# unused
    "lightfilter": (0, 0, Q_SURF_LIGHTFILTER, 0), 					# unused
    "nolightmap": (0, 0, Q_SURF_VERTEXLIT, 0), 						# unused
    "pointlight": (0, 0, Q_SURF_VERTEXLIT, 0), 						# unused
    "clusterportal": (Q_CONT_CLUSTERPORTAL, Q_CONT_SOLID, 0, 0), 	# unused
    "donotenter": (Q_CONT_DONOTENTER, Q_CONT_SOLID, 0, 0), 			# unused
    "botclip": (Q_CONT_BOTCLIP, Q_CONT_SOLID, 0, 0), 				# unused
    "sky": (0, 0, Q_SURF_SKY, 0),									# unused
    "ladder": (0, 0, Q_SURF_LADDER, 0),								# unused
    "nodlight": (0, 0, Q_SURF_NODLIGHT, 0),							# unused
}

def build_surfparam(props):
    cont, _, surf, _ = surfparms["default"]
    for i in props:
        if 'surfaceparm' not in i: continue
        sp = i['surfaceparm']
        if len(sp) < 1: continue
        sp = sp[0]
        # if not isinstance(sp, str): continue # invalid value
        if sp not in surfparms: continue
        i_c, i_ca, i_s, i_sa = surfparms[sp]
        cont |= i_c
        cont &= ~i_ca
        surf |= i_s
        surf &= ~i_sa

    return cont, surf

comment_re = re.compile(r"\s*(([^/]|/[^/]|/$)*)\s*(//(.*))*")
def l(sh) -> Optional[str]:
    ln = sh.readline()
    if len(ln) == 0: return None
    return comment_re.match(ln.decode('utf-8'))[1].strip()

open_brace_re = re.compile(r"^\s*\{\s*$")
close_brace_re = re.compile(r"^\s*\}\s*$")
space_re = re.compile(r"\s\s*")

def parse_property(line):
    vars = list([x for x in space_re.split(line) if x])
    i = 0
    while i < len(vars):
        if vars[i] == '(':
            sub = []
            while i < len(vars):
                i += 1
                if vars[i] == ')': break
                sub.append(vars[i])
            yield sub
        else:
            yield vars[i]
        i += 1
    
def get_property_key(prop):
    prop.keys()

def parse_shader_file(shader_io):
    return list(parse_shader(shader_io.read().decode('utf-8')))

def parse_shader(shader_code, tolerate=True, verbose=False):
    state = 0
    # 0 - look for shader (beginning of the line)
    # 1 - body
    # 2 - pass
    text = shader_code # [NOTE] expected only ascii symbols, but comment may contain more
    lng = len(text)
    space = [' ', '\t']
    line_break = ['\n', '\r']
    ln = 0 # [FIXME] positions are mess!
    ps = 0
    curname = None
    props = []
    passes = []
    curpass = []
    i = 0
    def is_comment(x):
        return x + 1 < lng and text[x] == '/' and text[x+1] == '/'
    while i < lng:
        if text[i] in space:
            ps += 1
            i += 1
            continue
        if text[i] in line_break:
            ps = 0
            ln += 1
            i += 1
            continue
        if is_comment(i):
            if verbose: print(f"comment")
            # comment
            i += 2
            ps += 2
            while True:
                if i >= lng:
                    return
                if text[i] in line_break:
                    ln += 1
                    ps = 0
                    if text[i-1] != '\\':
                        break
                i += 1
                ps += 1
            i += 1
            if verbose: print(f"end comment")
            continue
        if text[i] == '{':
            if verbose: print(f"start of state: {state}")
            if curname is None:
                ermsg = f"Unexpected beginning of shader body at line: {ln} pos: {ps}"
                if tolerate: print(ermsg); return
                else: raise ValueError(ermsg)
            if state == 2:
                ermsg = f"Unexpected '{{' at line: {ln} pos: {ps}"
                if tolerate: print(ermsg); return
                else: raise ValueError(ermsg)
            if state == 1: # new pass started
                curpass = []
            elif state == 0: # new body started
                props = []
                passes = []
            state += 1
            ps += 1
            i += 1
            continue
        if text[i] == '}':
            if verbose: print(f"end of state: {state}")
            if state == 0:
                ermsg = f"Unexpected '}}' at line: {ln} pos: {ps}"
                if tolerate: print(ermsg); return
                else: raise ValueError(ermsg)
            if state == 1: # body ended
                yield {"name": curname, "properties": props, "pass": passes}
            elif state == 2: # pass ended
                if curpass: passes.append(curpass)
            state -= 1
            ps += 1
            i += 1
            continue
        # letter found
        s = ""
        while True:
            s += text[i]
            i += 1
            if text[i] in line_break:
                ln += 1
                ps = 0
                break
            if is_comment(i) or text[i] == '{' or text[i] == '}':
                ps += 1
                break
        if verbose: print("Read string: " + s)
        if state == 0:
            curname = s
        elif state == 1:
            prop = list(parse_property(s))
            if len(prop) == 0: continue
            props.append({prop[0].lower(): prop[1:]})
                
        elif state == 2:
            prop = list(parse_property(s))
            if len(prop) == 0: continue
            curpass.append({prop[0].lower(): prop[1:]})

    if state != 0:
        ermsg = f"Unexpected end of file at mode: {state}"
        if tolerate: print(ermsg); return
        else: raise ValueError(ermsg)
        
def property_print(prop):
    def print_v(p):
        for x in p:
            if isinstance(x, list):
                yield f"( {' '.join([str(y) for y in x])} )"
            else:
                yield str(x)
    for k, v in prop.items():
        yield f"{k}  {' '.join([x for x in print_v(v)])}"

def shader_formatted_print(shader):
    s = shader["name"] + '\n'
    if "contents" not in shader or "flags" not in shader:
        shader["contents"], shader["flags"] = build_surfparam(shader["properties"])
        
    s += f"// contents: 0x{shader.get('contents'):x}\n"
    s += f"// flags: 0x{shader.get('flags'):x}\n"
    tab = " " * 2
    s += "{\n"
    for prop in shader["properties"]:
        for i in property_print(prop):
            s += f"{tab}{i}\n"
    for ps in shader["pass"]:
        s += tab + "{\n"
        for prop in ps:
            for i in property_print(prop):
                s += f"{tab*2}{i}\n"
        s += tab + "}\n"
    s += "}\n\n"
    return s

def get_references(shader):
    for p in shader.get("properties"):
        mp = p.get("skyparms", [])
        if len(mp) > 2: # correctly defined
            if mp[0] != '-':
                # [FIXME] may be also tga =\
                for i in ["bk", "dn", "ft", "lf", "rt", "up"]:
                    yield f"{mp[0]}_{i}.jpg"
            if mp[2] != '-':
                for i in ["bk", "dn", "ft", "lf", "rt", "up"]:
                    yield f"{mp[2]}_{i}.jpg"

    for m in shader.get("pass", []):
        for p in m:
            mp = p.get('map', None)
            if mp and not mp[0].startswith('$'): yield mp[0]
            mp = p.get('clampmap', None)
            if mp and not mp[0].startswith('$'): yield mp[0]
            mp = p.get('animmap', None)
            if not mp: continue
            for m in mp:
                if not m.startswith('$'):
                    yield m

def get_image(shader):
    for p in shader.get("properties", []):
        mp = p.get('qer_editorimage', None)
        if mp and not mp[0].startswith('$'): return mp[0]
    
    maps = []
    for m in shader.get("pass", []):
        blend_priority = 0
        for p in m:
            if 'blendfunc' in p:
                if p['blendfunc'][0].lower() == 'add' or (p['blendfunc'][0].lower() == 'gl_one' and p['blendfunc'][1].lower() == 'gl_one'):
                    blend_priority = -1
                    break
                else:
                    blend_priority = 1
        for p in m:
            mp = p.get('map', None)
            if mp and not mp[0].startswith('$'): 
                maps.append((blend_priority, mp[0]))
                break
            mp = p.get('clampmap', None)
            if mp and not mp[0].startswith('$'):
                maps.append((blend_priority, mp[0]))
                break
            mp = p.get('animmap', None)
            if not mp: continue
            found = False
            for m in mp:
                if not m.startswith('$'):
                    maps.append((blend_priority, mp[0]))
                    found = True
                    break
            if found: break
    if len(maps) < 1: return None
    maps = sorted(maps, key=lambda x: x[0])
    return maps[-1][1]

texgroup_re = re.compile("(textures|env)/([^/]*)/.*")

def get_group(path):
    s = path.split('/')
    if s[0] not in ("textures", "env"): return None
    if len(s) < 2: return None
    return s[1] if len(s) > 2 else '<empty>'
#    alternate regex version
#    g = texgroup_re.match(path)
#    if not g: return None
#    return g[2]

def match_group(group, path):
    s = path.split('/')
    if len(s) < 2: return False
    if group == '<empty>' and len(s) == 2: return True
    return s[1].lower() == group.lower()

def pk3_shaders(pak):
    return [x for x in pak.filelist if x.filename.startswith('scripts/') and x.filename.endswith(".shader")]
def pk3_parse_shaders(pak):
    shaders = []
    for sh in pk3_shaders(pak):
        with pak.open(sh) as f:
            s = list(parse_shader_file(f))
        shaders += s
    return shaders

def pk3_parse_textures(pak):
    return [x.filename for x in pak.filelist 
        if (x.filename.startswith('textures/') or x.filename.startswith('env/')) and (
            x.filename.lower().endswith('.tga') or x.filename.lower().endswith('.jpg')
        )]

def list_textures(pak, root_dir):
    return pk3_parse_shaders(pak, root_dir), pk3_parse_textures(pak, root_dir)

def merge_groups(g0, g1):
    return { k: g0.get(k, []) + g1.get(k, []) for k in set(g0).union(g1) }

def append_groups(to, frm):
    for k,v in frm.items():
        to[k] = to.get(k, []) + v
        
class ShaderPass:
    def __init__(self, params):
        self.tcmod = []
        parkeys = ['map', 'rgbgen', 'alphagen', 'tcgen', 'blendfunc', 'depthfunc', 'depthwrite', 'detail', 'alphafunc']
        for i in params:
            for k in parkeys:
                if k in i:
                    self.map = i[k]
            if 'tcmod' in i:
                self.tcmod.append(i['tcmod'])