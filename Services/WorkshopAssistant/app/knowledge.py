import hashlib
import re
from pathlib import Path


def chunks(text: str, size: int = 650, overlap: int = 100) -> list[str]:
    text = text.replace("\r\n", "\n").strip()
    if not text:
        return []
    result = []
    for section in re.split(r"\n(?=#{1,3} )", text):
        section = section.strip()
        for start in range(0, len(section), size - overlap):
            part = section[start : start + size]
            if part:
                result.append(part)
            if start + size >= len(section):
                break
    return result


def documents(root: Path):
    for path in sorted(root.rglob("*")):
        if path.is_file() and path.suffix.lower() in {".md", ".txt"}:
            text = path.read_text(encoding="utf-8-sig")
            source = path.relative_to(root).as_posix()
            digest = hashlib.sha256(text.encode()).hexdigest()
            for index, content in enumerate(chunks(text)):
                yield source, digest, index, content
