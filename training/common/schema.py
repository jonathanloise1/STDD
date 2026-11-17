"""Validator for the JSON Schema subset of operations.md 7.1.

Supported: ``type`` (string or list), ``properties``, ``required``, ``additionalProperties: false``, ``enum``, ``pattern``,
``minimum``, ``maximum``, ``minLength``, ``maxLength``, ``items``, ``maxItems``, ``anyOf``, ``$ref`` to ``#/$defs/...``.
Annotations (``description``, ``title``, ``$schema``, ``$id``, ``$defs``, ``x-enum-from``) are ignored. Returns a list of
error strings; an empty list means the value is valid.
"""
from __future__ import annotations

import re
from typing import Any

_TYPES = {"string": str, "boolean": bool, "object": dict, "array": list}


def _is_type(value: Any, name: str) -> bool:
    if name == "null":
        return value is None
    if name == "integer":
        return isinstance(value, int) and not isinstance(value, bool)
    if name == "number":
        return isinstance(value, (int, float)) and not isinstance(value, bool)
    return isinstance(value, _TYPES[name])


def validate(schema: dict[str, Any], value: Any, defs: dict[str, Any] | None = None, path: str = "$") -> list[str]:
    defs = defs if defs is not None else schema.get("$defs", {})
    if "$ref" in schema:
        ref = schema["$ref"]
        if not ref.startswith("#/$defs/") or ref[8:] not in defs:
            return [f"{path}: unresolved reference {ref}"]
        return validate(defs[ref[8:]], value, defs, path)
    if "anyOf" in schema:
        branches = [validate(s, value, defs, path) for s in schema["anyOf"]]
        return [] if any(not b for b in branches) else [f"{path}: matches none of the alternatives"]
    errors: list[str] = []
    if "type" in schema:
        names = schema["type"] if isinstance(schema["type"], list) else [schema["type"]]
        if not any(_is_type(value, n) for n in names):
            return [f"{path}: expected {'|'.join(names)}, got {type(value).__name__}"]
    if "enum" in schema and value not in schema["enum"]:
        errors.append(f"{path}: {value!r} is not one of {schema['enum']}")
    if isinstance(value, str):
        if "minLength" in schema and len(value) < schema["minLength"]:
            errors.append(f"{path}: shorter than {schema['minLength']}")
        if "maxLength" in schema and len(value) > schema["maxLength"]:
            errors.append(f"{path}: longer than {schema['maxLength']}")
        if "pattern" in schema and not re.search(schema["pattern"], value):
            errors.append(f"{path}: does not match {schema['pattern']}")
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        if "minimum" in schema and value < schema["minimum"]:
            errors.append(f"{path}: below {schema['minimum']}")
        if "maximum" in schema and value > schema["maximum"]:
            errors.append(f"{path}: above {schema['maximum']}")
    if isinstance(value, list):
        if "maxItems" in schema and len(value) > schema["maxItems"]:
            errors.append(f"{path}: more than {schema['maxItems']} items")
        if "items" in schema:
            for i, item in enumerate(value):
                errors += validate(schema["items"], item, defs, f"{path}[{i}]")
    if isinstance(value, dict):
        props = schema.get("properties", {})
        for name in schema.get("required", []):
            if name not in value:
                errors.append(f"{path}: missing required property {name}")
        if schema.get("additionalProperties") is False:
            for name in value:
                if name not in props:
                    errors.append(f"{path}: unexpected property {name}")
        for name, sub in props.items():
            if name in value:
                errors += validate(sub, value[name], defs, f"{path}.{name}")
    return errors
