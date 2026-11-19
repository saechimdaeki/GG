#!/usr/bin/env python3
"""Import LastTrain's asset selection from the local Unity Asset Store cache."""
import re
import tarfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets/Resources/Art"
CACHE = Path.home() / "Library/Unity/Asset Store-5.x"
URP_LIT = "fileID: 4800000, guid: 933532a4fcc9baf4fa0491de14d08ed7, type: 3"
ZOMBIE = {
    "FBXs/Zombie1.FBX", "FBXs/Zombie2.FBX", "FBXs/Zombie3.FBX", "Materials/Zombie.mat",
    *("Animations/" + name + ".anim" for name in (
        "Z_Idle", "Z_Walk_InPlace", "Z_Run_InPlace", "Z_Attack", "Z_FallingBack")),
    *("Textures/" + name for name in (
        "Zombie.tga", "Zombie_metallic.tga", "Zombie_ao.png", "Zombie_nm.png", "zombie_emission.tga")),
}
SCAVENGER = {
    "FBX/Scavenger.fbx", *("Materials/Scavenger_" + name + ".mat" for name in (
        "Body", "eye_transparent", "hairs")),
    *("Textures/Scavenger_" + name for name in (
        "Normal.png", "MetallicSmoothness.png", "hair.tga", "AlbedoTransparency.png", "AO.png")),
}
CITY = {f"S_G_{number}_S.png" for number in ("000101", "000115", "000163")}


def urp_material(name, raw):
    data = raw.decode().replace("fileID: 46, guid: 0000000000000000f000000000000000, type: 0", URP_LIT)
    for old, new in (("_MainTex:", "_BaseMap:"), ("- _Color:", "- _BaseColor:"),
                     ("_Glossiness:", "_Smoothness:"), ("_METALLICGLOSSMAP", "_METALLICSPECGLOSSMAP")):
        data = data.replace(old, new)
    if name.endswith("Zombie.mat"):
        data = data.replace("_EmissionColor: {r: 1, g: 1, b: 1, a: 1}",
                            "_EmissionColor: {r: 0.08, g: 0.08, b: 0.08, a: 1}")
    if name.endswith("_hairs.mat"):
        data = data.replace("  - _ALPHABLEND_ON", "  - _ALPHATEST_ON")
        data = re.sub(r"m_CustomRenderQueue: -?\d+", "m_CustomRenderQueue: 2450", data)
        data = data.replace("RenderType: Transparent", "RenderType: TransparentCutout")
        data = data.replace("    m_Floats:", "    m_Floats:\n    - _AlphaClip: 1")
        data = data.replace("_ZWrite: 0", "_ZWrite: 1").replace("_DstBlend: 10", "_DstBlend: 0")
        data = data.replace("_SrcBlend: 5", "_SrcBlend: 1")
    if name.endswith("_eye_transparent.mat"):
        data = data.replace("  - _ALPHAPREMULTIPLY_ON", "  - _SURFACE_TYPE_TRANSPARENT")
        data = data.replace("    m_Floats:", "    m_Floats:\n    - _Surface: 1")
    return data.encode()


def extract(package, prefix, folder, selection):
    pkg = next(CACHE.rglob(package + ".unitypackage"), None)
    if pkg is None:
        raise SystemExit(f"Unity Package Manager cache does not contain {package}.")
    found = set()
    with tarfile.open(pkg) as archive:
        for item in archive.getmembers():
            if not item.name.endswith("/pathname"):
                continue
            path = archive.extractfile(item).read().decode().splitlines()[0]
            if not path.startswith(prefix) or path[len(prefix):] not in selection:
                continue
            name = path[len(prefix):]
            found.add(name)
            target = ROOT / folder / name
            target.parent.mkdir(parents=True, exist_ok=True)
            entry = item.name.removesuffix("/pathname")
            data = archive.extractfile(entry + "/asset").read()
            target.write_bytes(urp_material(name, data) if name.endswith(".mat") else data)
            target.with_name(target.name + ".meta").write_bytes(archive.extractfile(entry + "/asset.meta").read())
    if found != selection:
        raise SystemExit(f"{package} missing expected files: {sorted(selection - found)}")
    print(f"{package}: {len(found)} assets imported")


if __name__ == "__main__":
    extract("Zombie", "Assets/Zombie/", "Pxltiger", ZOMBIE)
    extract("Free Scavenger", "Assets/Homeless_people/Free_Scavenger/", "Scavenger", SCAVENGER)
    extract("Japanese Otaku City", "Assets/ZRNAssets/005339_08932_25_14/Textures/", "Zenrin", CITY)
