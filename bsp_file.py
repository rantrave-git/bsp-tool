from typing import Iterable, Any
import struct
import re


def w(l):
    return ", ".join([str(x) for x in l])


def h(l):
    return "0x" + ''.join('{:02x}'.format(x) for x in l)


def f_n(num, l):
    if num == 1:
        return float(l)
    return [float(x) for x in l[0:num]]


def i_n(num, l):
    if num == 1:
        return int(l)
    return [int(x) for x in l[0:num]]


def b_n(num, l):
    if num == 1:
        return int(l)
    return [int(x) for x in l[0:num] if x < 256 and x >= 0]


eps = 1e-6


def feq(a, b):
    if isinstance(a, list) or isinstance(a, tuple):
        return all([abs(i - j) <= eps * abs(i) for i, j in zip(a, b)])
    return abs(a - b) <= eps * abs(a)


def bracy(l, num, f, br=None):
    beg = br[0] if br else ''
    end = (br[1] if len(br) > 1 else br[0]) if br else ''
    s = ''
    if f == 'B':
        if num > 1 and num <= 64:
            s = h(l)
        elif num > 64:
            s = h(l[:32]) + '...' + h(l[-32:])
        else:
            s = f'{l:02x}'
    elif f == 's':
        s = l
    else:
        if num > 1:
            s = w(l)
        else:
            s = f'{l}'

    return beg + s + end

class bsp_constants:
    Encoding = "utf-8"

class packable:
    def __init__(self):
        self.__structformat = None

    def __repr__(self):
        return f"{self.__class__.__name__}: " + str(self)

    def _layout(self) -> dict:
        raise
    @classmethod
    def _format(cls):
        raise

    @classmethod
    def __getformat(cls):
        return cls._format()

    @classmethod
    def size(cls):
        return struct.calcsize("<"+"".join(cls.__getformat()))

    @staticmethod
    def __expand(num, value):
        if isinstance(value, list) or isinstance(value, tuple):
            for i in value[:num]:
                yield i
        elif isinstance(value, str):
            yield value.encode(bsp_constants.Encoding)[:num]
        else:
            yield value

    @staticmethod
    def __getnum(fmt: str):
        for i, c in enumerate(fmt):
            if not c.isnumeric():
                if i > 0:
                    return int(fmt[:i])
                else:
                    return 1
        return 1

    @staticmethod
    def __decode(fmts, value):
        v = iter(value)
        for f in fmts:
            num = __class__.__getnum(f)
            if f[-1] == 's':  # aka string
                vv = next(v)
                i = vv.find(b'\x00')
                if i > 0:
                    vv = vv[:i]
                yield vv.decode(bsp_constants.Encoding)
            else:
                if num == 1:
                    yield next(v)
                else:
                    yield [next(v) for x in range(num)]

    def write(self, buffer):
        fmts = self.__class__.__getformat()
        lo = self._layout()
        format = "<"+"".join(fmts)
        struct.pack_into(format, buffer, 0, *[x for k, y in zip(fmts, lo)
                                              for x in self.__class__.__expand(self.__class__.__getnum(k), y)])

    @classmethod
    def read(cls, buffer) -> "packable":
        fmts = cls._format()
        format = "<"+"".join(fmts)
        v = struct.unpack_from(format, buffer, 0)
        return cls(*__class__.__decode(fmts, v))

    @staticmethod
    def createstruct(name, layouts):
        typeinits = {
            'f': f_n,
            'i': i_n,
            's': lambda n, x: x[:n],
            'B': b_n
        }
        definits = {
            'f': lambda n: [0.0] * n if n > 1 else 0.0,
            'i': lambda n: [0] * n if n > 1 else 0,
            's': lambda _: "",
            'B': lambda n: [0] * n if n > 1 else 0,
        }
        import random
        randinit = {
            'f': lambda n: [random.random() for x in range(n)] if n > 1 else random.random(),
            'i': lambda n: [random.randint(-1024, 1024) for x in range(n)] if n > 1 else random.randint(-1024, 1024),
            's': lambda n: ''.join([chr(random.randint(ord('a'), ord('z'))) for x in range(random.randint(0, n))]),
            'B': lambda n: [random.randint(0, 255) for x in range(n)] if n > 1 else random.randint(0, 255),
        }
        equal = {
            'f': feq,
            'i': lambda a, b: a == b,
            's': lambda a, b: a == b,
            'B': lambda a, b: a == b,
        }

        def ctor(self, layouts, *values):
            for i, (f, varn, *_rest) in enumerate(layouts):
                num = __class__.__getnum(f)
                deflt = definits[f[-1]](num) if len(_rest) < 2 else _rest[1]
                v = typeinits[f[-1]](num, values[i] if i <
                                     len(values) else deflt)
                setattr(self, varn, v)

        t = type(name, (packable,), {
            "__init__": lambda self, *values: ctor(self, layouts, *values),
            "_format": staticmethod(lambda: [x[0] for x in layouts]),
            "_layout": lambda self: [getattr(self, x[1]) for x in layouts],
            "__str__": lambda self: ' '.join([bracy(getattr(self, x[1]), __class__.__getnum(x[0]), x[0][-1], x[2] if len(x) > 2 else None) for x in layouts]),
            "__eq__": lambda self, other: all([equal[x[0][-1]](getattr(self, x[1]), getattr(other, x[1])) for x in layouts]),
        }, )

        def rand(layouts):
            r = t()
            for f, varn, *_ in layouts:
                setattr(r, varn, randinit[f[-1]](__class__.__getnum(f)))
            return r

        t.random = lambda: rand(layouts)
        return t


class visdata:
    __structformat = "<2i"

    def __init__(self, n_vecs: int, sz_vecs: int, vecs=None):
        self.n_vecs = n_vecs
        self.sz_vecs = sz_vecs
        if vecs is None:
            self.vecs = bytearray(self.__sz())
        else:
            self.vecs = vecs

    def __sz(self):
        return self.n_vecs * self.sz_vecs

    # @staticmethod
    # def size():
    #     return 1

    def size(self):
        if self.__sz() == 0: return 0
        return struct.calcsize(__class__.__structformat) + self.__sz()

    def write(self, buffer):
        if self.__sz() == 0: return
        struct.pack_into(__class__.__structformat, buffer,
                         0, self.n_vecs, self.sz_vecs)
        shft = struct.calcsize(__class__.__structformat)
        m = memoryview(buffer[shft:shft + self.__sz()])
        m[:] = self.vecs

    def visible(self, cluster):
        return list([x for x in range(0, self.sz_vecs) if self.vecs[cluster * self.sz_vecs + x // 8] & (1 << (x % 8))])

    @staticmethod
    def read(buffer):
        if len(buffer) == 0:
            return visdata(0, 0, b'')
        n, sz = struct.unpack_from(__class__.__structformat, buffer, 0)
        shft = struct.calcsize(__class__.__structformat)
        return visdata(n, sz, buffer[shft:shft + n * sz])

    def __str__(self):
        return f"[{self.n_vecs} : {self.sz_vecs}]"

    def __repr__(self):
        return f"{self.__class__.__name__}: " + str(self)


class entity:
    def __init__(self, d={}):
        self.d = d

    def __str__(self):
        return "{\n" + '\n'.join([f'  "{k}" "{v}"' for k, v in self.d.items()]) + "\n}\n"

    def __repr__(self):
        return f"{self.__class__.__name__}: " + str(self)


class entities:
    def __init__(self, ents):
        self.entities = ents

    @staticmethod
    def read(buffer: memoryview) -> "entities":
        __ent_prop_re = re.compile(r'\s*"([^"]*)"\s*"([^"]*)"')
        allents = buffer.tobytes().decode(bsp_constants.Encoding)
        ents = []
        for i in allents.split('}'):
            d = {}
            for l in i.split('\n'):
                m = __ent_prop_re.match(l)
                if m:
                    d[m[1]] = m[2]
            if len(d) > 0:
                ents.append(entity(d))
        return entities(ents)

    def to_bytes(self):
        return "".join([str(x) for x in self.entities]).encode(bsp_constants.Encoding)

    @staticmethod
    def size():
        return 1

    def __iter__(self):
        return iter(self.entities)


class geodata:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.brushs = []


def encode_decode_test(o, log=False):
    # print(o)
    b = bytearray(o.__class__.size())
    o.write(b)
    oo = o.__class__.read(b)
    if log:
        print(oo)

    return o == oo


_tx = ("i", "texture", "t ")
_ef = ("i", "effect", "e ")
_br = ("i", "brush", "b ")
_pl = ("i", "plane", "p ")
_vx = ("i", "vertex", "v ")
_nr = ("3f", "normal", "<>")


def _n(name): return ("i", f"n_{name}", "[]")


def _sn(name, p=None): return [("i", name, p), _n(name)]


_mm = [("3i", "mins", "()"), ("3i", "maxs", "()")]
_mmf = [("3f", "mins", "()"), ("3f", "maxs", "()")]

dirnames = [
    "entities", "textures", "planes", "nodes", "leafs", "leaffaces", "leafbrushs",
    "models", "brushs", "brushsides", "vertexs", "meshverts", "effects", "faces",
    "lightmaps", "lightvols", "visdata"
]

bspheader = packable.createstruct("bspheader", [("4B", "magic", None, b"IBSP"), (
    "i", "version", None, 0x2e)] + [("2i", x, "\n ") for x in dirnames])
texture = packable.createstruct(
    "texture", [("64s", "name", "'"), ("i", "flags", "[]"), ("i", "contents", "[]")])
plane = packable.createstruct("plane", [_nr, ("f", "dist")])
node = packable.createstruct("node", [_pl, ("2i", "children", '[]'), *_mm])
leaf = packable.createstruct("leaf", [(
    "i", "cluster", "c "), ("i", "area"), *_mm, *_sn("leafface"), *_sn("leafbrush")])
# leaffaces: int
# leafbrushs: int
model = packable.createstruct(
    "model", [*_mmf, *_sn("face", "f "), *_sn("brush", "b ")])
brush = packable.createstruct("brush", [*_sn("brushside", "s "), _tx])
brushside = packable.createstruct("brushside", [_pl, _tx])
vertex = packable.createstruct("vertex", [(
    "3f", "co", "()"), ("2f", "uv", "()"), ("2f", "lm_uv", "()"), _nr, ("4B", "color", "C ")])
# meshverts: int
effect = packable.createstruct(
    "effect", [("64s", "name", "'"), _br, ("i", "unknown", None, 5)])
face = packable.createstruct("face", [_tx, _ef, ("i", "type", "[]"), *_sn("vertex", "v "), *_sn("meshvert", "x "), ("i", "lm_index"),
                                      ("2i", "lm_start", "[]"), ("2i", "lm_size", "[]"), ("3f", "lm_origin", "()"), ("6f", "lm_vecs", "()"),
                                      _nr, ("2i", "vsize", "[]")])
lightmap = packable.createstruct("lightmap", [(f"{128 * 128 * 3}B", "map")])
lightvol = packable.createstruct(
    "lightvol", [("3B", "ambient", "C "), ("3B", "directional", "C "), ("2B", "dir")])


class inttype:
    @staticmethod
    def size():
        return 4

def _slice(s, n):
    return slice(s, s+n, 1)


types = {
    "entities": entities,
    "textures": texture,
    "planes": plane, 
    "nodes": node, 
    "leafs": leaf, 
    "leaffaces": inttype,
    "leafbrushs": inttype,
    "models": model, 
    "brushs": brush, 
    "brushsides": brushside, 
    "vertexs": vertex, 
    "meshverts": inttype,
    "effects": effect, 
    "faces": face,
    "lightmaps": lightmap,
    "lightvols": lightvol, 
    "visdata": inttype,
}

class bspfile:
    __pad = b'\x42\x53\x50\x20\x62\x79\x20\x72\x61\x6e\x74\x72\x61\x76\x65\x00'
    def __init__(self):
        self.header = bspheader()
        self.entities = None
        self.textures = []
        self.planes = []
        self.nodes = []
        self.leafs = []
        self.leaffaces = []
        self.leafbrushs = []
        self.models = []
        self.brushs = []
        self.brushsides = []
        self.vertexs = []
        self.meshverts = []
        self.effects = []
        self.faces = []
        self.lightmaps = []
        self.lightvols = []
        self.visdata = None


    @staticmethod
    def read_range(target_type: Any, mem: memoryview):
        n = len(mem) // target_type.size()
        return [target_type.read(mem[_slice(i * target_type.size(), target_type.size())]) for i in range(n)]

    @staticmethod
    def read_ints(mem: memoryview):
        return struct.unpack_from(f"<{len(mem) // 4}i", mem, 0)

    @staticmethod
    def write_range(collection: Iterable[packable], mem: memoryview):
        for i, x in enumerate(collection):
            x.write(mem[_slice(i * x.size(), x.size())])

    @staticmethod
    def write_ints(collection, mem: memoryview):
        return struct.pack_into(f"<{len(collection)}i", mem, 0, *collection)

    def print_header(self):
        for name in dirnames:
            sz = getattr(self.header, name)
            print(f"{name}: {sz} [{sz[1] / types[name].size()}]")


    def validate_header(self):
        intervals = [(x, getattr(self.header, x)) for x in dirnames]
        for i, (n0, (x0, l0)) in enumerate(intervals):
            for j, (n1, (x1, l1)) in enumerate(intervals):
                if j == i: continue
                # [x0, x0+l0] [x1, x1+l1]
                if x1 > x0 and x1 < x0+l0 or x0 > x1 and x0 < x1+l1:
                    print(f"{n0} intersects to {n1}")

    def read(self, buffer):
        m = memoryview(buffer)
        header = self.header = bspheader.read(m[:bspheader.size()])
        print(self.header)
        self.entities = entities.read(m[_slice(*header.entities)])
        self.textures = bspfile.read_range(
            texture, m[_slice(*header.textures)])
        self.planes = bspfile.read_range(plane, m[_slice(*header.planes)])
        self.nodes = bspfile.read_range(node, m[_slice(*header.nodes)])
        self.leafs = bspfile.read_range(leaf, m[_slice(*header.leafs)])
        self.leaffaces = bspfile.read_ints(m[_slice(*header.leaffaces)])
        self.leafbrushs = bspfile.read_ints(m[_slice(*header.leafbrushs)])
        self.models = bspfile.read_range(model, m[_slice(*header.models)])
        self.brushs = bspfile.read_range(brush, m[_slice(*header.brushs)])
        self.brushsides = bspfile.read_range(
            brushside, m[_slice(*header.brushsides)])
        self.vertexs = bspfile.read_range(vertex, m[_slice(*header.vertexs)])
        self.meshverts = bspfile.read_ints(m[_slice(*header.meshverts)])
        self.effects = bspfile.read_range(effect, m[_slice(*header.effects)])
        self.faces = bspfile.read_range(face, m[_slice(*header.faces)])
        self.lightmaps = bspfile.read_range(
            lightmap, m[_slice(*header.lightmaps)])
        self.lightvols = bspfile.read_range(
            lightvol, m[_slice(*header.lightvols)])
        self.visdata = visdata.read(m[_slice(*header.visdata)])

    def to_bytes(self):
        header = self.header
        if self.entities is None or self.visdata is None: raise ValueError("BSP is uninitialized")
        ents = self.entities.to_bytes()
        span_size = 48 + len(bspfile.__pad)
        entities_padding = 1024
        header.entities = (header.size() + span_size, len(ents))
        header.textures = (
            ((header.entities[0] + header.entities[1] + entities_padding) // 256) * 256, texture.size() * len(self.textures))
        header.planes = (
            header.textures[0] + header.textures[1], plane.size() * len(self.planes))
        header.nodes = (
            header.planes[0] + header.planes[1], node.size() * len(self.nodes))
        header.leafs = (
            header.nodes[0] + header.nodes[1], leaf.size() * len(self.leafs))
        header.leaffaces = (
            header.leafs[0] + header.leafs[1], 4 * len(self.leaffaces))
        header.leafbrushs = (
            header.leaffaces[0] + header.leaffaces[1], 4 * len(self.leafbrushs))
        header.models = (
            header.leafbrushs[0] + header.leafbrushs[1], model.size() * len(self.models))
        header.brushs = (
            header.models[0] + header.models[1], brush.size() * len(self.brushs))
        header.brushsides = (
            header.brushs[0] + header.brushs[1], brushside.size() * len(self.brushsides))
        header.vertexs = (
            header.brushsides[0] + header.brushsides[1], vertex.size() * len(self.vertexs))
        header.meshverts = (
            header.vertexs[0] + header.vertexs[1], 4 * len(self.meshverts))
        header.effects = (
            header.meshverts[0] + header.meshverts[1], effect.size() * len(self.effects))
        header.faces = (
            header.effects[0] + header.effects[1], face.size() * len(self.faces))
        header.lightmaps = (
            header.faces[0] + header.faces[1], lightmap.size() * len(self.lightmaps))
        header.lightvols = (
            header.lightmaps[0] + header.lightmaps[1], lightvol.size() * len(self.lightvols))
        header.visdata = (
            header.lightvols[0] + header.lightvols[1], self.visdata.size())
        total = header.visdata[0] + header.visdata[1]
        buffer = bytearray(total)
        mem = memoryview(buffer)
        header.write(mem[_slice(0, header.size())])
        print(header)

        pad = header.size() + span_size - len(bspfile.__pad)
        mem[pad:pad+len(bspfile.__pad)] = bspfile.__pad
        
        mem[_slice(*header.entities)] = ents

        bspfile.write_range(self.textures, mem[_slice(*header.textures)])
        bspfile.write_range(self.planes, mem[_slice(*header.planes)])
        bspfile.write_range(self.nodes, mem[_slice(*header.nodes)])
        bspfile.write_range(self.leafs, mem[_slice(*header.leafs)])
        bspfile.write_ints(self.leaffaces, mem[_slice(*header.leaffaces)])
        bspfile.write_ints(self.leafbrushs, mem[_slice(*header.leafbrushs)])
        bspfile.write_range(self.models, mem[_slice(*header.models)])
        bspfile.write_range(self.brushs, mem[_slice(*header.brushs)])
        bspfile.write_range(self.brushsides, mem[_slice(*header.brushsides)])
        bspfile.write_range(self.vertexs, mem[_slice(*header.vertexs)])
        bspfile.write_ints(self.meshverts, mem[_slice(*header.meshverts)])
        bspfile.write_range(self.effects, mem[_slice(*header.effects)])
        bspfile.write_range(self.faces, mem[_slice(*header.faces)])
        bspfile.write_range(self.lightmaps, mem[_slice(*header.lightmaps)])
        bspfile.write_range(self.lightvols, mem[_slice(*header.lightvols)])

        self.visdata.write(mem[_slice(*header.visdata)])

        return buffer
    
    def clone(self):
        res = bspfile()
        res.read(self.to_bytes())
        return res

    def add_model(self, brushes, faces):
        pass
    def add_entity(self, entity):
        pass

    def validate(self):
        if self.entities is None or self.visdata is None:
            return None, None
        errors = []
        warnings = []

        if len(self.models) == 0:
            return ["Tree model is not found"], []
        if len(self.nodes) == 0:
            return ["Tree root is not found"], []

        def rng(field):
            return lambda obj: ((getattr(obj, field), getattr(obj, f"n_{field}")), field)
        def val(field):
            return lambda obj: (getattr(obj, field), field)
        def vali(obj): return (obj, "value")

        def false(obj): return False
        def true(obj): return True
        def nonnegative(obj): return obj >= 0
        def negative(obj): return obj < 0
        def ensure(obj, getter, obj_range, used, allow_empty=False, precond=true):
            v, name = getter(obj)
            if v is None: return None
            if not precond(v): return None
            if isinstance(v, tuple):
                if allow_empty and len(obj_range) == 0: return None
                start = v[0]
                end = v[0] + v[1]
                if start < 0 or end < start:
                    return f"{name} configuration is broken: ({start}; {end})"
                if end > len(obj_range):
                    return f"has {name} ({start}; {end}) that's out of bounds"
                for f in range(start, end): used[f] += 1
            else:
                if allow_empty and len(obj_range) == 0: return None
                if v < 0 or v >= len(obj_range):
                    return f"{name} ({v}) is out of bounds"
                used[v] += 1
            return None

        def df(field, fnc, allow_empty=False, precond=true):
            return (fnc(field), f"{field}s", allow_empty, precond)

        maps = {
            ("entities"): [(lambda obj: (int(obj.d.get("model", "*0")[1:]), "model"), "models", False, true)], # [TODO] add entitiy validation
            ("models"): [
                df("brush", rng),
                df("face", rng),
            ],
            ("nodes"): [
                (lambda obj: (obj.children[0], "children[0]"), "nodes", False, nonnegative),
                (lambda obj: (obj.children[1], "children[1]"), "nodes", False, nonnegative),
                (lambda obj: (-obj.children[0] - 1, "children[0]"), "leafs", False, nonnegative),
                (lambda obj: (-obj.children[1] - 1, "children[1]"), "leafs", False, nonnegative),
            ],
            ("leafs"): [
                df("leafface", rng),
                df("leafbrush", rng)
            ], # [TODO] add visdata cluster validation
            ("leaffaces"): [(vali, "faces", False, true)],
            ("leafbrushs"): [(vali, "brushs", False, true)],
            ("brushs"): [df("brushside", rng), df("texture", val)],
            # ("meshverts"): [(vali, "vertexs", False, true)],
            ("faces"): [
                df("texture", val),
                df("effect", val, precond=nonnegative),
                df("vertex", rng),
                # df("meshvert", rng),
                (val("lm_index"), "lightmaps", True, nonnegative)
            ],
        }
        usages = {}
        for v in maps.values():
            for _, rng_field, _, _ in v:
                obj_range = getattr(self, rng_field)
                usages.setdefault(rng_field, [0 for _ in range(len(obj_range))])

        for k, v in maps.items():
            src_range = getattr(self, k)
            for getter, rng_field, allow_empty, precond in v:
                obj_range = getattr(self, rng_field)
                for i, obj in enumerate(src_range):
                    try:
                        err = ensure(obj, getter, obj_range, usages[rng_field], allow_empty, precond)
                        if err is not None:
                            errors.append(f"{k}[{i}]: {err}")
                    except:
                        print(f"{k}: {i}")
                        raise
    
        for i, f in enumerate(self.faces):
            mv_start, mv_end = f.meshvert, f.meshvert+f.n_meshvert
            v_range = range(0, f.n_vertex)
            if any((self.meshverts[mv] not in v_range for mv in range(mv_start, mv_end))):
                errors.append(f"faces[{i}]: some meshverts ({[self.meshverts[mv] for mv in range(mv_start, mv_end)]}) are out of face vertices range")

        if len(errors) > 0:
            return errors, []

        usages["models"][0] += 1
        frange = range(*rng("face")(self.models[0])[0])
        brange = range(*rng("brush")(self.models[0])[0])

        treeleafs = []
        def tree(node, treeleafs):
            if node < 0:
                treeleafs.append(-node-1)
                return
            tree(self.nodes[node].children[0], treeleafs)
            tree(self.nodes[node].children[1], treeleafs)
        tree(0, treeleafs)
        for i in treeleafs:
            l = self.leafs[i]
            if any((self.leaffaces[x] not in frange for x in range(*rng("leafface")(l)[0]))):
                errors.append(f"leafs[{i}]: leaffaces are out of model[0] face range")
            if any((self.leafbrushs[x] not in brange for x in range(*rng("leafbrush")(l)[0]))):
                errors.append(f"leafs[{i}]: leafbrushs are out of model[0] brush range")

        restleafs = set(range(len(self.leafs))).difference(set(treeleafs))
        usages["leafs"][0] += 1
        for m in self.models[1:]:
            mb = set(range(m.brush, m.brush+m.n_brush))
            mf = set(range(m.face, m.face+m.n_face))
            for i in restleafs:
                l = self.leafs[i]
                lb = set([self.leafbrushs[x] for x in range(l.leafbrush, l.leafbrush + l.n_leafbrush)])
                lf = set([self.leaffaces[x] for x in range(l.leafface, l.leafface + l.n_leafface)])
                if lb == mb and lf == mf:
                    restleafs.remove(i)
                    usages["leafs"][i] += 1
                    break
        
        usages["nodes"][0] += 1
        for k, usage in usages.items():
            if any((x == 0 for x in usage)):
                warnings.append(f"{k}: some elements are unused: {[i for i, x in enumerate(usage) if x == 0]}")

        for k in ['nodes', 'leafs']:
            if any((x > 1 for x in usages[k])):
                warnings.append(f"{k}: some elements are referenced multiple times: : {[i for i, x in enumerate(usages[k]) if x > 1]}")
        
        # [TODO] validate lightvols
        # [TODO] validate visdata

        return errors, warnings

def classify(plane, pos_w):
    d = plane.dot(pos_w)
    if d < -1e-3: return 0x2
    if d > 1e-3: return 0x1
    return 0x3

def classify_faces(bsp, node_index, planes, faces_co, indices, leaf_faces):
    if node_index < 0:
        # leaf
        lst = leaf_faces.setdefault(-node_index-1, [])
        lst += indices
        return
    back = []
    front = []
    node = bsp.nodes[node_index]
    for f in indices:
        side = 0
        for v in faces_co[f]:
            side |= classify(planes[node.plane], v)
        
        if side & 0x1:
            back.append(f)
        if side & 0x2:
            front.append(f)
    classify_faces(bsp, node.children[0], planes, faces_co, back, leaf_faces)
    classify_faces(bsp, node.children[1], planes, faces_co, front, leaf_faces)


def test():
    tests = [encode_decode_test(texture.random()),
             encode_decode_test(plane.random()),
             encode_decode_test(node.random()),
             encode_decode_test(leaf.random()),
             encode_decode_test(model.random()),
             encode_decode_test(brush.random()),
             encode_decode_test(brushside.random()),
             encode_decode_test(vertex.random()),
             encode_decode_test(effect.random()),
             encode_decode_test(face.random()),
             encode_decode_test(lightmap.random()),
             encode_decode_test(lightvol.random()), ]

    if not all(tests):
        print(f"\x1b[31msome tests are failed: {tests}\x1b[0m")
    else:
        print("All tests: \x1b[32mPASSED\x1b[0m")
    

if __name__ == "__main__":
    test()
