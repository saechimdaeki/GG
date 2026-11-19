"""Import selected static HOLLOWS visuals from an account's Unity download."""
from argparse import ArgumentParser
from collections import deque
from pathlib import Path
import re, tarfile

parser = ArgumentParser(description=__doc__)
parser.add_argument("unitypackage", type=Path, help="HOLLOWS FREE package downloaded through Unity Package Manager")
package = parser.parse_args().unitypackage
root = Path(__file__).resolve().parent.parent / 'Assets/Resources/Art/Hollows'
prefabs = ('valve_radio.prefab','hospital_bed.prefab','box_body_ambulance.prefab','medicine_cabinet.prefab','tool_chest.prefab')
guid_pattern = re.compile(rb'guid: ([a-f0-9]{32})')
block_pattern = re.compile(r'^--- !u!(114|64) &(-?\d+).*?(?=^--- !u!|\Z)', re.M|re.S)
selected = {}
with tarfile.open(package, 'r:gz') as archive:
    members = { member.name: member for member in archive.getmembers() }
    def read(name):
        return archive.extractfile(members[name]).read()
    paths = {
        name.rsplit('/', 1)[0]: Path(read(name).decode().splitlines()[0]).relative_to('Assets/FunctionalHorrorProps')
        for name in members if name.endswith('/pathname') and read(name).startswith(b'Assets/FunctionalHorrorProps/')
    }
    pending = deque(guid for guid,path in paths.items() if path.name in prefabs and path.parent == Path('Prefabs'))
    assert len(pending) == len(prefabs)
    while pending:
        guid = pending.popleft()
        if guid in selected or guid not in paths:
            continue
        path = paths[guid]
        allowed = {'.prefab': 'Prefabs', '.mat': 'Materials', '.png': 'Textures', '.asset': 'Meshes'}
        if path.parent.as_posix() != allowed.get(path.suffix):
            raise ValueError(f"Unexpected file in model dependencies: {path}")
        data = read(f'{guid}/asset')
        if path.suffix == '.prefab':
            yaml = data.decode()
            ids = {match.group(2) for match in block_pattern.finditer(yaml)}
            yaml = block_pattern.sub('', yaml)
            yaml = re.sub(r'^  - component: \{fileID: (-?\d+)\}\r?\n',
                          lambda match: '' if match.group(1) in ids else match.group(0),yaml,flags=re.M)
            data = yaml.encode()
        elif path.suffix == '.mat':
            yaml = data.decode()
            yaml = re.sub(r'^  m_Shader:.*$', '  m_Shader: {fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3}', yaml, flags=re.M)
            yaml = yaml.replace('    - _MainTex:', '    - _BaseMap:').replace('    - _Color:', '    - _BaseColor:').replace('    - _Glossiness:', '    - _Smoothness:')
            data = yaml.encode()
        selected[guid] = (path, data, read(f'{guid}/asset.meta'))
        if path.suffix in ('.prefab','.mat'):
            pending.extend(match.decode() for match in guid_pattern.findall(data))
    for path,data,meta in selected.values():
        target = root / path
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes(data)
        target.with_name(target.name + '.meta').write_bytes(meta)
print(f'{len(selected)} assets, {sum(len(d) for _,d,_ in selected.values()) // (1024*1024)} MB')
