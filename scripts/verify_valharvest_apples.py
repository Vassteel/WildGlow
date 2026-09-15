"""Verify fruit attachment points against a locally extracted Valharvest asset bundle.

Requires UnityPy. Pass the Valharvest.Bundles.valharvest embedded resource as argv[1].
Reads local assets only; does not export or redistribute models/textures.
"""
from pathlib import Path
import collections
import re
import sys
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler

env = UnityPy.load(sys.argv[1])
objects = {o.path_id: o for o in env.objects}
source = (Path(__file__).resolve().parents[1] / 'WildGlow/FruitSurface.cs').read_text()
point_source = source.split('Vector3[] Tops = {')[1].split('};')[0]
points = [tuple(float(x.strip().removesuffix('f')) for x in row.split(','))
          for row in re.findall(r'new Vector3\(([^)]+)\)', point_source)]
verified = set()
for obj in env.objects:
    if obj.type.name != 'GameObject':
        continue
    tree = obj.read()
    if tree.m_Name not in ('apple_tree', 'apple_tree_1'):
        continue
    pickable = next(c.component for c in tree.m_Component
                    if c.component.type.name == 'MonoBehaviour'
                    and c.component.read().m_Script.read().m_ClassName == 'Pickable')
    data = pickable.read_typetree()
    fruit = objects[data['m_hideWhenPicked']['m_PathID']].read()
    assert fruit.m_Name == 'apples'
    mf = next(c.component.read() for c in fruit.m_Component if c.component.type.name == 'MeshFilter')
    mesh = mf.m_Mesh.read()
    assert mesh.m_Name == 'apple' and mesh.m_VertexData.m_VertexCount == 1792 and not mesh.m_IsReadable
    handler = MeshHandler(mesh)
    handler.process()
    vertices = handler.m_Vertices
    parent = list(range(len(vertices)))

    def root(i):
        while parent[i] != i:
            parent[i] = parent[parent[i]]
            i = parent[i]
        return i

    def union(i, j):
        parent[root(i)] = root(j)

    for submesh in handler.get_triangles():
        for a, b, c in submesh:
            union(a, b)
            union(a, c)
    # Weld duplicated seam vertices before counting connected fruit components.
    positions = {}
    for i, vertex in enumerate(vertices):
        key = tuple(round(x, 5) for x in vertex)
        if key in positions:
            union(i, positions[key])
        positions[key] = i
    parts = collections.defaultdict(list)
    for i, vertex in enumerate(vertices):
        parts[root(i)].append(vertex)
    tops = [max(part, key=lambda v: v[1]) for part in parts.values()]
    assert len(tops) == len(points) == 9
    for top in tops:
        assert sum(sum((a-b)**2 for a, b in zip(top, point)) < 1e-12 for point in points) == 1
    verified.add(tree.m_Name)
assert verified == {'apple_tree', 'apple_tree_1'}
print('PASS: both Valharvest tree variants hide the apples mesh; all nine runtime attachment points match distinct fruit components.')
