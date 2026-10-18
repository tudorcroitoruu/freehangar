#!/usr/bin/env python3
import re
import sys

from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont


def normalized(value):
    return re.sub(r"[^a-z0-9]", "", (value or "").lower())


def set_name(font, name_id, value):
    table = font["name"]
    records = [record for record in table.names if record.nameID == name_id]
    for record in records:
        try:
            record.string = value.encode(record.getEncoding(), errors="replace")
        except Exception:
            pass


def main():
    if len(sys.argv) != 5:
        raise SystemExit("usage: figma_font_instance.py SOURCE OUTPUT FAMILY STYLE")
    source, output, family, style = sys.argv[1:]
    font = TTFont(source)
    if "fvar" not in font:
        raise SystemExit(3)

    requested = normalized(style)
    coordinates = None
    for instance in font["fvar"].instances:
        instance_name = font["name"].getDebugName(instance.subfamilyNameID) or ""
        if normalized(instance_name) == requested:
            coordinates = dict(instance.coordinates)
            break
    if coordinates is None:
        aliases = {"semibold": "demibold", "demibold": "semibold"}
        alias = aliases.get(requested)
        for instance in font["fvar"].instances:
            instance_name = font["name"].getDebugName(instance.subfamilyNameID) or ""
            if alias and normalized(instance_name) == alias:
                coordinates = dict(instance.coordinates)
                break
    if coordinates is None:
        raise SystemExit(4)

    instantiateVariableFont(font, coordinates, inplace=True, optimize=True)
    full_name = f"{family} {style}".strip()
    postscript_name = re.sub(r"[^A-Za-z0-9-]", "", f"{family}-{style}")
    set_name(font, 1, family)
    set_name(font, 2, style)
    set_name(font, 4, full_name)
    set_name(font, 6, postscript_name)
    set_name(font, 16, family)
    set_name(font, 17, style)
    font.save(output)


if __name__ == "__main__":
    main()
