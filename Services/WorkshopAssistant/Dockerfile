ARG PYTHON_IMAGE=python:3.12-slim
FROM ${PYTHON_IMAGE}
ARG PIP_INDEX_URL=https://pypi.org/simple
WORKDIR /app
COPY requirements.txt .
RUN pip install --no-cache-dir --index-url "${PIP_INDEX_URL}" -r requirements.txt
COPY app ./app
COPY knowledge ./knowledge
ENV PYTHONUNBUFFERED=1 FASTEMBED_CACHE_PATH=/models
EXPOSE 8018
CMD ["uvicorn", "app.main:app", "--host", "0.0.0.0", "--port", "8018"]
