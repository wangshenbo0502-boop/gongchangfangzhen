# External assets

Downloaded from the original publishers, not extracted from Factory I/O.

## Poly Haven

License: CC0 1.0, https://polyhaven.com/license

- Modular Industrial Pipes 01: https://polyhaven.com/a/modular_industrial_pipes_01
- Metal Tool Chest: https://polyhaven.com/a/metal_tool_chest
- Barrel 02: https://polyhaven.com/a/Barrel_02
- Industrial Storage Cart: https://polyhaven.com/a/industrial_storage_cart
- Concrete Floor Painted: https://polyhaven.com/a/concrete_floor_painted
- Corrugated Iron 02: https://polyhaven.com/a/corrugated_iron_02

Downloads are resolved through https://api.polyhaven.com and checked against its MD5 manifests.
Original filenames and texture sets are retained.

## Adobe Source Han Sans

Source: https://github.com/adobe-fonts/source-han-sans
License: SIL Open Font License 1.1. See SourceHanSans/LICENSE.txt.

## Factory Training - Industry Sample

Original listing: https://assetstore.unity.com/packages/templates/packs/factory-training-industry-sample-344832

Version: 1.0, authored for Unity 6000.3.0f1.
License: Unity Companion License, https://unity.com/legal/licenses/unity-companion-license

The original public Unity CDN URL was resolved from Unity's official product metadata
in Evidence/asset-service-0.json. The legacy authenticated download-info endpoint
returned HTTP 401, but the original public CDN endpoint accepts ordinary downloads.
No third-party package mirror is used.

Tools/download_factory_training.py verifies the exact original size and CDN ETag/MD5,
and writes SHA256 and provenance to Downloads/FactoryTraining-source.json.
Tools/prepare_factory_training.py keeps asset GUIDs, relocates resources into
Assets/WorkshopDemo/External/FactoryTraining, and excludes all ProjectSettings entries.
The original package is retained unchanged under Downloads.
If Unity's package importer reports completion without creating the scene files,
Tools/extract_factory_training.py directly extracts the same isolated assets and
their original .meta files. Existing project GUID ownership is preserved; the
collision report is Evidence/factory-guid-ownership.json.

Factory Training is licensed source content for Unity, not CC0 content.

## Grab3D Equipment Candidates

License: CC0 1.0. Each model folder retains the upstream manifest, LICENSE.txt,
download URL, byte count and locally calculated SHA256 in source.json.

- Water pump: https://grab3d.com/models/tools/water-pump/
- Industrial fan: https://grab3d.com/models/electronics/industrial-fan/
- Silo: https://grab3d.com/models/buildings/silo/
- Electrical cabinet: https://grab3d.com/models/electronics/electrical-cabinet/
- Conveyor belt: https://grab3d.com/models/tools/conveyor-belt/
- Pressure tank: https://grab3d.com/models/tools/pressure-tank/

These are existing downloaded FBX models. Suitability, appearance and embedded
materials must be checked in Unity before choosing them for the finished scene.
