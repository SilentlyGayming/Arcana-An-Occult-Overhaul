import argparse
import hashlib
import os
import re
import shutil
import struct
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path
from tempfile import NamedTemporaryFile, TemporaryDirectory

from dbpf import Key, LAYO, S3SA, STBL, XML, fnv64, index, read, write
from hud_icons import generate as generate_hud_keys, resources as hud_resources, status_resources, life_state_resources
from journal_layouts import layouts
from localization import generate as generate_english, read_table, resources as locale_resources
from prepare_sims3_deps import REQUIRED, prepare_references, validated_references
from station import resources as station_resources
from trees import IMAGE, TREES, icons
from ui_resources import resources as ui_image_resources
from vampire_bitemark import replacement_payloads, thumbnail_payloads

PROJECT = Path(__file__).resolve().parent.parent
PACKAGE = "Arcana - An Occult Overhaul.package"
DLL = "Arcana - An Occult Overhaul.dll"

ASSEMBLY = "SupernaturalExpanded.Journal"
BOOTSTRAP = ASSEMBLY + ".JournalBootstrap"
ASSEMBLY_KEY = Key(S3SA, 0, fnv64(ASSEMBLY))
TUNING_KEY = Key(XML, 0, fnv64(BOOTSTRAP))
TUNING = Path(__file__).resolve().parent.parent / "src" / "Core" / "JournalBootstrap.xml"


def validate_dll(data):
    if len(data) < 64 or data[:2] != b"MZ":
        raise ValueError("Expected a managed PE DLL")
    pe = struct.unpack_from("<I", data, 60)[0]
    if data[pe:pe + 4] != b"PE\0\0":
        raise ValueError("Missing PE header")
    machine, = struct.unpack_from("<H", data, pe + 4)
    pe_type, = struct.unpack_from("<H", data, pe + 24)
    if (machine, pe_type) != (0x014C, 0x010B):
        raise ValueError("Expected a 32-bit Windows game assembly")
    cli_rva, = struct.unpack_from("<I", data, pe + 24 + 96 + 14 * 8)
    metadata = data.find(b"BSJB")
    if not cli_rva or metadata < 0 or b"SupernaturalExpanded.Journal" not in data:
        raise ValueError("Expected the compiled SupernaturalExpanded.Journal managed assembly")
    version_length, = struct.unpack_from("<I", data, metadata + 12)
    version = data[metadata + 16:metadata + 16 + version_length].rstrip(b"\0")
    if version != b"v2.0.50727":
        raise ValueError("Expected CLR v2 metadata. Compile against the Sims 3 mscorlib.")


def validate_tuning(tuning):
    if not tuning.startswith(b"<"):
        raise ValueError("Bootstrap tuning must start with XML and have no byte-order mark")
    element = ET.fromstring(tuning).find("./Current_Tuning/kInstantiator")
    if element is None or element.get("value", "").lower() != "true":
        raise ValueError("Bootstrap tuning is missing kInstantiator=True")


def script_resource(assembly):
    block_size = 512
    blocks = (len(assembly) + block_size - 1) // block_size
    header = struct.pack("<BI64sH", 1, 0x2BC4F79F, bytes(64), blocks)
    return header + bytes(blocks * 8) + assembly.ljust(blocks * block_size, b"\0")


def read_script_resource(resource):
    version, checksum_type, checksum, blocks = struct.unpack_from("<BI64sH", resource)
    start = 71 + blocks * 8
    if (version != 1 or checksum_type != 0x2BC4F79F or any(checksum)
            or any(resource[71:start]) or len(resource) != start + blocks * 512):
        raise ValueError("Unexpected unencrypted custom S3SA resource")
    return resource[start:]


def verify(package, assembly, tuning, resources):
    entries = index(package)
    hud = dict(hud_resources() + status_resources() + life_state_resources())
    found = {entry.key: entry for entry in entries}
    for key, data in hud.items():
        if key not in found or read(package, found[key]) != data:
            raise ValueError(f"HUD icon missing or changed: {key}")
    if [entry.key for entry in entries] != [key for key, data in resources]:
        raise ValueError("Package resource list differs from the requested journal resources")
    extracted = read_script_resource(read(package, entries[0]))
    if extracted[:len(assembly)] != assembly or any(extracted[len(assembly):]):
        raise ValueError("S3SA round trip failed")
    if read(package, entries[1]) != tuning:
        raise ValueError("Bootstrap tuning round trip failed")
    for entry, (key, data) in zip(entries[2:], resources[2:]):
        packaged = read(package, entry)
        if packaged != data:
            raise ValueError(f"Round trip failed for {key}")
        if key.type == LAYO and ET.fromstring(data).tag != "graph":
            raise ValueError(f"Layout round trip failed for {key}")
        if key.type == STBL:
            read_table(packaged)


def content_resources():
    resources = [(Key(LAYO, 0, fnv64(name)), data) for name, data in layouts()]
    for tree in TREES:
        resources.extend((Key(IMAGE, 0, instance), data) for _, instance, data in icons(tree).values())
    resources.extend((Key(IMAGE, 0, instance), data) for instance, data in ui_image_resources())
    resources.extend(hud_resources())
    resources.extend(status_resources())
    resources.extend(life_state_resources())
    resources.extend(locale_resources())
    resources.extend((Key(typ, group, instance), data)
                     for typ, group, instance, data in replacement_payloads() + thumbnail_payloads())
    resources.extend(station_resources())
    if len({key for key, _ in resources}) != len(resources):
        raise ValueError("Duplicate package resource key")
    return resources


def package(dll_path, package_path):
    dll = Path(dll_path).read_bytes()
    tuning = TUNING.read_bytes()
    validate_dll(dll)
    validate_tuning(tuning)
    resources = [(ASSEMBLY_KEY, script_resource(dll)), (TUNING_KEY, tuning)] + content_resources()
    package_path = Path(package_path)
    package_path.parent.mkdir(parents=True, exist_ok=True)
    with NamedTemporaryFile(dir=package_path.parent, suffix=".package.tmp", delete=False) as temp:
        staging = Path(temp.name)
    try:
        write(staging, resources)
        verify(staging, dll, tuning, resources)
        staging.replace(package_path)
    finally:
        staging.unlink(missing_ok=True)


def find_compiler(dotnet_path):
    if not dotnet_path and os.name == "nt":
        framework = Path(os.getenv("WINDIR", r"C:\Windows")) / "Microsoft.NET" / "Framework" / "v4.0.30319" / "csc.exe"
        if framework.is_file():
            return [str(framework)], False

    dotnet = dotnet_path or shutil.which("dotnet")
    if dotnet:
        versions = subprocess.check_output([dotnet, "--list-sdks"], text=True).splitlines()
        for entry in reversed(versions):
            match = re.fullmatch(r"(\S+) \[(.+)\]", entry)
            if match:
                version, directory = match.groups()
                roslyn = Path(directory) / version / "Roslyn" / "bincore" / "csc.dll"
                if roslyn.is_file():
                    return [dotnet, str(roslyn)], True
    raise RuntimeError("Install a .NET SDK or provide --dotnet with its executable path.")


def run_stage(name, command):
    result = subprocess.run(command)
    if result.returncode != 0:
        raise RuntimeError(f"{name} failed (exit code {result.returncode}). See the diagnostics above.")


def build(args):
    replacement_payloads()
    thumbnail_payloads()
    if args.game_assemblies:
        paths, hashes = validated_references(args.game_assemblies.resolve())
    else:
        paths, hashes = prepare_references(PROJECT, args.game_dir, args.refresh_deps)

    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    compiler, roslyn = find_compiler(args.dotnet)
    options = ["/nologo", "/noconfig", "/nostdlib+", "/langversion:3", "/platform:x86",
               "/target:library", "/optimize+", "/warnaserror+", "/codepage:65001"]
    if roslyn:
        options.append("/deterministic+")
    source_root = PROJECT / "src"
    sources = {path.relative_to(source_root): path for path in source_root.rglob("*.cs")}

    with TemporaryDirectory(dir=output) as temp:
        staging = Path(temp)
        ref_dir = staging / "Sims3"
        ref_dir.mkdir()
        for name in REQUIRED:
            data = paths[name].read_bytes()
            if hashlib.sha256(data).hexdigest() != hashes[name]:
                raise ValueError(f"{paths[name]} changed while preparing the build. Run the build again.")
            (ref_dir / name).write_bytes(data)
        generated = (
            (Path("Shared/OccultAssetData.g.cs"), generate_hud_keys),
            (Path("Localization/ArcanaEnglish.g.cs"), generate_english),
        )
        for relative_path, generate in generated:
            path = staging / relative_path
            generate(path)
            sources[relative_path] = path

        dll = staging / (ASSEMBLY + ".dll")
        staged_package = staging / PACKAGE
        print(f"Compiling {len(sources)} C# files for CLR v2 / x86.", flush=True)
        run_stage("C# compilation", compiler + options + ["/out:" + str(dll)]
                  + ["/r:" + str(ref_dir / name) for name in REQUIRED]
                  + [str(sources[path]) for path in sorted(sources)])
        package(dll, staged_package)
        dll.replace(output / DLL)
        staged_package.replace(output / PACKAGE)
    print(f"Build complete: {output / PACKAGE}")


def main():
    parser = argparse.ArgumentParser(description="Build the Arcana assembly and package.")
    references = parser.add_mutually_exclusive_group()
    references.add_argument("--game-assemblies", type=Path, help="Use the original DLLs from a local folder.")
    references.add_argument("--game-dir", help="Override the game installation or Game/Bin folder.")
    parser.add_argument("--refresh-deps", action="store_true", help="Re-extract the local references from the game.")
    parser.add_argument("--output", type=Path, default=PROJECT / "build")
    parser.add_argument("--dotnet", help="Use a specific .NET SDK executable.")
    args = parser.parse_args()
    if args.game_assemblies and args.refresh_deps:
        parser.error("--refresh-deps needs an installation. Use --game-dir or SIMS3_GAME_DIR.")
    try:
        build(args)
    except (OSError, ValueError, RuntimeError, struct.error, subprocess.CalledProcessError) as error:
        print("ERROR: Arcana build failed:", error, file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
