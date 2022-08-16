from typing import Iterable
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


class packable:
    def __init__(self):
        self.__structformat = None

    def __repr__(self):
        return f"{self.__class__.__name__}: " + str(self)

    def _layout(self) -> dict:
        pass

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
            yield value.encode("ascii")[:num]
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
                yield next(v).decode("ascii").split('\x00')[0]
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
    def read(cls, buffer):
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

    def size(self):
        return struct.calcsize(__class__.__structformat) + self.__sz()

    def write(self, buffer):
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
    def read(buffer: memoryview):
        __ent_prop_re = re.compile(r'\s*"([^"]*)"\s*"([^"]*)"')
        allents = buffer.tobytes().decode("ascii")
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
        return "".join([str(x) for x in self.entities]).encode("ascii")


class geodata:
    def __init__(self):
        self.vertices = []
        self.faces = []
        self.brushes = []


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

dirnames = [
    "entities", "textures", "planes", "nodes", "leafs", "leaffaces", "leafbrushes",
    "models", "brushes", "brushsides", "vertexes", "meshverts", "effects", "faces",
    "lightmaps", "lightvols", "visdata"
]

bspheader = packable.createstruct("bspheader", [("4B", "magic", None, b"IBSP"), (
    "i", "version", None, 0x2e)] + [("2i", x, "\n ") for x in dirnames])
texture = packable.createstruct(
    "texture", [("64s", "name", "'"), ("i", "flags", "[]"), ("i", "contents", "[]")])
plane = packable.createstruct("plane", [_nr, ("f", "dist")])
node = packable.createstruct("node", [_pl, ("2i", "children", '[]'), *_mm])
leaf = packable.createstruct("node", [(
    "i", "cluster", "c "), ("i", "area"), *_mm, *_sn("leafface"), *_sn("leafbrush")])
# leaffaces: int
# leafbrushes: int
model = packable.createstruct(
    "model", [*_mm, *_sn("face", "f "), *_sn("brush", "b ")])
brush = packable.createstruct("brush", [*_sn("brushside", "s "), _tx])
brushside = packable.createstruct("brushside", [_pl, _tx])
vertex = packable.createstruct("vertex", [(
    "3f", "co", "()"), ("2f", "uv", "()"), ("2f", "lm_uv", "()"), _nr, ("4B", "color", "C ")])
# meshverts: int
effect = packable.createstruct(
    "effect", [("64s", "name", "'"), _br, ("i", "unknown", None, 5)])
face = packable.createstruct("face", [_tx, _ef, ("i", "type", "[]"), *_sn("vertex", "v "), *_sn("meshvert", "v "), ("i", "lm_index"),
                                      ("2i", "lm_start", "[]"), ("2i", "lm_size", "[]"), ("3f", "lm_origin", "()"), ("6f", "lm_vecs", "()"), _nr, ("2i", "vsize", "[]")])
lightmap = packable.createstruct("lightmap", [(f"{128 * 128 * 3}B", "map")])
lightvol = packable.createstruct(
    "lightvol", [("3B", "ambient", "C "), ("3B", "directional", "C "), ("2B", "dir")])


def _slice(s, n):
    return slice(s, s+n, 1)


class bspfile:
    def __init__(self):
        self.entities = None
        self.textures = []
        self.planes = []
        self.nodes = []
        self.leafs = []
        self.leaffaces = []
        self.leafbrushes = []
        self.models = []
        self.brushes = []
        self.brushsides = []
        self.vertexes = []
        self.meshverts = []
        self.effects = []
        self.faces = []
        self.lightmaps = []
        self.lightvols = []
        self.visdata = None

    @staticmethod
    def read_range(cls: type, mem: memoryview):
        n = len(mem) // cls.size()
        return [cls.read(mem[_slice(i * cls.size(), cls.size())]) for i in range(n)]

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

    def read(self, buffer):
        m = memoryview(buffer)
        header = bspheader.read(m[:bspheader.size()])
        print(header)
        self.entities = entities.read(m[_slice(*header.entities)])
        self.textures = bspfile.read_range(
            texture, m[_slice(*header.textures)])
        self.planes = bspfile.read_range(plane, m[_slice(*header.planes)])
        self.nodes = bspfile.read_range(node, m[_slice(*header.nodes)])
        self.leafs = bspfile.read_range(leaf, m[_slice(*header.leafs)])
        self.leaffaces = bspfile.read_ints(m[_slice(*header.leaffaces)])
        self.leafbrushes = bspfile.read_ints(m[_slice(*header.leafbrushes)])
        self.models = bspfile.read_range(model, m[_slice(*header.models)])
        self.brushes = bspfile.read_range(brush, m[_slice(*header.brushes)])
        self.brushsides = bspfile.read_range(
            brushside, m[_slice(*header.brushsides)])
        self.vertexes = bspfile.read_range(vertex, m[_slice(*header.vertexes)])
        self.meshverts = bspfile.read_ints(m[_slice(*header.meshverts)])
        self.effects = bspfile.read_range(effect, m[_slice(*header.effects)])
        self.faces = bspfile.read_range(face, m[_slice(*header.faces)])
        self.lightmaps = bspfile.read_range(
            lightmap, m[_slice(*header.lightmaps)])
        self.lightvols = bspfile.read_range(
            lightvol, m[_slice(*header.lightvols)])
        self.visdata = visdata.read(m[_slice(*header.visdata)])

    def to_bytes(self):
        header = bspheader()
        ents = self.entities.to_bytes()
        header.entities = (header.size(), len(ents))
        header.textures = (
            header.entities[0] + header.entities[1], texture.size() * len(self.textures))
        header.planes = (
            header.textures[0] + header.textures[1], plane.size() * len(self.planes))
        header.nodes = (
            header.planes[0] + header.planes[1], node.size() * len(self.nodes))
        header.leafs = (
            header.nodes[0] + header.nodes[1], leaf.size() * len(self.leafs))
        header.leaffaces = (
            header.leafs[0] + header.leafs[1], 4 * len(self.leaffaces))
        header.leafbrushes = (
            header.leaffaces[0] + header.leaffaces[1], 4 * len(self.leafbrushes))
        header.models = (
            header.leafbrushes[0] + header.leafbrushes[1], model.size() * len(self.models))
        header.brushes = (
            header.models[0] + header.models[1], brush.size() * len(self.brushes))
        header.brushsides = (
            header.brushes[0] + header.brushes[1], brushside.size() * len(self.brushsides))
        header.vertexes = (
            header.brushsides[0] + header.brushsides[1], vertex.size() * len(self.vertexes))
        header.meshverts = (
            header.vertexes[0] + header.vertexes[1], 4 * len(self.meshverts))
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
        mem[_slice(*header.entities)] = ents

        bspfile.write_range(self.textures, mem[_slice(*header.textures)])
        bspfile.write_range(self.planes, mem[_slice(*header.planes)])
        bspfile.write_range(self.nodes, mem[_slice(*header.nodes)])
        bspfile.write_range(self.leafs, mem[_slice(*header.leafs)])
        bspfile.write_ints(self.leaffaces, mem[_slice(*header.leaffaces)])
        bspfile.write_ints(self.leafbrushes, mem[_slice(*header.leafbrushes)])
        bspfile.write_range(self.models, mem[_slice(*header.models)])
        bspfile.write_range(self.brushes, mem[_slice(*header.brushes)])
        bspfile.write_range(self.brushsides, mem[_slice(*header.brushsides)])
        bspfile.write_range(self.vertexes, mem[_slice(*header.vertexes)])
        bspfile.write_ints(self.meshverts, mem[_slice(*header.meshverts)])
        bspfile.write_range(self.effects, mem[_slice(*header.effects)])
        bspfile.write_range(self.faces, mem[_slice(*header.faces)])
        bspfile.write_range(self.lightmaps, mem[_slice(*header.lightmaps)])
        bspfile.write_range(self.lightvols, mem[_slice(*header.lightvols)])

        self.visdata.write(mem[_slice(*header.visdata)])

        return buffer


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
