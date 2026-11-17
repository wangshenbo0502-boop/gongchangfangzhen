import asyncio
from pathlib import Path
from unittest.mock import AsyncMock, MagicMock, patch

import httpx
import pytest
from fastapi import HTTPException

from app.knowledge import chunks, documents
from app.main import ChatRequest, KnowledgeStore, authorize, chat, store


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
    assert "5 个模型" in text and "32" in text
    assert "模拟" in text and "未派发" in text
    assert all(code in text for code in ("P-ALIGN", "P-DRY", "TK-VENT", "F-BLADE", "PLC-IO"))


def test_explicit_fault_codes_survive_semantic_search_misses(monkeypatch):
    instance = KnowledgeStore()
    monkeypatch.setattr(instance, "embed", lambda _: [[0.0] * 512])
    connection = MagicMock()
    connection.__enter__.return_value = connection
    monkeypatch.setattr(instance, "connect", lambda: connection)
    exact = {"source": "泵.md", "chunk_index": 6, "content": "P-ALIGN 和 P-DRY 排查证据", "score": 1}
    other = {"source": "储罐.md", "chunk_index": 1, "content": "储罐漏损", "score": .8}
    connection.execute.return_value.fetchall.side_effect = [[exact], [exact], [other, exact]]
    result = instance.search("p-align 和 P-DRY，P-ALIGN 与 P-01 有何关系")
    assert len(result) == 2
    assert result[0]["content"] == exact["content"]
    assert result[0]["match"] == "faultCode"
    assert result[1]["content"] == other["content"]
    calls = connection.execute.call_args_list
    assert len(calls) == 3
    assert "P\\-ALIGN" in calls[0].args[1][1]
    assert "P\\-DRY" in calls[1].args[1][1]


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
