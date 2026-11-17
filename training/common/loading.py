"""Loading a base model for text-only use (models.md 6.2).

The Qwen3.5 repositories are published as multimodal checkpoints: their architecture is a ``...ForConditionalGeneration``
class with a vision tower, and the text model is inside it. The pipeline only ever feeds text. ``load_text_model`` tries the
plain causal-LM class first (which is all a text-only model such as Qwen3-0.6B needs) and falls back to the class named in the
repository's own ``architectures``; ``freeze_vision`` then switches off the gradients of every vision parameter so the adapter
never touches it. Imports of torch and transformers happen inside the functions, so tests and dry runs do not need them.
"""
from __future__ import annotations

from typing import Any, Mapping

VISION_MARKERS = ("visual", "vision_tower", "vision_model", "multi_modal_projector", "mm_projector")


def is_vision_parameter(name: str) -> bool:
    """True for a parameter of the vision tower or the projector into the text model."""
    return any(marker in name.split(".") for marker in VISION_MARKERS)


def freeze_vision(model: Any) -> int:
    """Set ``requires_grad`` false on every vision parameter; return how many parameters were frozen."""
    frozen = 0
    for name, param in model.named_parameters():
        if is_vision_parameter(name):
            param.requires_grad = False
            frozen += 1
    return frozen


def architecture_class_name(config: Any) -> str | None:
    """First entry of ``config.architectures`` that ends with ``ForConditionalGeneration``, if any."""
    for name in getattr(config, "architectures", None) or []:
        if name.endswith("ForConditionalGeneration"):
            return name
    return None


def load_text_model(source: str, revision: str | None, dtype: Any, loading: Mapping[str, Any] | None = None, **kwargs: Any) -> Any:
    """Load ``source`` (a hub id or a folder) for text-only use.

    ``loading`` is the ``loading`` block of ``train.json``: ``attn_implementation`` (``null`` keeps the library default) and
    ``freeze_vision_tower`` (default true). The vision tower is frozen only when there is one."""
    import transformers  # type: ignore
    loading = loading or {}
    extra = dict(kwargs)
    if loading.get("attn_implementation"):
        extra["attn_implementation"] = loading["attn_implementation"]
    try:
        model = transformers.AutoModelForCausalLM.from_pretrained(source, revision=revision, torch_dtype=dtype, **extra)
    except (ValueError, KeyError):
        config = transformers.AutoConfig.from_pretrained(source, revision=revision)
        name = architecture_class_name(config)
        cls = getattr(transformers, name, None) if name else None
        if cls is None:
            raise
        model = cls.from_pretrained(source, revision=revision, torch_dtype=dtype, **extra)
    if loading.get("freeze_vision_tower", True):
        freeze_vision(model)
    return model
