"""Crea una copia editable de Bushido sin las cachés locales de Unity.

Ejecutar con Python 3. La carpeta de salida y el ZIP se crean junto al proyecto.
No se modifica ni se borra el proyecto original. Se conservan los archivos .meta,
los paquetes, las pruebas y SourceAssets para que el equipo pueda seguir editando.
"""

import hashlib
import json
from pathlib import Path
import shutil
import zipfile


PROJECT = Path(__file__).resolve().parents[1]
DESTINATION = PROJECT.parent / "Bushido-entrega-equipo"
ARCHIVE = DESTINATION.with_suffix(".zip")
CONTENTS = (
    "Assets", "Packages", "ProjectSettings", "SourceAssets", "Tools",
    "README.md", ".gitignore",
)


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def main():
    if DESTINATION.exists() or ARCHIVE.exists():
        raise SystemExit("La entrega ya existe. Cambia el nombre de salida antes de generar otra.")

    for name in CONTENTS:
        source = PROJECT / name
        if not source.exists():
            raise SystemExit(f"Falta un archivo o carpeta del proyecto: {name}")

    DESTINATION.mkdir()
    for name in CONTENTS:
        source = PROJECT / name
        target = DESTINATION / name
        if source.is_dir():
            shutil.copytree(source, target)
        else:
            shutil.copy2(source, target)

    files = sorted(path for path in DESTINATION.rglob("*") if path.is_file())
    manifest = []
    for target in files:
        relative = target.relative_to(DESTINATION)
        checksum = sha256(target)
        if checksum != sha256(PROJECT / relative):
            raise RuntimeError(f"La copia no coincide con el original: {relative}")
        manifest.append({"path": relative.as_posix(), "bytes": target.stat().st_size, "sha256": checksum})

    manifest_path = DESTINATION / "ENTREGA_ARCHIVOS.json"
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False), encoding="utf-8")
    files.append(manifest_path)

    with zipfile.ZipFile(ARCHIVE, "w", zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path in files:
            archive.write(path, Path("Bushido") / path.relative_to(DESTINATION))

    with zipfile.ZipFile(ARCHIVE) as archive:
        bad_file = archive.testzip()
        if bad_file:
            raise RuntimeError(f"Archivo dañado en el ZIP: {bad_file}")
        if len(archive.namelist()) != len(files):
            raise RuntimeError("El ZIP no contiene todos los archivos esperados.")

    print(json.dumps({
        "folder": str(DESTINATION), "zip": str(ARCHIVE),
        "files": len(files), "editable_mb": round(sum(p.stat().st_size for p in files) / 1048576, 1),
        "zip_mb": round(ARCHIVE.stat().st_size / 1048576, 1),
        "verified": True,
    }, ensure_ascii=False))


if __name__ == "__main__":
    main()
