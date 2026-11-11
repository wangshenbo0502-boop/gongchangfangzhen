import asyncio
from pathlib import Path
from unittest.mock import AsyncMock, patch

import httpx
import pytest
from fastapi import HTTPException

from app.knowledge import chunks, documents
from app.main import ChatRequest, authorize, chat, store


def test_chunking_preserves_tail_and_chinese():
    text = "循环水泵" * 500
    parts = chunks(text)
    assert parts[0] == text[:650]
    assert parts[-1].endswith(text[-100:])
    assert all(len(part) <= 650 for part in parts)


def test_seed_documents_are_scene_specific():
    docs = list(documents(Path(__file__).parents[1] / "knowledge"))
    assert docs
    text = "\n".join(doc[3] for doc in docs)
    assert all(identifier in text for identifier in ("P-01", "TK-01", "F-01", "PLC-01"))
    assert "5 个模型" in text and "16 个可操作工况" in text
    assert "模拟" in text and "未派发" in text


def test_auth_rejects_wrong_token(monkeypatch):
    monkeypatch.setenv("WORKSHOP_ASSISTANT_TOKEN", "local-test-token")
    with pytest.raises(HTTPException) as error:
        authorize("Bearer wrong")
    assert error.value.status_code == 401
    authorize("Bearer local-test-token")


def test_unavailable_knowledge_is_not_fake_answer(monkeypatch):
    monkeypatch.setattr(store, "ready", False)
    with pytest.raises(HTTPException) as error:
        asyncio.run(chat(ChatRequest(question="水泵为什么报警？")))
    assert error.value.status_code == 503


def test_rag_passes_snapshot_and_citations_and_maps_bad_key(monkeypatch):
    monkeypatch.setattr(store, "ready", True)
    monkeypatch.setenv("DEEPSEEK_API_KEY", "test-only")
    sources = [{"source": "泵.md", "chunk_index": 0, "content": "保护停机", "score": .9}]
    monkeypatch.setattr(store, "search", lambda _: sources)
    request = ChatRequest(question="当前故障是什么？", snapshot={
        "capturedAt": "2026-11-08T10:00:00+08:00", "selectedDeviceId": "P-01",
        "runningCount": 7, "recordCount": 0, "devices": [],
        "alarms": [{"id": "ALM-001", "deviceId": "P-01", "progress": "未派发",
                    "faultCode": "P-CAV", "faultName": "汽蚀", "severity": "预警",
                    "action": "演示：检查入口供液", "trips": False}],
    })
    response = httpx.Response(200, json={"choices": [{"message": {"content": "水泵超温 [1]"}}]})
    with patch("app.main.httpx.AsyncClient.post", new_callable=AsyncMock, return_value=response) as post:
        result = asyncio.run(chat(request))
        content = post.call_args.kwargs["json"]["messages"][-1]["content"]
        assert "P-01" in content and "ALM-001" in content and "[1]" in content
        assert "P-CAV" in content and "汽蚀" in content and "入口供液" in content
        assert result["sources"][0]["id"] == 1
        assert result["snapshotAt"] == request.snapshot.capturedAt
    with patch("app.main.httpx.AsyncClient.post", new_callable=AsyncMock,
               return_value=httpx.Response(401, json={"error": "secret upstream body"})):
        with pytest.raises(HTTPException) as error:
            asyncio.run(chat(request))
        assert error.value.status_code == 502
        assert "secret" not in error.value.detail
