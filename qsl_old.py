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
        if len(i) < 2: continue
        if i[0] != 'surfaceparm': continue
        if i[1] not in surfparms: continue
        i_c, i_ca, i_s, i_sa = surfparms[i[1]]
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
    return [x for x in space_re.split(line) if x]
    
def read_shader(shader_io):
    return parse_shader_2(shader_io.read().decode('utf-8'))

def parse_shader_2(shader_code, verbose=False):
    state = 0
    # 0 - look for shader (beginning of the line)
    # 1 - body
    # 2 - pass
    text = shader_code # [NOTE] expected only ascii symbols, but comment may contain more
    lng = len(text)
    space = [' ', '\t']
    line_break = ['\n', '\r']
    ln = 0
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
                raise ValueError(f"Unexpected beginning of shader body at line: {ln} pos: {ps}")
            if state == 2:
                raise ValueError(f"Unexpected '{{' at line: {ln} pos: {ps}")
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
                raise ValueError(f"Unexpected '}}' at line: {ln} pos: {ps}")
            if state == 1: # body ended
                yield {"name": curname, "properties": props, "passes": passes}
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
            prop = parse_property(s)
            lprop = len(prop)
            if lprop == 0: continue
            if lprop == 1:
                props.append({prop[0]: None})
                continue
            if lprop == 2:
                props.append({prop[0]: prop[1]})
                continue
            props.append({prop[0]: prop[1:]})
                
        elif state == 2:
            prop = parse_property(s)
            if len(prop) > 0:
                curpass.append({prop[0]: prop[1:]})

    if state != 0:
        raise ValueError(f"Unexpected end of file at mode: {state}")
        
def shader_formatted_print_2(shader):
    s = shader["name"] + '\n'
    if "contents" not in shader or "flags" not in shader:
        shader["contents"], shader["flags"] = build_surfparam(shader["properties"])
        
    s += f"// contents: 0x{shader.get('contents'):x}\n"
    s += f"// flags: 0x{shader.get('flags'):x}\n"
    tab = " " * 2
    s += "{\n"
    for i in shader["properties"]:
        s += tab + " ".join(i) + "\n"
    for i in shader["pass"]:
        s += tab + "{\n"
        for j in i["properties"]:
            s += tab + tab + " ".join(j) + "\n"
        s += tab + "}\n"
    s += "}\n\n"
    return s


def parse_pass(shader_io):
    properties = []
    while True:
        ln = l(shader_io)
        if ln is None: return None
        if open_brace_re.fullmatch(ln): return None # unexpected opening brace
        if close_brace_re.fullmatch(ln): break
        if ln == '': continue
        properties.append(parse_property(ln))
    if len(properties) == 0:
        return {}
    return {"properties": properties}

def parse_shader(shader_io: IO[bytes]):
    while True:
        name = l(shader_io)
        if name is None: return None
        if name != '':
            break
    
    # look for open brace
    while True:
        ln = l(shader_io)
        if ln is None: raise ValueError(f"Error parsing shader '{name}': no data")
        if open_brace_re.fullmatch(ln): break
    
    # parse members
    properties = []
    passes = []
    while True:
        ln = l(shader_io)
        if ln is None: raise ValueError(f"Error parsing shader '{name}': no closing brace")
        if ln == '': continue # just an empty line or comment
        if open_brace_re.fullmatch(ln):
            ps = parse_pass(shader_io)
            if ps is None: raise ValueError(f"Error parsing shader '{name}': unexpected error while parsing shader pass")
            if len(ps) != 0: passes.append(ps)
            continue
        if close_brace_re.fullmatch(ln): break # end of shader
        properties.append(parse_property(ln))

    return { "name": name, "properties": properties, "pass": passes }

def shader_formatted_print(shader):
    s = shader["name"] + '\n'
    if "contents" not in shader or "flags" not in shader:
        shader["contents"], shader["flags"] = build_surfparam(shader["properties"])
        
    s += f"// contents: 0x{shader.get('contents'):x}\n"
    s += f"// flags: 0x{shader.get('flags'):x}\n"
    tab = " " * 2
    s += "{\n"
    for i in shader["properties"]:
        s += tab + " ".join(i) + "\n"
    for i in shader["pass"]:
        s += tab + "{\n"
        for j in i["properties"]:
            s += tab + tab + " ".join(j) + "\n"
        s += tab + "}\n"
    s += "}\n\n"
    return s

def parse_shader_file(shader_io):
    while True:
        s = parse_shader(shader_io)
        if s is None: return 
        yield s

def get_references(shader):
    for m in shader.get("pass", []):
        for p in m.get("properties", []):
            if len(p) < 2: continue
            cmd = p[0].lower()
            if cmd == 'map' or cmd == 'clampmap':
                if p[1].startswith('$'): continue
                yield p[1]
                continue
            if cmd == 'animmap':
                for mp in p[2:]:
                    if mp.startswith('$'): continue
                    yield mp

def get_image(shader):
    for p in shader.get("properties", []):
        if p[0].lower() == 'qer_editorimage':
            return p[1]
    
    for m in shader.get("pass", []):
        for p in m.get("properties", []):
            cmd = p[0].lower()
            if cmd == 'map' or cmd == 'clampmap':
                if p[1].startswith('$'): continue
                return p[1]
            if cmd == 'animmap':
                for mp in p[2:]:
                    if mp.startswith('$'): continue
                    return mp
    return None

def to_name(path):
    return "__".join(path.split('/')[1:])

texgroup_re = re.compile("textures/([^/]*)/.*")
def pk3_shaders(pak):
    return [x for x in pak.filelist if x.filename.startswith('scripts/') and x.filename.endswith(".shader")]
def pk3_parse_shaders(pak, root_dir):
    shaders = []
    pakname = os.path.relpath(pak.filename, root_dir)
    for sh in pk3_shaders(pak):
        with pak.open(sh) as f:
            s = list(parse_shader_file(f))
            for ss in s:
                ss["shader"] = sh.filename
                ss["pak"] = pakname
            shaders += s
    for sh in shaders:
        sh["contents"], sh["flags"] = build_surfparam(sh["properties"])

    groups = {}
    for sh in shaders:
        g = texgroup_re.match(sh["name"])
        if not g: continue
        g = g[1]
        if g not in groups:
            groups[g] = []
        groups[g].append(sh)

    return groups

def pk3_textures(pak, root_dir):
    groups = {}
    pakname = os.path.relpath(pak.filename, root_dir)
    all_tex = [{"path": x.filename, "pak": pakname} for x in pak.filelist 
        if x.filename.startswith('textures/') and (
            x.filename.lower().endswith('.tga') or x.filename.lower().endswith('.jpg')
        )]
    for p in all_tex:
        g = texgroup_re.match(p['path'])
        if not g: continue
        g = g[1]
        if g not in groups:
            groups[g] = []
        groups[g].append(p)

    return groups

def list_textures(pak, root_dir):
    return pk3_parse_shaders(pak, root_dir), pk3_textures(pak, root_dir)

def merge_groups(g0, g1):
    return { k: g0.get(k, []) + g1.get(k, []) for k in set(g0).union(g1) }

def append_groups(to, frm):
    for k,v in frm.items():
        to[k] = to.get(k, []) + v
        