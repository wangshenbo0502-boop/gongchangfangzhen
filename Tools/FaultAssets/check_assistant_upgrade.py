"""Run inside the assistant container; credentials remain in container memory."""
import json
import os
from datetime import datetime, timezone
from pathlib import Path

import httpx
import psycopg

from app.knowledge import documents


def main():
    root = Path(os.getenv("KNOWLEDGE_PATH", "/app/knowledge"))
    names = ["循环水泵故障速查.md", "储罐故障速查.md",
             "排风机故障速查.md", "控制柜故障速查.md"]
    expected = {(source, index): (digest, content)
                for source, digest, index, content in documents(root) if source in names}
    # Do not emit connection parameters or exception messages containing credentials.
    connection = psycopg.connect(
        host=os.getenv("POSTGRES_HOST", "pgvector"),
        port=int(os.getenv("POSTGRES_PORT", "5432")),
        user=os.getenv("POSTGRES_USER", "workshop"),
        password=os.getenv("POSTGRES_PASSWORD", ""),
        dbname=os.getenv("POSTGRES_DB", "workshop_knowledge"),
        connect_timeout=5,
    ) if not os.getenv("DATABASE_URL") else psycopg.connect(os.environ["DATABASE_URL"])
    with connection:
        with connection.cursor() as cursor:
            cursor.execute("SELECT source,chunk_index,digest,content FROM workshop_knowledge_chunks")
            indexed = {(source, index): (digest, content)
                       for source, index, digest, content in cursor.fetchall()}
    checks = [{"source": name,
               "chunks": sum(source == name for source, _ in expected),
               "pass": any(source == name for source, _ in expected) and
               all(indexed.get(key) == value for key, value in expected.items() if key[0] == name)}
              for name in names]
    token = os.getenv("WORKSHOP_ASSISTANT_TOKEN", "")
    headers = {"Authorization": "Bearer " + token} if token else {}
    question = ("循环水泵 P-SEAL 已到场，怎样区分密封泄漏与 P-CAV 汽蚀？"
                "新增 P-ALIGN 和 P-DRY 要核对什么证据？这些效果能证明 Abaqus 已求解吗？")
    snapshot = {
        "capturedAt": datetime.now(timezone.utc).isoformat(),
        "dataSource": "simulation", "selectedDeviceId": "P-01",
        "devices": [{"deviceId": "P-01", "name": "循环水泵", "kind": "Pump",
                     "status": "故障", "temperature": 60, "vibration": 4, "rpm": 0,
                     "faultCode": "P-SEAL", "resultSource": "教学模拟"}],
        "alarms": [], "runningCount": 3, "recordCount": 0, "inspectorStatus": "已到场",
    }
    with httpx.Client(timeout=100) as client:
        health = client.get("http://127.0.0.1:8018/health")
        listing = client.get("http://127.0.0.1:8018/api/knowledge", headers=headers)
        chat = client.post("http://127.0.0.1:8018/api/chat", headers=headers,
                           json={"question": question, "history": [], "snapshot": snapshot})
    result = {
        "time": datetime.now(timezone.utc).isoformat(), "health_status": health.status_code,
        "health": health.json(), "knowledge_status": listing.status_code,
        "document_count": len(listing.json().get("documents", [])),
        "updated_document_checks": checks, "question": question,
        "chat_status": chat.status_code, "chat": chat.json(),
    }
    print(json.dumps(result, ensure_ascii=False, indent=2))
    if not all(item["pass"] for item in checks) or chat.status_code != 200:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
